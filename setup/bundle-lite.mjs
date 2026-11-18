// Bundles the Node side that needs no map-building tools into <out>/dist with esbuild (setup/build.sh):
//   dist/agent/index.mjs    pc/agent
// The navigation server, the map generator and the game-file parser (setup/bundle.mjs) need the tm-maps checkout and
// the Windows addons; build.sh adds them when they are there.
//
// usage: node setup/bundle-lite.mjs <dist dir>
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { build } from 'esbuild';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const DIST = path.resolve(process.argv[2] ?? path.join(ROOT, 'dist'));

// the JS stand-in for trucksim-telemetry's native addon (reads the host's telemetry bridge)
const shim = path.join(ROOT, 'pc/patches/scsSDKTelemetry.js');
for (const dir of ['pc/agent']) {
  const to = path.join(ROOT, dir, 'node_modules/trucksim-telemetry/build/Release');
  fs.mkdirSync(to, { recursive: true });
  fs.copyFileSync(shim, path.join(to, 'scsSDKTelemetry.js'));
}

fs.rmSync(DIST, { recursive: true, force: true });
fs.mkdirSync(DIST, { recursive: true });
fs.writeFileSync(path.join(DIST, 'package.json'), '{ "type": "module", "private": true }\n');

const common = {
  bundle: true, platform: 'node', format: 'esm', target: 'node22', logLevel: 'warning', legalComments: 'none',
  banner: { js: "import { createRequire as __cr } from 'node:module'; const require = __cr(import.meta.url);" },
  loader: { '.node': 'copy' },
};
for (const [name, entry] of [['agent', 'pc/agent/index.mjs']]) {
  const outfile = path.join(DIST, name, 'index.mjs');
  await build({ ...common, entryPoints: [path.join(ROOT, entry)], outfile });
  console.log(`dist/${name}/index.mjs  ${(fs.statSync(outfile).size / 1e6).toFixed(1)} MB`);
}
