// Tests for the review report (Issue #389).
//
// The report's job is to be trustworthy at the top: the owner should be able to
// read "what needs a decision" without opening screenshots. The negative
// fixtures below are the point of the exercise - a report that cannot say
// "unverified", "stale" or "no design node" is worse than none.
import { describe, expect, test } from 'bun:test';
import { buildManifest, type EvidenceRecord, type ManifestInput } from '../evidence-manifest/manifest.mts';
import {
  buildReport,
  claimsWithoutDesignNode,
  deterministicFindings,
  renderMarkdown,
  type HumanDecision,
  type ReviewNote,
} from './report.mts';

const CANDIDATE = '0.1.23+af183bf4';

function input(records: EvidenceRecord[]): ManifestInput {
  return { candidateBuild: CANDIDATE, candidatePlatform: 'macos', records };
}

function record(overrides: Partial<EvidenceRecord> = {}): EvidenceRecord {
  return {
    claim: '371',
    state: 'Opening は映像のみ',
    buildSha: CANDIDATE,
    platform: 'macos',
    resolution: '3420x2224',
    path: 'docs/evidence/368-review-2026-11-12/369-launch-1.png',
    designNode: 'QJtTv',
    ...overrides,
  };
}

function manifestOf(records: EvidenceRecord[]) {
  return buildManifest(input(records), '.');
}

describe('deterministic findings', () => {
  test('a fully creditable, node-referenced manifest has no findings', () => {
    const manifest = manifestOf([record()]);
    expect(deterministicFindings(manifest)).toEqual([]);
    expect(claimsWithoutDesignNode(manifest)).toEqual([]);
  });

  test('a stale capture is a finding, not a silent pass', () => {
    const manifest = manifestOf([record({ buildSha: '0.1.23+b5b98a6e' })]);
    const findings = deterministicFindings(manifest);
    expect(findings).toHaveLength(1);
    expect(findings[0].kind).toBe('stale');
    expect(findings[0].claim).toBe('371');
  });

  test('a missing image is a finding', () => {
    const manifest = manifestOf([record({ path: 'docs/evidence/nope.png' })]);
    expect(deterministicFindings(manifest)[0].kind).toBe('missing');
  });

  test('a capture without a build is a finding', () => {
    const manifest = manifestOf([record({ buildSha: undefined })]);
    expect(deterministicFindings(manifest)[0].kind).toBe('unverified');
  });

  test('an unsupported design node is a finding', () => {
    const manifest = buildManifest(
      input([record({ designNode: 'NOT-A-NODE' })]),
      '.',
    );
    // The manifest only reports mismatch when it is given the design node list.
    const withNodes = buildManifest(
      { ...input([record({ designNode: 'NOT-A-NODE' })]), designNodes: ['QJtTv'] },
      '.',
    );
    expect(deterministicFindings(withNodes)[0].kind).toBe('mismatch');
    expect(deterministicFindings(manifest)).toEqual([]);
  });

  test('a creditable claim with no design node is still reported', () => {
    const manifest = manifestOf([record({ designNode: undefined })]);
    expect(deterministicFindings(manifest)).toEqual([]);
    const noNode = claimsWithoutDesignNode(manifest);
    expect(noNode).toHaveLength(1);
    expect(noNode[0].claim).toBe('371');
  });
});

describe('report completeness', () => {
  test('is complete only with evidence, design nodes and no open decision', () => {
    const manifest = manifestOf([record()]);
    expect(buildReport({ manifest }).complete).toBe(true);
  });

  test('is incomplete when a human decision is open', () => {
    const decision: HumanDecision = {
      id: '1',
      claim: '369',
      question: 'メニューバー 76 px を許容するか',
      context: 'macOS プレイヤーの制約。',
    };
    const report = buildReport({ manifest: manifestOf([record()]), decisions: [decision] });
    expect(report.complete).toBe(false);
    expect(report.decisions).toHaveLength(1);
  });

  test('is incomplete when a claim has no design node', () => {
    const manifest = manifestOf([record({ designNode: undefined })]);
    expect(buildReport({ manifest }).complete).toBe(false);
  });

  test('is incomplete when any evidence is not creditable', () => {
    const manifest = manifestOf([record({ buildSha: '0.1.23+b5b98a6e' })]);
    expect(buildReport({ manifest }).complete).toBe(false);
  });
});

describe('markdown', () => {
  const note: ReviewNote = {
    claim: '371',
    observation: 'Opening は映像のみで、ロゴ・背景パネルは描かれていない。',
    unproven: ['映像の自動遷移'],
  };

  test('puts the decisions first and never claims a pass', () => {
    const manifest = manifestOf([record()]);
    const md = renderMarkdown(buildReport({ manifest, notes: [note] }), manifest);

    // The decisions section comes before the evidence tables.
    expect(md.indexOf('## 1. 判断が必要なこと')).toBeLessThan(md.indexOf('## 2. 証跡の状態'));

    // No PASS wording anywhere: the report is not a verdict.
    expect(md).not.toContain('APPROVED');
    expect(md).not.toContain('PASS');
    expect(md).toContain('UI の合否を判定しません');
  });

  test('reports the counts and the unusable evidence', () => {
    const manifest = manifestOf([
      record(),
      record({ claim: '372', state: 'stale', buildSha: '0.1.23+b5b98a6e' }),
    ]);
    const md = renderMarkdown(buildReport({ manifest }), manifest);

    expect(md).toContain('| 対象の主張 | 2 |');
    expect(md).toContain('根拠として使える (creditable) | 1 (50%)');
    expect(md).toContain('| stale | 1 |');
    expect(md).toContain('#372');
  });

  test('lists the claims that cannot be compared with the design', () => {
    const manifest = manifestOf([record({ designNode: undefined })]);
    const md = renderMarkdown(buildReport({ manifest }), manifest);
    expect(md).toContain('design ノード参照が無い主張');
    expect(md).toContain('#371');
  });

  test('separates the reviewer reading from the machine findings', () => {
    const manifest = manifestOf([record()]);
    const md = renderMarkdown(buildReport({ manifest, notes: [note] }), manifest);

    expect(md).toContain('## 3. レビューの読み (人の解釈)');
    expect(md).toContain('合格の主張ではありません');
    expect(md).toContain(note.observation);
    expect(md).toContain('未確認:');
    expect(md).toContain('映像の自動遷移');
  });

  test('states what the report cannot say', () => {
    const manifest = manifestOf([record()]);
    const md = renderMarkdown(buildReport({ manifest }), manifest);
    expect(md).toContain('## 4. この報告で言えないこと');
    expect(md).toContain('実 Win64 での動作');
  });

  test('says so plainly when there is nothing to decide', () => {
    const manifest = manifestOf([record()]);
    const md = renderMarkdown(buildReport({ manifest }), manifest);
    expect(md).toContain('なし。');
    expect(md).toContain('未確認なし');
  });
});
