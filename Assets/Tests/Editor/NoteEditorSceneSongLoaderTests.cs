using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using NoteEditor.Model;
using NoteEditor.Notes;
using EditorNote = NoteEditor.Notes.Note;
using NoteEditor.Presenter;
using NoteEditor.Presenter.FlashBeatSong;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FlashBeat.Tests.Editor
{
    // Issue #225 の独立レビュー B-2:
    // FlashBeatSongLoader がシーン / プレハブのどこにも配置されておらず、
    // 旧 MusicSelectorPresenter / MusicLoader は no-op になっているため、
    // 「未保存変更があれば確認して切り替えられる」経路が製品に存在しなかった。
    //
    // ここでは NoteEditor シーンを実際に開いて
    // (1) ローダーがシーンに載っていること
    // (2) 一覧とプレハブが配線されていること
    // (3) 未保存変更のある状態から確認ダイアログへ到達できること
    // を検証する。
    public class NoteEditorSceneSongLoaderTests
    {
        const string ScenePath = "Assets/NoteEditor/Scenes/NoteEditor.unity";

        Scene previewScene;
        bool previewSceneOpened;

        [SetUp]
        public void SetUp()
        {
            SingletonTestHelper.EnsureSingletons();
            previewScene = EditorSceneManager.OpenPreviewScene(ScenePath);
            previewSceneOpened = previewScene.IsValid();
        }

        [TearDown]
        public void TearDown()
        {
            SingletonTestHelper.TeardownSingletons();
            if (previewSceneOpened)
                EditorSceneManager.ClosePreviewScene(previewScene);
        }

        static T GetPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"{target.GetType().Name} に {fieldName} がありません");
            return (T)field.GetValue(target);
        }

        static FlashBeatSongLoader FindLoader(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var loader = root.GetComponentInChildren<FlashBeatSongLoader>(true);
                if (loader != null)
                    return loader;
            }

            return null;
        }

        // B-2: ローダーが NoteEditor シーンに載っていること。
        [Test]
        public void NoteEditorScene_ContainsFlashBeatSongLoader()
        {
            var loader = FindLoader(previewScene);

            Assert.IsNotNull(loader,
                "NoteEditor シーンに FlashBeatSongLoader がありません (回復経路が到達不能)");
        }

        // B-2: ローダーが一覧コンテナと項目プレハブまで配線されていること。
        [Test]
        public void NoteEditorScene_LoaderIsWiredToFileList()
        {
            var loader = FindLoader(previewScene);
            Assert.IsNotNull(loader);

            var container = GetPrivateField<Transform>(loader, "fileItemContainer");
            var prefab = GetPrivateField<GameObject>(loader, "fileItemPrefab");

            Assert.IsNotNull(container, "一覧の表示先 (fileItemContainer) が未割り当てです");
            Assert.IsNotNull(prefab, "項目のプレハブ (fileItemPrefab) が未割り当てです");
            Assert.AreEqual("FileListItem", prefab.name, "想定と違うプレハブが設定されています");
        }

        // B-2: 確認ダイアログも同じ GameObject に載っていること。
        // (無ければ実行時に AddComponent されるが、シーンに明示的に
        // 載せておくことで「到達可能」がシーン上で確認できる)。
        [Test]
        public void NoteEditorScene_ContainsSongSwitchPromptDialog()
        {
            var loader = FindLoader(previewScene);
            Assert.IsNotNull(loader);

            var dialog = loader.GetComponent<SongSwitchPromptDialog>();
            Assert.IsNotNull(dialog,
                "NoteEditor シーンに曲切替の確認ダイアログがありません");
        }

        // B-2: 曲一覧が本番の Resources から実際に作られること。
        [Test]
        public void NoteEditorScene_RefreshList_BuildsSongListFromResources()
        {
            var loader = FindLoader(previewScene);
            Assert.IsNotNull(loader);

            var container = GetPrivateField<Transform>(loader, "fileItemContainer");
            var expected = FlashBeatSongLoader.EnumerateJsonFilesForTest(
                FlashBeatSongLoader.ResolveResourcesDirectory()).ToList();

            Assert.IsNotEmpty(expected,
                "本番の Resources ディレクトリに譜面 JSON が見つかりません "
                + "(ResolveResourcesDirectory が解決できていません)");

            loader.RefreshList();

            var items = container.Cast<Transform>().Select(t => t.gameObject).ToList();
            Assert.AreEqual(expected.Count, items.Count,
                "曲一覧の件数が譜面 JSON の件数と一致しません");

            // 各項目がプレハブから作られ、ボタンで曲切替に繋げられていること。
            foreach (var item in items)
            {
                Assert.IsNotNull(item.GetComponent<FileListItem>(), "項目に FileListItem がありません");
                var button = item.GetComponent<UnityEngine.UI.Button>();
                Assert.IsNotNull(button, "項目に Button がありません (切替に到達できません)");
            }
        }

        // B-2: 未保存変更のある状態から、確認ダイアログへ到達できること。
        // (ここが「回復経路」本体。保存 / 破棄 / キャンセル の 3 選択を提示する。)
        [Test]
        public void NoteEditorScene_RecoveryPath_ShowsPromptWhenSongHasUnsavedChanges()
        {
            var loader = FindLoader(previewScene);
            Assert.IsNotNull(loader);
            var dialog = loader.GetComponent<SongSwitchPromptDialog>();
            Assert.IsNotNull(dialog);

            // 本番と同じ経路で「未保存の変更がある」状態を作る (IsDirty を手代入しない)。
            var presenter = EditNotesPresenter.Instance;
            presenter.Init();
            presenter.RequestForAddNote.OnNext(new EditorNote(new NotePosition(4, 0, 0), NoteTypes.Single));
            Assert.IsTrue(EditData.IsDirty.Value, "dirty が上がらない (dirty 検知が壊れている)");

            var nameBefore = EditData.Name.Value;

            loader.OnFileSelected("/tmp/SomeOtherSong.json");

            Assert.IsTrue(dialog.IsVisible,
                "未保存変更があるのに確認ダイアログが出ていません (回復経路が到達不能)");
            Assert.IsTrue(dialog.HasPendingChoice,
                "確認ダイアログに選択肢がありません");

            // キャンセルしたら、いま編集中の曲もファイルも変わらない。
            dialog.Choose(SongSwitchChoice.Cancel);
            Assert.AreEqual(nameBefore, EditData.Name.Value, "キャンセルで曲が変わっています");
            Assert.IsFalse(dialog.IsVisible, "キャンセル後もダイアログが残っています");
        }
    }
}
