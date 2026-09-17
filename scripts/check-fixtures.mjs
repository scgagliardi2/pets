#!/usr/bin/env node
/**
 * Fixture drift check.
 *
 * The golden fixtures in test/fixtures/ are vendored copies of the Unity repo's
 * shared/fixtures/. They are the only artifact that proves this implementation and the Unity one
 * agree on how combat actually resolves, and a copy is exactly the kind of thing that goes stale
 * without anyone noticing.
 *
 * This re-fetches them from GitHub and fails loudly if any hash has moved, so a rule change on
 * the Unity side is a build failure here rather than two implementations quietly disagreeing.
 *
 *   node scripts/check-fixtures.mjs            check against the pinned commit
 *   node scripts/check-fixtures.mjs --ref main  check against a branch's current head
 *
 * Checking against a branch is the useful mode: the pinned commit can't change, so it only
 * verifies the local copies weren't edited.
 */

import { createHash } from 'node:crypto';
import { readFile, readdir } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));
const fixturesDir = join(here, '..', 'test', 'fixtures');

const refFlag = process.argv.indexOf('--ref');
const ref = refFlag >= 0 ? process.argv[refFlag + 1] : null;

const sha256 = (buf) => 'sha256:' + createHash('sha256').update(buf).digest('hex');

const provenance = JSON.parse(await readFile(join(fixturesDir, 'PROVENANCE.json'), 'utf8'));
const target = ref ?? provenance.sourceCommit;

console.log(`Fixture check against ${ref ? `ref ${ref}` : `pinned commit ${target.slice(0, 8)}`}`);

// 1. Local files must match the manifest. Catches a hand-edited fixture.
const localFiles = (await readdir(fixturesDir))
  .filter((f) => f.endsWith('.json') && f !== 'PROVENANCE.json')
  .sort();

const problems = [];

const manifestNames = Object.keys(provenance.files).sort();
for (const name of localFiles) {
  if (!manifestNames.includes(name)) {
    problems.push(`${name}: present locally but not in PROVENANCE.json`);
  }
}
for (const name of manifestNames) {
  if (!localFiles.includes(name)) {
    problems.push(`${name}: in PROVENANCE.json but missing locally`);
    continue;
  }
  const actual = sha256(await readFile(join(fixturesDir, name)));
  if (actual !== provenance.files[name]) {
    problems.push(`${name}: local copy edited (manifest says ${provenance.files[name].slice(7, 19)}, file is ${actual.slice(7, 19)})`);
  }
}

// 2. Remote files must match too. Catches an upstream rule change.
let remoteChecked = 0;
for (const name of manifestNames) {
  const url = provenance.rawUrlTemplate
    .replace('{commit}', target)
    .replace('{file}', name);
  let response;
  try {
    response = await fetch(url);
  } catch (err) {
    console.warn(`  ! could not reach GitHub (${err.message}) — skipping the remote half`);
    break;
  }
  if (response.status === 404) {
    problems.push(`${name}: no longer exists upstream at ${target}`);
    continue;
  }
  if (!response.ok) {
    console.warn(`  ! ${name}: HTTP ${response.status} — skipping`);
    continue;
  }
  const remote = sha256(Buffer.from(await response.arrayBuffer()));
  remoteChecked++;
  if (remote !== provenance.files[name]) {
    problems.push(
      `${name}: UPSTREAM CHANGED. A battle rule moved in the Unity repo. Re-vendor the fixture, ` +
        `re-run the suite, and reconcile the sim before shipping.`,
    );
  }
}

if (problems.length > 0) {
  console.error(`\n${problems.length} problem(s):\n`);
  for (const p of problems) console.error(`  - ${p}`);
  process.exit(1);
}

console.log(
  `OK: ${manifestNames.length} fixture(s) match the manifest` +
    (remoteChecked > 0 ? `, ${remoteChecked} verified against upstream.` : ' (remote check skipped).'),
);
