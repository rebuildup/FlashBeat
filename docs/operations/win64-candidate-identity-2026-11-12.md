# 展示候補 `0.1.23+b6860a80` の成果物同一性 検証 (2026-11-12)

凍結候補 ZIP の **同一性** を、実機を操作せずに確認した記録。
**この文書は実機受入の証拠ではない。** 「アーティファクトが文書どおりの物か」だけを示す。

## 1. 結論

| 項目 | 結果 |
|---|---|
| ZIP の SHA-256 | **文書と一致** |
| ZIP サイズ | **文書と一致** |
| `version.json` | 文書と一致 |
| 更新チャンネル | `stable` |
| 同梱メディア | **0 件** (policy = song-free) |
| **実 Win64 での動作** | **未検証** (この文書の対象外) |

## 2. 実測値

### 2.1 外側の同一性

```
$ shasum -a 256 FlashBeat-0.1.23-win64-b6860a80.zip
9ee7e4a6ff89c4548e7bf4dc5836a5ab08b57878baea2c148e5a6e6309283d20  FlashBeat-0.1.23-win64-b6860a80.zip

$ ls -l FlashBeat-0.1.23-win64-b6860a80.zip
275052255 bytes
```

`docs/operations/win64-candidate-acceptance-2026-11-10.md` の記載
(`9ee7e4a6…` / 275,052,255 bytes) と一致し、sidecar `.zip.sha256` の値とも一致する。

### 2.2 中身の同一性

```
$ unzip -l FlashBeat-0.1.23-win64-b6860a80.zip | tail -2
---------                     -------
424349463                     224 files
```

| エントリ | 内容 |
|---|---|
| `version.json` | `version=0.1.23` / `buildSha=b6860a801178…` / `tag=v0.1.23-win64-b6860a80` / `platform=win64` / `mediaPolicy=song-free` / `createdAt=2026-11-11T10:24:47Z` |
| `FlashBeat_Data/StreamingAssets/update-channel.txt` | `stable` |
| `tools/update/Update.ps1` | 51,327 bytes (更新スクリプト同梱) |
| `FlashBeat.exe` | 667,648 bytes |
| `flashbeat-media-policy.json` | `policy=song-media-excluded`、除外 `Assets/Game/Resources/Musics` と `Assets/Game/Resources/Videos/UnityYoutube` |
| 同梱メディア (`.mp4` / `.m4a` / `.wav` / `.ogg` / `.mp3`) | **0 件** |

ファイル数 224 / 展開後 424,349,463 bytes は、文書の「224 entries / 138 managed DLL」と
矛盾しない。

## 3. この検証で言えること / 言えないこと

**言えること**

- 手元の ZIP は、文書に記録された候補と **バイト単位で同じ**である。
- ZIP は契約どおりの構造 (version.json 同梱 / updater 同梱 / song-free / stable)。
- 古い契約違反 ZIP (`.build-info.txt` 付き / BurstDebug 同梱 / version.json 無し) ではない。

**言えないこと (重要)**

- **実 Windows で起動するか。** これは実機でしか分からない。
- **1 曲が完走するか。** 映像・音声・判定・Result は実機でのみ確認できる。
- **FHD / 2K で正しく全画面になるか。** 実解像度のディスプレイが要る。
- **ストリーム音声が鳴るか。** `Player.log` の rms と音声モード行が要る。

したがってこの文書をもって #233 / #242 を完了扱いにしない。

## 4. 実機の状態 (読み取りのみ / 変更なし)

2026-11-12 17:07 JST に `device-windows` へ **読み取り専用** で問い合わせた範囲:

| 項目 | 結果 |
|---|---|
| 到達性 | 到達可 (`hostname` = `UUU`) |
| `FlashBeat` プロセス | **無し** (稼働していない) |
| Desktop の `FlashBeat*` | **無し** (配置されていない) |

**アプリの起動・停止・ZIP の展開・キー入力は一切行っていない。**
`device-windows` が展示機と同一である可能性が高く、オーナーの許可が無いため、
導入と実行は実施しない (`docs/operations/windows-machine-availability-2026-11-10.md`)。

## 5. 残っている受入項目

| # | 項目 | 状態 |
|---|---|---|
| 1 | 実 Win64 で 1 曲完走 (この artifact) | **未実施** |
| 2 | FHD 1920×1080 の実フルスクリーン | **未実施** (この機に FHD パネルが無い) |
| 3 | 2K 2560×1440 の実フルスクリーン | **未実施** (前候補 `0.1.22+7af9242d` では PASS 済み) |
| 4 | 人間による音声聴取 | **未実施** (信号ベースの判定のみ) |
| 5 | Result → Register のオンライン送信 | **未実施** (本番 API へ書き込まない) |

1〜3 は実機の前でしか埋められない。4 は運用判断、5 はオーナー承認が要る。
