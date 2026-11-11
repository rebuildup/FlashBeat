using System.IO;
using FlashBeat.Gameplay;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// v6 UI の「表示と実挙動の一致」を検証するソース走査テスト (#224 関連)。
///
/// - ポーズ設定ページは正本 uDNC0 の Play タブ 4 行 (NoteSpeed / NoteTiming /
///   Key Layout / キー設定) を持ち、Key Layout と キー設定 は KeyConfigUI.Open() に
///   接続する (ポーズ中は PauseGate.IsPaused により GameScene でも開ける)。
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
        string src = ReadSource("Game/Scripts/UI/V6/Game/Pause/V6PauseView.cs");
        Assert.IsTrue(src.Contains("\"Key Layout\""), "正本の Key Layout 行が無い");
        Assert.IsTrue(src.Contains("\"キー設定\""), "正本の キー設定 (F2) 行が無い");
        Assert.IsTrue(src.Contains("AddRow(rows, pageRoot, 3, \"キー設定\""),
            "キー設定 は Play タブ 4 行目 (index 3) に置く");
    }

    [Test]
    public void PauseSettingsPage_KeySettingsOpenFromThePauseMenu()
    {
        // ポーズ行の「キー設定」は KeyConfigUI.Open() に接続する。
        // ポーズ中 (PauseGate.IsPaused) は判定・譜面が止まっているので GameScene でも開ける
        // (演奏中は音符のキーを奪うため開かない)。
        string src = ReadSource("Game/Scripts/UI/V6/Game/Pause/V6PauseView.cs");
        Assert.IsTrue(src.Contains("KeyConfigUI.Open()"), "キー設定行が KeyConfigUI.Open に接続されていない");
        Assert.IsTrue(src.Contains("AddRow(rows, pageRoot, 3, \"キー設定\", () => \"F2\", null, OpenKeyConfig)"),
            "キー設定 行の実行 (Enter) が OpenKeyConfig に接続されていない");

        PauseGate.HardReset();
        try
        {
            Assert.IsFalse(KeyConfigUI.IsAllowedNow("GameScene"), "演奏中はキー設定を開かない");
            PauseGate.Enter();
            Assert.IsTrue(KeyConfigUI.IsAllowedNow("GameScene"), "ポーズ中はキー設定を開ける");
        }
        finally
        {
            PauseGate.HardReset();
        }
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
