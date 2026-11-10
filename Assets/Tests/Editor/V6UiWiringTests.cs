using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// v6 UI の「表示と実挙動の一致」を検証するソース走査テスト (#224 関連)。
///
/// - ポーズ設定ページに、名前の割当が無い惰性の行 (Key Layout) を残さない。
///   KeyConfigUI は Title / Option 画面限定 (AllowedScenes) でポーズからは開けず、
///   値のリセットだけなら行の名前と合わないため、行ごと廃止した。
/// - ポーズビューに、どのキーにも配線されていない値変更 API (AdjustSelected)
///   を残さない。←→ は行選択 (MoveSelection) に使う。
/// </summary>
public class V6UiWiringTests
{
    private static string ReadSource(string relativePath)
    {
        return File.ReadAllText(Path.Combine(Application.dataPath, relativePath));
    }

    [Test]
    public void PauseSettingsPage_HasNoInertKeyLayoutRow()
    {
        string src = ReadSource("Game/Scripts/UI/V6/Game/Pause/V6PauseView.cs");
        Assert.IsFalse(src.Contains("\"Key Layout\""),
            "ポーズ設定に割当の無い Key Layout 行が戻っています。開けない操作の行は描画しないこと。");
    }

    [Test]
    public void PauseSettingsPage_DoesNotOfferKeySettingsItCannotOpen()
    {
        // KeyConfigUI.Open() は Title / Option 画面でしか動かない (AllowedScenes)。
        // ポーズに「キー設定」行を置くと、押しても何も起きない行になる (issue #167)。
        string src = ReadSource("Game/Scripts/UI/V6/Game/Pause/V6PauseView.cs");
        Assert.IsFalse(src.Contains("KeyConfigUI.Open()"),
            "ポーズに開けないキー設定の入口が戻っています。編集は Option 画面の Key Layout 行から。");
    }

    [Test]
    public void PauseView_DoesNotKeepUnwiredValueChangeApi()
    {
        string src = ReadSource("Game/Scripts/UI/V6/Game/Pause/V6PauseView.cs");
        Assert.IsFalse(src.Contains("AdjustSelected"),
            "ポーズ ビューに未配線の値変更 API が戻っています。←→ は MoveSelection (行選択) の正本。");
    }

    // --- issue-162 §8 再照合 (2026-11-10) ---

    [Test]
    public void PauseView_DoesNotKeepTheDeadBgmVolumeRow()
    {
        // 動画音声は MainVolume に従い、BGMVolume の実行時消費者は GameScene に居ない
        // (VideoTime は配置されていない)。押しても効かない行は描画しない (B-11)。
        string src = ReadSource("Game/Scripts/UI/V6/Game/Pause/V6PauseView.cs");
        Assert.IsFalse(src.Contains("\"BGMVolume\""),
            "ポーズに効かない BGMVolume 行が戻っています。動画音声は MainVolume に従います。");
    }

    [Test]
    public void KeyConfigFooter_LegendMatchesTheRealKeys()
    {
        // 実挙動 (KeyConfigUI.HandleInput): ←→ / PgUp / PgDn = ページ切替、
        // Tab / ↑↓ = 行移動。以前は ←→「変更」・Tab「切替」と実挙動と逆に表示していた (C-8)。
        string src = ReadSource("Game/Scripts/UI/KeyConfigUI.cs");
        Assert.IsTrue(src.Contains("(\"←→\", \"切替\")"), "←→ はページ切替です (C-8)");
        Assert.IsTrue(src.Contains("(\"Tab\", \"選択\")"), "Tab は行移動です (C-8)");
        Assert.IsFalse(src.Contains("(\"Tab\", \"切替\")"), "Tab「切替」の誤表記が戻っています (C-8)");
    }

    [Test]
    public void OptionView_NoteTimingDescMentionsTheOffsetWriteBack()
    {
        // NoteTiming の保存は全曲の譜面設定 (offset) への書き戻しを伴う
        // (OptionSceneManager.AdjustNoteTiming)。説明に明記する (C-7)。
        string src = ReadSource("Game/Scripts/UI/V6/Option/V6OptionView.cs");
        Assert.IsTrue(src.Contains("offset) に書き込まれます"),
            "NoteTiming の説明に全曲 offset への書き戻しを明記すること (C-7)");
    }

    [Test]
    public void OptionView_KeyLayoutResetGoesThroughConfirmation()
    {
        // ← での全レーン既定へのリセットは確認ダイアログを経由する (C-6)。
        string src = ReadSource("Game/Scripts/UI/V6/Option/V6OptionView.cs");
        Assert.IsTrue(src.Contains("OpenResetKeyLayoutConfirm"),
            "Key Layout のリセットは確認モーダルを経由すること (C-6)");
    }
}
