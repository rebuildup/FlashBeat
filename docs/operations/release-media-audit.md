# 公開済み Release のメディア同梱監査 (Issue #77)

- **対象**: `rebuildup/FlashBeat` の公開済み Release 17 件 (2026-11-08 時点)
- **目的**: どの配布 ZIP に第三者楽曲 (LeaF / t+pazolite / r-906 / STEAKA / Azari ほか) の
  音声・動画が同梱されているかを **実測** し、配布対象から外す Release と、
  そのまま残してよい Release を切り分ける
- **重要**: 本ドキュメントは **調査と提案まで**。Release の公開・削除・タグ書き換え・
  アセット差し替えは **行っていない** (判断はオーナー / Supervisor。Issue #77 の明示事項)

## 1. 結論 (先に読む部分)

| 判定 | Release | 件数 | 扱い |
|---|---|---|---|
| **第三者楽曲メディア同梱** | `v0.1.0-win64-743c88d` | **1** | 展示・配布の対象から外す (推奨は §5) |
| 第三者楽曲メディアなし (song-free) | `v0.1.1-win64-2e8c7bc` / `v0.1.1-win64-6fb6810` / `v0.1.2`〜`v0.1.14` ほか | 16 | そのまま残す |

- 第三者楽曲を含むのは **ビルド `743c88d` の 1 件だけ**。`resources.resource` が
  **1,399,081,540 bytes** (1.30 GiB) で、他の 16 件はすべて **196,446,346 bytes** (187 MiB)。
  この差は自作の背景アニメーション (約 196MB) との差で、差額の約 1.2GB が第三者楽曲の
  音声・動画 (283,865,881 bytes の WAV/MP3 + 1,144,714,929 bytes の MP4) である。
  (内訳の出典: `docs/evidence/win64-2026-11-06-issue13/media-rights-audit.md`)
- `v0.1.0-win64-743c88d` は 2026-11-08 時点で **prerelease = true / draft = false**。
  Issue #77 作成時は `prerelease = false` と記録されていたが、現在は prerelease に
  下がっている (公開自体は継続)。stable チャンネルの更新候補にはならないため、
  自動更新がこのビルドに落ちることはない。

## 2. 判定方法 (再現手順)

判定は **ZIP を丸ごとダウンロードせず**、HTTP Range リクエストで
ZIP 終端ディレクトリ (central directory) だけを取得して行う。
中央ディレクトリには各エントリの名前と **非圧縮サイズ** が入っており、
`FlashBeat_Data/resources.resource` のサイズだけで
「Unity が `Resources/**` に第三者楽曲メディアを焼き込んだか」が分かる。

```bash
# 全 Release の判定 (17 件)
GITHUB_TOKEN=$(gh auth token) python3 tools/release/audit-release-media.py

# 特定の Release だけ
python3 tools/release/audit-release-media.py --tag v0.1.0-win64-743c88d

# 機械可読出力 (Evidence 用)
GITHUB_TOKEN=$(gh auth token) python3 tools/release/audit-release-media.py --json
```

判定の根拠 (4 点):

1. **ルーズなメディアエントリ** — ZIP 直下に `*.wav / *.mp3 / *.mp4 / *.mov / *.ogg / *.m4a / *.webm / *.avi`
   があるかどうか (17 件すべて 0 件)。
2. **`FlashBeat_Data/resources.resource` の非圧縮サイズ** —
   自作の背景アニメーションだけなら 196,446,346 bytes、第三者楽曲込みなら 1,399,081,540 bytes。
   判定閾値 300MB (`--threshold-bytes`)。
3. **`version.json` の有無** — media policy が導入されたビルドから同梱。
4. **`flashbeat-media-policy.json` の有無** — ビルドが「どのパスを除外したか」を証明する manifest。
   (`StreamingAssets/update-channel.txt` も併せて確認する)

手動で同じことを確かめる場合 (Windows 展示機・手元 ZIP ):

```powershell
# ZIP の中身一覧 (media ファイルの有無)
Get-ChildItem .\FlashBeat-0.1.0-win64-743c88d.zip | Out-Null
tar.exe -tf .\FlashBeat-0.1.0-win64-743c88d.zip | Select-String -Pattern '\.(wav|mp3|mp4|mov|ogg|m4a|webm|avi)$'

# resources.resource のサイズ (展開後)
Get-Item .\FlashBeat_Data\resources.resource | Select-Object Length

# 除外証明 manifest の有無
Test-Path .\flashbeat-media-policy.json
```

## 3. 実測結果 (2026-11-08)

`python3 tools/release/audit-release-media.py` の出力 (Release タグ順)。

| Release タグ | prerelease | ZIP size (bytes) | resources.resource | ルーズなメディア | version.json | media manifest | update-channel | 判定 |
|---|---|---|---|---|---|---|---|---|
| `v0.1.0-win64-743c88d` | **yes** | **1,459,717,135** | **1,399,081,540** | 0 | — | — | — | **third-party-media** |
| `v0.1.1-win64-2e8c7bc` | no | 263,236,916 | 196,446,346 | 0 | ✓ | ✓ | ✓ | song-free |
| `v0.1.1-win64-6fb6810` | **yes** | 263,250,679 | 196,446,346 | 0 | — | — | — | song-free |
| `v0.1.2-win64-4b0685d` | no | 263,236,920 | 196,446,346 | 0 | ✓ | ✓ | ✓ | song-free |
| `v0.1.3-win64-3b7d564` | no | 263,246,230 | 196,446,346 | 0 | ✓ | ✓ | ✓ | song-free |
| `v0.1.4-win64-4d6b407` | no | 263,246,443 | 196,446,346 | 0 | ✓ | ✓ | ✓ | song-free |
| `v0.1.5-win64-b677f8f` | no | 263,247,045 | 196,446,346 | 0 | ✓ | ✓ | ✓ | song-free |
| `v0.1.6-win64-920557d` | no | 263,249,569 | 196,446,346 | 0 | ✓ | ✓ | ✓ | song-free |
| `v0.1.7-win64-6b98e29` | no | 263,645,432 | 196,446,346 | 0 | ✓ | ✓ | ✓ | song-free |
| `v0.1.8-win64-5942e1b` | no | 263,646,737 | 196,446,346 | 0 | ✓ | ✓ | ✓ | song-free |
| `v0.1.9-win64-c083422` | no | 263,651,848 | 196,446,346 | 0 | ✓ | ✓ | ✓ | song-free |
| `v0.1.10-win64-734e112` | no | 263,655,704 | 196,446,346 | 0 | ✓ | ✓ | ✓ | song-free |
| `v0.1.11-win64-5f94d12` | no | 265,312,659 | 196,446,346 | 0 | ✓ | ✓ | ✓ | song-free |
| `v0.1.11-win64-98137a5` | **yes** | 265,312,794 | 196,446,346 | 0 | ✓ | ✓ | ✓ | song-free |
| `v0.1.12-win64-a0975b4` | no | 265,312,674 | 196,446,346 | 0 | ✓ | ✓ | ✓ | song-free |
| `v0.1.13-win64-5ee86a9` | no | 265,312,613 | 196,446,346 | 0 | ✓ | ✓ | ✓ | song-free |
| `v0.1.14-win64-617c2b6` | no (latest) | 265,292,377 | 196,446,346 | 0 | ✓ | ✓ | ✓ | song-free |

補足:

- `v0.1.1-win64-6fb6810` (テスト用 prerelease) だけ `version.json` /
  `flashbeat-media-policy.json` が無い。**メディア自体は入っていない**ことは
  `resources.resource` = 196,446,346 bytes (他の song-free ビルドと同一) で確認できる。
  除外証明の manifest が無いのは、media policy 導入前のビルドだから。
- ルーズなメディアが ZIP 直下にある Release は **1 件も無い** (17/17 で 0 件)。
  メディアは常に `resources.resource` に焼き込まれている。

## 4. 再現時の注意

- 判定は **中央ディレクトリに記録された非圧縮サイズ** に基づく。
  `resources.resource` の *圧縮後* サイズではない点に注意 (圧縮率は ZIP ごとに変わる)。
- 新しい Release を出したら、この判定を **再実行** して表に追記する
  (`docs/operations/update-release.md` の発行手順 §8 参照)。
- 判定は GitHub の公開状態“Sees 時点のスナップショット”であり、Release が差し替えられると値が変わる。

## 5. 推奨 (判断はオーナー / Supervisor)

### 5.1 `v0.1.0-win64-743c88d` への推奨: **prerelease のまま保持し、配布対象から除外** (取り下げではなく)

- **推奨**: すでに prerelease なので **追加の公開操作は行わない**。
  ただし **展示・配布の配布リストから外し**、リリース本文に
  「第三者楽曲メディア同梱・展示利用不可」を明記する (下線)。
- **理由**:
  - 削除すると、**このビルド固有の既知の挙動を再現・参照する手段** (proven artifact) を失う。
    `v0.1.0` のビルドはフォント修正 (#27) を含む唯一の記録でもある。
  - 一方 **prerelease なので stable チャンネルの自動更新候補には入らない**
    (`UpdateChecker` は prerelease を候補にしない。Channel 判定は `update-release.md` 参照)。
    したがって「展示に配布されうる」リスクは既に構造的に塞がれている。
  - Release 本文への注記は所有権・DL 数の記録を壊さず、誤配布防止の案内になる。
- **代替案と却不採理由**:
  - *完全削除*: proven artifact の喪失 + 参照 URL の 404 化。費用に見合わない。
  - *同バージョンの再パッケージ*: 契約上「同じバージョンで別 SHA の Release」は
    自動更新の「同じバージョンの別ビルド」判定に入り、stable チャンネルの更新経路を壊す
    (`update-release.md` の「既存バイナリ互換」節)。配布しない Release をあえて作り直す必要はない。
  - *media policy 導入済みのビルドに差し替える*: 上記と同じ「バージョン重複」の罠に受益しない。

### 5.2 今後のルール (発行手順への追記)

1. **media policy 導入前 (2026-11-06 頃) のビルドは配布リストに載せない**。
   対象 Release は `v0.1.0-win64-743c88d` のみ (上記表で manifest を持たないもの)。
2. 新しい Release を出したら `tools/release/audit-release-media.py` を実行し、
   同梱メディア 0 件と `flashbeat-media-policy.json` の存在を確認する (発行前チェック)。
3. 展示用 ZIP のダウンロードページには、**stable チャンネルの最新安定版だけ**を載せる。
   prerelease / 旧世代 ZIP を掲載しない。

## 6. 実施していないこと (Known Issues)

- **Release の公開・削除・タグ書き換え・アセットの差し替えは実施していない** (Issue #77 の明示的な禁止事項)。
  §5.1 の「本文への注記」も、実行はしていない (Supervisor / オーナー承認待ち)。
- **ZIP 実体のダウンロード検証はしていない** (1.36GB の取得を避けるため)。
  判定は ZIP 構造のメタデータ (中央ディレクトリ) に基づく実測であり、
  展開して中身を目視したわけではない。
  展開検証が必要なら、展示機で `tar.exe -tf` / `Get-Item resources.resource` で確認できる (§2)。
- `v0.1.1-win64-6fb6810` は `version.json` / manifest が無いため、
  「除外を manifest で証明」までは做不到。実測サイズ (196,446,346 bytes) による判定。

## 7. Evidence (実行コマンドと出力)

```console
$ GITHUB_TOKEN=$(gh auth token) python3 tools/release/audit-release-media.py --tag v0.1.0-win64-743c88d
=== v0.1.0-win64-743c88d ===
  name        : FlashBeat 0.1.0-win64 (build 743c88d)
  prerelease : True
  published   : 2026-11-06T13:41:01Z
  asset       : FlashBeat-0.1.0-win64-743c88d.zip (1,459,717,135 bytes, DL 5)
      verdict        : third-party-media
      zip entries    : 216
      resources.resource : 1,399,081,540 bytes
      loose media    : 0 件
      version.json   : なし / media manifest: なし / update-channel: なし
```

```console
$ GITHUB_TOKEN=$(gh auth token) python3 tools/release/audit-release-media.py --tag v0.1.14-win64-617c2b6
=== v0.1.14-win64-617c2b6 ===
  name        : FlashBeat 0.1.14 (win64) — プレイフィールドのフラット描画化 + 判定エフェクトのレーン修正 (#168)
  prerelease : False
  published   : 2026-11-07T16:07:31Z
  asset       : FlashBeat-0.1.14-win64-617c2b6.zip (265,292,377 bytes, DL 1)
      verdict        : song-free
      zip entries    : 224
      resources.resource : 196,446,346 bytes
      loose media    : 0 件
      version.json   : あり / media manifest: あり / update-channel: あり
```

全 17 件の実行は `python3 tools/release/audit-release-media.py` で再現できる
(実行時間 ≈ 60 秒 / 通信量は Release あたり数 KB〜数十 KB)。

## 8. 関連

- メディア除外の実装と manifest: `docs/operations/update-release.md` §メディア排除
- 権利監査 (内訳の出典): `docs/evidence/win64-2026-11-06-issue13/media-rights-audit.md`
- 判定ツール: `tools/release/audit-release-media.py`
- Issue: #77 (旧 Release の第三者メディア), #41 (media policy 導入の経緯)
