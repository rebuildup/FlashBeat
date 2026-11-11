# Win64 展示候補 — オーナー実施の受入手順 (2026-11-10)

> **この文書は cc (開発側) が用意した「候補の同一性」と「手順」です。**
> 実行は **オーナー** が行います。cc は展示 PC を操作・ビルド・上書きしません。

> **2026-11-11 更新 — 候補を `0.1.23+09ece5a2` に差し替えた。**
> 版数はリポジトリ直下の `VERSION` (現在 `0.1.23`) が正本で、`FLASHBEAT_VERSION_BASE` は
> 任意の上書き。以前の暗黙の既定値 `0.1.0` は廃止し、`VERSION` も環境変数も解決できなければ
> **ビルドが失敗する**。これは「未設定のまま `0.1.0` になる」経路を塞ぐもので、
> `FLASHBEAT_VERSION_BASE` に明示した値と配布済み版数の大小を検査するものではない —
> 候補を作る前に `VERSION` を配布済みより上げるのは**運用上の確認**で、
> `BuildVersionSourceTests.RepositoryVersionFile_IsNewerThanTheDistributedVersion` が
> リポジトリの `VERSION` と `0.1.22` の大小だけを固定している。
> 前候補 `0.1.22+7af9242d` は Windows 実機 2K の受入まで済んでいるが、
> #351 / #352 / v6 UI 修正より前の main のため、この候補に置き換えた (証跡は 0.1.22 のものとして区別する)。

---

## 0. 前提 (cc が確認済みの範囲)

| 項目 | 内容 |
|---|---|
| ビルド環境 | macOS (Apple Silicon) + Unity 6000.5.6f1 + WindowsStandaloneSupport |
| ビルド手順 | `FLASHBEAT_UPDATE_CHANNEL=stable bash script/win64-build-mac.sh <sha> <outdir>` (基準版数は `VERSION` から自動。正規経路は `script/win64-build-mac.sh` → `tools/release/package-win64.ps1` のみ) |
| 出力 | `<outdir>/FlashBeat-0.1.23-win64-<sha>.zip` と `.zip.sha256` の **2 点だけ** (`.build-info.txt` は作らない) |
| 中身 (実測) | 224 entries / 138 managed DLL / FlashBeat.exe / updater (`tools/update/Update.ps1`) / `StreamingAssets/update-channel.txt` (**stable**) / `version.json` (ZIP ルート) / `flashbeat-media-policy.json` / **同梱音楽・動画なし** (第三者楽曲はビルド時に退避) |
| **未検証** | **この候補 (`0.1.23+09ece5a2`) の Windows 実機での実行・全画面・1 曲完走・ストリーム音声**。前候補 `0.1.22+7af9242d` は 2K 実機受入済み (`exhibit-evidence-2026-11-10/win-2k-operation.md`) だが、この候補は未実施 |

### 現在の候補 (2026-11-11 再生成 / main `09ece5a2`)

| 項目 | 値 |
|---|---|
| ZIP | `FlashBeat-0.1.23-win64-09ece5a2.zip` (275,052,241 bytes) |
| SHA-256 | `314d3de6170f2bc73ea5b14fa8d30096fd2ce7f9072db7d39659e6a53e95cf8b` |
| ソース SHA | `09ece5a213a6bfdd1d1eeba242396c059de981af` (`origin/main` — PR #355 / #356 / #357 merge 後) |
| Application.version | `0.1.23+09ece5a2` (ビルド時の焼き込み検査 `bundleVersion: 0.1.23+09ece5a2` が通過。Mac アプリでも `Player.log` / Info.plist で一致を確認済み) |
| version.json | `version=0.1.23` / `tag=v0.1.23-win64-09ece5a2` / `mediaPolicy=song-free` / `buildSha=09ece5a2…` (ZIP から読み出して確認済み) |
| 更新チャンネル | `stable` (`FlashBeat_Data/StreamingAssets/update-channel.txt` — ZIP から読み出して確認済み) |
| ビルド日時 | 2026-11-11T09:54:55Z (ビルド元 commit の日時) |
| ビルド結果 | `Build Finished, Result: Success.` / `error CS` **0 件** / 224 entries / 138 managed DLL / mediaPolicy=song-free |
| `.zip.sha256` | sidecar の値と `shasum -a 256` の実測が一致 |

**前候補 `7af9242d` からの差分** (展示に効くもの):

| 変更 | 内容 | 受入での見え方 |
|---|---|---|
| 準備完了前の Space (#351) | 準備画面で押した Space を捨てず、準備完了時に開始する | 「押したのに始まらない」現象が消える (前候補の 2K 受入で記録した中程度不具合) |
| Media cache 消去 (#352) | 即消去せず確認ダイアログを挟む | 誤操作でキャッシュが消えない |
| v6 画面の正本合わせ (PR #355, r12〜r28) | Title Exit / 登録モーダル / 曲リスト / Pause / Option 4 タブ / Loading / Result 判定行 / History / 空状態 を design の実座標へ | 画面の座標・折返し・行送りが前候補と変わる |
| 版数の正本化 (PR #357) | `VERSION` (0.1.23) を基準にし、暗黙の `0.1.0` を廃止 | 表示版数が `0.1.23+09ece5a2`。更新判定が配布版より古くならない |

> **ビルドは必ず main のチェックアウトから行う。** 2026-11-10 に `w/ja-text-cleanup`
> チェックアウト (=古い `script/win64-build-mac.sh`) からビルドしてしまい、
> `0.1.22+<sha>-win64-<sha>.zip` + `.build-info.txt` という **契約違反の ZIP** ができた。
> 旧スクリプトはビルド出力をそのまま ZIP 化する (version.json なし / BurstDebug 同梱)。
> 現在の正規経路は `script/win64-build-mac.sh` → `tools/release/package-win64.ps1` のみで、
> `dist/forbidden-old-script/` 配下の成果物は **使用禁止**。

> **古い候補は使用禁止。** 次の ZIP はソースが古いか、旧スクリプト (ビルド出力をそのまま ZIP 化する
> 経路) で作られており、`version.json` 非同梱 / `FlashBeat_BurstDebugInformation_DoNotShip` 同梱 /
> `.build-info.txt` 付きのいずれかに該当する。**ZIP 名と SHA-256 が上表と一致しない候補は使わない。**
> (末尾の `7af9242d` だけは構造・実機受入とも正規だが、現候補に置き換えられたもの。)
>
> - `0.1.0+877bde7-win64-877bde7.zip`
> - `0.1.22+0bab549-win64-0bab549.zip`
> - `0.1.22+e45c494-win64-e45c494.zip`
> - `0.1.22+36a3572-win64-36a3572.zip`
> - `0.1.0+c9fb812-win64-c9fb812.zip` / `FlashBeat-0.1.0-win64-60854c8.zip`
>   (構造は正規だがバージョンが `0.1.0` のため更新判定に使えない)
> - `FlashBeat-0.1.22-win64-60854c8.zip` (構造は正規。**#327〜#331 より前の main** のため、
>   候補 `4445279` に置き換えられた)
> - `FlashBeat-0.1.22-win64-4445279.zip` (構造は正規。**#339〜#344 より前の main** のため、
>   候補 `7af9242d` に置き換えられた。UI 欠陥修正と表示設定の保存を含まない)
> - `FlashBeat-0.1.22-win64-7af9242d.zip` (構造は正規で **Windows 実機 2K 受入済み**。
>   ただし #351 / #352 / v6 UI 修正より前の main のため、今回の候補 `09ece5a2` に置き換えられた。
>   この ZIP の実機証跡は 0.1.22 のものとして扱い、新候補の証跡と混ぜない)
> - `0.1.22+4445279-win64-4445279.zip` (旧スクリプトで作られた契約違反の ZIP。`dist/forbidden-old-script/`)
>
> 旧形式 (`0.1.22+<sha>-win64-<sha>.zip`) は **もう生成されない**。もし見つけたら、
> それは旧版 `script/win64-build-mac.sh` (w/ja-text-cleanup など古いチェックアウト) で
> 作られたものなので、正規候補として扱わないこと。

**版の基準について**: 公開済みの最新 Release は `v0.1.21-win64-d581c4c` (`v0.1.22-win64-7af9242d` は下書きで未公開)。
自動更新はバージョンの大小で判定するため、**同じ版 (`0.1.21`) で別 SHA の Release を作っても
更新が効きません**。次の配布は **`0.1.23`** とする
(リポジトリ直下の `VERSION` が `0.1.23`。`FLASHBEAT_VERSION_BASE` の指定は不要)。
`VERSION` を書き換えずに次の候補を作ると、同じ `0.1.23` で別 SHA の候補になり更新が効かない。

### 0.1 オーナーが行う操作 (これだけ)

1. ZIP を**新しいフォルダ**へ展開する (既存の展示ディレクトリへ上書きしない)
2. `Get-FileHash -Algorithm SHA256` で上表の SHA-256 と照合する
3. `FlashBeat.exe` を**引数なし**で起動する
4. Title → Select → Game (1 曲) → Result → Retry を 1 周し、**動画・ノーツ・判定・音声**を確認する
5. Option を開き、4 タブ (Play / Display / Sound / Data) の表示と操作を確認する (#241)
6. 実ディスプレイ全画面のスクリーンショットと `Player.log` を保存する (§3.6)

開発側 (cc) が行うのは、ログとスクリーンショットの取り込み・Issue への反映・
Release 公開判定だけです。展示 PC の操作とビルドの上書きは行いません。

---

## 1. 候補の同一性 (SHA-256 で確認する)

配布 ZIP には **`.zip.sha256` sidecar** が同梱されます。展開前に必ず照合してください。

```powershell
# PowerShell (Windows)
Get-FileHash -Algorithm SHA256 "FlashBeat-0.1.23-win64-09ece5a2.zip" | Select-Object -ExpandProperty Hash
# → 小文字に DownCase して .zip.sha256 の値と一致することを確認する
```

一致しない場合は **その ZIP を使っていないこと** (取得し直してください)。

`sha256` が一致しても **ZIP の中身が Windows で動くことの証明にはなりません**。
実機での確認 (§3) が未実施のあいだ、この候補は **未検証** です。

---

## 2. 展開 (所有者の手元で)

1. ZIP を任意の作業フォルダへ展開する (例: `C:\FlashBeat-candidate\`)
2. `FlashBeat.exe` が直下にあることを確認する
3. **既存の展示用ディレクトリへ上書きしない**。既存インストール (0.1.14〜0.1.21) を消さない

> Git LFS は関係ありません (ビルド済み成果物なので、LFS ポインタは含まれません)。

---

## 3. 実機での受入手順 (3 解像度)

### 3.1 大原則 — 擬似全画面は禁止

- 検証は **その解像度の実ディスプレイ上でアプリが全画面表示している状態** でのみ有効。
- **ウィンドウのドラッグ / リサイズで「そのサイズに見せかける」擬似再現は禁止。**
  リサイズで得たスクリーンショットは **証拠にならない**。
- 起動直後の実効値は `FullscreenDiagnostics` が Player.log に出力します。
  **PlayerSettings の設定値を「実測値」と言い換えないでください。**

### 3.2 対象は 3 解像度だけ

| ID | 解像度 | 入手方法 | 現候補での状態 |
|---|---|---|---|
| FHD | 1920×1080 | 実ディスプレイ (展示PC / FHD モニタ) | **未検証** (前候補でも未実施。Windows 機の FHD 面は inactive だった) |
| 2K | 2560×1440 | 実ディスプレイ (2K モニタ) | 前候補 `0.1.22+7af9242d` で **実全画面 PASS** (`exhibit-evidence-2026-11-10/win-2k-operation.md`)。**現候補では未実施** |
| MAC | 2560×1664 | Mac 本機 (ネイティブ) | 現候補 `0.1.23+09ece5a2` で **PASS** (2026-11-11, `docs/ui/v6-repro-audit-2026-11-11.md` §9.18) |

2K モニタが確保できない場合は **「未証明」と記録**してください (Web のビューポート確認は代替になりません)。
前候補の 2K 証跡は **0.1.22 のもの**であり、現候補 `0.1.23+09ece5a2` の証跡として流用しないでください。

### 3.3 手順 (各解像度で同じ)

1. ZIP を展開 (`§2`)
2. `FlashBeat.exe` を **引数なし** で起動
3. 起動直後に **画面全体** を撮影 (PrintScreen。ウィンドウ枠 / タスクバー / 黒帯が 1px でも見えたら不合格)
4. Player.log をコピー:
   `%USERPROFILE%\AppData\LocalLow\rebuildup\FlashBeat\Player.log`
   `[FullscreenDiagnostics]` 行の `screen=` / `fullScreenMode=` / `dpi=` / `displays=` を確認
5. **1 曲完走**的操作ログを残す (下記 §3.4)

### 3.4 1 曲完走で見るもの (Issue #233 / #242 / #4 / #13 の受入)

| 順 | 画面 | 見るもの | 合格の目安 |
|---|---|---|---|
| 1 | Title | タイトルが出る。**「アップデートあり」表示** (#118) が出るか否かも確認 | 描画が崩れていない |
| 2 | Select | 曲一覧が出る。**Ready を連打しても Scene 遷移が重複しない** (#222) | 遷移が 1 回だけ |
| 3 | Game (準備) | 準備画面の進捗が出る。**準備完了前に Space を押しても開始するか** (#351) | 押し直さずに開始する |
| 4 | Game (プレイ) | **8 レーン / ノーツ / 判定ラインが視認できる** (#233)。**映像が出るか** (#233)。密所でも同じ | **ノーツが見えないなら不合格** |
| 5 | Game (音) | **ストリーム音声が鳴るか** (#13 / #4)。`Player.log` の `audioMode=Stream` を確認 | 鳴らないなら `#4` は未解決 |
| 6 | Result | リザルトが出る。スコア登録 (名前入力) が動くか (#134) | — |
| 7 | Retry | Retry で Game に戻れる | — |

### 3.5 期待値 (ビルドに入っている状態) — 迷ったときの照合用

同じ ZIP でも「何が出るのが正しいか」を知らないと合否を付けられないため、コード上の現在値を載せます。
**ここに書いた値と違う画面が出たら、まず `Player.log` を保存してから §4 の表で切り分けてください。**

| 対象 | 現在値 | 根拠 |
|---|---|---|
| プレイフィールド | **v6 の遠近プレイフィールドは無効**。従来の 3D レーンが動く (オーナー判断による差し戻し) | `Assets/Game/Scripts/UI/V6/V6UiMode.cs:27` `Playfield = false` |
| ゲーム HUD | v6 HUD (スコア/コンボ/ゲージ/歌詞/キーガイド行) | `V6UiMode.cs:15` `GameHud = true` |
| 判定表示 | 3D の判定表示 (v6 のレーン別判定行は無効) | `V6UiMode.cs:43` `VerdictRow = false` |
| 動画の減光 | しない (v6 の減光レイヤーは無効) | `V6UiMode.cs:35` `VideoDim = false` |
| 選曲画面 | v6 の 3 カラム | `V6UiMode.cs:18` `SelectScreen = true` |
| Title のタブ行 / キー凡例 | **どちらも描く** (2026-11-11 に正本 QJtTv へ合わせて撤回。フッター右下の START / 設定は描かない) | `V6UiMode.cs:54` `TitleTabs = true`, `:62` `TitleLegend = true`, `:68` `TitleFooterActions = false` |
| Title のヘッダー / Exit | ヘッダーの "FlashBeat" 文字は描かず、**左端**に Exit ボタン (Icon + キーキャップ + "Exit") を置く | `V6UiMode.cs:74` `TitleHeaderTitle = false`, `V6TitleExit.cs` (Header ローカル (0,20) 162×56) |
| 準備画面の Space | 準備完了前に押しても**捨てられず**、映像/音声の準備完了 (または 30 秒経過) **かつノーツ準備完了**の最初のフレームで 1 回だけ開始する (#351) | `MusicManager.cs` の `canStart` / `PlaybackStartGate.ShouldStart` |
| Media cache の消去 | Option → Data で確認ダイアログが出てから消す (#352) | `V6OptionView.cs:875` `ConfirmKind.MediaCache` |
| Result の登録モーダル | 名前欄の Placeholder は "YOU"。入力上限は**モード別**: ローカル登録 8 文字 (A-Z 0-9 のみ) / オンライン登録 16 文字。Skip / Register | `V6ResultView.NameMaxLength()` / `V6ResultLogic.LocalNameMaxLength = 8` / `RankingClient.NicknameMaxLength = 16` |
| 音の経路 | `[MusicManager] PLAYBACK_START … source=StreamCombined \| StreamSeparate \| BundledWave` | `MusicManager.cs:1166` の `Debug.Log` |
| 映像 | ダウンロードしたローカル mp4 を再生 (URL 直渡しは診断のみ) | `MusicManager.cs:637` `PrepareLocalVideo` |
| フォント | NotoSansJP (静的アトラス + 動的フォールバック) | `JapaneseFontAssetBuilder.cs:233,249` |
| 表示設定 (Key Guide / Lyrics) | Option で切り替えた値は **保存**され、次回起動でも残る (前候補までは起動ごとに既定へ戻っていた) | `PlaySession.cs:107` `Load()` / `TitleSceneManager.cs:51` |
| Key Layout のリセット | カスタム配列のとき `←` を押すと **確認ダイアログ**が出る (即リセットしない) | `V6OptionView.cs:880` `OpenResetKeyLayoutConfirm()` |

> 「ノーツが見えない」「映像が出ない」の切り分けは、まずこの表の**期待値どおりの画面か**を確認し、
> そのうえで `Player.log` の該当行 (`video file:` / `VideoPlayer prepared=` / `PLAYBACK_START` /
> `source=`) を採取してください。**`0x80070005` の 1 行だけでは不合格の根拠になりません**
> (URL の診断 probe が期待どおり失敗しただけのことがあります)。

### 3.6 記録するファイル

`docs/operations/exhibit-evidence-2026-11-10/` 配下に保存してください。

| ファイル | 内容 |
|---|---|
| `win-fhd-fullscreen.png` / `win-2k-fullscreen.png` | 実全画面スクリーンショット |
| `win-fhd-Player.log` / `win-2k-Player.log` | Player.log のコピー |
| `win-fhd-operation.md` / `win-2k-operation.md` | 操作ログ (1 曲完走含む / 何を押した / 何が起きた) |
| `win-fullplay-*.png` | Select / Game / Result の場面 |

---

## 4. 判定的注意 (既知の未解決)

| 事象 | 想定 | 対処 |
|---|---|---|
| 映像が再生されず `Player.log` に `0x80070005` (Access Denied) | Windows Media Foundation の URL リーダーの制約 (#4 / #13) | **同じ ZIP を別 PC で試す**。Player.log の該当行をそのまま記録。コーデック導入は展示PC の管理者に確認 |
| 音声が鳴らない | 同上 (映像音声は video トラック経由) | `audioMode=Bundled` なら同梱 wav 由来。`audioMode=Stream` ならストリーム由来。どちらかを記録 |
| 起動直後真っ白 | 初回 AssetDatabase / フォントの読み込み中 | 数秒待って再撮影。**板 (白) が見えたまま**ならスクリーンショットを添付 |

---

## 5. cc 側の次の一手 (報告を受けてから)

1. 実機ログとスクリーンショットを `docs/evidence/` に取り込む
2. 伸びた受入項目を Issue ごとに閉じ直す (#233 / #242 / #88 / #96 / #4 / #13 / #59)
3. **Windows 実機受入が通ってから** GitHub Release として publish する
   (未検証の配布は公開しない)

---

## 6. CI (GitHub Actions) の Unity ライセンス secret

**展示候補の ZIP は CI では作れません。** `build-win64` は `workflow_dispatch` と main への push
だけで走り、2026-11-11 時点でも `license-precheck` で失敗します (`UNITY_EMAIL` /
`UNITY_PASSWORD` が未登録)。したがって候補 `09ece5a2` は §0 の macOS 経路
(`script/win64-build-mac.sh` → `tools/release/package-win64.ps1`) でだけ作られています。
PR で走るライセンス不要の検証は `verify` (リポジトリ整合性) と `web` だけで、**どちらも
ビルド成果物を作りません**。責任範囲の全体は [`ci-pr-gate.md`](ci-pr-gate.md) を参照。

2026-11-11 時点でも `build-win64` / `build-macos` は **ライセンス確認ステップで失敗**している
(`--error UNITY_EMAIL と UNITY_PASSWORD を Actions Secrets に登録してください。`)。
これはコードの問題ではなく secret 未登録によるもの。

| secret | 位置づけ | 備考 |
|---|---|---|
| `UNITY_EMAIL` / `UNITY_PASSWORD` | **推奨** (Unity アカウント認証) | GitHub-hosted runner では `.ulf` の機械結合が一致しないため |
| `UNITY_LICENSE` (`.ulf`) | 代替 | 登録元マシンに固定される。hosted runner では実質不可 |
| `UNITY_SERIAL` | 代替 (Pro/Plus) | シリアル認証を使う場合 |

登録後の期待結果:

- `ライセンス設定の確認` ステップが成功し、続く `BuildScript.BuildStandaloneWindows64` /
  `BuildStandaloneMacOS` が実行されて artifact が残る
- 失敗が続く場合は run ログの `[Licensing::...]` 行を確認する (値を Issue に貼らない)

登録・値の管理は**オーナー**が行う。secret の値はログ・Issue・PR に出さない。
手順の詳細は `docs/operations/unity-ci-license.md` を参照。

---

🤖 この文書は Claude Code が作成しました (cc は展示 PC を操作していません)。
