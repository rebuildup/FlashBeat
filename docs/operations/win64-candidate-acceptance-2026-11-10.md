# Win64 展示候補 — オーナー実施の受入手順 (2026-11-10)

> **この文書は cc (開発側) が用意した「候補の同一性」と「手順」です。**
> 実行は **オーナー** が行います。cc は展示 PC を操作・ビルド・上書きしません。

---

## 0. 前提 ((cc が確認済みの範囲)

| 項目 | 内容 |
|---|---|
| ビルド環境 | macOS (Apple Silicon) + Unity 6000.5.6f1 + WindowsStandaloneSupport |
| ビルド手順 | `FLASHBEAT_VERSION_BASE=0.1.22 bash script/win64-build-mac.sh origin/main /Users/rebui/work/dist/win64` |
| 出力 | `<outdir>/0.1.22+<sha>-win64-<sha>.zip` / `.zip.sha256` / `.build-info.txt` |
| 中身 (実測) | 223 entries / FlashBeat.exe / updater (`tools/update/Update.ps1`) / `StreamingAssets/update-channel.txt` / `flashbeat-media-policy.json` / **同梱音楽・動画なし** (第三者楽曲はビルド時に退避) |
| **未検証** | **Windows 実機での実行・全画面・1 曲完走・ストリーム音声**。ここが本手順の目的 |

**版の基準について**: 現行の GitHub Release は `v0.1.21-win64-d581c4c`。
自動更新はバージョンの大小で判定するため、**同じ版 (`0.1.21`) で別 SHA の Release を作っても
更新が効きません**。次の配布は **`0.1.22`** として作ってください (`FLASHBEAT_VERSION_BASE=0.1.22`)。

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

🤖 この文書は Claude Code が作成しました (cc は展示 PC を操作していません)。
