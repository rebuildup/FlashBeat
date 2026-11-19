# FlashBeat 0.2.0 — 一元化アーキテクチャへの移行仕様（初期版）

Status: Draft — ticket #729、親 #728。更新時には参照元コードと独立受入条件を併記する。
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

## 未完の調査（このDraftで完了を宣言しない）

- namespace、GUID、serialization、asmdef、GameObject lifetime、Settings read/writeの参照グラフ。
- 閉鎖済Issue/PRと現行codeの食い違い・偽陽性検証パターンの全数整理。
- 効果音ラボassetの正本ファイルと配布許諾。
- 性能基準環境のactual monitor Hz、ノーツ座標frame pacing、FPS計測。
- Web画面の実ブラウザ視覚調査とユーザー操作の情報優先度検証。

新たな決定は適切なIssue/ADR/Specへ追記し、本書を指示の重複集積場所にしない。