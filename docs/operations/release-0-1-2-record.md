# Release 記録 — `v0.1.2-win64-4b0685d` (Issue #38 / #39 / #40 系: パッケージング・配布・更新機構)

記録日: 2026-11-07 03:35 JST · 担当: release worker (B) · 対象: 展示配布用の Win64 成果物

## 1. 成果物 (公開済み)

| 項目 | 値 |
|---|---|
| Release | [`v0.1.2-win64-4b0685d`](https://github.com/rebuildup/FlashBeat/releases/tag/v0.1.2-win64-4b0685d) (draft=false / prerelease=false = **stable**) |
| ZIP | `FlashBeat-0.1.2-win64-4b0685d.zip` (263,236,920 bytes / 223 entries) |
| SHA-256 sidecar | `FlashBeat-0.1.2-win64-4b0685d.zip.sha256` |
| ZIP の SHA-256 | `d14bb53a8d4a96a177f398ed3a76ad4953fa350fd08ed0d54c527ca3afde4d7f` |
| ビルド元 commit | `4b0685d2298c1ff6b8a6a8e9694a1dfd733c1564` (origin/52 = 全画面修正 #52 + 統合線) |
| ビルド版識別子 | `0.1.2+4b0685d` (`FLASHBEAT_VERSION_BASE=0.1.2` でビルド) |
| 更新チャンネル | **stable** (`FlashBeat_Data/StreamingAssets/update-channel.txt` = `stable`) |
| メディアポリシー | **song-free** (第三者楽曲の音声・動画は非同梱。`flashbeat-media-policy.json` が証明) |
| 展開後の exe SHA-256 | `3031bb5e15e85eb0eaa4de271b81e2c8f5e8eeb1254c3dcc0a70c4b950c39a60` (Unity の launcher stub。全ビルド同一) |
| 展開後のフォルダ fingerprint | `2e8956cc567c77b8031a0772b883b0db268d931d47868397ce777fc5195aadbd` (202 files / 385,775,636 bytes) |
| 展開先 (Windows) | `D:\4_apps\Unity\Build\FlashBeatBuild\dist-52` |

`song-free` の証明: 配布物の `flashbeat-media-policy.json` に
`Assets/Game/Resources/Musics` と `Assets/Game/Resources/Videos/UnityYoutube` の除外が記録され、
`resources.resource` は 196MB (自作の背景アニメーションのみ) に収まっています。
第三者楽曲の音声・動画 (合計 1.4GB) は ZIP に入っていません。

## 2. ビルドとパッケージングの手順 (再現用)

```powershell
# 1) ビルド (Windows / Unity 6000.5.6f1)
cd C:\Users\rebui\Desktop\FlashBeat.36
git fetch origin 52; git checkout -B build-52 4b0685d2298c1ff6b8a6a8e9694a1dfd733c1564
$env:FLASHBEAT_VERSION_BASE='0.1.2'      # 前回 Release より必ず大きい版数にする
# Unity -batchmode -nographics -quit -projectPath . -executeMethod BuildScript.BuildStandaloneWindows64

# 2) パッケージング (ZIP + .sha256 sidecar、内容検査)
powershell -NoProfile -ExecutionPolicy Bypass -File tools\release\package-win64.ps1 `
    -SourceDir C:\Users\rebui\Desktop\FlashBeat.36\Builds\Windows `
    -OutDir C:\Temp\fb-dist-52 `
    -Version 0.1.2-win64-4b0685d `
    -CommitSha 4b0685d2298c1ff6b8a6a8e9694a1dfd733c1564 `
    -MediaPolicy song-free

# 3) 公開 (stable。prerelease にはしない)
gh release create v0.1.2-win64-4b0685d --target <full sha> --title "..." --notes "..." --latest <zip> <zip.sha256>
```

**版数ルール**: 自動更新は「バージョンが厳密に大きい Release」だけを適用します
(同じバージョンで別 SHA の Release は自動更新の対象にしません)。
したがって **修正版を出すたびに `FLASHBEAT_VERSION_BASE` を上げて** ください
(0.1.1 → 0.1.2)。前リリースと同バージョンの Release を公開すると、
自動更新の判定があいまいになります (運営者が `Update.ps1 -Tag` で手動適用する運用になります)。

## 3. GitHub Actions (CI) の検証結果

| 実行 | 結果 | 内容 |
|---|---|---|
| `42270031326` (main push, `unity-activate@v4`) | failure (7s) | `Unable to resolve action 'game-ci/unity-activate@v4'` — v4 というタグは存在しません (実在タグは v2 / v3.0.0-beta.1) |
| `42270300331` (main, workflow_dispatch) | failure (7s) | main の workflow がまだ v4 のまま。**main には v2 修正が未着地** (`origin/52` の `4b0685d` が修正版) |
| `42270443261` (`ref 52`, workflow_dispatch) | success (36s) / **ビルドは未生成** | v2 修正でジョブは正常起動。ライセンス secret 未設定のため `skip=true` → Unity インストール・ビルド・パッケージ・アップロードの **全ステップが skipped** |

**判定: CI は「一度もビルドを生成していない」。** job の conclusion は success ですが、
成果物アーティファクトは 0 件で、Win64 ビルドは走っていません (skip は成功ではない)。
次のステップ: Unity ライセンスの secret (`UNITY_LICENSE` の base64 .ulf か
`UNITY_EMAIL` + `UNITY_PASSWORD`) を GitHub の secret に登録してから再実行すること。
未登録のままだと、毎回 skip されます。

`.github/workflows/**` の所有権は監督へ移っているため、workflow の修正は
PR / コミットで提案します (今回は `4b0685d` が既に対応済み。main への着地が必要)。

## 4. 展示 PC への適用

1. ブラウザまたは `gh` で `v0.1.2-win64-4b0685d` の ZIP と `.sha256` を取得する。
2. `Get-FileHash .\FlashBeat-0.1.2-win64-4b0685d.zip -Algorithm SHA256` が
   `d14bb53a…4d7f` と一致することを確認する (不一致なら再ダウンロード)。
3. 既存フォルダを `FlashBeat-backup-<stamp>` へ退避し、ZIP をその場に展開する
   (または配布物内の `tools\update\Update.ps1 -Tag v0.1.2-win64-4b0685d` を使う)。
4. `FlashBeat.exe` を直接起動する。起動時の Player.log に
   `[UpdateChecker] 更新チャンネル: stable` が出ることを確認する。

stable チャンネルの候補判定により、**バージョンが大きい 0.1.2 だけが**自動更新の
対象になります。テスト用の prerelease (`v0.1.1-win64-6fb6810` など) は stable ビルドの
候補になりません (展示 PC がテスト版に自動更新されることはない)。

## 5. 未解決 / 引き継ぎ

- **0.1.2 バイナリの実機起動**は実機操作の所有者 (#52 worker) の担当。
  同じコード線 (0.1.1+2e8c7bc) は実機で タイトル → 選曲 → ゲーム → リザルト → リトライ を
  完走し、判定 268/268、YouTube 映像と音声 (`audioMode=StreamCombined`, rms>0)、
  カタログ取得 (公開済み譜面が選曲に反映) を確認済み (stage-manifest の S5)。
- **統合 mp4 経路 (yt-dlp + ffmpeg マージ)** はツール level で exhibit PC 上で確認済み
  (h264 + aac の 1 本 mp4 ができる)。ゲーム内では未実行 — 実測した曲では
  YouTube の progressive mp4 (itag 18) が使われたため。
- **CI の Win64 ビルド**はライセンス secret 登録まで未成立。
- **展示フォルダ** (`D:\4_apps\Unity\Build\FlashBeatExhibit*`) は旧世代のままである。
  適用は運営者 / #52 worker の手動作業。

## 6. 関連

- パッケージング手順: [`update-release.md`](update-release.md)
- 更新の運用 (チャンネル・手動適用): [`update-patch.md`](update-patch.md)
- 展示運営者 runbook: [`exhibit-runbook.md`](exhibit-runbook.md)
- 成果物インデックス: `release/check/stage-manifest.json` (stage S5 / generation 4)
