// Pairing (pc/agent/pair.mjs): the code, the token, limits, persistence.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';

const { createPairing, tokenOf, isLocal } = await import('../pc/agent/pair.mjs');
const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'rigbuddy-pair-'));
let t = 1_000_000;
const p = createPairing(dir, { now: () => t });

// code: 6 digits, stable until it runs out
const c1 = p.currentCode();
assert.match(c1.code, /^\d{6}$/);
assert.equal(p.currentCode().code, c1.code);
t += 10 * 60 * 1000 + 1;
assert.notEqual(p.currentCode().expiresAt, c1.expiresAt, 'a code runs out');

// wrong code: no token; right code: a token that works, and the code is used up
assert.equal(p.pair('000000x', 'tablet').error, 'code');
const code = p.currentCode().code;
const r = p.pair(` ${code} `, 'Tablet <b>1</b>');
assert.match(r.token, /^[0-9a-f]{64}$/);
assert.equal(p.isValid(r.token), true);
assert.equal(p.isValid('0'.repeat(64)), false);
assert.equal(p.isValid(undefined), false);
assert.equal(p.isValid(''), false);
assert.notEqual(p.currentCode().code, code, 'one device per code');
assert.equal(p.pair(code, 'again').error, 'code', 'a used code is no good');
assert.equal(p.list()[0].name, 'Tablet b1b', 'name is cleaned');
assert.ok(!JSON.stringify(p.list()).includes(r.token));

// only a hash is stored; the file survives a restart
const raw = fs.readFileSync(path.join(dir, 'devices.json'), 'utf8');
assert.ok(!raw.includes(r.token));
assert.equal(createPairing(dir, { now: () => t }).isValid(r.token), true);

// guessing: 5 wrong codes lock pairing for a minute, even for the right code
t += 1000;
const q = createPairing(fs.mkdtempSync(path.join(os.tmpdir(), 'rigbuddy-pair-')), { now: () => t });
for (let i = 0; i < 5; i++) assert.equal(q.pair('abcdef', 'x').error, 'code');
assert.equal(q.pair(q.currentCode().code, 'x').error, 'locked');
t += 61 * 1000;
assert.ok(q.pair(q.currentCode().code, 'x').token, 'the lock ends');

// revoke
p.revokeAll();
assert.equal(p.isValid(r.token), false);
assert.deepEqual(p.list(), []);

// token from a request
const url = s => new URL(s, 'http://x');
assert.equal(tokenOf({ headers: { authorization: 'Bearer abc' } }, url('/a')), 'abc');
assert.equal(tokenOf({ headers: {} }, url('/ws?token=xyz')), 'xyz');
assert.equal(tokenOf({ headers: {} }, url('/a')), null);
assert.equal(isLocal({ socket: { remoteAddress: '::ffff:127.0.0.1' } }), true);
assert.equal(isLocal({ socket: { remoteAddress: '192.168.1.20' } }), false);

console.log('pair: ok');
