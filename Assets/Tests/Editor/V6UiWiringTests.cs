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
}
