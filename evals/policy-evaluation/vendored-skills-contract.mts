#!/usr/bin/env -S bun
// Verify that every Skill recorded in skills-lock.json is really vendored in
// this repository and that its content still matches the recorded hash.
//
// Failure modes, each reported per Skill:
//   missing-vendored : the Skill is in the lock but neither .agents/skills/ nor
//                      .claude/skills/ contains it
//   hash-mismatch    : the vendored content differs from the lock
//   copy-divergent  : .agents/skills/ and .claude/skills/ differ for that Skill
//   unrecorded       : a vendored upstream Skill is missing from the lock
//   not-adopted      : a Skill that ADR-0001 section 6 rejected is still
//                      vendored
//
// Exit code is non-zero as soon as one Skill fails.
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { folderHash } from './skills-hash.mts';

const root = resolve(import.meta.dirname, '../..');
const lockPath = join(root, 'skills-lock.json');

// Skills the Spec Kit CLI generates. They are not locked (see
// docs/adr/ADR-0001-agent-environment.md), so they are skipped by the
// completeness and adoption checks below.
const GENERATED_PREFIX = 'speckit-';

// Skills ADR-0001 section 6 rejected. Vendoring one of them leaves an
// unadopted Skill callable, which spec R1 forbids.
const REJECTED = ['linear-release-control'];

type LockEntry = {
  source?: string;
  sourceType?: string;
  ref?: string;
  skillPath?: string;
  computedHash?: string;
};

function isRecord(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

function fail(reason: string, message: string): void {
  console.error(`skills-check: FAIL [${reason}] ${message}`);
}

function vendoredNames(base: string): string[] {
  const dir = join(root, base);
  if (!existsSync(dir)) return [];
  return readdirSync(dir, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
    .sort();
}

function main(): void {
  if (!existsSync(lockPath)) {
    fail('lock-missing', `${lockPath} does not exist`);
    process.exitCode = 1;
    return;
  }

  let json: unknown;
  try {
    json = JSON.parse(readFileSync(lockPath, 'utf8'));
  } catch (error) {
    fail('lock-unparsable', error instanceof Error ? error.message : String(error));
    process.exitCode = 1;
    return;
  }

  if (!isRecord(json) || !isRecord(json.skills)) {
    fail('lock-invalid', 'skills-lock.json must have an object member "skills"');
    process.exitCode = 1;
    return;
  }

  const names = Object.keys(json.skills);
  if (names.length === 0) {
    fail('lock-empty', 'skills-lock.json records no Skill');
    process.exitCode = 1;
    return;
  }

  let failed = 0;
  for (const name of names) {
    const entry = json.skills[name] as LockEntry;
    if (!isRecord(entry) || typeof entry.computedHash !== 'string' || entry.computedHash.length === 0) {
      fail('entry-invalid', `${name}: no computedHash in skills-lock.json`);
      failed++;
      continue;
    }
    if (!entry.source || !entry.ref) {
      fail('entry-invalid', `${name}: skills-lock.json must record "source" and a pinned "ref"`);
      failed++;
      continue;
    }

    const agentDir = join(root, '.agents/skills', name);
    const claudeDir = join(root, '.claude/skills', name);
    const agentOk = existsSync(join(agentDir, 'SKILL.md')) && statSync(join(agentDir, 'SKILL.md')).isFile();
    const claudeOk = existsSync(join(claudeDir, 'SKILL.md')) && statSync(join(claudeDir, 'SKILL.md')).isFile();

    if (!agentOk && !claudeOk) {
      fail('missing-vendored', `${name}: recorded in skills-lock.json but not vendored under .agents/skills/ or .claude/skills/`);
      failed++;
      continue;
    }
    if (!agentOk) {
      fail('missing-vendored', `${name}: not vendored under .agents/skills/`);
      failed++;
      continue;
    }
    if (!claudeOk) {
      fail('missing-vendored', `${name}: not vendored under .claude/skills/`);
      failed++;
      continue;
    }

    const agentHash = folderHash(agentDir);
    const claudeHash = folderHash(claudeDir);
    if (agentHash !== claudeHash) {
      fail('copy-divergent', `${name}: .agents/skills/ and .claude/skills/ differ (${agentHash.slice(0, 12)} vs ${claudeHash.slice(0, 12)})`);
      failed++;
      continue;
    }
    if (agentHash !== entry.computedHash) {
      fail('hash-mismatch', `${name}: vendored hash ${agentHash} != lock hash ${entry.computedHash}`);
      failed++;
      continue;
    }
  }

  // Completeness: every vendored upstream Skill must be recorded in the lock,
  // otherwise the lock does not describe what is actually in the repository.
  const agentNames = vendoredNames('.agents/skills');
  const claudeNames = vendoredNames('.claude/skills');
  for (const base of ['.agents/skills', '.claude/skills']) {
    for (const name of base === '.agents/skills' ? agentNames : claudeNames) {
      if (name.startsWith(GENERATED_PREFIX)) continue;
      if (REJECTED.includes(name)) {
        fail('not-adopted', `${name}: ADR-0001 section 6 rejected this Skill, but it is vendored under ${base}/`);
        failed++;
        continue;
      }
      if (!names.includes(name)) {
        fail('unrecorded', `${name}: vendored under ${base}/ but not recorded in skills-lock.json`);
        failed++;
      }
    }
  }

  if (failed > 0) {
    fail('summary', `${failed} check(s) failed for ${names.length} recorded Skill(s)`);
    process.exitCode = 1;
    return;
  }

  console.log(`skills-check: PASS (${names.length} Skill(s) match skills-lock.json)`);
}

main();
