# FlashBeat 0.2.0 — 一元化アーキテクチャへの移行仕様（初期版）

Status: Working spec — ticket #729、親 #728。2026-11-20 に release-0-2-0 の実コード全数調査を反映。移動先の最終確定は #740 が本書の地図に従って行う。
Target: `release-0-2-0`。旧リリース候補と配布物は変更しない。

## 目的と境界

この作業はV6をさらに修正してV7を作ることではない。Unityゲーム、設定画面・Pause・プレビュー、Web Studio、スコア契約、出荷画面品質を、**一つの製品として保守できる構造**へ置き換える。機能退行を「旧表示を廃止したから」で正当化しない。

- **正本**: #728のオーナー要求と、その後に更新する各Issue/ADR/Spec。旧 `docs/architecture/v6-ui.md` / `docs/design/v6/review-2026-11-11-decisions.md` は歴史的な実装契約であり、現在の不具合を正当化する上位要件ではない。
- **出荷境界**: v0.2.0への統合までは `main` に新しい表示経路を入れない。リリース候補のfreezeと品質保証を経て、release→mainは明示承認を得る。
- **互換性**: 楽曲・譜面、保存スコアと `ScoreKey`、Web API、Cloudflare D1、Unity serialized GUID/Scene、入力/判定・HOLD/メディア同期を維持または明示的にバージョンを分けて移行する。
- **外部ライブラリ**: WebはVoid + Vite+ + React19 + TypeScript + Panda CSS + Park UIを保持。新しいUIシステムの並列導入はしない。

## 調査で確認した現状

| 対象 | 現行の根拠 | 再構築する理由 |
| --- | --- | --- |
| キー音源 | `Assets/Game/Scenes/GameScene.unity` で `hitSound` と `clickSound` のserialized GUIDが同じ | 指定のクリック音が出荷シーンに配線されていない（#730） |
| 判定ポップ | `V6HudVerdict.cs` + `V6HudLogic` の局所落下、`docs/design/v6/review-2026-11-11-decisions.md` §10.3 | オーナーの**判定位置から上昇→下方退場**に反する（#731） |
| Game Preview | `V6OptionPreviewRig.cs` の独自clock/queue/Renderer/score非書戻し | 本編と独立した意味論・描画経路のままで全MISSの報告（#732） |
| 設定 | `V6OptionView*` と `V6PauseView*`、`PlaySession` と`OptionSettings` | 通常OptionとPauseで機能・UI・プレビューが分裂（#733） |
| 歌詞 | `V6HudLyrics.cs` が歌詞データの有無とパネル可視性を結びつける | ON/OFFを設定固定にできない（#734） |
| ノーツ | `flat-rendering.md` にはunlit/描画順/上部shadeの改善記録 | 実画面で白く見えないという反例が残る（#735） |
| スコア | `docs/adr/ADR-0029.md` / `Scoring.cs`: v3はボーナス込みmaxRatioで100万点正規化 | 基礎100万 + 追加ボーナスの要求を満たさない（#736） |
| Select復帰 | `GManager.songID` と各Sceneのリセット/選択構造 | Result→Selectで曲文脈が初期化される（#738） |
| 表示速度 | ノーツ移動が体感20fps未満のユーザー報告 | FPS、座標更新率、frame pacing、基準環境の区別が未計測（#739） |
| Unity構造 | `Assets/Game/Scripts/UI/V5/` と `V6/`、各所のflag / partial等 | 保守・状態所有・命名・重複の複合負債（#740） |
| Web構造/UX | `web/AGENTS.md`, `web/package.json` と実画面 | 現代的スタックに対して構造・情報優先度を再検討（#741, #742） |

上記は「原因が確定したもの」と「原因は未検証の観測」を区別する。クリック音の同一GUIDは確定。ノーツ暗転や低FPSの詳細原因は未確定。

## 目標アーキテクチャ：依存方向と実行モード

### Unity：単一のドメインコア + 複数ホスト

1. **Gameplay domain**: 譜面・時間・レーン入力・判定・スコア・ボーナス等の純粋な規則。Unity GameObjectとRenderTextureを知らない。
2. **Session & navigation**: 現在の曲、選曲コンテキスト、ゲーム状態、Pause/Resume、設定Snapshotを所有する。Scene遷移と表示層の都合で意図しないResetをしない。
3. **Presentation & rendering**: Unity scene・ノーツ表示・HUD・アニメーション・レイアウト・sound driverを扱う。判定線の位置・イベント時刻を同一コアの出力から受ける。デザイン変更時にGameplayルールへ副作用を及ぼさない。
4. **Settings**: Option/Pause/Selectが使用する一つの設定モデルとUI部品。Pauseはホストとして凍結/再開を付け加えるだけで、設定実装を複製しない。
5. **Preview**: Gameとは別の副作用境界を持つが、時間・判定・モーション・ノーツ描画の規則は共有する。本番スコア/永続化/外部APIを書き換えない。
6. **Infrastructure**: 音源/入力/永続化/ネットワーク/asset lifetimeのアダプタ。資源・購読の所有と解放を一意にする。

最終ディレクトリ/assembly配置は現行コード・Scene/Prefab依存グラフを採ってから確定する。`V5/V6` からの機械的一括移動を要求せず、移行期は明示的アダプタを使える。ただし「いつまでにどの条件で削除するか」を各ticketに記す。最終配布コードにバージョン別runtime切替を残さない。

### Web：作業単位の情報設計とframework-native境界

- `pages` / `routes` はVoidの契約を保持し、画面コンポーネントでAPI/DB/validationを同時に担わない。
- 検証・譜面編集・Publish・Dashboard・Song managementは利用者のユースケースで境界を作り、domain/schema/transport/stateを必要十分に分離する。
- 重複した状態・古いReact前提・不要なrender/effect・ローカルschemaコピーは用途と契約を確認して縮小する。
- `GoogleChrome/modern-web-guidance` を`project-init`の`skill-selection`方針で評価・個別導入し、Web platform API / semantics / responsiveness を対象とする（`web/AGENTS.md`のframework-native方針と競合しない範囲）。
- 情報優先度を「主行動」「その判断に必要な状態」「補助操作」「詳細/回復」に分け、単なる配色や余白調整より操作導線と認知負荷の再設計を先に置く。

## オーナーの実行契約（独立oracle）

### 判定（#731）
- ノーツの判定確定位置を起点に、判定テキストが**上方へ移動**する。
- 意味のある滞留時間を置き、終了時に**下方へ落ちながら消える**。
- 本編とPreviewは同じ計算に従う。旧「上から落ちて静止位置へ着地」は負例。
- 振幅・所要時間・イージングはデザイン意図と可読性から決め、旧コード定数を流用することだけで正当化しない。

### 歌詞（#734）
- `ShowLyrics`相当の設定がtrueなら、歌詞本文の有無に関わらずゲーム中パネル枠の可視性が安定している。falseなら枠ごと非表示。
- 現在行/ルビ/ローマ字/空区間は子コンテンツの状態であって、親枠の出現・消失条件ではない。

### 採点（#736）
- 基礎スコアの理論最大値 = 1,000,000。
- combo継続等の**追加ボーナス**が基礎スコアの上へ加算され、フルコンボ時に100万点を超える。最終スコアは表示・ローカル・APIで一致。
- v1/v2/v3既存レコードはその採点版のまま残し、新版で無断再計算しない。server-side validation、scoreKey、ranking groupingも版を区別。
- ボーナス表示は少なくとも利用者が増分を判別できる寿命・動線を確保する。

### Audio（#730）
- 効果音ラボ由来の指定クリック音源をrepo/authorized素材から特定し、実Sceneのclip GUIDが独立していることを保証する。権利/由来の確認なく類似音へ差し替えない。

### FPS（#739）
- Unityのフレーム時間とノーツ位置の更新頻度は別々に観測。実測前に「20fps」「60fps改善」を既成事実化しない。
- タイムラインとHOLD判定の正しさを維持し、描画に補間/キャッシュ等を採用する際はbefore/afterで効果を示す。

## 依存と並行作業

- **先行**: #729（依存/所有/移行地図）、#744（要求独立の検証器）、#730（確定した出荷sceneの音）、#739（性能計測をまず確立）。
- **機能並行**: #731 判定軌道、#734 歌詞、#735 描画、#737 Result、#738 ナビゲーション。ただし共通HUD/Sessionの編集はowned pathを区切る。
- **共有コア合流**: #732 Preview、#733 Option/Pause、#740 Unity構造は #729 の境界・#731 のモーション・#734 のvisibilityと整合を取って段階統合。
- **採点独立stream**: #736 はUnity/Web/契約を同一版で整合。server公開移行を先行させない。
- **Web stream**: #741 structural/skill audit と #742 情報設計を並行で進め、修正PRでは画面/ownerを衝突させない。
- **横断**: #743 UI inventory/残バグの独立検出。単なる監査で終わらず修正ticketを発行する。

## 品質と収束

- ticketの軽量検証・独立review・releaseへの自律landingは`docs/operations/development-convergence.md`と`github-delivery`による。重いCI/全E2E/実機GUIは各ticketの隠れた完了条件にしない。
- 最終候補のfreeze後、同一SHAでUnity全EditMode/PlayMode/出荷Scene asset、Web契約とE2E/visual、Win/Mac build、audio/権利、採点parity、FPS/frame pacing、代表的画面の実描画結果を統合検査する。
- 旧正本・旧testが誤った期待値を採用していた場合、実装より先に外部要求からoracleを定義する。全件PASSでもオーナーの再現事実に反するなら契約/検証器から直す。
- `release-0-2-0 → main` の統合と公開だけは明示承認を必要とする。

## 実測による現行構造マップ（2026-11-20 コード全数調査）

以下は `release-0-2-0` 時点の実コードから抽出した事実。設計判断の根拠であり、移動先の確定は #740 がこの地図に従って行う。

### Assembly / 依存の実態

- runtime は単一 asmdef `FlashBeat`（`Assets/Game/Scripts/FlashBeat.asmdef`）。Editor 用に `FlashBeat.Editor`、テストに `FlashBeat.Tests`（`Assets/Tests/Editor`、約115ファイル）・`FlashBeat.V5CanvasTests` / `FlashBeat.V6CanvasTests`（`Assets/Game/Tests`、約70ファイル）・`FlashBeat.PlayModeTests`（1ファイル）。asmdef 境界は存在するが runtime 内部は実質一枚岩。
- `Assets/Game/Scripts/` 直下: `Common/`（時計・歌詞・入力ゲート・チャートデータ等）、`Gameplay/`（Judge/NotesManager/LongNote/Scoring/MusicManager 等）、`Persistence/`（ScoreKey/ScorePersistence）、`Songs/`（GManager/SongData/PlaySessionSnapshot/SongCatalog）、`UI/`（SceneManager 群 + `V5/` + `V6/`）。
- `V5/` は UI だけでなく **`PlaySession`（セッション状態）を内包**しており、V6 もこれに依存する。「V5 を消す」は UI 撤去ではなく状態所有の再配置を伴う。
- `GManager` は static のグローバル状態（曲カタログ・セッション・音量・設定値の実行時コピー）を持ち、全レイヤから参照される。縮小は #740 の中核作業。

### V5/V6 の実際の分岐

- 分岐は `V6UiMode`（`UI/V6/V6UiMode.cs`）の static bool 群のみ。出荷設定: `GameHud/SelectScreen/VerdictRow/TitleTabs/TitleLegend/LegacyOverlaysHidden = true`、`Playfield/VideoDim = false`。
- つまり出荷経路は **V6 の uGUI HUD/画面 + レガシー 3D プレイフィールド**（`NotesManager` が `Note.prefab` を実体化、`FlatUnlit` シェーダ）。V6 の uGUI プレイフィールド実装（`V6PlayfieldView`）は存在するがフラグ OFF で死んでいる。
- V5 側は `HideLegacyV5Canvases` 等で非表示化されているがコードとテストは残存。`V5LegacyReferenceAuditTests` が参照台帳を固定している。

### 重複・分裂している実装（#728 要求との対応）

| 領域 | 分裂の実態 | 統合先 |
| --- | --- | --- |
| Option/Pause 設定 | `V6OptionView*` と `V6PauseView*` が行構築・調整・保存を別実装。Pause は Sound が2項目のみ・Display が2項目のみ・Data タブが別物、プレビューは静的偽物 | #733：共通 setting schema/row/preview container |
| ゲームプレビュー | `V6OptionPreviewRig` が独自 clock/judge queue/note pool/RenderTexture。時計状態が chart 変更時にリベースされない既知の欠陥あり | #732：本編と同一 clock/judgement/motion contract |
| 判定モーション | `V6HudLogic`+`V6HudVerdict`（本編）と `V6OptionViewPreviewOverlay`（プレビュー）に同じ運動が二重実装 | #731：純粋タイムライン一元化 |
| 歌詞パネル | `V6HudLyrics.UpdateLyrics` が `PlaySession.LyricsEnabled` と歌詞データ有無を同じ条件で制御。legacy `TextPanel` 経路も残存 | #734：visibility policy 単一化 |
| 採点 | `Scoring.cs`(v3) + `web/routes/api/v1/_score-model.ts` + `contracts/openapi.v1.yaml` + `contracts/parity/score-vectors.json` + D1 CHECK。v4 は新規 | #736：scoringVersion=4 を全層同時 |
| 選曲状態 | `GManager.songID` は残るが `V6SelectDataSource` が毎回初期化し先頭曲を自動選択 | #738：SelectionContext をセッション層へ |
| 音源参照 | `GameScene.unity` の serialized GUID が唯一の結合点。#730 で click/hit 分離済み | #744：serialized 参照の系統的監査へ一般化 |

### 互換性の制約（移動・改名時に壊してはいけないもの）

- `.cs` ファイルを `.meta` ごと移動すれば script GUID は維持され、Scene/Prefab の参照は壊れない。ただし `SerializeField` 名・クラス名の変更は serialized データを壊す。
- `ScoreKey`（`songId@chartVersion[@scoringVersion]`）、OpenAPI 契約、D1 スキーマ、`FlashBeat-keyconfig.json`、`*.setting.json`、Chart JSON schema は外部契約。
- `GManager` の static フィールドは EditMode テストが `SingletonTestHelper` でリセットする前提。安易なプロパティ化はテストを壊す。
- Web の `pages/`+`.server.ts` は Void の file-based 契約。`routes/api/v1/` の `_`-prefix モジュールは内部共有で外部契約ではない。

### 目標配置の方向（#740 が具体化）

runtime を依存方向で分ける。ドメイン規則（採点・判定窓・タイミング・レーン写像）は UnityEngine 非依存を目指し、セッション（曲/選曲/設定Snapshot/遷移）が状態を所有し、プレゼンテーション（V6 UI・HUD・プレビュー host）が描画と入力を担い、インフラ（音声/入力/永続化/通信/メディアキャッシュ）がアダプタとして両者を繋ぐ。`V5/V6` の版名は最終的に製品の機能名へ置き換えるが、**機械的一括 rename はしない** — 画面単位・責務単位で段階的に行い、各段階で削除条件を満たすことを確認する。

Web 側は `pages`/`routes` の Void 契約を維持し、`src/lib` のドメインロジックと `src/app`/`src/components` の画面をユースケース境界で整理する。詳細は #741/#742。

## 残る調査（このSpecで完了を宣言しない範囲）

- namespace、GUID、serialization、asmdef、GameObject lifetime、Settings read/writeの参照グラフの**全数**棚卸し（本書は主要経路の実測。#740 が対象ファイル単位で補完する）。
- 閉鎖済Issue/PRと現行codeの食い違い・偽陽性検証パターンの全数整理（#744）。
- 効果音ラボassetの正本ファイルと配布許諾 — **#730 で解決済み**（`Assets/Game/Audio/決定ボタンを押す31.mp3`、由来と規約は PR #746 に記録）。
- 性能基準環境のactual monitor Hz、ノーツ座標frame pacing、FPS計測 — **#739 の診断で有力仮説を特定済み**（`GManager.step_time = songVideo.time` のメディア時計量子化がノーツ位置を支配。実測プローブは未実装）。
- Web画面の実ブラウザ視覚調査とユーザー操作の情報優先度検証（#741/#742）。

新たな決定は適切なIssue/ADR/Specへ追記し、本書を指示の重複集積場所にしない。