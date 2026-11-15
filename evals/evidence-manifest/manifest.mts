// Evidence manifest for the v6 real-device UI review (Issue #388).
//
// Purpose: turn the ad-hoc screenshot archive under docs/evidence/ into a
// trustworthy, diffable index so that a later report (Issue #389) can tell
// "changed", "failed" and "unverified" apart without a human opening every
// image.
//
// The manifest is a *claim ledger*, not a verdict. It records, per claim:
//
//   claim       the Issue/acceptance item the screenshot is meant to support
//   state       what the screenshot is a picture of
//   buildSha    the build the evidence was captured from
//   platform    the OS the capture ran on
//   resolution  the capture resolution
//   path        the image inside the repository
//   status      creditable | unverified | stale | missing | mismatch
//
// `creditable` means **only** "this capture can be used as evidence for this
// claim": it came from the candidate build, on the claim's platform, at a
// recorded resolution, and the file is really here. It is **not** a UI
// acceptance verdict. Whether the screen is visually correct is decided by the
// review (Issue #389) against the design, never by this manifest.
//
// Rules that keep it honest (all enforced below and covered by the tests):
//
//   * A record is only `creditable` when the build SHA, the platform and the
//     resolution are all present and the image really exists on disk.
//   * A macOS capture is never credited to Win64, and a WSL capture is not
//     Windows-native. The platform has to match the claim's platform.
//   * Evidence captured from a build other than the candidate SHA is `stale`.
//   * A record whose file is absent is `missing`; a record pointing at a
//     design node that the design does not contain is `mismatch`.
//   * Nothing is inferred: an absent field makes the record `unverified`,
//     never `creditable`.
//
// This tool does not open the design file itself when the caller already has
// the node list; `--pen` reads the ids straight out of the `.pen` document,
// which is plain JSON (version 2.22), so no Pencil connection is needed.
import { createHash } from 'node:crypto';
import { existsSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';

export type EvidenceStatus =
  | 'creditable'
  | 'unverified'
  | 'stale'
  | 'missing'
  | 'mismatch';

export type EvidencePlatform = 'win64' | 'macos' | 'wsl' | 'editor';

export interface EvidenceRecord {
  /** Issue number the capture supports (e.g. "369"). */
  claim: string;
  /** What the capture shows. */
  state: string;
  /** Build the capture came from, e.g. "0.1.23+af183bf4". */
  buildSha?: string;
  /** OS the capture ran on. */
  platform?: EvidencePlatform;
  /** Capture resolution, e.g. "3420x2224". */
  resolution?: string;
  /** Image path relative to the repository root. */
  path: string;
  /** design/flashbeat-ui.pen node the capture is compared against. */
  designNode?: string;
  /**
   * Reason the claim has no design node by design (e.g. a screen the v6
   * design does not cover). Only meaningful without `designNode`; it marks
   * the claim as intentionally exempt from design comparison rather than
   * silently missing a reference.
   */
  designNotApplicable?: string;
  /** Free-form note; never affects the status. */
  note?: string;
}

export interface ManifestInput {
  /** The build the review is about (the candidate artefact). */
  candidateBuild: string;
  /** Platform the acceptance claims are made for. */
  candidatePlatform: EvidencePlatform;
  records: EvidenceRecord[];
  /** Node ids the design actually contains (the caller reads the pen file). */
  designNodes?: string[];
}

export interface ManifestEntry extends EvidenceRecord {
  status: EvidenceStatus;
  /** Deterministic explanation, so the report never has to guess. */
  reason: string;
  /** sha256 of the image bytes, when the file exists. */
  sha256?: string;
  /** Image size in bytes, when the file exists. */
  bytes?: number;
}

export interface Manifest {
  manifestVersion: 1;
  candidateBuild: string;
  candidatePlatform: EvidencePlatform;
  generatedFrom: 'docs/evidence';
  counts: Record<EvidenceStatus, number>;
  entries: ManifestEntry[];
}

const STATUS_ORDER: EvidenceStatus[] = [
  'creditable',
  'unverified',
  'stale',
  'missing',
  'mismatch',
];

/** A build string is a version plus a short sha, e.g. "0.1.23+af183bf4". */
export function shortSha(build: string | undefined): string | undefined {
  if (!build) return undefined;
  const plus = build.lastIndexOf('+');
  if (plus < 0) return undefined;
  const sha = build.slice(plus + 1).trim();
  return sha.length >= 7 ? sha : undefined;
}

/**
 * Two builds are the same artefact when their short shas agree. The version
 * prefix alone is never enough: 0.1.23 was built from several commits.
 */
export function sameBuild(a: string | undefined, b: string | undefined): boolean {
  const shaA = shortSha(a);
  const shaB = shortSha(b);
  if (!shaA || !shaB) return false;
  return shaA.toLowerCase() === shaB.toLowerCase();
}

/** Platforms that may be credited to the candidate platform. */
export function platformMatches(
  capture: EvidencePlatform | undefined,
  candidate: EvidencePlatform,
): boolean {
  if (!capture) return false;
  if (capture === candidate) return true;
  // WSL is a Linux guest: it is never Windows-native evidence.
  if (candidate === 'win64' && capture === 'wsl') return false;
  if (candidate === 'win64' && capture === 'macos') return false;
  if (candidate === 'wsl' && capture === 'win64') return false;
  return false;
}

/**
 * Collect every node id a `.pen` document contains. The file is plain JSON:
 * nodes live under `children` arrays, `ref` nodes carry `descendants` patches
 * (keyed by child id, valued by property overlays or full node replacements),
 * and either shape may nest further. Every `id` string and every
 * `descendants` key is a node the design can legitimately contain, so both are
 * collected. Over-collecting only ever *weakens* a `mismatch` verdict, never
 * invents one.
 */
export function collectDesignNodeIds(pen: unknown): string[] {
  const ids = new Set<string>();

  const visit = (value: unknown): void => {
    if (Array.isArray(value)) {
      for (const item of value) visit(item);
      return;
    }
    if (value === null || typeof value !== 'object') return;

    const node = value as Record<string, unknown>;
    if (typeof node.id === 'string' && node.id.length > 0) ids.add(node.id);

    const descendants = node.descendants;
    if (descendants !== null && typeof descendants === 'object' && !Array.isArray(descendants)) {
      for (const key of Object.keys(descendants)) {
        if (key.length > 0) ids.add(key);
      }
    }

    for (const child of Object.values(node)) visit(child);
  };

  visit(pen);
  return [...ids];
}

function hashFile(path: string): string {
  return createHash('sha256').update(readFileSync(path)).digest('hex');
}

/**
 * Decide one record's status. Pure apart from the file-existence lookup, which
 * is injected so the tests can run without the real image tree.
 */
export function classifyRecord(
  record: EvidenceRecord,
  input: ManifestInput,
  fileExists: (relativePath: string) => boolean = (p) => existsSync(p),
): { status: EvidenceStatus; reason: string } {
  if (!record.claim || !record.state) {
    return { status: 'unverified', reason: 'claim or state is missing' };
  }

  if (!fileExists(record.path)) {
    return { status: 'missing', reason: `the image ${record.path} is not in the repository` };
  }

  if (record.designNode && input.designNodes && !input.designNodes.includes(record.designNode)) {
    return {
      status: 'mismatch',
      reason: `design node ${record.designNode} is not in the design`,
    };
  }

  if (!record.buildSha) {
    return { status: 'unverified', reason: 'the capture does not record its build' };
  }

  if (!sameBuild(record.buildSha, input.candidateBuild)) {
    return {
      status: 'stale',
      reason: `captured from ${record.buildSha}, the candidate is ${input.candidateBuild}`,
    };
  }

  if (!record.platform) {
    return { status: 'unverified', reason: 'the capture does not record its platform' };
  }

  if (!platformMatches(record.platform, input.candidatePlatform)) {
    return {
      status: 'unverified',
      reason: `a ${record.platform} capture cannot be credited to ${input.candidatePlatform}`,
    };
  }

  if (!record.resolution) {
    return { status: 'unverified', reason: 'the capture does not record its resolution' };
  }

  return { status: 'creditable', reason: 'build, platform and resolution match the candidate' };
}

/** Build the manifest. Entries keep the input order so diffs stay readable. */
export function buildManifest(input: ManifestInput, root = '.'): Manifest {
  const entries: ManifestEntry[] = input.records.map((record) => {
    const absolute = resolve(root, record.path);
    const { status, reason } = classifyRecord(record, input, (p) => existsSync(resolve(root, p)));

    const entry: ManifestEntry = { ...record, status, reason };
    if (status !== 'missing' && existsSync(absolute)) {
      entry.sha256 = hashFile(absolute);
      entry.bytes = statSync(absolute).size;
    }
    return entry;
  });

  const counts = STATUS_ORDER.reduce(
    (acc, status) => ({ ...acc, [status]: 0 }),
    {} as Record<EvidenceStatus, number>,
  );
  for (const entry of entries) counts[entry.status] += 1;

  return {
    manifestVersion: 1,
    candidateBuild: input.candidateBuild,
    candidatePlatform: input.candidatePlatform,
    generatedFrom: 'docs/evidence',
    counts,
    entries,
  };
}

/**
 * Compare two manifests and report what changed between them. This is the
 * primitive Issue #389 builds its review report on: only deltas need a human.
 */
export interface ManifestDiff {
  added: string[];
  removed: string[];
  changed: { claim: string; state: string; from: EvidenceStatus; to: EvidenceStatus }[];
}

function key(entry: EvidenceRecord): string {
  return `${entry.claim}\u0000${entry.state}`;
}

export function diffManifests(before: Manifest, after: Manifest): ManifestDiff {
  const beforeByKey = new Map(before.entries.map((e) => [key(e), e]));
  const afterByKey = new Map(after.entries.map((e) => [key(e), e]));

  const added = [...afterByKey.keys()].filter((k) => !beforeByKey.has(k)).map(k => k.replace('\u0000', ' / '));
  const removed = [...beforeByKey.keys()].filter((k) => !afterByKey.has(k)).map(k => k.replace('\u0000', ' / '));

  const changed: ManifestDiff['changed'] = [];
  for (const [k, afterEntry] of afterByKey) {
    const beforeEntry = beforeByKey.get(k);
    if (!beforeEntry) continue;
    if (beforeEntry.status !== afterEntry.status) {
      changed.push({
        claim: afterEntry.claim,
        state: afterEntry.state,
        from: beforeEntry.status,
        to: afterEntry.status,
      });
    }
  }

  return { added, removed, changed };
}

// --- CLI --------------------------------------------------------------------
//
// Usage:
//   bun evals/evidence-manifest/manifest.mts <records.json> [--root <dir>] [--out <file>] [--pen <file.pen>]
//
// The records file carries the claims; the candidate build and platform come
// from the same file so a manifest can never be generated for an unnamed build.
// `--pen` reads the design's node ids from the `.pen` document itself, so the
// design-node check always runs against the design that is actually in the
// repository instead of a hand-copied list that can go stale.

/** Positional args: everything that is not a `--flag` or a flag's value. */
function positionalArgs(args: string[], valueFlags: string[]): string[] {
  const out: string[] = [];
  for (let i = 0; i < args.length; i++) {
    const arg = args[i];
    if (arg.startsWith('--')) {
      if (valueFlags.includes(arg)) i++;
      continue;
    }
    out.push(arg);
  }
  return out;
}

const VALUE_FLAGS = ['--root', '--out', '--pen'];

function main(argv: string[]): number {
  const args = argv.slice(2);
  const inputPath = positionalArgs(args, VALUE_FLAGS)[0];
  if (!inputPath) {
    console.error('usage: manifest.mts <records.json> [--root <dir>] [--out <file>] [--pen <file.pen>]');
    return 2;
  }

  const rootFlag = args.indexOf('--root');
  const root = rootFlag >= 0 ? args[rootFlag + 1] : '.';
  const outFlag = args.indexOf('--out');
  const out = outFlag >= 0 ? args[outFlag + 1] : undefined;
  const penFlag = args.indexOf('--pen');
  const penPath = penFlag >= 0 ? args[penFlag + 1] : undefined;

  const input = JSON.parse(readFileSync(resolve(inputPath), 'utf8')) as ManifestInput;
  if (penPath) {
    const pen = JSON.parse(readFileSync(resolve(penPath), 'utf8')) as unknown;
    input.designNodes = collectDesignNodeIds(pen);
  }
  const manifest = buildManifest(input, root);
  const text = JSON.stringify(manifest, null, 2) + '\n';

  if (out) {
    writeFileSync(out, text, 'utf8');
  } else {
    process.stdout.write(text);
  }

  // Non-zero when anything is not creditable, so CI treats a partial manifest
  // as a failure instead of a silent green.
  const allCreditable = manifest.counts.creditable === manifest.entries.length;
  return allCreditable ? 0 : 1;
}

if (import.meta.main) {
  process.exit(main(process.argv));
}
