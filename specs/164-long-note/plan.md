---
description: "FlashBeat lean per-issue implementation plan template (project-local override of the Spec Kit core plan template)"
---

# Plan: ロングノーツ (HOLD) の仕様凍結 — Web 譜面編集 → JSON → Unity 判定 (Issue #164)

**Issue**: #164 | **Branch**: `164` | **Base**: `release-0-1-11` | **Date**: 2026-11-07
**Spec**: [`specs/164-long-note/spec.md`](spec.md)

## Summary

Tap しかない既存譜面形式に HOLD を **後方互換** で足す。JSON 形式は新規に作らず、
既存の `notes[]` オブジェクトに `type: 2` と `endNum` (終了行) を追加する
だけにする。契約スキーマ (`contracts/schemas/chart.v1.schema.json`) を
`type: const 1` から `type: enum [1,2]` へ拡張し、`type: 2` のときだけ
`endNum` を要求する `if/then` を足す。Web 側は `web/src/lib/chart/long-note.ts`
に HOLD の中核ロジック (生成・長さ計算・終端計算・snap・検証) を置き、
`note-grid.tsx` で HOLD を描画して終端ハンドルで長さを編集できるようにする。
Unity 側は `Assets/Game/Scripts/Gameplay/LongNote.cs` に **純粋関数** として
判定 (先頭 1 判定 + 終端離し判定) を実装し、`NotesManager` が `endNum` を
読み込んで HOLD ノートを生成し、`Judge` が先頭判定と終端判定を処理する。
判定・配点・タイミング式はすべて tap と同一の式を使い、既存の tap 譜面が
1 バイトも変わらないことをテストで固定する。

### 仕様凍結の詳細 (spec.md R1–R11 の実装根拠)

**データモデル**

```jsonc
// TAP (既存・変更なし)
{ "type": 1, "num": 4, "block": 3, "LPB": 4 }
// HOLD (新規)
{ "type": 2, "num": 4, "block": 3, "LPB": 4, "endNum": 12 }
```

- `num` = 開始行 (tap と同じ意味)
- `endNum` = 終了行。`endNum > num` が必須
- `LPB` = 両方の行の共通解像度。HOLD は単一解像度
- `block` = レーン (0–7)。tap と同じ

**タイミング** (tap と同一の式を `NotesManager.NoteTimeSeconds`  Spawn する)

```
開始秒 = 60 / BPM * num      / LPB
終了秒 = 60 / BPM * endNum   / LPB
長さ秒 = 60 / BPM * (endNum - num) / LPB
```

**判定** (2 判定方式。spec.md R3/R4)

| 局面 | 判定 | 幅 | スコア |
| --- | --- | --- | --- |
| 開始時の押下 | tap と同一 (Perfect/Great/Bad) | ≤0.10 / ≤0.15 / ≤0.20 s | 5 / 3 / 1 |
| 保持中 | 判定しない | — | — |
| 終端での離し | Great 相当で成功 / 幅外で BAD | 終端 ±0.15 s | 成功なら追加スコアなし (先頭で配点済み) |
| 保持中の離し | 即 BAD | — | 1 (先頭の点数は確定済み) |
| 押し直し | 救済しない | — | — |

- 押し直しを禁じる理由: Issue #30 の「押し直しの可否」は未確定点であり、
  救済ありにすると「離して BAD → 押し直して Perfect」の评分が同一 note に
  二重で発生し、`maxScore = note数 × 5` の定義と矛盾する。**1 note = 1 判定**に固定する。
- 終端の離しで追加スコアを発生させない理由: 同じく `maxScore` の定義と矛盾するため。
  離しの成否は **HOLD が成功したか** のフラグにのみ影響し、スコアは先頭で確定する。

**`maxScore`**: `noteNum * 5` のまま。HOLD も 1 note として数える
(2 判定方式では配点が tap と同一なので、note 数で割れば 100 万点算出が壊れない)。

**スコアリング互換**: HOLD の配点が tap と同一であるため、既存のランキング
記録の解釈は変わらない (`scoringVersion` の分離は不要)。Issue #30 §6 の
「混在しない仕組み」は、本 Issue では「配点を同一にする」ことで回避する。

## Owned Paths

```text
contracts/schemas/chart.v1.schema.json
web/src/lib/chart/chart.v1.schema.json          (契約のバイト同一コピー)
web/src/lib/chart/types.ts
web/src/lib/chart/long-note.ts                   (新規)
web/src/lib/chart/validate.ts                    (新規)
web/src/lib/chart/notes.ts
web/src/lib/chart/schema.ts
web/src/lib/chart/import.ts
web/src/lib/chart/export.ts
web/src/components/chart-editor/note-grid.tsx
web/src/components/chart-editor/chart-editor.tsx (long note 関連部分のみ)
web/src/lib/chart/__tests__/long-note.test.ts    (新規)
Assets/Game/Scripts/Gameplay/LongNote.cs         (新規)
Assets/Game/Scripts/Gameplay/NotesManager.cs
Assets/Game/Scripts/Gameplay/Judge.cs            (long note 判定に必要な最小限)
Assets/Game/Scripts/Gameplay/Notes.cs            (long note 表示に必要な最小限)
Assets/Tests/Editor/LongNoteTests.cs             (新規)
specs/164-long-note/{spec,plan,tasks}.md
```

**Structure Decision**: HOLD のロジックを `web/src/lib/chart/long-note.ts` と
`Assets/Game/Scripts/Gameplay/LongNote.cs` に切り出すのは、判定が
「タイミング → 判定幅 → スコア」の純関数で書け、Web 側と Unity 側が
同じ式を使うことで **片方の実装がずれたときにテストで落ちる** ようにするため。
UI (`note-grid.tsx`) と 状態管理 (`chart-editor.tsx`) は既存的所有のまま
long note の差分だけを加える。

## Dependencies & Gate

- **Depends on**: なし。Web の公開経路 (issue #16/#19) と Unity の cloud catalog
  (issue #17) は既に存在し、HOLD の追加に触れずに再利用できる。
  `web/src/lib/chart/chart.v1.schema.json` は契約のコピーなので、
  契約変更と同時に更新する (Drift は `contract-schema.test.ts` が検出する)
- **Blocks**: HOLD を含む譜面のランキング/`scoringVersion` 分離 Issue
- **Gate**:
  1. `cd web && npm test` が green
  2. `cd web && npx tsc --noEmit` がエラーなし
  3. `cd web && npx vp check` (oxfmt / oxlint) が green
  4. Unity EditMode テストが green (**Supervisor 実行**)
  5. `git diff --stat 2f2b6ba -- Assets/Game/Resources/` が空
  6. Supervisor による実ブラウザ E2E と実機 Unity プレイの確認

## Exclusions

- Prefab / Scene への HOLD ノートの見た目反映 (`Assets/Game/Prefabs/**`,
  `Assets/**/*.unity`) — 他 Worker / Supervisor の排他所有。
  本 Issue の HOLD 描画は **コード側** のみ
- 歌詞 (`web/src/lib/chart/lyrics*.ts`) — Worker E
- `web/src/app/**` / `web/pages/**` — Worker F
- `editor-toolbar.tsx` / `web/src/components/ui/**` / `topbar.tsx` — Worker C
- 本番 Cloudflare デプロイ、Unity Editor 実行、実機 Windows — Supervisor
- HOLD 用の新規 JSON バージョン (v2) — 「未知の JSON 形式を作らない」

## Constitution Check

*GATE: must pass before implementation.*

- Authority: [`constitution/CONSTITUTION.md`](../../../constitution/CONSTITUTION.md) and
  [`organization/profiles/release-driven-solo.md`](../../../organization/profiles/release-driven-solo.md)
  are unchanged by this issue and remain the top-level contract.
- Mutable Ownership Safety: Unity Editor / `Library/` / `Assets/**/*.unity` /
  本番 DB / Cloudflare デプロイには **触れない**。本 Issue の Unity 差分は
  `.cs` ファイルだけで、Prefab や Scene への参照追加を伴わないため、
  Editor を起動せずにコンパイルできる。Web 側は `web/src/lib/chart/**` と
  `web/src/components/chart-editor/{note-grid,chart-editor}.tsx` のみを触り、
  他 Worker の排他ファイルには触れない
- Evidence Integrity: 自動テストのログは候補 SHA に紐づける。
  実行していない検証 (Unity EditMode / 実ブラウザ E2E / 実機プレイ) は
  **「未実行 (Supervisor 実施)」** と明記し、合格と書かない
- Violations: なし。Issue #30 の未確定点 (押し直しの可否、採点分離) は
  「1 note = 1 判定」「tap と同じ配点」という互換優先の選択として
  **明示的に凍結** し、spec.md に理由ごと記録した

## Verification Plan

| # | Check | Command / Method | Evidence to attach |
|---|-------|-------------------|--------------------|
| V1 | Web ユニットテスト | `cd web && npx vitest run src/lib/chart/__tests__/` | テスト出力 + SHA |
| V2 | Web 型チェック | `cd web && npx tsc --noEmit` | エラー 0 |
| V3 | Web lint / format | `cd web && npx vp check` | green |
| V4 | 契約コピー整合 | `cd web && npx vitest run src/lib/chart/__tests__/contract-schema.test.ts` | green |
| V5 | 公開 API 契約 | `cd web && node --test tests/publish-schema.test.ts` | green |
| V6 | Unity EditMode | `Unity -batchmode -quit -projectPath . -runTests -testPlatform editmode` | TestResults.xml (**Supervisor**) |
| V7 | 既存譜面の無変更 | `git diff --stat 2f2b6ba -- Assets/Game/Resources/` | 空 |
| V8 | 実ブラウザ E2E | 下記「E2E 手順書」 | スクリーンショット (**Supervisor**) |
| V9 | 実機 Unity プレイ | 下記「E2E 手順書 §6」 | 動画 / スクリーンショット (**Supervisor**) |

## E2E 手順書 (Issue #164 T5)

> 前提: -branch `164` の SHA が `origin/164` に push 済みである。
> - 本番デプロイは **行わない** (production deploy は deploy-owner のみ)。
>   手順はローカル D1 (`vp dev`) と ローカル Unity ビルドに対して行う。
> - 画面解像度は **FHD 1920x1080 / 2K 2560x1440 / Mac 2560x1664** の 3 条件
>   (Issue #164 の Release gate)。それ未満の検証adequate しない。

### §0. チェックアウト

```sh
git checkout 164
git pull --ff-only origin 164
cd web && npm ci
```

### §1. 既存 tap 譜面の回帰 (Web)

1. `cd web && vp dev` を起動 → `http://localhost:5173/chart`
2. 既存譜面 (`db/fixtures/who.chart.json` = `Assets/Game/Resources/Who.json`) を
   インポートする
3. **期待**: Validation パネルにエラー 0件、note 数が 139 で表示される
4. **期待**: 書き出したファイルが入力と **バイト同一** (Expand / Download で
   `cmp` -command 相当で確認)
5. **期待**: HOLD 用の UI (長押しボタン等) が出ても、tap 譜面を開いた状態では
   既存 note の位置・形状が変わらない

### §2. HOLD の作成 (Web 実ブラウザ)

1. `/chart` を開く。_resolution_ を 4 (16分) にする
2. レーン 3 の 4 拍目に **HOLD を追加** する (右下 / ツールバーの HOLD 操作。
   具体的な操作名は `editor-toolbar.tsx` の Worker C 領域touch するため、
   本 Issue では **グリッド右クリック / Shift+クリック** など
   `chart-editor.tsx` 内で完結する導線を実装する)
3. **期待**: 開始ハンドルが 4 拍目、保持ボディが伸びた状態で描画される
4. 終端ハンドルを 8 拍目までドラッグする
5. **期待**: 長さ表示が `(8 - 4) / 4 = 1.0` 拍に更新され、書き出した JSON の
   `endNum` が `32` (8 拍 × 4 行) になる
6. 短的 HOLD (終端を開始の 1 行後) まで縮める
7. **期待**: 最小長 (1 拍) より短くならない。0 拍、試すと 1 拍に補正される

### §3. 書き出しと公開 (Web 実ブラウザ + ローカル D1)

> **既知のブロッカー (Issue #164 のスコープ外):**
> `web/routes/api/v1/_publish-model.ts` の意味論検証
> (`semanticDiagnostics`) が `note.type !== 1` を弾くため、**HOLD を含む譜面は
> 現状 `PUT /api/v1/admin/songs/{songId}` に送れない** (422)。
> このファイルは Issue #164 の所有パスに含まれないため、Worker D は変更して
> いない。必要な修正は 1 行 (下記「Known Issues」参照)。
>
> **回避策 (検証 purposes):** 契約層 (`validatePublishRequest`) は HOLD を
> **正しく受け付けている** ので、§4 の GET 往復までは意味論検証を HOLD 対応に
> した状態で確認する。

1. 譜面を書き出す (JSON download)
2. **期待**: 書き出した JSON に `"type":2` と `"endNum":12` が含まれる
3. ローカル D1 を準備する

```sh
cd web
# .env に開発用トークンを置く (gitignore 済み)
echo 'FLASHBEAT_ADMIN_TOKEN=local-dev-token' >> .env
npx void db migrate
npx void db seed
```

4. `/publish` 画面から該当の曲 (例: 新しい曲 `holdtest` / `youtubeId` =
   実在する動画 ID) を入力し、HOLD 入り譜面と metadata を送信する
5. **期待**: 成功表示。`chartVersion` が 1 (初回 publish) になる
   (ブロッカー未修正なら 422 + `only Tap notes (type 1) are supported` が出る。
   これが Known Issues の証拠になる)

### §4. 公開結果の取得 (GET) — 無損失往復の証明

```sh
curl -s http://localhost:5173/api/v1/catalog | jq '.songs[] | select(.title=="holdtest")'
curl -s "http://localhost:5173/api/v1/charts/holdtest?version=1" > /tmp/hold.json
jq '.notes[] | select(.type==2)' /tmp/hold.json
```

6. **期待**: `jq` の出力に `"type": 2` と `"endNum": 32` が含まれ、
   `notes` の並び順と `name` / `maxBlock` / `BPM` / `offset` が
   公開前と一致する (差分は `diff <(jq -S . /tmp/hold.json) <(jq -S . published.json)` で空)

### §5. 公開前バリデーション (負系)

1. 長さ 0 の HOLD (終端 = 開始) を書き出して公開する
2. **期待**: `422` と `diagnostics` に `/chart/notes/N/endNum` を含む
3. 同じレーン・同じ時刻に HOLD を 2 本置いて公開する
4. **期待**: `422` + 重複を示す diagnostic
5. `durationSec` より長い HOLD を置いて公開する
6. **期待**: `422` + 曲長超過の diagnostic

### §6. Unity 実機でのプレイ (実機 Windows / Supervisor)

1. 本 Issue の SHA 拿出一った Unity プロジェクトで **Windows 64 ビルド** を作る

```sh
Unity -batchmode -quit -projectPath . -executeMethod BuildScript.BuildStandaloneWindows64
```

2. 検証用の HOLD 入り譜面 (`holdtest.json` + `holdtest_text.json`) を
   `Assets/Game/Resources/` に配置し、`GManager` の配列に 1 曲分だけ追加する
   (**注意**: `GManager.cs` は他 Worker の排他所有なので、Supervisor が
   実施する。本 Issue の PR には含めない)
3. 実機で以下を確認する (スクリーンショット / 動画)

| # | 操作 | 期待 |
| --- | --- | --- |
| 1 | HOLD の開始位置でキーを押す | PERFECT / GREAT / BAD が 1 回出る |
| 2 | 保持したまま終端まで進む | 追加の判定表示は出ない |
| 3 | 終端でキーを離す | HOLD が確定する (追加の判定表示は出ない) |
| 4 | 短い HOLD (1 拍) を同じ手順で | 1 拍でも確定する |
| 5 | 長い HOLD (8 拍) を保持 | 途中で判定が重ならない |
| 6 | 保持中に離す | 即 BAD 表示、コンボは継続 |
| 7 | 離した後に押し直す | その HOLD は成功にならない (救済されない) |
| 8 | 既存 tap 譜 (Who) をプレイ | 判定・スコア・コンボが変更前と同一 |

4. **期待**: 上記 8 項目がすべて成立する。3 と 6 / 7 の表示は
   Issue #30 の未確定点に対する本 Issue の凍結仕様 (spec.md R3/R4) と一致する

### §7. 証跡のまとめ

1. 各手順のスクリーンショット (FHD / 2K / Mac の 3 解像度) を PR に添付
2. 実行したコマンドと実結果 (V1–V9) を報告に書く
3. 実行しなかった項目は **「未実行 (Supervisor 実施)」** と明記する

## Rollback

Single-issue rollback: `contracts/schemas/chart.v1.schema.json` の `type` を
`const 1` に戻して `web/src/lib/chart/chart.v1.schema.json` を同期すれば契約だけを
1 コミットで戻せる。Web / Unity 側の HOLD 実装は `type: 2` の note しか読まないので、
戻しても既存 tap 譜面の挙動は変わらない。全体を戻す場合はブランチ `164` の
merge commit を revert するかブランチを削除する。Owned Paths の境界内の差分なので、
他ブランチの作業には影響しない。
