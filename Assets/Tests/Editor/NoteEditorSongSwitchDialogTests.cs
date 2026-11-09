using System;
using System.Collections.Generic;
using NUnit.Framework;
using NoteEditor.Presenter.FlashBeatSong;
using UnityEngine;

namespace FlashBeat.Tests.Editor
{
    // Issue #225 の独立レビュー S-3: ダイアログの重入で保留中の切替が
    // 黙って消え、ユーザーの出した切替先が変わる。
    //
    // 実物の SongSwitchPromptDialog (MonoBehaviour) を使って検証する。
    public class NoteEditorSongSwitchDialogTests
    {
        GameObject dialogGo;

        [SetUp]
        public void SetUp()
        {
            dialogGo = new GameObject("SongSwitchPromptDialog");
        }

        [TearDown]
        public void TearDown()
        {
            if (dialogGo != null) UnityEngine.Object.DestroyImmediate(dialogGo);
        }

        SongSwitchPromptDialog CreateDialog()
        {
            return dialogGo.AddComponent<SongSwitchPromptDialog>();
        }

        [Test]
        public void Show_WhileVisible_IsRejectedAndFirstChoiceIsKept()
        {
            var dialog = CreateDialog();
            var firstCalled = false;
            var secondCalled = false;

            Assert.IsTrue(dialog.Show("最初の要求", _ => firstCalled = true));
            Assert.IsFalse(dialog.Show("2 番目の要求", _ => secondCalled = true),
                "ダイアログ表示中の 2 番目の要求が受け付けられました");

            dialog.Choose(SongSwitchChoice.Cancel);

            Assert.IsTrue(firstCalled, "最初の要求のコールバックが失われました");
            Assert.IsFalse(secondCalled, "拒否された 2 番目の要求が実行されました");
        }

        [Test]
        public void Show_AfterChoose_AcceptsNewRequest()
        {
            var dialog = CreateDialog();
            dialog.Show("最初の要求", _ => { });
            dialog.Choose(SongSwitchChoice.Cancel);

            Assert.IsFalse(dialog.IsVisible);
            Assert.IsTrue(dialog.Show("次の要求", _ => { }), "閉じた後の要求が拒否されました");
        }

        [Test]
        public void ShowError_ShowsErrorMessageToUser()
        {
            var dialog = CreateDialog();

            dialog.ShowError("保存に失敗しました");

            Assert.IsTrue(dialog.IsVisible, "エラーが画面に出ていません");
            StringAssert.Contains("保存に失敗しました", dialog.ErrorMessage);
        }

        [Test]
        public void RequestSwitch_WhileDialogVisible_ReturnsBusyAndKeepsPendingSwitch()
        {
            var dialog = CreateDialog();
            var loaded = new List<string>();

            var coordinator = new SongSwitchCoordinator(
                () => true,
                () => "SongA",
                dialog,
                () => true,
                (string path, out string error) => { error = ""; loaded.Add(path); return true; });

            Assert.AreEqual(SongSwitchResult.AwaitingChoice, coordinator.RequestSwitch("/tmp/SongB.json"));

            // ダイアログを表示したまま 2 回目の要求を出す。
            Assert.AreEqual(SongSwitchResult.Busy, coordinator.RequestSwitch("/tmp/SongC.json"),
                "ダイアログ表示中の要求が Busy になりませんでした");

            // 保留中だった「曲 B への切替」は失われていない。
            dialog.Choose(SongSwitchChoice.DiscardAndSwitch);
            CollectionAssert.AreEqual(new[] { "/tmp/SongB.json" }, loaded);
        }
    }
}
