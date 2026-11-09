---
description: "FlashBeat lean per-issue specification template (project-local override of the Spec Kit core spec template)"
---

# Spec: ロングノーツ (HOLD) の仕様凍結 — Web 譜面編集 → JSON → Unity 判定 (Issue #164)

**Issue**: #164 | **Branch**: `164` | **PR base**: `release-0-1-11` | **Date**: 2026-11-07 | **Status**: Draft

**Input**: `ロングノーツ (HOLD) の仕様凍結とWeb編集からUnity判定までの実装`

> Lean rules (enforced by `docs/agent/spec-kit-workflow.md`):
> one Issue = one branch = one worktree = one Draft PR; ≤5 tasks; no retrospective
> re-specification of the legacy game; no second authority for project governance.

## Objective

Tap のみ Journals な現行の譜面形式に **HOLD（ロングノーツ）** を後方互換で追加し、
「Web スタジオで長押しを置き → 正しい JSON として公開 → Unity が同じレーン・同じ
長さで判定する」までを一気通貫で実装する。既存の tap 譜面・既存の公開曲・既存のビルドは
一切壊さない。

この Issue で確定する (凍結する) もの:

| 凍結対象 | 決定 |
| --- | --- |
| データモデル | `notes[].type` に `2 = HOLD` を追加。HOLD は `num` = 開始拍、`LPB` = 共通解像度、`endNum` = 終了拍 (行番号) を **同一 note オブジェクト** に持たせる |
| タイミング | 開始 = `num / LPB` 拍、終了 = `endNum / LPB` 拍。`NotesManager.NoteTimeSeconds` と同じ式 |
| 判定 | **先頭 1 判定 + 終端で離す** (2 判定方式)。中間経過で連続評価はしない |
| 離す許容幅 | 終端の `Great` 判定幅 (±0.15s) を「早期リリース」「保持継続」の両方向に適用 |
| 中断 | 途中で離すと **即 BAD + コンボ維持**。押し直しは **不可** (その note は終了まで失格) |
| スコアリング | HOLD は tap と **同一の配点** (Perfect 5 / Great 3 / Bad 1)。`maxScore` は note 数×5 のまま |
| 互換形式 | `type` を `1` → `2` に変えるだけで新しい JSON フィールド **不要**。tap 譜面は 1 バイトも変わらない |

## Owned Paths

- `contracts/schemas/chart.v1.schema.json` — `notes[].type` を `const 1` から
  `enum [1, 2]` へ拡張し、`type: 2` のときだけ `endNum` を要求する
  `if/then` 条件を足す (**後方互換**: 既存 tap 譜面はそのまま通る)
- `contracts/schemas/chart.v1.schema.json` のコピー
  `web/src/lib/chart/chart.v1.schema.json` — バイト同一を保つ (契約テストが検出する)
- `web/src/lib/chart/types.ts` — `ChartNote.type` の意味、`ChartNote.endNum` 追加、
  ノート種別定数 (`NOTE_TYPE_TAP` / `NOTE_TYPE_HOLD`) の追加
- `web/src/lib/chart/long-note.ts` (新規) — HOLD の中核ロジック (生成 / 長さ計算 /
  終端計算 / 最小長制約 /  snap / 検証)
- `web/src/lib/chart/notes.ts` — HOLD の追加・長さ変更・移動操作
- `web/src/lib/chart/validate.ts` (新規) — 意味論バリデーション (契約スキーマでは
  表現できない「終端 <= 開始」「同じレーンに重なり」「曲長超過」の検査)
- `web/src/lib/chart/schema.ts` — `validateChart` が意味論チェックも_Returns_
  ようにする (既存 API を壊さない)
- `web/src/lib/chart/export.ts` / `import.ts` — HOLD の無損失往復 (既存の
  「解析済みオブジェクトをそのまま書き出す」方針を維持)
- `web/src/components/chart-editor/note-grid.tsx` — HOLD の描画 (開始/終端の
  ハンドル、保持ボディ) と drag / resize による長さ編集
- `web/src/components/chart-editor/chart-editor.tsx` — long note 関連部分のみ
  (HOLD 追加操作、長さ変更操作、選択状態、キーボード操作)
- `Assets/Game/Scripts/Gameplay/LongNote.cs` (新規) — HOLD の判定ロジック
  (純 C#、MonoBehaviour 非依存で EditMode テスト可能)
- `Assets/Game/Scripts/Gameplay/NotesManager.cs` — `Note.endNum` の読み込み、
  HOLD ノートの生成、`maxScore` の不変
- `Assets/Game/Scripts/Gameplay/Judge.cs` — long note 判定に必要な最小限
- `Assets/Game/Scripts/Gameplay/Notes.cs` — long note の表示に必要な最小限
- `Assets/Tests/Editor/LongNoteTests.cs` (新規) — HOLD 判定の EditMode テスト
- `web/src/lib/chart/__tests__/long-note.test.ts` (新規) — Web 側の往復/検証テスト
- `specs/164-long-note/{spec,plan,tasks}.md`

## Exclusions (explicitly out of scope)

- `web/src/app/**`, `web/pages/**` — Worker F の排他所有
- `web/src/lib/chart/lyrics*.ts` および歌詞の編集/表示 — Worker E の排他所有
- `web/src/components/chart-editor/editor-toolbar.tsx`,
  `web/src/components/ui/select.tsx`, `web/src/app/topbar.tsx` — Worker C の排他所有
- `web/src/components/ui/**` のその他 — Worker C / F の排他所有
- `Assets/Game/Scripts/UI/V5/*View.cs`, `Assets/Game/Scripts/**/*SceneManager.cs`,
  `Assets/**/*.unity`, `Assets/Game/Prefabs/**` — 他 Worker / Supervisor の排他所有。
  HOLD ノートの見た目追加は **コード側 (スクリプト) のみ** で行い、Prefab / Scene への
  反映は本 Issue の外とする
- ランキングへの HOLD スコア反映、`scoringVersion` による記録分離 — 別 Issue
  (#30 §6 の未確定点。本 Issue は「Tap と同じ配点」という互換選択により、
  ランキングの既存記録を壊さない方針に固定する)
- 本番 Cloudflare デプロイ、Unity Editor 実行、実機 Windows での検証 —
  Supervisor の直列作業
- 譜面 JSON の v2 スキーマ Version — 「未知の JSON 形式を作らない」方針により、
  既存 `type` フィールドの意味拡張 (tap=1 → hold=2) のみに留める

## User Scenarios & Testing *(mandatory)*

### Scenario 1 — 既存 tap 譜面が壊れない (Priority: P1)

**Given** Unity プロジェクト同梱の 26 曲ぶんの tap 譜面
(`Assets/Game/Resources/*.json`) と、既に公開済みの catalog が存在する
**When** HOLD 仕様を適用した変更 Certified ビルドで既存曲を再生し、
Web エディタで既存譜面を読み込み/書き出しし、公開 API を通す
**Then** すべての既存譜面の note 数・最後の note の時刻・判定結果が
**変更前と完全に一致**し、JSON ファイルは 1 バイトも変わらない

**Independent Test**:
`cd web && npx vitest run src/lib/chart/__tests__/` (既存の
`roundtrip.test.ts` / `editing.test.ts` / `validation.test.ts` が無改変で通る)
+ `Unity -batchmode -quit -projectPath . -runTests -testPlatform editmode`
(Supervisor 実行。`ChartDataIntegrityTests.TheExistingTapChartsAreStillInterpretedTheSameWay`
と `EveryNoteOfTheExistingChartsIsATapNote` が通ることを要求)

### Scenario 2 — Web で HOLD を置き、公開し、Unity が同じ長さ/レーンで判定する (Priority: P1)

**Given** Web スタジオの譜面エディタに既存の tap 譜面を開いた状態
**When** 任意のレーンに HOLD を配置し、終端ハンドルをドラッグして長さを
調整し、書き出して公開 API (`PUT /api/v1/admin/songs/{songId}`) へ送る
**Then** 公開 JSON に `type: 2` と `endNum` が入り、`GET /api/v1/charts/{songId}`
から同一内容の譜面が取得でき、Unity がその譜面を読み込んで開始位置と
終了位置运河に判定する

**Independent Test**:
`cd web && npx vitest run src/lib/chart/__tests__/long-note.test.ts`
(HOLD の生成 → 書き出し → 読み込み → 再書き出しの無損失往復、
公開セマンティクス検証の通過を検証) +
`cd web && node --test tests/publish-schema.test.ts` (契約コピー整合)

### Scenario 3 — HOLD の判定挙動 (押す/保持/離す/押し直し) (Priority: P2)

**Given** 開始 4 拍・終了 8 拍の HOLD 1 本を含む譜面
**When** プレイヤーが出発時にキーを押し、終端まで保持し、終端で離す
**Then** 先頭で 1 回だけ判定が出て (Perfect/Great/Bad)、終端での離しが
許容幅内なら HOLD は成功として確定し、許容幅外なら BAD になる。
途中で離すと即 BAD となり、押し直してもその HOLD は成功にならない

**Independent Test**: `LongNoteTests.cs` (EditMode)。
`LongNote.JudgeHead` / `LongNote.JudgeRelease` / `LongNote.IsHolding` の
純粋関数テスト (短い HOLD / 長い HOLD / 離す / 押し直し を含む)

### Edge Cases

- **終端 <= 開始** (長さ 0 または負) の HOLD は **公開前に拒否**され、理由が
  JSON pointer 付きで_VALIDATION PANEL_ に表示される
- **同じレーンで時間が重なる 2 本の HOLD** は公開前に拒否される
- **曲長 (durationSec) を超える HOLD** は公開前に拒否される
- **最小長**: HOLD の長さは 1 拍以上 (開始行と終了行は異なる行)。
  これ未満の リサイズ操作は 1 拍まで補正される
- **空中離し**: 終端より前にキーを離すと、保持中の状態は「中断」として
  BAD になり、以降の押し直しは救済しない
- **同レーンtap + HOLD の混在**: HOLD の開始/終了と同じレーン・同じ時刻に
  tap があってよい (別レーン Same。tap として独立に判定される)
- **テンポ変更 (BPM)**: `endNum` は行番号で保存されるため、BPM が変わっても
  拍数は保たれる (秒数は変わるのは仕様どおり)
- **分割解像度 (LPB)**: HOLD の `LPB` は `num` と `endNum` の両方に適用される。
  異なる `LPB` を持つノートの混在は許さない (既存の tap 仕様も解像度ごとの混在可だが、
  HOLD は単一解像度を要求する)
- **押しっぱなし**: 終端をすぎても離さないまま保持されている場合、終端
  `GREAT_WINDOW` 経過で成功として確定する (ノーツがキューに残ったままに
  ならないように)
- **押されなかった HOLD**: 開始時刻 + `BAD_WINDOW` をすぎても押されなかった
  HOLD は、通常の tap と同じように **Miss** として数える
- **保持中の HOLD がキューに残ること**: HOLD は終端まで消えないので、
  判定の窓と Miss 判定は保持中の HOLD を飛ばす。**保持中の HOLD が
  後ろの tap の判定を妨げない** ことを保証する
- **旧クライアント (Web bundle / Unity build)**: 古い Unity ビルドは
  `type: 2` の note を tap として誤って読む可能性があるため、公開前の
  バリデーションが「HOLD を含む譜面は未対応クライアントでの再生を保証しない」
  ことを警告として出す (実装は本 Issue のスコープ外、警告文言のみ)

## Requirements

- **R1**: `contracts/schemas/chart.v1.schema.json` は `notes[].type` が `1` (tap) または
  `2` (HOLD) を許可し、`type: 2` の note には整数 `endNum >= 0` を要求する。
  `type: 1` の note は `endNum` を持たなくても (あるいは持っても extra
  property として無視されても) 従来通り通る
- **R2**: HOLD の長さは「開始行 `num` と終了行 `endNum` の差」として表現し、
  拍数 = `(endNum - num) / LPB`、秒数 = `60 / BPM * (endNum - num) / LPB` で計算する。
  判定はこの式と同一の式を使う
  (例: 120 BPM・LPB 4 で開始行 16・終了行 20 なら 1 拍 = 0.5 秒、
  開始行 0・終了行 32 なら 8 拍 = 4 秒)
- **R3**: HOLD の判定は「先頭の 1 判定 (tap と同じ幅: Perfect <= 0.10s,
  Great <= 0.15s, Bad <= 0.20s) + 終端での離し判定 (Great 幅 ±0.15s)」の
  2 判定方式とする。保持中 (開始〜終端) の連続評価は行わない
- **R4**: 保持中にキーを離すと **即 BAD** (スコア 1、コンボは継続) とし、
  押し直してもその HOLD は成功にならない (復帰不可)
- **R5**: HOLD の配点は tap と同一 (Perfect 5 / Great 3 / Bad 1) とし、
  `GManager.maxScore` は「note 数 × 5」の定義を維持する (HOLD も 1 note として数える)
- **R6**: HOLD と tap は同じレーンキーで押せる (8レーンの代替キーすべてが対象)。
  同じレーンで HOLD と tap が同時に存在しても判定は独立する
- **R7**: Web エディタは HOLD の開始/終了を配置でき、終端ハンドルのドラッグで
  長さを編集でき、長さは 1 拍以上 (解像度のグリッドにスナップ) に保たれる
- **R8**: HOLD を含む譜面は import → export → import で無損失に往復する
  (note の順序、`endNum`、未知のトップレベルフィールド、lyrics ブロックを含む)
- **R9**: 公開前バリデーションは「終端 <= 開始」「同レーンの重複 HOLD」
  「曲長超過」を **JSON pointer 付き** で拒否する
- **R10**: 既存の tap 譜面・既存の公開曲・既存の Web bundle / Unity build の
  挙動は変更されない (既存の tap チャートの解釈は `ChartDataIntegrityTests` の
  pinned 値と一致し続ける)
- **R11**: 未知の JSON 形式を新規に作らない。HOLD は既存 note オブジェクトへの
  フィールド追加 (`endNum`) と `type` の値追加 (2) だけで表現する

## Known Issues (この Issue が解かないもの)

### K1 — publish API の意味論検証が HOLD を弾く (解決済み)

> **状態: 解決 (継続作業セッションで修正)。** `semanticDiagnostics()` は
> `type` を `1 | 2` を受け付けるように拡張され、`type === 2` のときだけ
> `endNum` の存在と `endNum > num` を検査する。HOLD 譜面の publish → GET
> 往復は統合テスト (`web/tests/api.test.ts`) で確認済み。

`web/routes/api/v1/_publish-model.ts` の `semanticDiagnostics()` が

```ts
if (note.type !== 1) {
  diagnostics.push({
    path: `${path}/type`,
    message: `only Tap notes (type 1) are supported, got type ${note.type}`,
  });
}
```

として Tap 以外を拒否するため、**HOLD を含む譜面は公開できない** (422)。

このファイルは Issue #164 の所有パス (`web/src/lib/chart/**`,
`web/src/components/chart-editor/{note-grid,chart-editor}.tsx`,
`contracts/schemas/chart.v1.schema.json`, `Assets/Game/Scripts/Gameplay/*`)
に含まれないため、Worker D は変更していない。

**必要な修正 (1 ファイル、1 箇所):** `type` の許可値を `1` から
`1 | 2` に広げ、`type === 2` のときだけ `endNum` があることと
`endNum > num` であることを検査する。**契約層 (`validatePublishRequest`) は
すでに HOLD を正しく受け付けている** ので、スキーマの変更は不要。

**影響範囲:** 公開 (publish) のみ。Web エディタでの作成・長さ編集・
書き出し・検証・GET 往復は影響を受けない。

**実装 (継続作業セッション):**

- `web/routes/api/v1/_publish-model.ts`
  - `NOTE_TYPE_TAP = 1` / `NOTE_TYPE_HOLD = 2` を公開し、`LegacyNote` に
    `endNum?: number` を追加した。
  - `type` の許可値を `1 | 2` に広げ、`type === 2` のときだけ
    `endNum` が存在することと `endNum > num` であることを検査する。
    どちらを満たさない場合は該当ノートの JSON パス
    (`/chart/notes/{i}/endNum`) を名指しする診断を返す。
  - 曲長チェック (`durationSec`) は HOLD では **終端 `endNum`** を使う。
    保持中はノーツが画面に残るため、開始位置ではなく終端が曲長を決める。
- テスト
  - `web/tests/publish-semantics.test.ts` (新規) — 意味論層の単体テスト。
    Tap のみ譜面が通ること、HOLD 譜面が通ること、`endNum` 欠落・`endNum <= num`・
    未知の `type`・曲長超過の HOLD が名指しで拒否されること。
  - `web/tests/api.test.ts` — 統合テストに HOLD の publish → GET 往復
    (publish → `GET /api/v1/charts/{songId}?version=N` が同一の `notes` を返す、
    カタログの `noteCount` が一致) と HOLD 3 種類の拒否 (422, 書き込みなし) を追加。

**影響範囲:** 公開 (publish) のみ。Tap 譜面の公開挙動は変わらない
(`type === 1` の判定は従来通り通り、`endNum` は Tap では無視される)。

### K2 — HOLD の見た目 (Prefab / Scene) は未反映

HOLD の体は `Notes.cs` が `localScale.z` を伸ばして表現する (コードのみ)。
Prefab (`Assets/Game/Prefabs/Note.prefab`) と Scene
(`Assets/**/*.unity`) は他 Worker / Supervisor の排他所有のため触っていない。
実機での見た目 (GameScene の white tap bar と同じ見た目になるか) は
Supervisor の実機検証で確認する。見た目が不十分なら、別 Issue で
HOLD 専用の Prefab を追加する。

### K3 — `GManager` への検証用曲の登録が未実施

実機検証用の HOLD 入り譜面 (`holdtest.json` + `holdtest_text.json`) と
`GManager.cs` への 1 曲分の追加は `GManager.cs` の排他所有のため
この PR に含めていない。手順は `plan.md` の「E2E 手順書 §6」に記載した
(Supervisor 実施)。

### K4 — ランキングの記録分離

HOLD の配点がタップと同一であるため、既存のランキング記録の解釈は
変わらない。`scoringVersion` による分離は不要と判断した (spec.md の
「スコアリング互換」参照)。もし将来的に HOLD の配点を変えるなら、
そのときに別の Issue で分離する。

## Dependencies & Gate

- **Depends on**: なし (Issue #164 は自立している)。
  Web の公開経路 (issue #16/#19) と Unity の cloud catalog (issue #17) は
  既に存在し、HOLD の追加に触れずに再利用できる
- **Blocks**: HOLD を含む譜面のランキング/`scoringVersion` 分離 Issue
  (#30 §6 の未確定点を解決する follow-up)
- **Gate**:
  1. `cd web && npm test` が green (既存の全テスト + 新規 long-note テスト)
  2. `cd web && npx tsc --noEmit` がエラーなし
  3. Unity EditMode テスト (`LongNoteTests` + 既存 `ChartDataIntegrityTests`) が green
     (実行は Supervisor)
  4. 契約コピー (`web/src/lib/chart/chart.v1.schema.json`) が
     `contracts/schemas/chart.v1.schema.json` とバイト同一
  5. Supervisor による実ブラウザ E2E (作成 → 公開 → 再取得) と
     実機 Unity プレイの確認

## Verification Evidence

[Evidence that will be attached to the PR, bound to the candidate SHA.]

| # | Check | Command / Method | Expected result |
|---|-------|-------------------|-----------------|
| V1 | Web ユニットテスト (既存 + 新規) | `cd web && npx vitest run src/lib/chart/__tests__/` | 全 green、`long-note.test.ts` を含む |
| V2 | Web 型チェック | `cd web && npx tsc --noEmit` | エラー 0 |
| V3 | 契約整合 (コピー) | `cd web && npx vitest run src/lib/chart/__tests__/contract-schema.test.ts` | green (バイト同一) |
| V4 | 公開 API 契約テスト | `cd web && node --test tests/publish-schema.test.ts` | green |
| V5 | Unity EditMode テスト | `Unity -batchmode -quit -projectPath . -runTests -testPlatform editmode -testResults TestResults.xml` | `LongNoteTests` 全 pass、既存 `ChartDataIntegrityTests` に regression なし (**Supervisor 実行**) |
| V6 | 既存 tap 譜面の無変更 | `git diff --stat 2f2b6ba -- Assets/Game/Resources/` | 差分 0 件 |
| V7 | 実ブラウザ E2E (公開 → 再取得) | `cd web && npm run e2e` + 手動 (1920x1080 / 2560x1440 / 2560x1664) | HOLD 譜面が publish → GET で同一 (**Supervisor 実行**) |

## Rollback

この Issue の差分は (a) 契約スキーマの `type` enum 拡張、(b) Web 側の HOLD ロジックと
UI、(c) Unity 側の HOLD 判定と新規ファイル、という 3 つの独立した差分に分割できる。
`contracts/schemas/chart.v1.schema.json` の `type` を `const 1` に戻し
`web/src/lib/chart/chart.v1.schema.json` のコピーを同期すれば **契約だけ**を
1 コミットで元に戻せる。Web / Unity 側の HOLD 実装は新しい note type しか読まないので、
戻しても既存 tap 譜面の挙動は変わらない。全体を戻す場合は merge commit を
revert するかブランチ `164` を削除すれば、他ブランチの作業には影響しない
(Owned Paths の境界が守られているため)。

## Success Criteria

- **SC1**: 既存 26 曲tap 譜面の note 数・最終 note 時刻・判定結果が
  変更前と一致し、`Assets/Game/Resources/` に差分がない
- **SC2**: Web で HOLD を配置・長さ編集・書き出し・公開でき、
  `GET /api/v1/charts/{songId}` から無損失に取得できる (自動テストで証明)
- **SC3**: Unity が HOLD を開始位置と終了位置の运河に判定し、
  保持中断・押し直しが仕様どおりに扱われる (EditMode テストで証明)
- **SC4**: HOLD を含む譜面が既存のランキング記録の解釈を壊さない
  (HOLD の配点が tap と同一であることの明文化と、既存公開曲に HOLD が
  含まれないことの確認)
