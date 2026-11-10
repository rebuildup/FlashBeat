# #241 と #343 の仕様矛盾 — 表示設定 (Key Guide / Lyrics) の寿命

**状態: 未決定。どちらの案にも実装を寄せていない (2026-11-10 時点の main `fd876534` は #343 の実装のまま)。**

この文書は「実装済みだから仕様もこうなった」という既成事実化を避けるために、
① 2 つの記述のどこが矛盾しているか、② 影響範囲はどこまで及ぶか、
③ 2 案の具体的な差分は何か、を並べたものである。
**採否はオーナーが決める。cc は承認までコードを変更しない。**

---

## 1. 何が矛盾しているか

| 出所 | 記述 | 意味 |
|---|---|---|
| **Issue #241** (2026-11-08 スコープ訂正) | 「設定の寿命を保つ。OptionSettings で永続化する設定は保存・復元を確認し、**PlaySession の Key Guide / Lyrics は既存のセッション設定として扱う**。**全設定を再起動後まで保存するという一律の新要件は撤回する。**」 | この 2 つは **セッション設定** (= 再起動で消える) として扱う、と明示している |
| **PR #343** (issue-162 D-1 の修正) | `PlaySession.SetKeyGuide` / `SetLyricsEnabled` が **即時に `PlayerPrefs` へ保存** し、`TitleSceneManager.Start` で `PlaySession.Load()` が復元する | この 2 つを **永続設定** に変えた |

矛盾の本体は「保存するかどうか」そのものより、**#241 が明示的に撤回した
「一律の永続化」の一部を、別 Issue の欠陥修正として実装してしまった**ことにある。
#343 の動機 (Settings タブは即時保存なのに Display タブだけ消える非対称) は妥当だが、
その解消方法 (永続化) は #241 が決めた寿命と衝突する。**修正の動機 ≠ 仕様の承認。**

---

## 2. 影響範囲 (実測)

### 2.1 保存経路として追加されたもの (#343)

| 対象 | 内容 |
|---|---|
| `Assets/Game/Scripts/Persistence/DisplaySettingsStorage.cs` (新規) | `IDisplaySettingsStorage` / `PlayerPrefsDisplaySettingsStorage`。キーは `FlashBeat.KeyGuide` / `FlashBeat.Lyrics` |
| `PlaySession.Save()` / `Load()` / `Storage` (`Assets/Game/Scripts/UI/V5/PlaySession.cs`) | 設定時に保存、起動時に復元。`Load()` は `TitleSceneManager.cs:51` から呼ばれる |
| `Assets/Tests/Editor/PlaySessionPersistenceTests.cs` (新規 7 テスト) | 復元・既定値・範囲外値・`Reset` が消さないこと |
| `Assets/Tests/Editor/ScorePersistenceTestSetup.cs` | 全 EditMode 実行でメモリ実装に差し替え |
| `docs/ui/issue-162-ui-contract.md` §8.1 D-1 | `live` → **`fixed`** |
| `docs/audit/open-issues-audit.md` §7.6 | fixed 35 / moot 6 / **live 0** |

### 2.2 設定を読み書きする箇所 (案の違いで挙動が変わる面)

| 画面 | 書込 (setter) | 読み出し |
|---|---|---|
| v6 Option (Display タブ) | `V6OptionView.cs:1061,1067` | 同 |
| v6 Pause (Display タブ) | `V6PauseView.cs:432,437` | 同 |
| v6 Select (Ready の設定行) | `V6SelectView.cs:1005,1007` | 同 |
| v5 Select | `V5/SelectView.cs:366,374` | 同 |
| v5 Pause | `V5/PauseMenuView.cs:349,356` | 同 |
| ゲーム HUD / 描画 | — | `PlaySession.KeyGuide` / `LyricsEnabled` の読み出し **31 箇所** |

### 2.3 案の違いで **新たに** 変わる挙動

1. **アプリ再起動後の値**: 永続化案では前回の選択が残る。セッション案では
   `Compact` + 歌詞 `Auto` に戻る。
2. **Clear save data の範囲**: 現状 `V6OptionView.ClearSaveData()` は
   スコア (`ScorePersistence.ResetAll`) とキー設定 (`KeyBindings` / `KeyActions`) だけを
   消し、**`FlashBeat.KeyGuide` / `FlashBeat.Lyrics` は消さない**。
   永続化案を残すなら、この 2 キーを消去対象に含めるか、
   「表示設定は save data ではない」と明記するかのどちらかが必要
   (現状はどちらでもない = **案に依らず埋めるべき穴**)。
3. **展示機での持ち越し**: 来場者が Key Guide を `Off` にしたまま退席すると、
   次の来場者もその設定で始まる (永続化案)。展示運用では
   **前の人の選択が次の人に残る** ことを許容するかの判断が要る。

### 2.4 変えないもの (どちらの案でも同じ)

- リトライ / リスタートで設定が消えないこと (`PlaySession.Reset()` は保存値に触らない。
  セッション案でも `Reset()` の挙動は不変)。
- 音量 / NoteSpeed / NoteTiming の永続化 (`OptionSettings`)。
- `V6UiMode.Playfield`、本番データ、Windows 実機環境。

---

## 3. 2 案の差分

### 案 A — #241 に合わせて「セッション設定」に戻す (オーナー優先案)

```diff
- Assets/Game/Scripts/Persistence/DisplaySettingsStorage.cs   (削除。+ .meta)
- Assets/Tests/Editor/PlaySessionPersistenceTests.cs          (削除。+ .meta / 7 テスト)
  Assets/Game/Scripts/UI/V5/PlaySession.cs
-   public static void SetKeyGuide(KeyGuideMode mode) { keyGuide = mode; Save(); }
+   public static void SetKeyGuide(KeyGuideMode mode) { keyGuide = mode; }
-   public static void SetLyricsEnabled(bool enabled) { lyricsEnabled = enabled; Save(); }
+   public static void SetLyricsEnabled(bool enabled) { lyricsEnabled = enabled; }
-   public static void Save() / Load() / Storage  // 削除
  Assets/Game/Scripts/UI/TitleSceneManager.cs
-   FlashBeat.UI.V5.PlaySession.Load();            // 起動時復元を削除
  Assets/Tests/Editor/ScorePersistenceTestSetup.cs
-   PlaySession.Storage = new MemoryDisplaySettingsStorage();  // 差し替え不要になる
  docs/ui/issue-162-ui-contract.md  §8.1 D-1: fixed → 「#241 によりセッション設定が正 (撤回)」
  docs/audit/open-issues-audit.md   §7.6: D-1 を「仕様判断で撤回」へ
```

| 項目 | 案 A |
|---|---|
| 再起動後 | 既定値 (`Compact` / `Auto`) に戻る |
| EditMode 総数 | **1417 → 1410** (7 減) |
| 既存インストールに残る `FlashBeat.KeyGuide` / `FlashBeat.Lyrics` | **死んだキーとして残る**。次に永続化を入れるなら移行規則が要る (今回の案 A では読まないので実害なし) |
| #241 との整合 | **一致** (#241 の記述をそのまま満たす) |
| 失うもの | Display タブの値が再起動で消える (Settings タブとの非対称が戻る)。ただしこれは #241 が「既存のセッション設定」と書いた状態そのもの |
| 追加の判断 | 不要 (Issue 本文が既に正) |

### 案 B — 永続化を残し、#241 の記述を更新する

```diff
  (コードは現状のまま)
  Issue #241 本文: 「PlaySession の Key Guide / Lyrics は既存のセッション設定として扱う」
                 → 「この 2 つは **永続化する** (issue-162 D-1/#343)」へ改訂
+ Assets/Game/Scripts/UI/V6/Option/V6OptionView.cs
+   ClearSaveData(): FlashBeat.KeyGuide / FlashBeat.Lyrics も消去する (表示設定を含める場合)
  docs/ui/issue-162-ui-contract.md  §8.1 D-1: fixed のまま (根拠に #343 を追記)
  docs/audit/open-issues-audit.md   §7.6: fixed のまま
```

| 項目 | 案 B |
|---|---|
| 再起動後 | 前回の選択が残る |
| EditMode 総数 | 1417 のまま |
| #241 との整合 | **Issue 本文の改訂が必要** (「実装済みだから」ではなく、オーナー承認として記録する) |
| 追加の判断 | ① 表示設定を save data 消去に含めるか ② 展示機で前の来場者の設定が残ることを許容するか |
| 失うもの | 「Display だけ寿命が違う」という一貫性の説明が必要になる (Settings タブと揃う一方、#241 が撤回した一律永続化に近づく) |

### 比較

| 観点 | 案 A (セッション) | 案 B (永続化) |
|---|---|---|
| Issue 本文との整合 | 一致 | **本文改訂が必要** |
| 変更規模 | 小 (削除のみ、7 テスト減) | 小 (本文 + 消去範囲) |
| 利用者体験 | 再起動で既定に戻る | 選択が残る |
| 展示運用 | 来場者ごとに既定から始まる (安全) | 前の来場者の設定を引き継ぐ |
| 追加で決めること | なし | 2 件 (消去範囲 / 持ち越しの許容) |
| オーナーの優先指示 | **「既存のセッション設定を維持する案を優先」** | 差分提示のみ |

---

## 4. 結論と、承認までの扱い

- **推奨は案 A** (オーナーが「既存のセッション設定を維持する案を優先」と指示しているため)。
  ただし案 A は「Display タブの値が再起動で消える」という非対称を
  **仕様として受け入れる** ことになる。#343 が見つけた非対称自体は事実であり、
  案 A を選ぶなら「これは仕様である」と #241 に残すのが筋。
- **案に依らず埋めるべき穴**: `ClearSaveData()` が表示設定を消さない件 (§2.3-2)。
  どちらの案でも「消去対象の説明」と実装を一致させる必要がある。
  ただしこれも **#241 の受入と一緒に判断する** (先に片方だけ変えない)。
- **承認までの扱い**: main は #343 の実装のまま。この文書以外の変更をしない。
  #241 は受入未完了 (3 解像度 + 実機) のまま開いておく。
