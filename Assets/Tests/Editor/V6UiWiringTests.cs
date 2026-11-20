using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// v6 UI の「表示と実挙動の一致」を検証するソース走査テスト (#224 関連)。
///
/// - ポーズ設定ページは正本 uDNC0 の Play タブ 4 行 (NoteSpeed / NoteTiming /
///   Key Layout / キー設定) を持ち、キー設定の 2 行は v6 モーダル
///   (design vo4kB、Issue #376) を開く。
/// - ←→ の値変更 (AdjustSelected) は V6GameRuntime に配線されていること。
///   未配線の API を残さない、が主旨 (行選択は ↑↓ = MoveSelection)。
/// </summary>
public class V6UiWiringTests
{
    private static string ReadSource(string relativePath)
    {
        return File.ReadAllText(Path.Combine(Application.dataPath, relativePath));
    }

    [Test]
    public void PauseSettingsPage_HasTheDesignPlayRows()
    {
        // 正本 uDNC0 の Play タブは 4 行: NoteSpeed / NoteTiming / Key Layout / キー設定 (F2)。
        // 旧テストは「開けないから行を描かない」を正としたが、行が無いと正本の情報が欠ける。
        // 行を描いたうえで操作に接続する (下の 2 テスト)。
        // Issue #733: 行定義は Option と共有の V6SettingsCatalog が単一ソース。
        // Pause はカタログから行を取り、固有の Data 行だけを自前で足す。
        string catalog = ReadSource("Game/Scripts/UI/V6/Common/V6SettingsCatalog.cs");
        Assert.IsTrue(catalog.Contains("\"NoteSpeed\""), "正本の NoteSpeed 行が無い");
        Assert.IsTrue(catalog.Contains("\"NoteTiming\""), "正本の NoteTiming 行が無い");
        Assert.IsTrue(catalog.Contains("\"Key Layout\""), "正本の Key Layout 行が無い");
        Assert.IsTrue(catalog.Contains("\"キー設定\""), "正本の キー設定 行が無い");
        // キー設定 は Play タブ 4 行目 (NoteSpeed → NoteTiming → Key Layout → キー設定)。
        int speed = catalog.IndexOf("\"NoteSpeed\"");
        int timing = catalog.IndexOf("\"NoteTiming\"");
        int layout = catalog.IndexOf("\"Key Layout\"");
        int keys = catalog.IndexOf("\"キー設定\"");
        Assert.IsTrue(speed >= 0 && speed < timing && timing < layout && layout < keys,
            "Play タブの行順が正本 (NoteSpeed / NoteTiming / Key Layout / キー設定) と違う");
        string src = ReadSource("Game/Scripts/UI/V6/Game/Pause/V6PauseView.cs");
        Assert.IsTrue(src.Contains("V6SettingsCatalog.RowsForTab"),
            "Pause が共有カタログから行を取っていない");
    }

    [Test]
    public void PauseSettingsPage_KeySettingsOpenFromThePauseMenu()
    {
        // ポーズ行の「キー設定」は v6 のモーダル (design vo4kB) を開く (Issue #376)。
        // Issue #733: 行はカタログの「キー設定」(isAction) で、決定はホストの
        // OpenKeyConfig → keyConfigModal.Show()。値欄は「開く」+ chevron アイコンで、
        // F2 は表示も監視もしない (決定 §4.1)。
        string catalog = ReadSource("Game/Scripts/UI/V6/Common/V6SettingsCatalog.cs");
        Assert.IsTrue(catalog.Contains("host.OpenKeyConfig"),
            "キー設定 行がホストの OpenKeyConfig に接続されていない");
        string src = ReadSource("Game/Scripts/UI/V6/Game/Pause/V6PauseView.cs");
        Assert.IsTrue(src.Contains("keyConfigModal.Show()"),
            "キー設定行が v6 モーダルに接続されていない");
        Assert.IsTrue(src.Contains("void OpenKeyConfig()"),
            "キー設定 を開く OpenKeyConfig が無い");
        Assert.IsFalse(src.Contains("KeyConfigUI.Open()"),
            "v6 ポーズから旧 KeyConfigUI を開かない (Issue #376)");
        Assert.IsFalse(src.Contains("\"F2\""),
            "F2 を画面文言として表示しない (Issue #376 受け入れ条件)");
    }

    [Test]
    public void PauseView_ValueChangeApiIsWiredToLeftRight()
    {
        // ←→ は選択中の行の値変更 (AdjustSelected)。凡例「変更」と実際の入力を一致させる。
        string pause = ReadSource("Game/Scripts/UI/V6/Game/Pause/V6PauseView.cs");
        string runtime = ReadSource("Game/Scripts/UI/V6/V6GameRuntime.cs");
        Assert.IsTrue(pause.Contains("public void AdjustSelected(int delta)"), "値変更 API が無い");
        Assert.IsTrue(runtime.Contains("pause.AdjustSelected(-1)") && runtime.Contains("pause.AdjustSelected(1)"),
            "←→ が AdjustSelected に配線されていない");
        Assert.IsTrue(runtime.Contains("pause.MoveSelection(-1)") && runtime.Contains("pause.MoveSelection(1)"),
            "↑↓ が MoveSelection に配線されていない");
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
        // Issue #733: 行定義は Option / Pause 共有の V6SettingsCatalog が持つ。
        string src = ReadSource("Game/Scripts/UI/V6/Common/V6SettingsCatalog.cs");
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

    // --- Issue #750: 色トークン / キー表示の正本一致 ---

    [Test]
    public void ConfirmDialogs_UseTheSharedDangerToken()
    {
        // 破壊的操作の確認ボタンは Option / Pause で同じ danger 色。
        // 旧 Pause はトークン外の #E53E3E で Option (#FF6B6B = V6Tokens.Danger) と
        // 色が違っていた (Issue #750)。
        string pause = ReadSource("Game/Scripts/UI/V6/Game/Pause/V6PauseViewDialogs.cs");
        string option = ReadSource("Game/Scripts/UI/V6/Option/V6OptionViewDialogs.cs");
        Assert.IsTrue(pause.Contains("V6Tokens.Danger"),
            "Pause の確認ボタンが V6Tokens.Danger を使っていない");
        Assert.IsFalse(pause.Contains("0xE5"),
            "Pause の確認ボタンにトークン外の色リテラルが残っている");
        Assert.IsTrue(option.Contains("V6Tokens.Danger"),
            "Option の確認ボタンが V6Tokens.Danger を使っていない");
    }

    [Test]
    public void HudMenuCap_FollowsTheKeyActionsBinding()
    {
        // HUD メニューボタンのキーキャップは固定 "Esc" ではなく、ポーズを開く
        // 実際の割り当て (KeyActions.KeyAction.Back) を映す (Issue #750)。
        string src = ReadSource("Game/Scripts/UI/V6/Game/Hud/V6HudChrome.cs");
        Assert.IsTrue(src.Contains("KeyActions.KeyAction.Back"),
            "メニューキャップが KeyAction.Back の実バインドを見ていない");
        Assert.IsFalse(src.Contains("\"Esc\""),
            "メニューキャップに固定 \"Esc\" が残っている");
    }
}
