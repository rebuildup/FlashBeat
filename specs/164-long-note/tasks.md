---
description: "FlashBeat lean per-issue task list template (project-local override of the Spec Kit core tasks template)"
---

# Tasks: ロングノーツ (HOLD) の仕様凍結 — Web 譜面編集 → JSON → Unity 判定 (Issue #164)

**Issue**: #164 | **Branch**: `164` | **Base**: `release-0-1-11`
**Inputs**: [`spec.md`](spec.md), [`plan.md`](plan.md)

**Hard limit: at most 5 tasks per issue.** If the work does not fit in 5 tasks,
the issue is too big — split it and open a follow-up Issue.

## Format: `[ID] [P?] Description (path)`

- **[P]**: can run in parallel with other `[P]` tasks — different files, no ordering dependency
- Every task names the exact file path it creates or changes
- One task = one verifiable state change

## Tasks

- [ ] **T1** 仕様凍結: `specs/164-long-note/spec.md` に HOLD のデータモデル
  (`type: 2` + `endNum`)、タイミング式、2 判定方式 (先頭 1 判定 + 終端離し)、
  中断/押し直しの扱い、配点 (tap と同一)、`maxScore` の不変、旧 tap 譜面との
  後方互換を **理由ごと** 記述し、`contracts/schemas/chart.v1.schema.json` の
  `notes[].type` を `const 1` から `enum [1, 2]` へ、`type: 2` のときだけ
  `endNum` (整数, 最小 0) を要求する `if/then` へ拡張する。
  併せて `web/src/lib/chart/chart.v1.schema.json` をバイト同一で同期し、
  `web/src/lib/chart/types.ts` に `NOTE_TYPE_TAP` / `NOTE_TYPE_HOLD` と
  `ChartNote.endNum` を追加する
  — **done when**: `cd web && npx vitest run src/lib/chart/__tests__/contract-schema.test.ts`
  が green (コピー整合)、既存 26 曲 tap 譜面の import/export が無変更で通る

- [ ] **T2** Web エディタ: `web/src/lib/chart/long-note.ts` (新規) に HOLD の
  生成 / 長さ計算 / 終端計算 / snap (最小 1 拍) / 検証ロジックを置き、
  `web/src/lib/chart/notes.ts` に HOLD の追加・長さ変更操作、
  `web/src/lib/chart/validate.ts` (新規) に「終端 <= 開始」「同レーンの重複
  HOLD」「曲長超過」の JSON pointer 付き検査、`web/src/lib/chart/schema.ts` の
  `validateChart` に意味論チェックの組み込みを行う。
  `web/src/components/chart-editor/note-grid.tsx` で HOLD を開始/終端ハンドル
  付きで描画し、`web/src/components/chart-editor/chart-editor.tsx` の long note
  関連部分 (HOLD 追加操作・長さ変更操作・選択状態) を実装する
  — **done when**: `cd web && npx tsc --noEmit` がエラー 0、
  `cd web && npx vitest run src/lib/chart/__tests__/long-note.test.ts` が green、
  既存 tap 譜面を読み込んだとき note の位置・形状が変化しない

- [ ] **T3** JSON 往復: `web/src/lib/chart/__tests__/long-note.test.ts` (新規) に
  HOLD 入り譜面の import → export → import の無損失往復 (note 順序、`endNum`、
  未知のトップレベルフィールド、`lyrics` ブロック)、`type: 1` を含む混在譜面の
  往復、意味論検証の拒否ケース (長さ 0 / 重複 / 曲長超過) をテストする。
  `web/src/lib/chart/import.ts` / `export.ts` が HOLD をそのまま往復することを
  保証する
  — **done when**: `cd web && npx vitest run src/lib/chart/__tests__/` が
  既存テスト含め全 green、`cd web && node --test tests/publish-schema.test.ts` が green

- [ ] **T4** Unity 判定: `Assets/Game/Scripts/Gameplay/LongNote.cs` (新規) に
  HOLD 判定の純関数 (開始判定 `JudgeHead`、保持中判定 `IsHolding`、終端の
  離し判定 `JudgeRelease`、中断判定 `JudgeBreak`) を実装し、
  `Assets/Game/Scripts/Gameplay/NotesManager.cs` が `Note.endNum` を読み込んで
  HOLD ノートを生成し、`maxScore = noteNum * 5` を維持、
  `Assets/Game/Scripts/Gameplay/Judge.cs` が先頭判定と終端離し判定を処理し、
  `Assets/Game/Scripts/Gameplay/Notes.cs` が HOLD の表示に必要な最小限だけ
  変更する。`Assets/Tests/Editor/LongNoteTests.cs` (新規) に
  短い HOLD / 長い HOLD / 離す / 押し直し / 中断の EditMode テストを追加する
  — **done when**: `LongNoteTests.cs` が新規・既存の EditMode テストと一緒に
  green (**実行は Supervisor**)、`git diff --stat 2f2b6ba -- Assets/Game/Resources/` が空

- [ ] **T5** E2E 手順: `specs/164-long-note/plan.md` の「E2E 手順書」に
  既存 tap 譜面の Web 回帰 (§1)、HOLD の作成と長さ編集 (§2)、
  書き出しとローカル D1 への公開 (§3)、GET による無損失往復 (§4)、
  公開前バリデーションの負系 (§5)、実機 Unity での 8 項目のプレイ確認 (§6)、
  証跡のまとめ (§7) を、解像度 FHD 1920x1080 / 2K 2560x1440 /
  Mac 2560x1664 の 3 条件で記載する
  — **done when**: `plan.md` に §0–§7 が揃い、各項目に「期待する表示/数値」と
  実行コマンドが書かれており、未実行項目が「未実行 (Supervisor 実施)」と
  区別できる

## Dependencies & Execution Order

1. [T1] → [T2] → [T3] (契約 → ロジック → テスト)
2. [T4] は [T1] と並行可能 (別ディレクトリ: `Assets/Game/Scripts/Gameplay/` と
   `Assets/Tests/Editor/`。依存するのは `endNum` の意味論だけで、
   契約 JSON の実装詳細には依存しない)
3. [T5] は [T2] / [T3] / [T4] の後に、実際の画面を見ながら記述を確定する

**Blocking dependency**: なし。Web の publish API (#16/#19) と Unity の cloud
catalog (#17) は既存で動くため、この Issue 単独で成立する。

## Verification Evidence

| Task | Check | Command / Method | Expected result |
|------|-------|-------------------|-----------------|
| T1 | 契約コピー整合 | `cd web && npx vitest run src/lib/chart/__tests__/contract-schema.test.ts` | green |
| T1 | 既存 tap 譜面の往復 | `cd web && npx vitest run src/lib/chart/__tests__/roundtrip.test.ts` | green (無改変) |
| T2 | 型チェック | `cd web && npx tsc --noEmit` | エラー 0 |
| T3 | Web ユニットテスト | `cd web && npx vitest run src/lib/chart/__tests__/` | 全 green |
| T3 | 公開 API 契約 | `cd web && node --test tests/publish-schema.test.ts` | green |
| T4 | Unity EditMode | `Unity -batchmode -quit -projectPath . -runTests -testPlatform editmode` | `LongNoteTests` pass、既存テストに regression なし (Supervisor 実行) |
| T4 | 既存譜面の無変更 | `git diff --stat 2f2b6ba -- Assets/Game/Resources/` | 空 |
| T5 | 手順書の存在 | `grep -c "^### §" specs/164-long-note/plan.md` | 8 (§0–§7) |

## Rollback

契約 (`contracts/schemas/chart.v1.schema.json` + `web/src/lib/chart/chart.v1.schema.json`)
だけなら `type` を `const 1` に戻す 1 コミットで戻せる。Web / Unity 側の HOLD 実装は
`type: 2` の note しか読まないので、戻しても既存 tap 譜面の挙動は変わらない。
全体を戻す場合はブランチ `164` の merge commit を revert するかブランチを削除する。
 Owned Paths の境界内の差分なので他ブランチの作業には影響しない。

## Notes

- Scope discipline: implement only the tasks above; new ideas become follow-up Issues.
- The Unity Editor / scene / prefab mutation is serialized: only the issue that
  owns the scene may run the Editor. 本 Issue は Editor を起動しない。
- `Assets/Game/Scripts/Songs/GManager.cs` への曲追加は本 Issue の外
  (他 Worker の排他所有)。E2E 手順書 §6 で Supervisor が実施する旨を明記した。
