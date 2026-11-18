// Pairing of the apps with the agent. The agent listens on the LAN, so a device has to prove it was allowed in:
//   - the PC shows a 6-digit code (tray menu / status window, GET /pair/code from this PC only);
//   - the app sends it once with POST /pair {code, name} and gets a long random token back, which it keeps;
//   - every later request carries the token (Authorization: Bearer <token>, or ?token= for a WebSocket / download).
// The code is good for CODE_TTL_MS, for one device, and wrong guesses are limited. Only a SHA-256 of each token is
// stored (devices.json in the data folder). Requests from this PC itself (the tray app, the dev scripts) need no token.
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';

const CODE_TTL_MS = 10 * 60 * 1000;
const MAX_DEVICES = 20;
const MAX_FAILS = 5;          // wrong codes allowed ...
const FAIL_WINDOW_MS = 60 * 1000; // ... per minute, from anywhere; then pairing waits out the window
const sha256 = s => crypto.createHash('sha256').update(s).digest();

export function createPairing(dataDir, { now = () => Date.now() } = {}) {
  const file = path.join(dataDir, 'devices.json');
  let devices = load();  // [{ id, name, hash (hex), addedAt }]
  let code = null;       // { value, expiresAt }
  let fails = [];        // times of recent wrong codes

  function load() {
    try {
      const j = JSON.parse(fs.readFileSync(file, 'utf8'));
      return Array.isArray(j) ? j.filter(d => typeof d?.hash === 'string' && /^[0-9a-f]{64}$/.test(d.hash)) : [];
    } catch {
      return [];
    }
  }

  function save() {
    try {
      fs.mkdirSync(dataDir, { recursive: true });
      const tmp = `${file}.tmp`;
      fs.writeFileSync(tmp, JSON.stringify(devices, null, 2), { mode: 0o600 });
      fs.renameSync(tmp, file);
    } catch (e) {
      console.warn(`pairing: cannot save devices.json: ${e.message}`);
    }
  }

  /** The code to show on the PC; a new one when the last one ran out or was used. */
  function currentCode() {
    const t = now();
    if (!code || code.expiresAt <= t) code = { value: String(crypto.randomInt(0, 1_000_000)).padStart(6, '0'), expiresAt: t + CODE_TTL_MS };
    return { code: code.value, expiresAt: code.expiresAt };
  }

  /** Is this token one of a paired device? */
  function isValid(token) {
    if (typeof token !== 'string' || token.length < 32 || token.length > 200) return false;
    const h = sha256(token);
    let ok = false;
    for (const d of devices) if (crypto.timingSafeEqual(h, Buffer.from(d.hash, 'hex'))) ok = true; // no early exit
    return ok;
  }

  /** The code from an app: { token } for the right one, { error: 'code' | 'locked' | 'full' }. */
  function pair(given, name) {
    const t = now();
    fails = fails.filter(f => t - f < FAIL_WINDOW_MS);
    if (fails.length >= MAX_FAILS) return { error: 'locked', retryAfterMs: FAIL_WINDOW_MS - (t - fails[0]) };
    const cur = currentCode();
    const a = sha256(String(given ?? '').trim()), b = sha256(cur.code);
    if (!crypto.timingSafeEqual(a, b)) {
      fails.push(t);
      return { error: 'code' };
    }
    if (devices.length >= MAX_DEVICES) return { error: 'full' };
    code = null; // one device per code
    const token = crypto.randomBytes(32).toString('hex');
    const label = String(name ?? '').replace(/[^\p{L}\p{N} ._()-]/gu, '').trim().slice(0, 40) || 'device';
    devices.push({ id: crypto.randomBytes(4).toString('hex'), name: label, hash: sha256(token).toString('hex'), addedAt: new Date(t).toISOString() });
    save();
    return { token };
  }

  /** Forget every paired device (they have to enter a code again). */
  function revokeAll() {
    devices = [];
    code = null;
    save();
  }

  const list = () => devices.map(({ id, name, addedAt }) => ({ id, name, addedAt }));

  return { currentCode, isValid, pair, revokeAll, list };
}

/** The token of a request: "Authorization: Bearer <token>", or ?token= (a WebSocket cannot set headers on every client). */
export function tokenOf(req, url) {
  const h = String(req.headers.authorization ?? '');
  if (/^bearer /i.test(h)) return h.slice(7).trim();
  return url.searchParams.get('token');
}

/** Is the request from this PC itself? (The tray app asks on 127.0.0.1; the apps come over the LAN.) */
export function isLocal(req) {
  const a = req.socket.remoteAddress ?? '';
  return a === '127.0.0.1' || a === '::1' || a === '::ffff:127.0.0.1';
}
