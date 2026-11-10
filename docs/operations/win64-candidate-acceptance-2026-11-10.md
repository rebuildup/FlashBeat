# Win64 展示候補 — オーナー実施の受入手順 (2026-11-10)

> **この文書は cc (開発側) が用意した「候補の同一性」と「手順」です。**
> 実行は **オーナー** が行います。cc は展示 PC を操作・ビルド・上書きしません。

---

## 0. 前提 ((cc が確認済みの範囲)

| 項目 | 内容 |
|---|---|
| ビルド環境 | macOS (Apple Silicon) + Unity 6000.5.6f1 + WindowsStandaloneSupport |
| ビルド手順 | `FLASHBEAT_VERSION_BASE=0.1.22 FLASHBEAT_UPDATE_CHANNEL=stable bash script/win64-build-mac.sh <sha> <outdir>` (正規経路は `script/win64-build-mac.sh` → `tools/release/package-win64.ps1` のみ) |
| 出力 | `<outdir>/FlashBeat-0.1.22-win64-<sha>.zip` と `.zip.sha256` の **2 点だけ** (`.build-info.txt` は作らない) |
| 中身 (実測) | 224 entries / 138 managed DLL / FlashBeat.exe / updater (`tools/update/Update.ps1`) / `StreamingAssets/update-channel.txt` (**stable**) / `version.json` (ZIP ルート) / `flashbeat-media-policy.json` / **同梱音楽・動画なし** (第三者楽曲はビルド時に退避) |
| **未検証** | **Windows 実機での実行・全画面・1 曲完走・ストリーム音声**。ここが本手順の目的 |

### 現在の候補 (2026-11-10 再生成 / main `60854c8`)

| 項目 | 値 |
|---|---|
| ZIP | `FlashBeat-0.1.22-win64-60854c8.zip` (274,848,934 bytes) |
| SHA-256 | `839ad62db7e7fa4995da6e67efa11578aeb139d9187298481be44e3708f95664` |
| ソース SHA | `60854c84a6e510d116c5373041538829fe334ed8` (`origin/main` — PR #324 / #325 merge 後) |
| Application.version | `0.1.22+60854c8` (Player の `globalgamemanagers` に埋まっていることを確認済み) |
| version.json | `version=0.1.22` / `tag=v0.1.22-win64-60854c8` / `mediaPolicy=song-free` |
| 更新チャンネル | `stable` (`StreamingAssets/update-channel.txt`) |
| ビルド日時 | 2026-11-10T06:38:18Z (ビルド元 commit の日時。ZIP は同一入力で再現する) |

> **古い候補は使用禁止。** 次の ZIP はソースが古いか、旧スクリプト (ビルド出力をそのまま ZIP 化する
> 経路) で作られており、`version.json` 非同梱 / `FlashBeat_BurstDebugInformation_DoNotShip` 同梱 /
> `.build-info.txt` 付きのいずれかに該当する。**ZIP 名と SHA-256 が上表と一致しない候補は使わない。**
>
> - `0.1.0+877bde7-win64-877bde7.zip`
> - `0.1.22+0bab549-win64-0bab549.zip`
> - `0.1.22+e45c494-win64-e45c494.zip`
> - `0.1.22+36a3572-win64-36a3572.zip`
> - `0.1.0+c9fb812-win64-c9fb812.zip` / `FlashBeat-0.1.0-win64-60854c8.zip`
>   (構造は正規だがバージョンが `0.1.0` のため更新判定に使えない)
>
> 旧形式 (`0.1.22+<sha>-win64-<sha>.zip`) は **もう生成されない**。もし見つけたら、
> それは旧版 `script/win64-build-mac.sh` (w/ja-text-cleanup など古いチェックアウト) で
> 作られたものなので、正規候補として扱わないこと。

**版の基準について**: 現行の GitHub Release は `v0.1.21-win64-d581c4c`。
自動更新はバージョンの大小で判定するため、**同じ版 (`0.1.21`) で別 SHA の Release を作っても
更新が効きません**。次の配布は **`0.1.22`** として作ってください (`FLASHBEAT_VERSION_BASE=0.1.22`)。

### 0.1 オーナーが行う操作 (これだけ)

1. ZIP を**新しいフォルダ**へ展開する (既存の展示ディレクトリへ上書きしない)
2. `Get-FileHash -Algorithm SHA256` で上表の SHA-256 と照合する
3. `FlashBeat.exe` を**引数なし**で起動する
4. Title → Select → Game (1 曲) → Result → Retry を 1 周し、**動画・ノーツ・判定・音声**を確認する
5. Option を開き、4 タブ (Play / Display / Sound / Data) の表示と操作を確認する (#241)
6. 実ディスプレイ全画面のスクリーンショットと `Player.log` を保存する (§3.5)

開発側 (cc) が行うのは、ログとスクリーンショットの取り込み・Issue への反映・
Release 公開判定だけです。展示 PC の操作とビルドの上書きは行いません。

---

## 1. 候補の同一性 (SHA-256 で確認する)

配布 ZIP には **`.zip.sha256` sidecar** が同梱されます。展開前に必ず照合してください。

```powershell
# PowerShell (Windows)
Get-FileHash -Algorithm SHA256 "FlashBeat-0.1.22-win64-<sha>.zip" | Select-Object -ExpandProperty Hash
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

| ID | 解像度 | 入手方法 |
|---|---|---|
| FHD | 1920×1080 | 実ディスプレイ (展示PC / FHD モニタ) |
| 2K | 2560×1440 | 実ディスプレイ (2K モニタ) |
| MAC | 2560×1664 | Mac 本機 (ネイティブ) — **macOS では 2026-11-10 時点で確認済み** |

2K モニタが確保できない場合は **「未証明」と記録**してください (Web のビューポート確認は代替になりません)。

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
| 3 | Game (準備) | 準備画面の進捗が出る | — |
| 4 | Game (プレイ) | **8 レーン / ノーツ / 判定ラインが視認できる** (#233)。**映像が出るか** (#233)。密所でも同じ | **ノーツが見えないなら不合格** |
| 5 | Game (音) | **ストリーム音声が鳴るか** (#13 / #4)。`Player.log` の `audioMode=Stream` を確認 | 鳴らないなら `#4` は未解決 |
| 6 | Result | リザルトが出る。スコア登録 (名前入力) が動くか (#134) | — |
| 7 | Retry | Retry で Game に戻れる | — |

### 3.5 記録するファイル

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

2026-11-10 時点で `build-win64` / `build-macos` は **ライセンス確認ステップで失敗**している
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
