# spec: Who 譜面の歌詞タイミング反転と Resources 重複 (#154)

**Issue**: #154 | **Branch**: `w1/154` | **PR base**: `release-0-1-14` | **Status**: Draft

## 症状

Issue #154 の 「Who の譜面が壊れている」 報告のうち、譜面データ側 2 項目。

### 1. 歌詞の `startTime` / `endTime` が反転している

`Assets/Game/Resources/*.json` の `lyrics` ブロックは旧形式の並列配列
(`startTime` / `endTime` / `mondai` / `furigana` / `romaji`) で、`endTime[i]` は
「歌詞行 i を表示し終わる時刻」を表す。

実測 (`float32` 比較) で **6 曲 80 行** が `startTime[i] >= endTime[i]` になっていた。

| 曲 | 反転行 | 内訳 |
|---|---|---|
| Who | 28 / 28 | 全行。`endTime` が `startTime` の 0.1 秒未満手前 (0.006–0.100 秒) |
| まにまに | 23 / 23 | 全行。同上 |
| ツイッターランド | 24 / 24 | 全行。同上 |
| チュートリアル | 3 / 53 | 10, 12, 14 行目 (0.009–0.021 秒) |
| セルフィー | 1 / 31 | 15 行目 (`end=29.900` だけが他の行と不整合 (単体不良)) |
| パノプティコン | 1 / 29 | 7 行目 (`end=53.290` が単体不良) |

残りの 21 曲には反転なし。`endTime` 配列そのものが空の曲 (イガク・春嵐・一龠 など 12 曲) は
対象外 (実行時のフォールバックが効く)。

### 2. 譜面 JSON が 2 つの Resources フォルダに重複していた

`Assets/Resources/` (歌詞なし) と `Assets/Game/Resources/` (歌詞あり) の両方に
全 27 曲があり、`.meta` の guid も別。`Resources.Load("Who.json")` が
どちらを返すか不定で、古い側が勝つと歌詞が丸ごと消える。

### 3. (対象外) 曲 ID と動画プレイヤーの対応が位置依存

`MusicManager.cs` の `YPlayer[GManager.songID]` がシーン上の並び順で動画を選ぶため、
サーバーの曲順 (CloudCatalogConfig の `enabled: true`) と `GameScene.unity` の
YPlayer 並びが食い違ると別の曲の動画が流れる。
本 Worker の担当外 (`Assets/Game/Scripts/**` と `Assets/**/*.unity` は他 Worker の担当)。

## 対処

### 1. `endTime` を実行時と同じ値に直す (80 行)

**採用した値**: `LyricsTimeline.ResolveEnd` が今日すでに使っているフォールバック値。

```
end = start + 4s                       // DefaultLineDuration
次の行の開始が start < next < end なら end = next
```

- 次の行の開始が使えるとき → `endTime[i] = startTime[i+1]` (行末は `start + 4s`)
- 書き換えは `endTime` 配列の該当トークンのみ。`name` / `BPM` / `offset` / `notes` /
  `lyrics.startTime` / `mondai` / `furigana` / `romaji` は 1 バイトも変えていない
  (スクリプトで旧新を比較して assert 済み)。

**なぜ swap (startTime と endTime の入れ替え) ではないのか**

指示されていた「2 つの値を入れ替える」方式は採用しなかった。`ResolveEnd` は
`endTime > start` ならその値を採用するため、swap すると表示区間が
`endTime - startTime` (旧データで 0.006–0.100 秒) になり、歌詞が
一瞬 (0.1 秒未満) 瞬くだけで事実上見えなくなる。`セルフィー` 15 行目と
`パノプティコン` 7 行目は swap すると行の開始が 2.4–7.7 秒早まり、
前後の行と重なる。

本次の値なら **画面に出る歌詞は今日と完全に一致** する
(旧新の譜面前後で `LyricsTimeline.Build` の結果 (開始・終了・本文) を全曲比較して
一致を確認済み)。データだけを自己整合に直し、表示は変えない。

### 2. Resources フォルダの重複

`Assets/Resources/` 側の譜面 JSON と `.meta` は すでに削除済み
(commit `0baadfd` "fix(assets): 譜面 JSON の Resources フォルダ重複を落とす (#154)"。
この commit は `origin/release-0-1-14` に含まれる)。

```
$ git ls-files 'Assets/Resources*'
Assets/Resources.meta
```

`Assets/Resources/` フォルダとその中の譜面 JSON は存在しない。
残っているのは**孤立したフォルダの `.meta`** だけ ( Unity が次回インポート時に
取り除く想定。本 Worker の担当外ファイルなので未変更)。

### 3. テスト

- 新規 `Assets/Tests/Editor/ChartLyricsTimingTests.cs`
  - `EveryLyricsRowStartsBeforeItEnds`:
    `Assets/Game/Resources/` の譜面 JSON を**全件**走査し、
    `endTime` を持つ行すべてで `startTime < endTime` であることを検査する。
- 既存 `Assets/Tests/Editor/ResourceChartUniquenessTests.cs` を拡張
  (重複するテストは追加しない):
  - `ResourceRoots()`: `Assets` 配下の `Resources` フォルダを**動的に**列挙する
    (ハードコードした 2 フォルダだけだと 3 つ目・4 つ目を見落とすため)。
  - `NoChartExistsInTwoResourcesFolders`: 既存の重複検査を動的列挙に差し替え。
  - `ChartFilesExistOnlyInTheCanonicalResourcesFolder` (新規):
    `Assets/Game/Resources` 以外の Resources フォルダに譜面 JSON が無いことを検査。
  - `TheLoadedChartHasTheMergedLyrics`: 既存の「歌詞つき譜面が読み込める」検査を維持。

## 検証

### 実行済み (Unity 不要・オフライン)

- 全 27 譜面の `startTime >= endTime` を再走査 → 違反 0 件。
- 変更ファイルは 6 件のみ、`git diff --stat` は 6 ファイル / 6 行 (1 行 JSON のため)。
- 旧新の JSON を比較して `name` / `BPM` / `offset` / `notes` / `lyrics.startTime` /
  `lyrics.mondai` / `lyrics.furigana` / `lyrics.romaji` が不変であることを assert。
- `LyricsTimeline.Build` / `ResolveEnd` を Python で再現し、旧新で
  全 27 曲・全行の (開始, 終了, 本文) が一致することを確認 → **画面出力は不変**。
- `git ls-files 'Assets/Resources*'` → `Assets/Resources.meta` のみ (譜面 JSON の重複なし)。

### 未実行 (要 Unity)

- EditMode テスト:
  - `ChartLyricsTimingTests.EveryLyricsRowStartsBeforeItEnds`
  - `ResourceChartUniquenessTests.NoChartExistsInTwoResourcesFolders`
  - `ResourceChartUniquenessTests.ChartFilesExistOnlyInTheCanonicalResourcesFolder`
  - `ResourceChartUniquenessTests.TheLoadedChartHasTheMergedLyrics`
  本 Worker は Unity を起動できないため未実行。レビュー時は Unity Editor で
  EditMode テストを走らせて確認すること。

## 完了条件

- [x] 反転していた歌詞行 80 行 (6 曲) を `startTime < endTime` に修正
- [x] 歌詞以外のデータ (notes / BPM / offset / 曲名 / 歌詞本文) は不変
- [x] 表示される歌詞が今日と同一であることをオフラインで検証
- [x] `Assets/Game/Resources/` 以外に譜面 JSON が無いことをテストで固定
- [x] EditMode テスト (歌詞タイミング / Resources 一意性) を追加
- [ ] Unity EditMode テストを green にする (レビュー環境で実行)
- [ ] `MusicManager.YPlayer[GManager.songID]` の ID ベース化 (別 Worker の担当)

## Known Issues

- 孤立した `Assets/Resources.meta` が残っている (フォルダ本体は削除済み)。
  本 Worker の担当外 (`Assets/Game/Resources/*.json` 以外) なので未変更。
- 歌詞行の「本当の終了時刻」はデータから復元できない
  (元 `_text.json` の段階から壊れた値が入っている。`Who` の `endTime` は
  概ね `floor(startTime, 0.1)` で、`startTime` のコピーになっている)。
  今回の修正は「実行時が今日使っている値」をデータに固定するもので、
  歌詞行の本来の長さの復元ではない。
- `MusicManager` の `YPlayer[GManager.songID]` (位置依存の動画対応) は未修正。
  曲 ID で引く形 (ID → YPlayer の対応表) に変える必要があるが、
  `Assets/Game/Scripts/Gameplay/MusicManager.cs` と `GameScene.unity` は他 Worker の担当。
