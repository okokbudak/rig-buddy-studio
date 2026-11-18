// The button box's side of the key presses: which keyboard key does the game have for a function ("horn" -> h)?
// The answer comes from the player's own controls.sii (the active profile of the game), so a touch on the tablet presses what
// the player pressed anyway, and the tablet can only ever press keys the game binds to a function: it never sends a key name
// of its own. RigBuddy.exe does the pressing (pc/host/KeyInjector.cs) over its loopback control port.
import fs from 'node:fs';
import net from 'node:net';
import os from 'node:os';
import path from 'node:path';

const CONTROL_PORT = Number(process.env.RIGBUDDY_CONTROL_PORT || 62845);
const DOCS = { ets2: 'Euro Truck Simulator 2', ats: 'American Truck Simulator' };
const APP_ID = { ets2: '227300', ats: '270880' };

const listDirs = dir => {
  try {
    return fs.readdirSync(dir, { withFileTypes: true }).filter(e => e.isDirectory()).map(e => path.join(dir, e.name));
  } catch {
    return [];
  }
};

/**
 * What a controls.sii line says about one function: `mix horn \`keyboard.h?0 | semantical.horn?0\``.
 * Returns {action: combo} where combo is the first keyboard-only alternative ("h", "lshift+d"); functions that are not
 * on the keyboard (a joystick button only, nothing) are left out.
 */
export function parseBindings(text) {
  const out = {};
  for (const m of text.matchAll(/config_lines\[\d+\]:\s*"mix (\w+) `([^`]*)`"/g)) {
    const [, action, expr] = m;
    if (!/^[\w.?\-& |]+$/.test(expr)) continue;   // a formula (steering, throttle ...), not a button
    for (const alt of expr.split('|')) {
      const keys = alt.split('&').map(t => /^\s*keyboard\.(\w+)\?\d+\s*$/.exec(t)?.[1]);
      if (keys.length && keys.every(Boolean)) {
        out[action] = keys.join('+');
        break;
      }
    }
  }
  return out;
}

/** The controls.sii of the profile that was played last (newest save), else the newest controls.sii. */
export function findControlsFile(game, latestSaveProfileDir) {
  if (latestSaveProfileDir) {
    const f = path.join(latestSaveProfileDir, 'controls.sii');
    if (fs.existsSync(f)) return f;
  }
  const steam = process.env.STEAM_PATH || 'C:/Program Files (x86)/Steam';
  const docs = path.join(os.homedir(), 'Documents', DOCS[game]);
  const roots = [
    ...listDirs(path.join(steam, 'userdata')).map(u => path.join(u, APP_ID[game], 'remote', 'profiles')),
    path.join(docs, 'profiles'),
    path.join(docs, 'steam_profiles'),
  ];
  let best = null;
  for (const root of roots) {
    for (const dir of listDirs(root)) {
      const file = path.join(dir, 'controls.sii');
      try {
        const st = fs.statSync(file);
        if (!best || st.mtimeMs > best.mtimeMs) best = { file, mtimeMs: st.mtimeMs };
      } catch { /* no controls.sii in this profile */ }
    }
  }
  return best?.file ?? null;
}

const cache = new Map();   // file -> {mtimeMs, bindings}
export function bindingsOf(file) {
  const mtimeMs = fs.statSync(file).mtimeMs;
  const hit = cache.get(file);
  if (hit && hit.mtimeMs === mtimeMs) return hit.bindings;
  const bindings = parseBindings(fs.readFileSync(file, 'utf8'));
  cache.set(file, { mtimeMs, bindings });
  return bindings;
}

function ask(line) {
  return new Promise(resolve => {
    const socket = net.createConnection({ host: '127.0.0.1', port: CONTROL_PORT });
    let buf = '';
    const done = value => {
      clearTimeout(timer);
      socket.destroy();
      resolve(value);
    };
    const timer = setTimeout(() => done(null), 2000);
    socket.on('connect', () => socket.write(`${line}\n`));
    socket.on('data', d => {
      buf += d;
      if (buf.includes('\n')) done(buf.split('\n')[0]);
    });
    socket.on('error', () => done(null));
  });
}

/**
 * Presses (down: true), releases (down: false) or taps (down: undefined) the key the game has for `action`.
 * Resolves {ok: true, key} or {ok: false, error}: 'badaction', 'nocontrols', 'unbound' (the function has no key: the player
 * has it on a joystick or nowhere), 'host' (RigBuddy.exe cannot be asked) or the host's own reason (game not in front ...).
 */
export async function pressAction(action, down, game, saveProfileDir) {
  if (!/^[a-z0-9_]{1,32}$/.test(action)) return { ok: false, error: 'badaction' };
  const file = findControlsFile(game, saveProfileDir);
  if (!file) return { ok: false, error: 'nocontrols' };
  const key = bindingsOf(file)[action];
  if (!key) return { ok: false, error: 'unbound' };
  const reply = await ask(`key ${down === undefined ? 'tap' : down ? 'down' : 'up'} ${key}`);
  if (reply === null) return { ok: false, error: 'host' };
  return reply === 'ok' ? { ok: true, key } : { ok: false, error: reply.replace(/^error:\s*/, '') };
}

/**
 * The same with a key the user chose on the tablet ("h", "lshift+d"): the button box's own buttons carry the key, so every
 * function works, also the ones the game has on no keyboard key until the user gives them one. RigBuddy.exe still refuses
 * what is not a key name, a combination that leaves the game (Alt+F4) and any press while the game is not in front.
 */
export async function pressKey(key, down) {
  if (!/^[a-z0-9]{1,12}(\+[a-z0-9]{1,12}){0,3}$/.test(key)) return { ok: false, error: 'badkey' };
  const reply = await ask(`key ${down === undefined ? 'tap' : down ? 'down' : 'up'} ${key}`);
  if (reply === null) return { ok: false, error: 'host' };
  return reply === 'ok' ? { ok: true, key } : { ok: false, error: reply.replace(/^error:\s*/, '') };
}

/** {function: key} the game has on the keyboard in the active profile: the app suggests these for its buttons. */
export function bindingsFor(game) {
  const file = findControlsFile(game, null);
  return file ? bindingsOf(file) : {};
}

/** Is the game's window in front right now? true / false, or null when RigBuddy.exe cannot be asked. */
export async function gameInFront() {
  const reply = await ask('key front');
  return reply === null ? null : reply === 'ok';
}

/** Lets go of everything (the tablet went away). */
export function releaseAll() {
  return ask('key release');
}
