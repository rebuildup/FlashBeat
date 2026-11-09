#!/usr/bin/env -S bun
// Vendor the Agent Skills this repository depends on, and rewrite
// skills-lock.json so that it describes exactly what is committed.
//
// Two kinds of Skill exist in this repository:
//
//   1. upstream Skills: the Skill directory is copied byte for byte from a
//      pinned upstream commit (rebuildup/project-init, rebuildup/writing-skills,
//      mattpocock/skills, rebuildup/design-skills, cloudflare/skills) into both
//      `.agents/skills/` and `.claude/skills/`. The two copies are identical,
//      so one `computedHash` describes both.
//   2. generated Skills: the Spec Kit CLI writes per-agent variants of the
//      upstream github/spec-kit templates, so `.agents/skills/` and
//      `.claude/skills/` differ by design.
//
// skills-lock.json records kind 1 only. The upstream repos of kind 1 are the
// dependencies of this repository (see specs/056-agent-env/spec.md); the
// Spec Kit CLI (specify-cli 1.3.1, pinned in mise.toml) is the producer of
// kind 2, so those Skills are not a locked dependency and carry no lock
// entry. This script still verifies kind 2 against the pinned upstream
// template, so drift is caught.
//
// The lock uses `version: 1`, the format shared with rebuildup/project-init
// and the `skills` CLI, so `bunx skills` keeps reading it.
//
// Every entry pins an exact upstream commit SHA, and the vendored copy is the
// upstream directory at that commit, verbatim. The `skills` CLI resolves the
// default branch instead, so its output is not reproducible; ADR-0001 lists
// "the `skills` CLI can verify `computedHash` on install" as a future
// re-evaluation point, which is why this script exists.
//
// Usage:
//   bun evals/policy-evaluation/vendor-skills.mts            # vendor + relock
//   bun evals/policy-evaluation/vendor-skills.mts --check    # verify only
//
// The vendor script needs network access. `mise run skills-check` does not and
// is the verification entry point used by CI.
import { execFileSync } from 'node:child_process';
import { cpSync, existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { folderHash, HASH_ALGORITHM } from './skills-hash.mts';

const root = resolve(import.meta.dirname, '../..');

type Source = {
  source: string;
  sourceType: 'github';
  repo: string;
  ref: string;
  // Skill directory name inside the upstream repository.
  skills: { name: string; path: string }[];
};

const PROJECT_INIT_REF = 'fae2dd6b4eb2a8c4744ff719f591e6e697efe4a4';
const WRITING_SKILLS_REF = 'c631933e5799fa1a1c418053557fd2cce1800076';
const MATTPOCOCK_REF = '49dd158d1076134a641b33efb035946536778336';
const DESIGN_SKILLS_REF = '342d70929dc5f33af5bb88f48362e11ed2dcf1cb';
const CLOUDFLARE_REF = '86505e44f87d801aa1864a3c4109e9a8ae5c409f';
const SPEC_KIT_REF = 'v1.3.1';

// The adopted set is fixed by docs/adr/ADR-0001-agent-environment.md section 6.
// `linear-release-control` is deliberately absent: release planning runs on
// GitHub Issues here, not Linear.
const SOURCES: Source[] = [
  {
    source: 'rebuildup/project-init',
    sourceType: 'github',
    repo: 'https://github.com/rebuildup/project-init.git',
    ref: PROJECT_INIT_REF,
    skills: [
      { name: 'agent-delivery-estimation', path: 'skills/agent-delivery-estimation' },
      { name: 'agent-recovery', path: 'skills/agent-recovery' },
      { name: 'code-naming', path: 'skills/code-naming' },
      { name: 'correctness-assurance', path: 'skills/correctness-assurance' },
      { name: 'design-refinement', path: 'skills/design-refinement' },
      { name: 'engineering-decisions', path: 'skills/engineering-decisions' },
      { name: 'github-delivery', path: 'skills/github-delivery' },
      { name: 'herdr-runtime', path: 'skills/herdr-runtime' },
      { name: 'interaction-discipline', path: 'skills/interaction-discipline' },
      { name: 'onboarding', path: 'skills/onboarding' },
      { name: 'parallel-orchestration', path: 'skills/parallel-orchestration' },
      { name: 'policy-evaluation', path: 'skills/policy-evaluation' },
      { name: 'quality-gate', path: 'skills/quality-gate' },
      { name: 'sandbox-runtime', path: 'skills/sandbox-runtime' },
      { name: 'secrets-management', path: 'skills/secrets-management' },
      { name: 'security-audit', path: 'skills/security-audit' },
      { name: 'security-maintenance', path: 'skills/security-maintenance' },
      { name: 'skill-selection', path: 'skills/skill-selection' },
      { name: 'worktree-workflow', path: 'skills/worktree-workflow' },
    ],
  },
  {
    source: 'rebuildup/writing-skills',
    sourceType: 'github',
    repo: 'https://github.com/rebuildup/writing-skills.git',
    ref: WRITING_SKILLS_REF,
    skills: [
      { name: 'natural-japanese', path: 'skills/natural-japanese' },
      { name: 'writing-discipline', path: 'skills/writing-discipline' },
    ],
  },
  {
    source: 'mattpocock/skills',
    sourceType: 'github',
    repo: 'https://github.com/mattpocock/skills.git',
    ref: MATTPOCOCK_REF,
    skills: [
      { name: 'code-review', path: 'skills/engineering/code-review' },
      { name: 'diagnosing-bugs', path: 'skills/engineering/diagnosing-bugs' },
      { name: 'tdd', path: 'skills/engineering/tdd' },
    ],
  },
  {
    source: 'rebuildup/design-skills',
    sourceType: 'github',
    repo: 'https://github.com/rebuildup/design-skills.git',
    ref: DESIGN_SKILLS_REF,
    skills: [{ name: 'design-intent', path: 'skills/design-intent' }],
  },
  {
    source: 'cloudflare/skills',
    sourceType: 'github',
    repo: 'https://github.com/cloudflare/skills.git',
    ref: CLOUDFLARE_REF,
    skills: [
      { name: 'cf-cli', path: 'skills/cf-cli' },
      { name: 'workers-best-practices', path: 'skills/workers-best-practices' },
    ],
  },
];

// Skills that were vendored by an earlier revision of this repository and are
// no longer adopted. ADR-0001 section 6 rejects `linear-release-control`
// because release planning runs on GitHub Issues, not Linear. Keeping the
// directory would leave an unadopted Skill callable, which spec R1 forbids.
const RETIRED = ['linear-release-control'];

// Skills produced by the Spec Kit CLI. The CLI is pinned in mise.toml
// (`specify-init` uses specify-cli 1.3.1) and writes per-agent variants.
const GENERATED = {
  source: 'github/spec-kit',
  sourceType: 'github' as const,
  ref: SPEC_KIT_REF,
  generator: 'specify-cli 1.3.1 (mise run specify-init)',
  templates: {
    'speckit-analyze': 'templates/commands/analyze.md',
    'speckit-checklist': 'templates/commands/checklist.md',
    'speckit-clarify': 'templates/commands/clarify.md',
    'speckit-constitution': 'templates/commands/constitution.md',
    'speckit-converge': 'templates/commands/converge.md',
    'speckit-implement': 'templates/commands/implement.md',
    'speckit-plan': 'templates/commands/plan.md',
    'speckit-specify': 'templates/commands/specify.md',
    'speckit-tasks': 'templates/commands/tasks.md',
  },
};

const checkOnly = process.argv.includes('--check');
const scratch = mkdtempSync(join(tmpdir(), 'flashbeat-skills-'));

function git(args: string[], cwd?: string): string {
  return execFileSync('git', args, { cwd, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }).trim();
}

function copyHashes(name: string): Record<string, string> {
  const copies: Record<string, string> = {};
  for (const base of ['.agents/skills', '.claude/skills']) {
    const dir = join(root, base, name);
    if (!existsSync(join(dir, 'SKILL.md'))) continue;
    copies[base + '/' + name] = folderHash(dir);
  }
  return copies;
}

function stripFrontmatter(text: string): string {
  const match = /^---\r?\n[\s\S]*?\r?\n---\r?\n/.exec(text);
  return match ? text.slice(match[0].length) : text;
}

// The Spec Kit CLI rewrites a handful of placeholders and paths when it
// writes the Skill, and it adds per-agent frontmatter. Reversing all of them
// lets the vendored copy be compared with the upstream template.
//
// The CLI writes `/speckit-<cmd>` for Claude Code and `$speckit-<cmd>` for
// Codex; the upstream template uses the `__SPECKIT_COMMAND_<CMD>__` token, so
// both spellings map back to it.
function normalizeGenerated(text: string): string[] {
  return stripFrontmatter(text)
    .split('\n')
    .filter((line) => !line.includes('When constructing command invocations from hook command names'))
    .map((line) =>
      line
        .replace(/\.specify\/memory\/constitution\.md/g, '/memory/constitution.md')
        .replace(/\.specify\/templates\//g, 'templates/')
        .replace(/`\.specify\/scripts\/[a-z]+\/[A-Za-z0-9_.-]+[^`]*`/g, '`{SCRIPT}`')
        .replace(/[/$]speckit-([a-z-]+)/g, (_m, name: string) => '__SPECKIT_COMMAND_' + name.replace(/-/g, '_').toUpperCase() + '__')
        .replace(/\$ARGUMENTS/g, '{ARGS}'),
    )
    .map((line) => line.trimEnd())
    .filter((line) => line.trim() !== '');
}

// Both sides go through the same normalization, otherwise the template's own
// spellings (which are already in token form) never line up with the CLI's
// rewritten spellings.
function templateMatches(vendored: string, template: string): boolean {
  const a = normalizeGenerated(vendored);
  const b = normalizeGenerated(template);
  if (a.length !== b.length) return false;
  return a.every((line, index) => line === b[index]);
}

try {
  const skills: Record<string, unknown> = {};

  for (const source of SOURCES) {
    const checkout = join(scratch, source.source.replace('/', '-'));
    git(['clone', '--quiet', source.repo, checkout]);
    git(['checkout', '--quiet', source.ref], checkout);
    const actual = git(['rev-parse', 'HEAD'], checkout);
    if (actual !== source.ref) {
      throw new Error(`${source.source}: pinned ref ${source.ref} resolved to ${actual}`);
    }
    for (const skill of source.skills) {
      const name = skill.name;
      const from = join(checkout, skill.path);
      if (!existsSync(join(from, 'SKILL.md'))) {
        throw new Error(`${name}: ${source.source}#${source.ref} does not contain ${skill.path}/SKILL.md`);
      }
      if (!checkOnly) {
        for (const base of ['.agents/skills', '.claude/skills']) {
          const target = join(root, base, name);
          mkdirSync(join(target, '..'), { recursive: true });
          rmSync(target, { recursive: true, force: true });
          cpSync(from, target, { recursive: true });
        }
      }
      const copies = copyHashes(name);
      const hashes = new Set(Object.values(copies));
      if (hashes.size !== 1) {
        throw new Error(`${name}: the two vendored copies differ (${[...hashes].join(' vs ')})`);
      }
      const vendored = [...hashes][0];
      const upstream = folderHash(from);
      if (vendored !== upstream) {
        throw new Error(`${name}: vendored hash ${vendored} != upstream ${upstream} at ${source.source}#${source.ref}`);
      }
      skills[name] = {
        source: source.source,
        sourceType: source.sourceType,
        ref: source.ref,
        skillPath: `${skill.path}/SKILL.md`,
        computedHash: vendored,
      };
      console.log(`vendored ${name} @ ${source.source}#${source.ref.slice(0, 12)} -> ${vendored.slice(0, 12)}`);
    }
  }

  // Retired Skills: drop the vendored copies so no unadopted Skill stays
  // callable. The generated (spec-kit) Skills are never touched.
  for (const name of RETIRED) {
    for (const base of ['.agents/skills', '.claude/skills']) {
      const target = join(root, base, name);
      if (!existsSync(target)) continue;
      if (checkOnly) {
        throw new Error(`${name}: retired Skill is still vendored under ${base}/`);
      }
      rmSync(target, { recursive: true, force: true });
      console.log(`retired ${name} @ ${base}/`);
    }
  }

  // Generated (Spec Kit CLI) skills: already committed, provenance verified
  // against the pinned upstream template. They are not locked, so they are
  // checked but not written to skills-lock.json.
  const specKitCheckout = join(scratch, 'github-spec-kit');
  git(['clone', '--quiet', '--branch', GENERATED.ref, '--depth', '1', 'https://github.com/github/spec-kit.git', specKitCheckout]);
  for (const [name, templatePath] of Object.entries(GENERATED.templates)) {
    const templateFile = join(specKitCheckout, templatePath);
    if (!existsSync(templateFile)) throw new Error(`${name}: template ${templatePath} not found at ${GENERATED.ref}`);
    const template = readFileSync(templateFile, 'utf8');
    for (const base of ['.agents/skills', '.claude/skills']) {
      const copy = join(root, base, name, 'SKILL.md');
      if (!existsSync(copy)) throw new Error(`${name}: not vendored under ${base}/`);
      if (!templateMatches(readFileSync(copy, 'utf8'), template)) {
        throw new Error(`${name}: ${base}/ does not match ${templatePath} at ${GENERATED.ref} beyond the known substitutions`);
      }
    }
    console.log(`verified ${name} @ ${GENERATED.source}#${GENERATED.ref}`);
  }

  const lock = {
    version: 1,
    hashAlgorithm: HASH_ALGORITHM,
    skills: Object.fromEntries(Object.entries(skills).sort(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0))),
  };

  const target = join(root, 'skills-lock.json');
  const serialized = JSON.stringify(lock, null, 2) + '\n';
  if (checkOnly) {
    if (readFileSync(target, 'utf8') !== serialized) {
      console.error('vendor-skills: FAIL: skills-lock.json would change (run without --check)');
      process.exitCode = 1;
    } else {
      console.log('vendor-skills: PASS (skills-lock.json is up to date)');
    }
  } else {
    writeFileSync(target, serialized);
    console.log(`vendor-skills: wrote ${target}`);
  }
} catch (error) {
  console.error('vendor-skills: FAIL: ' + (error instanceof Error ? error.message : String(error)));
  process.exitCode = 1;
} finally {
  rmSync(scratch, { recursive: true, force: true });
}
