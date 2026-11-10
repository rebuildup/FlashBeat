# #241 — 表示設定の「寿命」と来場者セッションの境界 (設計のみ / 実装していない)

**状態: 設計案。コードは変更していない (2026-11-10 / main `c9431b3a` は #343 の実装のまま)。**
オーナー指示 (2026-11-10): 案 A (セッション設定に戻す) を優先候補とするが未承認。
**同じ人の Retry・選曲変更では保持し、来場者交代時には初期化**できる境界を、既存の画面遷移で
無理なく扱えるかを確認する。

---

## 1. 設定の寿命は 3 つに分かれている

| 層 | 何が入るか | 現在の実装 |
|---|---|---|
| **A. プレイ設定** (音量 / NoteSpeed / NoteTiming) | 利用者が Option で決める | `OptionSettings` — `PlayerPrefs` に即時保存し、`TitleSceneManager.Start` で `Load()` |
| **B. 実行セッション設定** (Key Guide / Lyrics) | Ready で選ぶ「このプレイの設定」 | `PlaySession` の**静的フィールド** + #343 で `PlayerPrefs` 保存 (`FlashBeat.KeyGuide` / `FlashBeat.Lyrics`) + `TitleSceneManager.Start` で `Load()` |
| **C. 実行カウンタ** (score / combo / 判定数) | 1 プレイの状態 | `GManager` 静的フィールド + `GManager.ResetSession()` |

`PlaySession.Reset()` は **既定値 (Compact / 歌詞あり) に戻すだけで、保存値には触らない**。
**このメソッドは実行時コードから呼ばれていない** (呼び出しはテストのみ。実測:
`grep -rn "PlaySession.Reset" Assets --include='*.cs'`)。

## 2. 既存の画面遷移で、どこが境界になり得るか (実測)

| 遷移 | コード | 同じ人か / 来場者の交代か |
|---|---|---|
| Opening → Title | `OpeningSceneManager` (Fade) | **来場者の入口** (展示では Title に戻して待つ) |
| Title → Select | `TitleSceneManager.cs:342` | 同じ人 |
| Title → Option | `TitleSceneManager.cs:347` | 同じ人 (来場者が設定を変える) |
| Option → Title | `OptionSceneManager.cs:369` | 同じ人 |
| Select → Game (Ready) | `SelectSceneManager` 経由 | 同じ人 |
| Game → Select (中断) | `GameSceneManager.cs:215` | 同じ人 |
| Game → Game (Retry) | `GameSceneManager.cs:220-221` | 同じ人 |
| Game → Result | `GameSceneManager.cs:225` | 同じ人 |
| Result → Game (Retry) | `ResultSceneManager.cs:218-222` | 同じ人 |
| Result → Select | `ResultSceneManager.cs:227` | 同じ人 (曲を選び直す) |
| **Result → Title** | **`ResultSceneManager.cs:234`** | **来場者の区切り** (展示の「次の人へ」) |

**Title シーンが唯一の「待機画面」** で、Result から Title へ戻る導線は既に存在する
(`ResultSceneManager.TitleScene()`)。来場者を入れ替えるとき、展示スタッフはこの導線で
Title に戻す運用になっており、**Title 進入 = 来場者の交代** とみなせる。
逆に、同じ人が Retry・選曲変更・Option 変更をする間は **Title を通らない**。

**注意**: 現状、**放置で自動的に Title へ戻る仕組みは無い** (アイドル復帰の実装なし)。
来場者が Select や Result に居たまま離席し、次の来場者がその画面から始めると、
設定は引き継がれる。Title 進入でリセットする案はこのケースを救わない。

## 3. 案 A (セッション設定) に、来場者境界を足す設計

```
Title 進入時           : PlaySession.Reset()      ← 来場者セッションの開始 (既定値から)
Select / Ready         : SetKeyGuide / SetLyricsEnabled (静的フィールドのみ、保存しない)
Game / Retry / Result  : 何もしない (同じ人の操作で設定が消えない)
Result → Select        : 何もしない (曲を選び直しても保持)
Result → Title         : 次に入ったときに Reset() が走る = 来場者交代
```

差分は小さく、既存の遷移に 1 か所フックを足すだけで表現できる:

- `TitleSceneManager.Start`: `PlaySession.Load()` を削除し、`PlaySession.Reset()` を呼ぶ。
- `PlaySession.Save()` / `Load()` / `Storage` と `DisplaySettingsStorage` を削除。
- `PlaySession.SetKeyGuide` / `SetLyricsEnabled` から `Save()` を外す。
- `PlaySessionPersistenceTests` 7 件を削除 (EditMode 1419 → 1412)。
- `ScorePersistenceTestSetup` の `PlaySession.Storage = ...` 差し替えを削除。

**この形なら「同じ人の Retry / 選曲変更では保持」と「Title に戻ったら初期化」が両立する。**
追加のタイマーも UI も要らない。

### 案 A の残る弱点

1. **Title を通らない交代** (Select / Result で離席) は初期化されない。
   救うには「アイドル N 秒で Title へ戻す」のような**新しい挙動**が要る (要件追加になるので、
   展示運用で必要かはオーナー判断。UI 変更を伴うため本件とは分ける)。
2. 既存インストールに残る `FlashBeat.KeyGuide` / `FlashBeat.Lyrics` は**読まれなくなる**
   (= 死んだキー)。実害は無いが、後で永続化を再導入すると古い値が復活しうる (下記 §4)。

## 4. 案 B (永続化を残す) との関係 — 「来場者境界」と両立させるには追加機構が要る

案 B のまま Title 進入で `Reset()` すると、**保存値を毎回捨てる**ので永続化の意味が
「アプリ再起動をまたぐ」だけになる (展示では再起動が稀なので実質無意味)。
案 B で来場者交代を扱うなら、次のどれかが必要:

- アイドル時間でリセット (新規挙動)、
- Option に「設定を初期化」を置く (UI 追加)、
- 来場者を区切らない (前の人の設定を引き継ぐ) と**仕様として明記**する。

いずれも **案 A より大きい変更** なので、優先候補は案 A のままにする。

## 5. `ClearSaveData()` の対象 (案に依らない整理)

現状 (`Assets/Game/Scripts/UI/V6/Option/V6OptionView.cs` の `ClearSaveData()`):

| 対象 | 消しているか |
|---|---|
| スコア (`ScorePersistence.ResetAll()`) | ○ |
| ハイスコア (`GManager.Hiscore`) | ○ |
| キー設定 (`KeyBindings.ResetToDefaults()`) | ○ |
| キーアクション (`KeyActions.ResetToDefaults()`) | ○ |
| **`FlashBeat.KeyGuide` / `FlashBeat.Lyrics` (#343 が追加)** | **×** |
| 表示設定の静的フィールド (`PlaySession` の値そのもの) | × (リセットしない) |

**案 A を採る場合**: この 2 キーはもう読まれない。消去対象に含める (残骸を残さない) か、
「表示設定は save data ではない」と明記して残すかのどちらかにする。推奨は
**消去対象に含める** (キーが死んだまま残ると、将来の再導入で古い値が復活する)。

**案 B を採る場合**: 消去対象に**必ず含める** (利用者が「データを消す」を押したのに
表示設定だけ残るのは不整合)。あわせて `PlaySession.Storage` のリセット経路も決める。

## 6. 実装判断を待つ (この文書では決めない)

| # | 決めること | 選択肢 |
|---|---|---|
| 1 | 寿命 | 案 A (セッションのみ) / 案 B (永続化) |
| 2 | 来場者境界 | Title 進入で初期化 (案 A と自然に両立) / アイドル復帰を新設 / 区切らない |
| 3 | `ClearSaveData` | `FlashBeat.KeyGuide` / `FlashBeat.Lyrics` を含める / 含めない |
| 4 | 死んだキー | 消す / 残す (将来の再導入時の移行規則をどうするか) |

**§3 の見積り (案 A + Title 境界)**: プロダクトコード 4 ファイル
(`PlaySession.cs` / `TitleSceneManager.cs` / `V6OptionView.cs` / `ScorePersistenceTestSetup.cs`)、
削除 2 ファイル (`DisplaySettingsStorage.cs` / `PlaySessionPersistenceTests.cs`)、
EditMode 1419 → 1412。実装・テスト・レビューは承認後。
