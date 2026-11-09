# Evidence: macOS から Win64 配布物 (ZIP + SHA-256) を 만드는 (Issue #41 / #35)

- 日付: 2026-11-09
- 対象 commit: `d8c1b39` (main / PR #251 merge 時点)
- 実施場所: macOS (この Mac) / Unity 6000.5.6f1 / PowerShell 7.6.6 (mise 管理の pwsh)
- 関連 PR: #253 (`tools/release/package-win64.ps1` を macOS でも実行可能にする)

## なぜ必要だったか

展示 (U-16プログラミングコンテスト山口大会 2026 / 2026-11-14 / 宇部高専) 用に
**Win64 の配布物 (ZIP と SHA-256)** が必要 (§5)。
一方 Win64 CI (`build-win64.yml`) は **Unity ライセンス** で詰まっていた:

- リポジトリの Actions Secrets に登録されているのは `UNITY_LICENSE` (Personal の `.ulf`) だけ。
  `UNITY_EMAIL` / `UNITY_PASSWORD` は未登録。
- Personal の `.ulf` は GitHub-hosted ランナーでは machine binding mismatch が必ず起きる
  (実測 2026-11-08 / run 42470147986) ため、ワークフローは **Personal を明示的に拒否** する
  ようにした (PR #249)。
- つまり **CI を緑にするにはオーナーによる secret 登録が必要** で、
  その手を待たずに配布物を用意せざるを得なかった。

そこで **macOS 側から Win64 をビルドできないか** を調べたところ、
この Mac の Unity 6000.5.6f1 には **WindowsStandaloneSupport が入っていた**:

```
$ ls /Applications/Unity/Hub/Editor/6000.5.6f1/PlaybackEngines/
MacStandaloneSupport
WindowsStandaloneSupport
```

## 実測: macOS から Win64 ビルド

```
$ cd /Users/rebui/work/FlashBeat.macverify
$ FLASHBEAT_VERSION_BASE=0.1.14 FLASHBEAT_UPDATE_CHANNEL=preview \
  /Applications/Unity/Hub/Editor/6000.5.6f1/Unity.app/Contents/MacOS/Unity \
    -batchmode -nographics -quit -projectPath /Users/rebui/work/FlashBeat.macverify \
    -executeMethod BuildScript.BuildStandaloneWindows64 -logFile win64-build.log
exit=0
```

ログ抜粋:

```
[BuildScript] bundleVersion = 0.1.14+d8c1b39
[BuildScript] 第三者楽曲の音声・動画をビルド対象から除外しました (Musics / Videos/UnityYoutube / loading.mp4)
[BuildScript] Starting StandaloneWindows64 Build...
[BuildScript] Build succeeded: 407649127 bytes, output to .../Builds/Windows/FlashBeat.exe
[BuildScript] 更新スクリプトをビルド出力へ複製しました: .../Builds/Windows/tools/update/Update.ps1
[BuildScript] 更新チャンネル 'preview' を書き出しました: .../Builds/Windows/FlashBeat_Data/StreamingAssets/update-channel.txt
[BuildScript] メディア除外マニフェストを書き出しました: .../Builds/Windows/flashbeat-media-policy.json
```

出力の確認:

```
$ file Builds/Windows/FlashBeat.exe
Builds/Windows/FlashBeat.exe: PE32+ executable (GUI) x86-64, for MS Windows

$ du -sh Builds/Windows
390M    Builds/Windows
```

## 実測: パッケージング (ZIP + SHA-256)

`tools/release/package-win64.ps1` は当初 Windows 前提で macOS では `tar.exe` が無く失敗した。
PR #253 で `Resolve-TarCommand` (libarchive の tar を OS に応じて選択) と
`Join-PathSegments` (OS のパス区切りに依存しないパス結合) に直して再実行:

```
$ pwsh -NoProfile -ExecutionPolicy Bypass -File tools/release/package-win64.ps1 \
    -SourceDir .../Builds/Windows -OutDir .../dist2 \
    -Version 0.1.14-win64-d8c1b39 -CommitSha d8c1b395e30ea8c4ec44e3e50d1e00914cc25744 \
    -MediaPolicy song-free
[media] 第三者楽曲メディアはビルド対象から除外済み (manifest: Assets/Game/Resources/Musics, Assets/Game/Resources/Videos/UnityYoutube)
[1/4] ZIP 作成中: .../dist2/FlashBeat-0.1.14-win64-d8c1b39.zip
[2/4] ZIP 内容検査中
[3/4] SHA-256 計算中
[4/4] 完了: entries=224 managedDlls=138 mediaPolicy=song-free size=274,826,784 bytes
SHA-256: 3ac76d0d7a9e30f1063d733fbd17028c853f6c4b8fc4950de51761b9b81abfb3
Sidecar: .../FlashBeat-0.1.14-win64-d8c1b39.zip.sha256
タグ (GitHub Release): v0.1.14-win64-d8c1b39
```

### 検査結果

| 項目 | 結果 |
| --- | --- |
| `file FlashBeat.exe` | `PE32+ executable (GUI) x86-64, for MS Windows` |
| `shasum -a 256 -c <zip>.sha256` | `OK` |
| SHA-256 | `3ac76d0d7a9e30f1063d733fbd17028c853f6c4b8fc4950de51761b9b81abfb3` |
| ZIP サイズ | 274,826,784 bytes |
| ZIP エントリ数 | 224 |
| Managed DLL 数 | 138 (ZIP 内とビルド出力で一致 — スクリプトの `Test-ZipContents` が検証) |
| ZIP ルート | `FlashBeat.exe` / `FlashBeat_Data/*` / `UnityPlayer.dll` / `version.json` あり |
| `version.json` | `{"version":"0.1.14","buildSha":"d8c1b39...","tag":"v0.1.14-win64-d8c1b39","platform":"win64","mediaPolicy":"song-free",...}` |
| メディアポリシー | `song-free` — 第三者楽曲の wav / 動画を含まないことを `flashbeat-media-policy.json` で証明 |
| 更新チャンネル | `preview` (未検証の自動更新をしないため。`stable` は実機受入後) |

### ZIP はバイト単位で再現しない (重要)

ZIP にはファイル modification time が埋め込まれるため、**同じビルド出力から
再パッケージすると SHA-256 は変わります** (実測: 同一 `Builds/Windows` から
再実行 → `eb3374749202bd017e32e16751dfa3639def26d86bdf4c5db3b20852076e28a5`,
サイズ 274,826,785 bytes。上の `3ac76d0d…` とは 1 byte 差)。

したがって:

- 上に書いた SHA-256 は **その 1 つの成果物に対する値** であり、
  「この SHA なら中身の完全性を保証できる」根拠です。
- **再現性検証 (同じソース → 同じ SHA) は成立しません。**
- 必ず **ZIP に同梱された `.sha256` sidecar** と突き合わせてください
  (`sha256sum -c` 互換)。sidecar は常に同梱の ZIP と 1 対 1 で対応します。
- 更新判定は SHA ではなく Release タグとバージョンを使います
  (`docs/operations/update-release.md` の配布仕様)。

### 成果物の置き場所 (この Mac 上)

```
/Users/rebui/work/FlashBeat.macverify/Builds/Windows/            # ビルド出力 (展開用)
/Users/rebui/work/FlashBeat.macverify/dist2/FlashBeat-0.1.14-win64-d8c1b39.zip
/Users/rebui/work/FlashBeat.macverify/dist2/FlashBeat-0.1.14-win64-d8c1b39.zip.sha256
```

## 未検証 / 残課題

1. **Windows 実機での起動・プレイは未検証**。
   macOS からビルドした `FlashBeat.exe` が展示 PC で実際に動くかは **未確認**。
   展示 PC は無操作のため触っていない。Windows 実機での目視確認は
   Issue #233 / #242 の受入条件 (Title→Select→Game→Result→Retry) に含まれる **オーナー作業**。
2. **Win64 CI は依然として失敗** (`build-win64.yml` は未変更)。
   緑にするには GitHub Actions Secrets への `UNITY_EMAIL` / `UNITY_PASSWORD` 登録が必要。
   ただし **ライセンス待ちで配布物の作成自体が止まっていた問題は macOS 経路で解消済み**。
3. 本番データ (実際の wav / 実動画) は `song-free` ポリシーで除外されている。
   実機では `yt-dlp` で解決したストリームを再生する設計のため、配布物として正しい。
4. この成果物は **未検証のため GitHub Release には publish していない**。
   Release 化は Windows 実機での受入後に行う。
