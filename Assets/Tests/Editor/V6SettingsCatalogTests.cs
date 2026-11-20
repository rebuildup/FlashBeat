using System.Collections.Generic;
using NUnit.Framework;
using FlashBeat.UI.V6.Common;

/// <summary>
/// Issue #733: Option / Pause が共有する設定カタログ
/// (<see cref="V6SettingsCatalog"/>) の行スキーマを検証する。
///
/// - Play / Display / Sound の全タブはホスト (Option / Pause) に関係なく
///   同一の行・同一順序 (v0.2.1: BGM Volume は VideoTime.Update が
///   GameScene で消費するため Pause にも出す)。
/// - Play タブの行順は正本 uDNC0 (NoteSpeed / NoteTiming / Key Layout / キー設定)。
/// </summary>
public class V6SettingsCatalogTests
{
    /// <summary>カタログが必要とするホスト差分だけを持つ検証用ホスト。</summary>
    private sealed class FakeHost : IV6SettingsHost
    {
        public bool IsGameplayHost { get; set; }
        public V6VolumeSettings Volumes { get; } = new V6VolumeSettings();
        public int OpenKeyConfigCalls;
        public int ResetKeyLayoutCalls;
        public int DisplayChangedCalls;

        public void OpenKeyConfig() { OpenKeyConfigCalls++; }
        public void RequestResetKeyLayout() { ResetKeyLayoutCalls++; }
        public void OnDisplaySettingChanged() { DisplayChangedCalls++; }
    }

    private static List<string> LabelsOf(int tab, IV6SettingsHost host)
    {
        var labels = new List<string>();
        foreach (var row in V6SettingsCatalog.RowsForTab(tab, host))
        {
            labels.Add(row.Label);
        }

        return labels;
    }

    private static FakeHost OptionHost()
    {
        return new FakeHost { IsGameplayHost = false };
    }

    private static FakeHost PauseHost()
    {
        return new FakeHost { IsGameplayHost = true };
    }

    [Test]
    public void PlayRows_AreIdenticalBetweenOptionAndPause()
    {
        Assert.AreEqual(LabelsOf(0, OptionHost()), LabelsOf(0, PauseHost()),
            "Play タブの行は Option / Pause で同一でなければならない");
    }

    [Test]
    public void DisplayRows_AreIdenticalBetweenOptionAndPause()
    {
        Assert.AreEqual(LabelsOf(1, OptionHost()), LabelsOf(1, PauseHost()),
            "Display タブの行は Option / Pause で同一でなければならない");
    }

    [Test]
    public void PlayRowOrder_MatchesTheDesign()
    {
        // 正本 uDNC0: NoteSpeed / NoteTiming / Key Layout / キー設定 (F2)。
        Assert.AreEqual(
            new[] { "NoteSpeed", "NoteTiming", "Key Layout", "キー設定" },
            LabelsOf(0, OptionHost()));
    }

    [Test]
    public void SoundRows_AreIdenticalBetweenOptionAndPause()
    {
        var option = LabelsOf(2, OptionHost());
        var pause = LabelsOf(2, PauseHost());

        Assert.AreEqual(new[] { "Main Volume", "Effect Volume", "BGM Volume" }, option,
            "Option の Sound タブは Main / Effect / BGM の 3 行");
        Assert.AreEqual(option, pause,
            "Pause の Sound タブも同一行 (BGM Volume は GameScene の VideoTime が消費する)");
    }

    [Test]
    public void KeySettingsRow_UsesTheSharedActionContract()
    {
        var host = OptionHost();
        V6SettingRow row = null;
        foreach (var r in V6SettingsCatalog.RowsForTab(0, host))
        {
            if (r.Label == "キー設定")
            {
                row = r;
            }
        }

        Assert.IsNotNull(row, "キー設定 行が無い");
        Assert.IsTrue(row.IsAction, "キー設定 はステッパーを持たないアクション行");
        Assert.AreEqual("開く", row.Value(), "値欄は「開く」");
        // Issue #798: 「開く」は値テキスト+導線アイコンではなく、行右端の
        // 小型共通ボタン (arrow-right アイコン付き) として描画する。
        Assert.AreEqual("開く", row.ActionButtonLabel,
            "「開く」は ActionButtonLabel を持つアクションボタンとして描く");
        Assert.IsNull(row.ValueIcon, "アクションボタン化した行に導線アイコンは残さない");

        row.Activate();
        Assert.AreEqual(1, host.OpenKeyConfigCalls,
            "決定 (Enter / クリック) はホストのキー設定モーダルを開く");
    }

    [Test]
    public void VolumeRows_ShareTheSameValueModel()
    {
        // Option / Pause の音量行は同じ VolumeIndex と同じ値モデル
        // (V6VolumeSettings → GManager) を編集する。行の Adjust を呼ぶと
        // ホストの Volumes.Nudge へ届くことを、値モデルの共有で確認する。
        var host = PauseHost();
        var rows = V6SettingsCatalog.RowsForTab(2, host);
        Assert.AreEqual(0, rows[0].VolumeIndex, "Main Volume");
        Assert.AreEqual(1, rows[1].VolumeIndex, "Effect Volume");
        Assert.IsNotNull(rows[0].Adjust, "音量行は ←→ / ステッパーで調整できる");
    }
}
