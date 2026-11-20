# アセット・権利監査台帳 (Issue #794, v0.2.2 RC 時点)

監査日: 2026-11-20。対象: git 作業ツリー / Git LFS / ゲーム配布物 / web / 過去 Release。
詳細根拠: `docs/evidence/win64-2026-11-06-issue13/media-rights-audit.md`、
`docs/operations/release-media-audit.md`。

## 分類サマリ

| 区分 | 内容 | 判定 |
|---|---|---|
| A. 自社・ライセンス明確 | 自社制作動画、Noto Sans JP (OFL)、TMP (UCL)、lucide (ISC)、UniRx/NoteEditor (MIT)、効果音ラボ「決定ボタンを押す31」(free-commercial-no-credit, PR #746) | 配布可 |
| B. 権利者許諾を主張するが記録なし | `Resources/V6/select/jacket/*.jpg` 26 点 (v6-screen-contract は「許諾済み Ramen 素材」と記述、許諾書・記録は repo 内に無し) | **要確認** — 許諾証跡の保管場所を要確認 |
| C. 第三者素材・根拠不明 | 下記「除去対象」 | **除去対象** |
| D. 合成・テスト用 | `E2EProbeSong.json` (notes 空)、`ニセモノ.json` (空)、`contracts/parity`、`web` 側 fixture のうち lyrics 除去済みのもの | 残置可 (要監視) |

## C. 除去対象 (権利根拠を確認できない第三者素材)

| パス | 内容 | 配布物への影響 |
|---|---|---|
| `Assets/Game/Resources/Musics/*.wav` (15 曲) | 第三者楽曲の音声 (LFS) | ビルド除外済みだが git/LFS/履歴に残存 |
| `Assets/Game/Resources/Videos/UnityYoutube/*.mp4` (26 本) | YouTube 楽曲動画 (LFS)。GameScene.unity に GUID 参照残存 | ビルド除外済みだが git/LFS/履歴に残存 |
| `Assets/Game/Resources/*.json` (27 譜面) | 実在楽曲の譜面 + **歌詞本文**。`publish-catalog.mjs` で本番 D1 に公開済み | **リリース ZIP の resources.resource に同梱** + 本番 API で配信中 |
| `Assets/Game/Art/Images/` の YouTube/ジャケット由来画像群 | hqdefault/mqdefault/maxresdefault、yt-dlp 取得の動画コマ、楽曲ジャケット。ResultScene.unity/GameScene.unity にスプライト参照あり | **リリースに同梱される可能性** (Resources 外だが scene 参照で同梱) |
| `web/src/lib/chart/fixtures/sample-chart.json` | `チュートリアル.json` の歌詞込み複製 | vitest のみだが git に歌詞が残る |
| `web/db/fixtures/who.*` + `web/scripts/publish-catalog.mjs` | notes のみ (lyrics 除去済み) だが楽曲メタデータを含む | ローカル D1 / 公開ツール |
| `Assets/NoteEditor/Audio/*` | 出所未記録のサンプル SE | エディタのみ (ゲーム非含有) |
| `Assets/Game/Audio/カーソル移動4.mp3` | 出所・ライセンス未記録 (name から無料 SE 系と推測されるが証跡なし) | **リリースに同梱** — #791 で click/hit 音に使用中。ライセンス確認か差替えが必要 |

## 残存リスク (HEAD 削除では解消しない)

- **Git 履歴・LFS オブジェクト**: 上記ファイルは全て履歴に残る。完全除去には履歴書換え (filter-repo 等) + force-push が必要 → **破壊的操作のため別途承認が必要**。
- **過去の公開 Release**: v0.2.0/v0.2.1 の ZIP は song-free だが、より古い Release (v0.1.x) にメディア同梱の可能性 → 要個別確認。
- **本番 D1 / Web**: catalog API が楽曲メタデータ・歌詞を配信中。公開停止・スキーマ変更は影響が大きく要判断。
- **GameScene.unity の stale GUID 参照**: UnityYoutube mp4 への参照が残り、ファイル削除時に missing になる。

## 実施済み (v0.2.2 RC)

- click/hit 音源を `Resources/Musics/` (ビルド除外領域) から `Assets/Game/Audio/` へ移動 — 配布物で音が出る状態に修正 (#791)。ただしライセンス未確認のまま。

## 未実施 (次スプリント・要承認)

1. C 区分ファイルの HEAD からの削除 + 参照修正 (Scene GUID、Resources.Load、Web seed、fixture、BuildScript exclusion 整理)。
2. `カーソル移動4.mp3` のライセンス確認またはライセンス明確な代替への差替え (ユーザー指定音源のため差替えは要承認)。
3. 歌詞・譜面の扱い方針決定 (ローカル同梱をやめてクラウドカタログ専用にするか、権利処理を行うか)。
4. Git 履歴/LFS の完全パージ (filter-repo + force-push、影響範囲の明示と承認)。
5. 過去 Release アセットの監査と必要なら差替え/削除。
6. 無許諾素材検出の品質ゲート (例: `media-rights-audit` 系チェックスクリプトの CI/プリコミット化)。
