# #394 ノーツ数依存の Transform 更新を削る — 実装と安全性

## 1. 確認したコスト (コード)

- ノーツは 1 曲で最大 **603 個** (Dogbite)。次点 360 (狂喜蘭舞)、343 (Chartreuse)。
- `NotesManager` がノーツごとに GameObject + `Notes` を生成する (ロード時は
  8ms/フレームの時間予算で分割)。
- `Notes.Update()` は**全ノーツが毎フレーム**走る。
- 既定 (`V6UiMode.Playfield = false`) では 3D 経路を通り、毎フレーム
  `transform.position = ...` を書く。

つまり 603 ノーツ × 毎フレーム = 603 回/フレームの Transform 書き込み。
**うち画面に関係あるのは数十個**。残りはカメラの視界外への書き込みになる。

## 2. 修正

`Notes.ShouldUpdateTransform(remaining)` を追加し、3D の Transform 更新を
画面に関係する範囲だけに限った。

```
remaining = noteTime - GManager.step_time   (判定時刻までの残り秒)

remaining > +2.5 秒  → 書かない (まだ遠い)
remaining < -1.0 秒  → 書かない (判定線を十分過ぎた)
それ以外             → 書く
```

- 上側 2.5 秒は design のアプローチ 1.6 秒 + 余裕。フレーム落ちで窓の境界を
  またいでも取りこぼさない。
- 下側 1.0 秒は「判定線を過ぎても画面下に残っている間」。
- 窓は連続 (入る/出るの 2 回だけ遷移する) ことをテストで固定した。
  飛び飛びだとノーツが一瞬止まって見える。

## 3. 判定に影響しない根拠

判定は **時刻表とキー入力**で行われる。`Judge` / `JudgeQueue` /
`JudgeScoreboard` は `Notes` の Transform を読まない (grep で確認)。

`Notes` の Transform を読むのは `AutoTestV6` の監査だけで、
そこは「既に可視と判定したノーツ」の z しか読まない。可視ノーツは
更新窓の内側なので、値は常に最新。

**したがって「ノーツの描画位置」と「判定結果」は変わらない。**
位置が古くなるのは画面外のノーツだけで、それは見えない。

## 4. 回帰テスト

`Assets/Tests/Editor/NoteCullTests.cs` (6 件):

| テスト | 固定すること |
|---|---|
| `NotesFarAheadAreNotUpdated` | まだ来ないノーツは書かない |
| `NotesInTheApproachWindowAreUpdated` | 画面に入る範囲は書く (判定線の瞬間を含む) |
| `NotesJustBehindTheJudgeLineAreStillUpdated` | 判定直後は書く |
| `NotesFarBehindAreNotUpdated` | 画面下へ抜けたら書かない |
| `TheUpdateWindowIsContinuous` | 窓が 1 つ (遷移 2 回) で飛び飛びでない |
| `TheWindowCoversTheDesignedApproachTime` | design の 1.6 秒 + 余裕を覆う |

## 5. 計測

**未計測。** この変更の効果を数値で示すには、最大ノーツの曲 (Dogbite 603) を
実機で `-fbperf` にかけ、before/after を同じ負荷帯で比べる必要がある。

- before の数値は `docs/evidence/381-fbperf-baseline-2026-11-12.md` の
  ツイッターランド (130 ノーツ) しか無い。**603 ノーツの before は未取得**。
- したがって本 Issue も「フレームレートが改善した」とは現時点で言えない。

## 6. 未検証のリスク

- 3D ノーツが画面外から入るとき、Transform が古いまま一瞬表示される可能性。
  窓の上側に 2.5 秒の余裕を置いて吸収しているが、**実機で目視確認していない**。
- HOLD の体 (`ApplyHoldLength`) は生成時に長さを決めるので、この変更の影響を
  受けない (毎フレームの Transform 書き換えではない)。
- Miss したノーツが画面下に残る時間は `BehindSlackSeconds` で決まる。
  実機で「消えるのが早すぎる / 遅すぎる」が出たら調整する。

数値と目視の確認が済むまで、この変更は「無駄な書き込みを減らした」以上の
主張をしない。
