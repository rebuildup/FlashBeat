// Tests for the evidence manifest (Issue #388).
//
// Issue #388 asks for positive *and* negative fixtures: a missing build, a
// wrong OS, a stale screenshot and a design-node mismatch must each be
// detected, and an ordinary passing record must come out `verified`. The
// negative cases matter more than the positive one: a manifest that cannot
// say "unverified" is worse than no manifest, because it turns an unknown
// into a green.
import { describe, expect, test } from 'bun:test';
import {
  buildManifest,
  classifyRecord,
  diffManifests,
  platformMatches,
  sameBuild,
  shortSha,
  type EvidenceRecord,
  type ManifestInput,
} from './manifest.mts';

const CANDIDATE = '0.1.23+af183bf4';

function input(records: EvidenceRecord[], overrides: Partial<ManifestInput> = {}): ManifestInput {
  return {
    candidateBuild: CANDIDATE,
    candidatePlatform: 'macos',
    records,
    ...overrides,
  };
}

function validRecord(overrides: Partial<EvidenceRecord> = {}): EvidenceRecord {
  return {
    claim: '369',
    state: '起動直後の全画面',
    buildSha: CANDIDATE,
    platform: 'macos',
    resolution: '3420x2224',
    path: 'docs/evidence/368-review-2026-11-12/369-launch-1.png',
    ...overrides,
  };
}

// Every image lookup is answered by this stub so the tests do not depend on
// the real evidence tree.
const exists = () => true;

describe('build identity', () => {
  test('the short sha is the part after +', () => {
    expect(shortSha('0.1.23+af183bf4')).toBe('af183bf4');
    expect(shortSha('0.1.23')).toBeUndefined();
    expect(shortSha(undefined)).toBeUndefined();
  });

  test('the same version with a different sha is a different build', () => {
    expect(sameBuild('0.1.23+af183bf4', '0.1.23+af183bf4')).toBe(true);
    expect(sameBuild('0.1.23+af183bf4', '0.1.23+b5b98a6e')).toBe(false);
    expect(sameBuild('0.1.23', '0.1.23+af183bf4')).toBe(false);
  });
});

describe('platform rules', () => {
  test('macOS evidence is never credited to win64', () => {
    expect(platformMatches('macos', 'win64')).toBe(false);
  });

  test('WSL is not Windows-native', () => {
    expect(platformMatches('wsl', 'win64')).toBe(false);
  });

  test('a matching platform is credited', () => {
    expect(platformMatches('win64', 'win64')).toBe(true);
    expect(platformMatches('macos', 'macos')).toBe(true);
  });

  test('a missing platform is not credited', () => {
    expect(platformMatches(undefined, 'macos')).toBe(false);
  });
});

describe('positive record', () => {
  test('a complete record for the candidate is creditable', () => {
    const result = classifyRecord(validRecord(), input([]), exists);
    expect(result.status).toBe('creditable');
  });
});

describe('negative fixtures', () => {
  test('a capture with no build is unverified, not creditable', () => {
    const result = classifyRecord(validRecord({ buildSha: undefined }), input([]), exists);
    expect(result.status).toBe('unverified');
    expect(result.reason).toContain('build');
  });

  test('a capture from another build is stale', () => {
    const result = classifyRecord(
      validRecord({ buildSha: '0.1.23+b5b98a6e' }),
      input([]),
      exists,
    );
    expect(result.status).toBe('stale');
    expect(result.reason).toContain('b5b98a6e');
  });

  test('a macOS capture for a win64 claim is unverified', () => {
    const result = classifyRecord(
      validRecord({ platform: 'macos' }),
      input([], { candidatePlatform: 'win64' }),
      exists,
    );
    expect(result.status).toBe('unverified');
    expect(result.reason).toContain('macos');
  });

  test('an image that is not in the repository is missing', () => {
    const result = classifyRecord(validRecord(), input([]), () => false);
    expect(result.status).toBe('missing');
  });

  test('a design node the design does not contain is a mismatch', () => {
    const result = classifyRecord(
      validRecord({ designNode: 'QJtTv' }),
      input([], { designNodes: ['FHcxk', 'QQ4jM'] }),
      exists,
    );
    expect(result.status).toBe('mismatch');
    expect(result.reason).toContain('QJtTv');
  });

  test('a record with no resolution is unverified', () => {
    const result = classifyRecord(validRecord({ resolution: undefined }), input([]), exists);
    expect(result.status).toBe('unverified');
    expect(result.reason).toContain('resolution');
  });

  test('a record without a claim or state is unverified', () => {
    expect(classifyRecord(validRecord({ claim: '' }), input([]), exists).status).toBe('unverified');
    expect(classifyRecord(validRecord({ state: '' }), input([]), exists).status).toBe('unverified');
  });

  test('the checks run in an order that reports the most actionable reason', () => {
    // A missing file is reported before the build mismatch, because a missing
    // image cannot be reviewed at all.
    const result = classifyRecord(
      validRecord({ buildSha: '0.1.23+b5b98a6e' }),
      input([]),
      () => false,
    );
    expect(result.status).toBe('missing');
  });
});

describe('manifest', () => {
  test('counts every status and keeps the input order', () => {
    const manifest = buildManifest(
      input([
        validRecord({ claim: '369', state: 'ok' }),
        validRecord({ claim: '370', state: 'no build', buildSha: undefined }),
        validRecord({ claim: '371', state: 'stale', buildSha: '0.1.23+b5b98a6e' }),
      ]),
      '.',
    );

    expect(manifest.entries.map((e) => e.claim)).toEqual(['369', '370', '371']);
    expect(manifest.counts.creditable).toBe(1);
    expect(manifest.counts.unverified).toBe(1);
    expect(manifest.counts.stale).toBe(1);
    expect(manifest.counts.missing).toBe(0);
    expect(manifest.counts.mismatch).toBe(0);
  });

  test('an all-creditable manifest is the only one that is fully green', () => {
    const manifest = buildManifest(input([validRecord()]), '.');
    expect(manifest.counts.creditable).toBe(manifest.entries.length);
  });

  test('the sha256 is recorded for files that exist', () => {
    const manifest = buildManifest(input([validRecord()]), '.');
    // A real capture from the #368 evidence set.
    expect(manifest.entries[0].sha256).toMatch(/^[0-9a-f]{64}$/);
    expect(manifest.entries[0].bytes).toBeGreaterThan(0);
  });

  test('a missing file records no hash', () => {
    const manifest = buildManifest(
      input([validRecord({ path: 'docs/evidence/does-not-exist.png' })]),
      '.',
    );
    expect(manifest.entries[0].status).toBe('missing');
    expect(manifest.entries[0].sha256).toBeUndefined();
  });
});

describe('diff', () => {
  test('reports added, removed and status changes', () => {
    const before = buildManifest(
      input([
        validRecord({ claim: '369', state: 'kept' }),
        validRecord({ claim: '370', state: 'goes away' }),
        validRecord({ claim: '371', state: 'flips', buildSha: '0.1.23+b5b98a6e' }),
      ]),
      '.',
    );
    const after = buildManifest(
      input([
        validRecord({ claim: '369', state: 'kept' }),
        validRecord({ claim: '371', state: 'flips' }),
        validRecord({ claim: '372', state: 'new' }),
      ]),
      '.',
    );

    const diff = diffManifests(before, after);
    expect(diff.added).toEqual(['372 / new']);
    expect(diff.removed).toEqual(['370 / goes away']);
    expect(diff.changed).toEqual([
      { claim: '371', state: 'flips', from: 'stale', to: 'creditable' },
    ]);
  });

  test('an unchanged manifest has an empty diff', () => {
    const manifest = buildManifest(input([validRecord()]), '.');
    const diff = diffManifests(manifest, manifest);
    expect(diff.added).toEqual([]);
    expect(diff.removed).toEqual([]);
    expect(diff.changed).toEqual([]);
  });
});
