# 歌詞同期 (Issue #30 §2)

対象 Issue: [#30](https://github.com/rebuildup/FlashBeat/issues/30) §2「歌詞は動画の再生時刻に同期する表示データ」。

## 1. 表示の正本

| 項目 | 正本 |
|---|---|
| データ | 譜面 JSON の `lyrics` (`startTime` / `endTime` / `mondai` / `furigana` / `romaji`) |
| 時刻 | 動画 (VideoPlayer.time) と同じタイムライン。フレーム時間 (`Time.deltaTime`) の累積ではない |
| 実装 | `Assets/Game/Scripts/UI/LyricsTimeline.cs` (純ロジック) + `GameSceneManager` (表示) |

ノーツと同じクロック `GManager.step_time` を使う。ノーツ位置は
`MediaTimeline.NotePositionZ(noteTime, GManager.step_time, noteSpeed)` で同じ基準になるため、
歌詞とノーツはフレーム落ちやバッファリングで互いにずれない。

## 2. 出し入れのルール

1. `lyrics.startTime[i]` の時刻で行を表示し始める。
2. 消す時刻は次の順で決める。
   - `lyrics.endTime[i]` が `startTime[i]` より後なら、それを使う。
   - `endTime` が無い、または開始より前の値 (旧データには区間が1つずれている譜面がある)
     なら、**次の行の開始時刻**、または **既定 4.0 秒** (`LyricsTimeline.DefaultLineDuration`)
     の早いほうまで表示する。
3. 表示区間は常に「開始 < 終了」。区間が壊れた行は捨て、譜面の行数より多い `mondai` は使わない。
4. 行の本文が空白 (全角スペース等) だけなら「ここで歌詞を消す」合図として扱い、
   パネルごと隠す。空のパネルを残さない。

## 3. パネルの出し入れ

歌詞パネル (`TextPanel`) は次の条件がすべて揃ったときだけ表示される。

- 歌詞表示が ON (`GameSceneManager.Menu`、既定は ON = 自動)
- いま表示すべき行があり、その行に文字がある
- ポーズメニュー (`Esc`) が開いていない

これにより、曲に歌詞が無いとき・間奏中・ポーズ中は歌詞領域が画面から消える。
歌詞の表示 ON/OFF はノーツ判定にも動画同期にも影響しない (表示だけが切り替わる)。

手動での ON/OFF はゲーム中のポーズメニュー (`Esc`) → `T` キーで行う
(`GameSceneManager.SwitchPanel` → `TextPanelOn` / `TextPanelOff`)。

## 4. 旧データとの関係

- `furigana` / `romaji` は**データ形式を変えない**。既存の `chart.lyrics` と `_text.json` を
  破壊しない (Issue #30 §2 の「一斉破壊しない」方針)。
- 表示は `mondai` (本文) と `furigana` (あれば) のみ。`romaji` のタイピング入力は
  GameScene 側では動かない (旧 `TypingSceneManager` の入力モードは別シーンのまま残す)。

## 5. テスト

`Assets/Tests/Editor/LyricsTimelineTests.cs` (EditMode)。

- 区間の決め方 (endTime 有・無し・開始より前・配列長の不一致)
- どの行が画面にあるか (開始/終了の境界、間奏の空き、空白行)
- 同梱の譜面すべてで「区間が正」「順番が崩れない」「その行の開始時刻には必ず出ている」

実機での確認手順は Issue #59 の検証ワーカーの担当 (PR 本文に手順を記載)。
