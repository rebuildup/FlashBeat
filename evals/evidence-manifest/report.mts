// Review report for the v6 real-device UI review (Issue #389).
//
// Input: the verified manifest from Issue #388. Output: a small, actionable
// report that separates
//
//   1. deterministic findings   - computed from the manifest and the evidence
//                                 files, no judgement involved
//   2. reviewer interpretation  - what the review team read out of a capture
//                                 (never phrased as PASS)
//   3. human decisions          - the questions only the owner can answer
//
// The governing rule is that **this report never declares a UI PASS**. It can
// say "this claim has creditable evidence", "this claim has no evidence", or
// "this claim's evidence is stale", and it reproduces the reviewer's note
// verbatim. Whether a screen looks right is a visual judgement this tool does
// not make.
import { readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import type { Manifest, ManifestEntry } from '../evidence-manifest/manifest.mts';

/** A reviewer's reading of one claim. Never a pass/fail verdict. */
export interface ReviewNote {
  claim: string;
  /** What was observed, in the reviewer's words. */
  observation: string;
  /** What remains unproven about this claim. */
  unproven: string[];
}

/** A question only the owner can answer. */
export interface HumanDecision {
  id: string;
  claim: string;
  question: string;
  context: string;
}

export interface ReviewInput {
  manifest: Manifest;
  notes?: ReviewNote[];
  decisions?: HumanDecision[];
}

export interface DeterministicFinding {
  kind: 'missing' | 'stale' | 'unverified' | 'mismatch';
  claim: string;
  state: string;
  path: string;
  reason: string;
}

export interface ReviewReport {
  candidateBuild: string;
  candidatePlatform: string;
  /** Total claims the manifest covers. */
  claims: number;
  /** Claims with evidence that can be used for this candidate. */
  creditable: number;
  findings: DeterministicFinding[];
  notes: ReviewNote[];
  decisions: HumanDecision[];
  /** True when nothing is left for a human to decide or verify. */
  complete: boolean;
}

/**
 * Collect the deterministic findings: everything that is not `creditable`.
 * Order follows the manifest so a diff of the report stays readable.
 */
export function deterministicFindings(manifest: Manifest): DeterministicFinding[] {
  const findings: DeterministicFinding[] = [];
  for (const entry of manifest.entries) {
    if (entry.status === 'creditable') continue;
    findings.push({
      kind: entry.status,
      claim: entry.claim,
      state: entry.state,
      path: entry.path,
      reason: entry.reason,
    });
  }
  return findings;
}

/**
 * Claims the manifest marks creditable but that carry no design-node reference.
 * Issue #388 requires `Issue → pen node/revision`; without it the review cannot
 * compare against the design, so it is reported rather than hidden.
 */
export function claimsWithoutDesignNode(manifest: Manifest): ManifestEntry[] {
  return manifest.entries.filter((e) => e.status === 'creditable' && !e.designNode);
}

export function buildReport(input: ReviewInput): ReviewReport {
  const { manifest } = input;
  const findings = deterministicFindings(manifest);
  const notes = input.notes ?? [];
  const decisions = input.decisions ?? [];

  const creditable = manifest.counts.creditable ?? 0;

  // The report is only complete when the evidence is all creditable *and*
  // every claim carries a design reference *and* no human decision is open.
  const allDesignNodes = claimsWithoutDesignNode(manifest).length === 0;

  return {
    candidateBuild: manifest.candidateBuild,
    candidatePlatform: manifest.candidatePlatform,
    claims: manifest.entries.length,
    creditable,
    findings,
    notes,
    decisions,
    complete: findings.length === 0 && allDesignNodes && decisions.length === 0,
  };
}

/**
 * Render the report as Markdown. The shape is deliberate: the owner reads the
 * top (what needs a decision), and everything below is reference.
 */
export function renderMarkdown(report: ReviewReport, manifest: Manifest): string {
  const lines: string[] = [];
  const pct = report.claims > 0 ? Math.round((report.creditable / report.claims) * 100) : 0;

  lines.push(`# v6 実機レビュー報告 — ${report.candidateBuild} (${report.candidatePlatform})`);
  lines.push('');
  lines.push('> この報告は **UI の合否を判定しません**。根拠として使える証跡があるか、');
  lines.push('> 何が未確認か、何を判断してほしいかだけを示します。');
  lines.push('');

  lines.push('## 1. 判断が必要なこと');
  lines.push('');
  if (report.decisions.length === 0) {
    lines.push('なし。');
  } else {
    for (const d of report.decisions) {
      lines.push(`### D-${d.id}: ${d.question}`);
      lines.push('');
      lines.push(d.context);
      lines.push('');
    }
  }

  lines.push('## 2. 証跡の状態 (機械判定)');
  lines.push('');
  lines.push(`| 項目 | 件数 |`);
  lines.push(`|---|---|`);
  lines.push(`| 対象の主張 | ${report.claims} |`);
  lines.push(`| 根拠として使える (creditable) | ${report.creditable} (${pct}%) |`);
  for (const kind of ['missing', 'stale', 'unverified', 'mismatch'] as const) {
    const count = manifest.counts[kind] ?? 0;
    lines.push(`| ${kind} | ${count} |`);
  }
  lines.push('');

  if (report.findings.length > 0) {
    lines.push('### 使えない証跡');
    lines.push('');
    lines.push('| 種別 | Issue | 状態 | 理由 |');
    lines.push('|---|---|---|---|');
    for (const f of report.findings) {
      lines.push(`| ${f.kind} | #${f.claim} | ${f.state} | ${f.reason} |`);
    }
    lines.push('');
  }

  const noNode = claimsWithoutDesignNode(manifest);
  lines.push('### design ノード参照が無い主張');
  lines.push('');
  if (noNode.length === 0) {
    lines.push('なし。');
  } else {
    lines.push(`${noNode.length} 件。design との照合ができないため、視覚的な妥当性は未確認です。`);
    lines.push('');
    lines.push('| Issue | 状態 |');
    lines.push('|---|---|');
    for (const e of noNode) lines.push(`| #${e.claim} | ${e.state} |`);
  }
  lines.push('');

  lines.push('## 3. レビューの読み (人の解釈)');
  lines.push('');
  lines.push('> ここは機械判定ではありません。**合格の主張ではありません。**');
  lines.push('');
  if (report.notes.length === 0) {
    lines.push('なし。');
  } else {
    for (const n of report.notes) {
      lines.push(`### #${n.claim}`);
      lines.push('');
      lines.push(n.observation);
      if (n.unproven.length > 0) {
        lines.push('');
        lines.push('未確認:');
        for (const u of n.unproven) lines.push(`- ${u}`);
      }
      lines.push('');
    }
  }

  lines.push('## 4. この報告で言えないこと');
  lines.push('');
  lines.push('- 画面が設計どおりに正しいか (design とのピクセル比較は未実施)。');
  lines.push('- 実 Win64 での動作 (この報告の evidence はすべて macOS かどうかで決まる)。');
  lines.push('- 未確認の操作が実機で成立するか。');
  lines.push('');
  lines.push(`報告時点の完全性: **${report.complete ? '未確認なし' : '未確認あり'}**`);
  lines.push('');

  return lines.join('\n');
}

// --- CLI --------------------------------------------------------------------

function main(argv: string[]): number {
  const args = argv.slice(2);
  const manifestPath = args.find((a) => !a.startsWith('--'));
  if (!manifestPath) {
    console.error('usage: report.mts <manifest.json> [--notes <notes.json>] [--out <file>]');
    return 2;
  }

  const manifest = JSON.parse(readFileSync(resolve(manifestPath), 'utf8')) as Manifest;

  const notesFlag = args.indexOf('--notes');
  const notes = notesFlag >= 0
    ? (JSON.parse(readFileSync(resolve(args[notesFlag + 1]), 'utf8')) as ReviewInput)
    : {};

  const report = buildReport({ manifest, notes: notes.notes, decisions: notes.decisions });
  const markdown = renderMarkdown(report, manifest);

  const outFlag = args.indexOf('--out');
  if (outFlag >= 0) {
    writeFileSync(args[outFlag + 1], markdown, 'utf8');
  } else {
    process.stdout.write(markdown);
  }

  return report.complete ? 0 : 1;
}

if (import.meta.main) {
  process.exit(main(process.argv));
}
