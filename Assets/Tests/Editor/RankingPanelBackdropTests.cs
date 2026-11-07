using NUnit.Framework;
using UnityEngine;

/// <summary>
/// ランキング用オーバーレイのバックドロップ (全画面・黒い半透明) が
/// 「閉じているのに画面が暗いまま」にならないことの回帰テスト。
/// 実機報告: 更新後Ranking と キー設定 以外の画面がほぼ見えず、入力も受けつけられない。
/// 原因: バックドロップ (ルート) と パネル本体 (子) が別物で、
/// 非表示にしていたのは 子 だけで 全画面の暗転 が残っていた。
/// </summary>
public class RankingPanelBackdropTests
{
    private GameObject host;

    [TearDown]
    public void TearDown()
    {
        if (host != null)
        {
            Object.DestroyImmediate(host);
            host = null;
        }
    }

    private static bool ExistsActive(string objectName)
    {
        RectTransform[] all = Resources.FindObjectsOfTypeAll<RectTransform>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            if (all[i].gameObject.name != objectName) continue;
            if (all[i].gameObject.activeInHierarchy) return true;
        }
        return false;
    }

    [Test]
    public void EnsureBuilt_DoesNotLeaveAVisibleFullScreenBackdrop()
    {
        host = new GameObject("RankingPanelUnderTest");
        RankingPanel panel = host.AddComponent<RankingPanel>();
        panel.EnsureBuilt();

        Assert.IsFalse(panel.BoardVisible, "ボードは初期状態で非表示");
        Assert.IsFalse(panel.ModalVisible, "送信モーダルは初期状態で非表示");
        Assert.IsFalse(ExistsActive("RankingBoard"), "ボードの暗転バックドロップが見えていないこと");
        Assert.IsFalse(ExistsActive("RankingSubmitModal"), "モーダルの暗転バックドロップが見えていないこと");
    }

    [Test]
    public void HideBoard_HidesTheBackdropToo()
    {
        host = new GameObject("RankingPanelUnderTest");
        RankingPanel panel = host.AddComponent<RankingPanel>();
        panel.EnsureBuilt();

        panel.ShowBoard(null, 0, null);
        Assert.IsTrue(panel.BoardVisible, "ボードは開いた状態");

        panel.HideBoard();
        Assert.IsFalse(panel.BoardVisible, "ボードは閉じた状態");
        Assert.IsFalse(ExistsActive("RankingBoard"), "閉じたら暗転バックドロップも消えること");
    }
}
