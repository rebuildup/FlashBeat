# アセット・権利監査台帳 (Issue #794)

監査日: 2026-11-21 (再監査。初回 2026-11-20)。対象: `794` ブランチ
(`origin/release-0-2-1` = `f24bee62` 時点) の git 作業ツリー / Git LFS /
ゲーム配布物 / web / 過去 Release。
詳細根拠: `docs/evidence/win64-2026-11-06-issue13/media-rights-audit.md`、
`docs/operations/release-media-audit.md`。

## 分類サマリ

| 区分 | 内容 | 判定 |
|---|---|---|
| A. 自社・ライセンス明確 | 自社制作動画・画像、Noto Sans JP (OFL)、TMP (UCL)、LiberationSans (OFL)、lucide (ISC、`Assets/Game/Icons/lucide/LICENSE` あり)、UniRx/NoteEditor (`Assets/NoteEditor/LICENSE`)、NuGet (`Assets/NuGet/LICENSE` = MIT)、効果音ラボ「決定ボタンを押す31」(free-commercial-no-credit, PR #746)、`com.ibicha.youtube-player` (Asset Store パッケージ、ライセンスはストア契約) | 配布可 |
| B. 権利者許諾を主張するが記録なし | `Resources/V6/select/jacket/*.jpg` 26 点 + `Resources/V5/select/jacket/*.png` 6 点 + `design/assets/jackets/*.jpg` 26 点 (v6-screen-contract は「許諾済み Ramen 素材」と記述、許諾書・記録は repo 内に無し)。**V5/V6 とも Resources 配下のためリリース ZIP の resources.resource に同梱される** | **要確認** — 許諾証跡の保管場所を要確認 |
| C. 第三者素材・根拠不明 | 下記「除去対象」 | **除去対象** (HEAD 削除は製品判断待ち) |
| D. 合成・テスト用 | `E2EProbeSong.json` (notes 空)、`ニセモノ.json` (notes 空)、`contracts/parity`、`web` 側 fixture のうち lyrics 除去済みのもの | 残置可 (要監視) |

## C. 除去対象 (権利根拠を確認できない第三者素材)

| パス | 内容 | HEAD / 配布物への影響 |
|---|---|---|
| `Assets/Game/Resources/Musics/*.wav` (15 曲) | 第三者楽曲の音声 (LFS)。`MusicManager` がストリーム音声無音時のフォールバックとして `Resources.LoadAsync("Musics/" + songName)` で読む | ビルド除外済み (`BuildScript.MediaExcludePaths`)。git/LFS/履歴に残存。**エディタ・テストでは現役** |
| `Assets/Game/Resources/Videos/UnityYoutube/*.mp4` (27 本) | YouTube 楽曲動画 (LFS)。GameScene.unity に GUID 参照残存 (ストリーム再生が主、ローカル clip はフォールバック/エディタ用) | ビルド除外済み。git/LFS/履歴に残存 |
| `Assets/Game/Resources/*.json` (実曲譜面 26 曲 + `ニセモノ.json`) | 実在楽曲の譜面 + **歌詞本文**。`publish-catalog.mjs` で本番 D1 に公開済み | `Resources.Load<TextAsset>(song.title)` でゲーム本体が読む (NotesManager / GameSceneManager / V6SelectDataSource)。**除去するとローカル譜面が全滅し製品が壊れる**。リリース ZIP の resources.resource にも同梱 (MediaExcludePaths 対象外) |
| `Assets/Game/Art/Images/` の YouTube/ジャケット由来画像群 | hqdefault/mqdefault/maxresdefault、yt-dlp 取得の動画コマ、楽曲ジャケット。**全点が GameScene/ResultScene/OptionScene.unity か ProjectSettings.asset から参照あり** (実測: GUID 参照 0 件は無し) | **リリースに同梱される** (scene 参照で resources.resource 入り)。除去にはシーン編集が必要 |
| `web/src/lib/chart/fixtures/sample-chart.json` | `チュートリアル.json` の**歌詞込み** byte コピー (contract-schema.test.ts が byte 一致を強制)。`fixtures.ts` は `*.test.*` からのみ import されるため本番バンドルには入らないが、公開 git repo に歌詞が残る | vitest のみ。本番 Web バンドル非含有 |
| `web/db/fixtures/who.chart.json` + `who.song.json` | 「Who」(Azari) の notes (lyrics 無し) とメタデータ。`db/seed.ts` がローカル D1 に seed、`tests/` と `fixture-identity.test.ts` が参照 | ローカル D1 / テスト。実曲の譜面データが repo に残る |
| `web/scripts/publish-catalog.mjs` | `Assets/Game/Resources/*.json` を読んで本番 D1 へ publish するツール (歌詞込みの譜面をそのまま PUT)。スクリプト自体にデータは埋め込まれていない | 本番 D1 の公開曲データ (歌詞含む) の発生源。公開停止・データ修正は要承認 |
| `Assets/NoteEditor/Audio/Clap.mp3`, `Click 2.wav` | 出所未記録のサンプル SE | エディタのみ (ゲーム非含有)。NoteEditor の Scene/Prefab から参照あり |
| `Assets/Game/Audio/カーソル移動4.mp3` | 出所・ライセンス未記録 (name から無料 SE 系と推測されるが証跡なし)。ユーザー指定のため差替え不可 (#791) | **リリースに同梱** — click/hit 音に使用中。ライセンス確認が必要 |
| `web/tests/e2e/artifacts/*.png` (9 点) / `web/docs/screenshots/*.png` (約 19 点) / `design/exports/screens/*.png` | コミット済みスクリーンショット。Web Studio の画面 (YouTube プレビュー・ジャケット・譜面) を含み、第三者画像が写り込んでいる可能性。artifacts は `docs/audit/open-issues-audit.md` から証跡として参照あり | 公開 repo。目視での写り込み確認と、証跡参照の整理が必要 |
| `design/assets/jackets/*.jpg` (26 点) + `design/assets/*.png` | V6 ジャケットの原本と画面モック。ジャケットは第三者楽曲のアートワーク由来の可能性 (B 区分と同根) | 公開 repo (design ツール用、出荷物ではない) |

## A 区分の補足 (ライセンス明確だが証跡の扱いに注意)

- `Assets/Game/Fonts/Japanese/NotoSansJP-Medium.ttf` — OFL。フォント自体は問題ないが OFL 本文は repo 内に同梱されていない (同梱推奨、軽微)。
- `Assets/ThirdParty/` — TextMesh Pro (UCL、`LiberationSans - OFL.txt` あり)、Simple Scene Fade Load System (FlatTutorials 製。asmdef は全プラットフォーム有効 = 出荷コードに含まれる。**ライセンスファイルが repo 内に無い** — Asset Store 由来なら要証跡)。
- `Assets/Game/Icons/lucide/` — ISC、LICENSE 同梱あり。`Assets/Game/Resources/Icons/` はその生成物。
- `Assets/Game/Audio/決定ボタンを押す31.mp3` — 効果音ラボ (free-commercial-no-credit)。出典 URL とライセンスページの記録は PR #746 を参照。

## 実施済み

- **v0.2.2 RC**: click/hit 音源を `Resources/Musics/` (ビルド除外領域) から `Assets/Game/Audio/` へ移動 — 配布物で音が出る状態に修正 (#791)。ただしライセンス未確認のまま。
- **`レーンキークリック.wav`** (無権利の加工代替) は削除済み。
- **2026-11-21 (本ブランチ)**: 孤立チェーンを削除 — `Assets/Game/Prefabs/YoutubePlayer 1.prefab` (どの Scene/Prefab/コードからも参照無し) と、それだけが参照していた `Assets/Game/Resources/Videos/背景アニメーションタイトル.mp4` (139.5MB、自作素材)。参照元の TypingScene は既に repo に存在せず、`BuildScript.MediaExcludePaths` の該当エントリと陳腐化したコメントも併せて整理。出荷物への影響なし (どちらもビルド非含有だった)。

## 残存リスク (HEAD 削除では解消しない)

- **Git 履歴・LFS オブジェクト**: 上記ファイルは全て履歴に残る。完全除去には履歴書換え (filter-repo 等) + force-push が必要 → **破壊的操作のため別途承認が必要**。
- **過去の公開 Release**: `v0.1.0-win64-743c88d` のみ第三者メディア同梱 (prerelease、配布対象外の運用)。v0.1.1 以降・v0.2.x は song-free を実測確認済み (`docs/operations/release-media-audit.md`)。取り下げ・非公開化の判断は要承認。
- **本番 D1 / Web**: catalog API が楽曲メタデータ・譜面 (歌詞含む)・ジャケット画像を配信中。公開停止・スキーマ変更は影響が大きく要判断。
- **GameScene.unity の UnityYoutube mp4 参照**: ファイル自体は現存するため missing にはならないが、C 区分ファイルを将来削除する際はシーン側の GUID 参照も同時に外す必要がある (Issue #744 の SerializedAssetReferenceAuditTests が未解決参照を検出する)。
- **ジャケット画像 (B 区分)**: V5/V6 とも Resources 同梱でリリース ZIP に入る。「許諾済み」の記録が見つからない限り、公開リリース判断の前に証跡確認が必要。

## 未実施 (次スプリント・要承認)

1. C 区分ファイルの HEAD からの削除 + 参照修正 (Scene GUID、Resources.Load、Web seed、fixture、BuildScript exclusion 整理)。**ローカル譜面・音声の代替供給方針 (クラウドカタログ専用化 or 権利処理) の決定が先**。現状は全て現役参照のため機械的な削除は不可。
2. `カーソル移動4.mp3` のライセンス確認またはライセンス明確な代替への差替え (ユーザー指定音源のため差替えは要承認)。
3. 歌詞・譜面の扱い方針決定 (ローカル同梱をやめてクラウドカタログ専用にするか、権利処理を行うか)。`web` fixture の byte-parity 契約 (`contract-schema.test.ts`) もこの決定に連動する。
4. Git 履歴/LFS の完全パージ (filter-repo + force-push、影響範囲の明示と承認)。
5. 過去 Release アセットの監査と必要なら差替え/削除 (`v0.1.0-win64-743c88d` の扱いを含む)。
6. 本番 D1 の公開曲データ棚卸しと、必要なら公開停止・データ修正。
7. 無許諾素材検出の品質ゲート — 一部は実装済み (`SerializedAssetReferenceAuditTests` = 未解決 GUID 検出、`tools/release/audit-release-media.py` = Release 同梱メディア監査、`package-win64.ps1`/`package-macos.sh` の song-free manifest 検査)。残るは「第三者由来アセットの混入を commit 時に検出する」仕組み (例: 新規メディア追加時の台帳エントリ必須化)。
8. `design/`・`web` スクリーンショットの第三者画像写り込みの目視確認。

## 2026-11-21 追記 (v0.2.4 収束作業)

### 実施した除去

- `web/tests/e2e/artifacts/*.png` (9 点) を git 管理から除去。Playwright/visual capture の生成物であり、`visitor-flow.spec.ts` が実行毎に再生成する。`web/.gitignore` に `tests/e2e/artifacts/` を追加して再混入を防止。`metadata.json` (contrast 計測のテキスト証跡) は `docs/audit/open-issues-audit.md` の参照先として残置。

### 再確認した残存リスク

- **本番 D1 (`flashbeat`)**: `songs` 30 行中 28 行が `archived=0` で公開中。全て第三者商業楽曲 (LeaF / t+pazolite / Azari / STEAKA 他) のメタデータ + `chart_versions` の譜面・歌詞。`GET /api/v1/catalog`・`/api/v1/charts/{id}` が無認証で配信中であることを実測確認。`publish-catalog.mjs` 由来の seed であり、ユーザー公開フロー (#800) の rights attestation 導入前のデータ。**公開停止・差替えは festival のレパートリーに直結するため user 判断が必要** (archive は可逆だが製品影響大)。
- **`web/docs/screenshots/*.png` / `specs/*/screenshots/*.png`**: catalog 行は YouTube `hqdefault.jpg` サムネイルを描画する (`src/app/song-thumbnail.tsx`) ため、スクリーンショットに第三者サムネイルが写り込んでいる可能性が高い。レビュー環境に画像判読手段がなく目視未実施。README 埋め込みのため除去は docs 品質とトレードオフ → user 判断へ。
- **`design/assets/jackets/*.jpg`**: `design/flashbeat-ui.pen` が `jackets/` を 36 箇所参照しており、Pencil の外部アセットとして現役。除去すると design ソースが壊れるため残置 (B 区分の許諾証跡確認は継続)。
- **`Assets/Game/Resources/*.json` (実曲譜面+歌詞) / `Musics/*.wav` / `UnityYoutube/*.mp4`**: 全て現役参照 (ローカルプレイ・テスト・フォールバック)。「ローカル同梱をやめてクラウドカタログ専用にするか」の製品判断待ちで、機械的削除は依然不可。
- **`BETTER_AUTH_SECRET`**: 2026-11-21 に生成し Infisical (dev) と Worker secret に設定済み。値は本台帳・ログ・Issue に記録しない。
