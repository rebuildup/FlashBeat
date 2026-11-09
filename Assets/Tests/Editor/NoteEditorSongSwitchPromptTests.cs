using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using NoteEditor.DTO;
using NoteEditor.Model;
using NoteEditor.Notes;
using EditorNote = NoteEditor.Notes.Note;
using NoteEditor.Presenter;
using NoteEditor.Presenter.FlashBeatSong;
using UnityEngine;

namespace FlashBeat.Tests.Editor
{
    // Issue #225 の残り: 曲切替時の未保存変更の回復経路 (保存 / 破棄 / キャンセル) と、
    // 「いま編集中の曲」以外の JSON を上書きしないことの回帰テスト。
    //
    // FlashBeatSongLoader は MonoBehaviour (シーン依存) なので、
    // 曲切替の判断ロジック (SongSwitchCoordinator) と保存本体 (ChartFileWriter) を
    // 直接検証する。UI (SongSwitchPromptDialog) は実機操作でしか検証できないため、
    // ここではダイアログを ISongSwitchPrompt のフェイクに差し替える。
    //
    // 「未保存変更がある」状態は EditData.IsDirty を手代入せず、
    // 本番と同じ購読 (EditNotesPresenter → NoteEditDirtyTracker) で作る。
    public class NoteEditorSongSwitchPromptTests
    {
        string tempDir;
        GameObject audioGo;

        // ダイアログの代わりに使う、選択とエラーを記録するフェイク。
        class RecordingPrompt : ISongSwitchPrompt
        {
            public bool Shown;
            public string Message;
            public SongSwitchChoice? Choice;
            public string ErrorMessage;
            public int ShowCount;

            public bool Show(string message, Action<SongSwitchChoice> onChoice)
            {
                ShowCount++;
                Shown = true;
                Message = message;
                if (Choice.HasValue)
                    onChoice(Choice.Value);
                return true;
            }

            public void ShowError(string message)
            {
                ErrorMessage = message;
            }
        }

        [SetUp]
        public void SetUp()
        {
            SingletonTestHelper.EnsureSingletons();
            audioGo = new GameObject("NoteEditorSongSwitchPromptAudio");
            var source = audioGo.AddComponent<AudioSource>();
            typeof(Audio).GetField("source_", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(Audio.Instance, source);
            EditData.Notes.Clear();
            EditData.Name.Value = "";
            EditData.IsDirty.Value = false;
            tempDir = Path.Combine(Path.GetTempPath(), "flashbeat_songswitch_prompt_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            SingletonTestHelper.TeardownSingletons();
            if (audioGo != null) UnityEngine.Object.DestroyImmediate(audioGo);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }

        static MusicDTO.Note Tap(int num, int block, int lpb)
        {
            return new MusicDTO.Note { type = 1, num = num, block = block, LPB = lpb, notes = new List<MusicDTO.Note>() };
        }

        static string SongJson(string name, int bpm, params MusicDTO.Note[] notes)
        {
            var dto = new MusicDTO.EditData
            {
                name = name,
                maxBlock = 8,
                BPM = bpm,
                offset = 0,
                notes = notes.ToList(),
                lyrics = new MusicDTO.LyricsDTO
                {
                    startTime = new float[0],
                    furigana = new string[0],
                    mondai = new string[0],
                    romaji = new string[0],
                    endTime = new float[0],
                },
            };
            return JsonUtility.ToJson(dto);
        }

        // ローダーの成功パスが行う Name / IsDirty 切替の模倣。
        static void LoadLikeLoader(string json, string songName)
        {
            EditDataSerializer.Deserialize(json);
            EditData.Name.Value = songName;
            EditData.IsDirty.Value = false;
        }

        // 本番と同じ経路で「未保存の変更がある」状態を作る
        // (IsDirty を手代入しない)。
        static void MakeDirtyByEditingNote()
        {
            var presenter = EditNotesPresenter.Instance;
            presenter.Init();
            presenter.RequestForAddNote.OnNext(new EditorNote(new NotePosition(4, 0, 0), NoteTypes.Single));
            Assert.IsTrue(EditData.IsDirty.Value, "編集しても dirty が上がらない (dirty 検知が壊れている)");
        }

        string WriteSongFile(string songName, string json)
        {
            var path = Path.Combine(tempDir, songName + ".json");
            File.WriteAllText(path, json, Encoding.UTF8);
            return path;
        }

        [Test]
        public void SongSwitchDecision_NeedsConfirmation_OnlyWhenUnsavedChangesExist()
        {
            Assert.IsFalse(SongSwitchDecision.NeedsConfirmation(false));
            Assert.IsTrue(SongSwitchDecision.NeedsConfirmation(true));
        }

        [Test]
        public void SongSwitchDecision_Resolve_MapsEachChoiceToAction()
        {
            Assert.AreEqual(SongSwitchAction.SaveThenSwitch, SongSwitchDecision.Resolve(SongSwitchChoice.SaveAndSwitch));
            Assert.AreEqual(SongSwitchAction.SwitchWithoutSaving, SongSwitchDecision.Resolve(SongSwitchChoice.DiscardAndSwitch));
            Assert.AreEqual(SongSwitchAction.StayOnCurrentSong, SongSwitchDecision.Resolve(SongSwitchChoice.Cancel));
        }

        [Test]
        public void RequestSwitch_NoUnsavedChanges_LoadsWithoutPrompt()
        {
            var prompt = new RecordingPrompt();
            var loaded = new List<string>();
            var saved = 0;

            var coordinator = new SongSwitchCoordinator(
                () => false,
                () => "SongA",
                prompt,
                () => { saved++; return true; },
                (string path, out string error) => { error = ""; loaded.Add(path); return true; });

            var result = coordinator.RequestSwitch("/tmp/SongB.json");

            Assert.AreEqual(SongSwitchResult.Switched, result);
            Assert.IsFalse(prompt.Shown, "未保存変更がないのに確認ダイアログが出ています");
            Assert.AreEqual(0, saved);
            CollectionAssert.AreEqual(new[] { "/tmp/SongB.json" }, loaded);
        }

        [Test]
        public void RequestSwitch_EmptyPath_IsRejected()
        {
            var prompt = new RecordingPrompt();
            var loaded = new List<string>();

            var coordinator = new SongSwitchCoordinator(
                () => false,
                () => "SongA",
                prompt,
                () => true,
                (string path, out string error) => { error = ""; loaded.Add(path); return true; });

            Assert.AreEqual(SongSwitchResult.InvalidRequest, coordinator.RequestSwitch(""));
            Assert.AreEqual(SongSwitchResult.InvalidRequest, coordinator.RequestSwitch(null));
            Assert.AreEqual(0, loaded.Count, "不正な要求で曲が変わっています");
        }

        [Test]
        public void RequestSwitch_UnsavedChanges_ShowsPromptBeforeLoading()
        {
            var prompt = new RecordingPrompt();
            var loaded = new List<string>();

            var coordinator = new SongSwitchCoordinator(
                () => true,
                () => "SongA",
                prompt,
                () => true,
                (string path, out string error) => { error = ""; loaded.Add(path); return true; });

            var result = coordinator.RequestSwitch("/tmp/SongB.json");

            Assert.AreEqual(SongSwitchResult.AwaitingChoice, result);
            Assert.IsTrue(prompt.Shown, "未保存変更があるのに確認ダイアログが出ていません");
            Assert.AreEqual(0, loaded.Count, "確認前に新しい曲を読み込んでいます");
            StringAssert.Contains("SongA", prompt.Message);
            StringAssert.Contains("SongB", prompt.Message);
        }

        [Test]
        public void RequestSwitch_SaveAndSwitch_SavesCurrentSongThenLoads()
        {
            var prompt = new RecordingPrompt { Choice = SongSwitchChoice.SaveAndSwitch };
            var order = new List<string>();

            var coordinator = new SongSwitchCoordinator(
                () => true,
                () => "SongA",
                prompt,
                () => { order.Add("save"); return true; },
                (string path, out string error) => { error = ""; order.Add("load:" + Path.GetFileNameWithoutExtension(path)); return true; });

            coordinator.RequestSwitch("/tmp/SongB.json");

            CollectionAssert.AreEqual(new[] { "save", "load:SongB" }, order);
        }

        [Test]
        public void RequestSwitch_DiscardAndSwitch_LoadsWithoutSaving()
        {
            var prompt = new RecordingPrompt { Choice = SongSwitchChoice.DiscardAndSwitch };
            var order = new List<string>();

            var coordinator = new SongSwitchCoordinator(
                () => true,
                () => "SongA",
                prompt,
                () => { order.Add("save"); return true; },
                (string path, out string error) => { error = ""; order.Add("load"); return true; });

            coordinator.RequestSwitch("/tmp/SongB.json");

            CollectionAssert.AreEqual(new[] { "load" }, order);
        }

        [Test]
        public void RequestSwitch_Cancel_KeepsCurrentSongAndChanges()
        {
            var prompt = new RecordingPrompt { Choice = SongSwitchChoice.Cancel };
            var order = new List<string>();

            var coordinator = new SongSwitchCoordinator(
                () => true,
                () => "SongA",
                prompt,
                () => { order.Add("save"); return true; },
                (string path, out string error) => { error = ""; order.Add("load"); return true; });

            coordinator.RequestSwitch("/tmp/SongB.json");

            Assert.AreEqual(0, order.Count, "キャンセルで保存や読み込みが実行されました");
        }

        [Test]
        public void RequestSwitch_SaveFails_DoesNotSwitchSong()
        {
            var prompt = new RecordingPrompt { Choice = SongSwitchChoice.SaveAndSwitch };
            var loaded = new List<string>();

            var coordinator = new SongSwitchCoordinator(
                () => true,
                () => "SongA",
                prompt,
                () => false,
                (string path, out string error) => { error = ""; loaded.Add(path); return true; });

            coordinator.RequestSwitch("/tmp/SongB.json");

            Assert.AreEqual(0, loaded.Count, "保存に失敗したのに曲が変わっています");
        }

        // S-3: 保存失敗がユーザーに伝わらない。
        [Test]
        public void RequestSwitch_SaveFails_ReportsErrorToUser()
        {
            var prompt = new RecordingPrompt { Choice = SongSwitchChoice.SaveAndSwitch };

            var coordinator = new SongSwitchCoordinator(
                () => true,
                () => "SongA",
                prompt,
                () => false,
                (string path, out string error) => { error = ""; return true; });

            coordinator.RequestSwitch("/tmp/SongB.json");

            Assert.IsNotNull(prompt.ErrorMessage, "保存失敗が UI に出ていません");
            StringAssert.Contains("保存", prompt.ErrorMessage);
        }

        // S-3: 読み込み失敗がユーザーに伝わらない。
        [Test]
        public void RequestSwitch_LoadFails_ReportsErrorToUser()
        {
            var prompt = new RecordingPrompt();

            var coordinator = new SongSwitchCoordinator(
                () => false,
                () => "SongA",
                prompt,
                () => true,
                (string path, out string error) => { error = "壊れた JSON"; return false; });

            var result = coordinator.RequestSwitch("/tmp/SongB.json");

            Assert.AreEqual(SongSwitchResult.LoadFailed, result);
            Assert.IsNotNull(prompt.ErrorMessage, "読み込み失敗が UI に出ていません");
            StringAssert.Contains("壊れた JSON", prompt.ErrorMessage);
        }

        [Test]
        public void ChartFileWriter_Save_WritesToCurrentSongFileAndClearsDirty()
        {
            LoadLikeLoader(SongJson("SongA", 120, Tap(4, 0, 4)), "SongA");
            MakeDirtyByEditingNote();

            var path = ChartFileWriter.SaveCurrentSong(tempDir);

            Assert.AreEqual(Path.Combine(tempDir, "SongA.json"), path);
            Assert.IsFalse(EditData.IsDirty.Value, "保存後に dirty が残っています");

            var saved = JsonUtility.FromJson<MusicDTO.EditData>(File.ReadAllText(path, Encoding.UTF8));
            Assert.AreEqual("SongA", saved.name);
            Assert.AreEqual(1, saved.notes.Count);
        }

        [Test]
        public void ChartFileWriter_Save_OtherSongName_ThrowsAndDoesNotOverwriteOtherSong()
        {
            LoadLikeLoader(SongJson("SongA", 120, Tap(4, 0, 4)), "SongA");
            var otherPath = WriteSongFile("SongB", SongJson("SongB", 150, Tap(8, 0, 4)));
            var before = File.ReadAllText(otherPath, Encoding.UTF8);

            Assert.Throws<InvalidOperationException>(() => ChartFileWriter.Save("SongB", tempDir));

            Assert.AreEqual(before, File.ReadAllText(otherPath, Encoding.UTF8),
                "別曲の JSON が上書きされました");
        }

        // S-2: 本番経路 (SaveCurrentSong) は別曲へ書き込まない。
        [Test]
        public void ChartFileWriter_SaveCurrentSong_DoesNotTouchOtherSongFiles()
        {
            LoadLikeLoader(SongJson("SongA", 120, Tap(4, 0, 4)), "SongA");
            var otherPath = WriteSongFile("SongB", SongJson("SongB", 150, Tap(8, 0, 4)));
            var before = File.ReadAllText(otherPath, Encoding.UTF8);

            var path = ChartFileWriter.SaveCurrentSong(tempDir);

            Assert.AreEqual(Path.Combine(tempDir, "SongA.json"), path);
            Assert.AreEqual(before, File.ReadAllText(otherPath, Encoding.UTF8),
                "別曲の JSON が上書きされました");
        }

        [Test]
        public void ChartFileWriter_SaveCurrentSong_WritesOnlyNotesOfCurrentSong()
        {
            LoadLikeLoader(SongJson("SongA", 120, Tap(4, 0, 4)), "SongA");
            LoadLikeLoader(SongJson("SongB", 150, Tap(8, 0, 4)), "SongB");

            var path = ChartFileWriter.SaveCurrentSong(tempDir);

            Assert.AreEqual(Path.Combine(tempDir, "SongB.json"), path);
            var saved = JsonUtility.FromJson<MusicDTO.EditData>(File.ReadAllText(path, Encoding.UTF8));
            Assert.AreEqual("SongB", saved.name);
            Assert.AreEqual(1, saved.notes.Count, "前の曲 (A) のノーツが混入しています");
            Assert.AreEqual(8, saved.notes[0].num);
        }

        // 総合: 未保存変更のある曲 A から曲 B へ「保存して切り替え」した時、
        // A は A の内容で保存され、B の JSON は上書きされない。
        [Test]
        public void SwitchWithSaveAndSwitch_SavesOnlyCurrentSongAndDoesNotOverwriteTargetSong()
        {
            var songAJson = SongJson("SongA", 120, Tap(4, 0, 4));
            var songBJson = SongJson("SongB", 150, Tap(8, 0, 4));
            var songAPath = WriteSongFile("SongA", songAJson);
            var songBPath = WriteSongFile("SongB", songBJson);
            var songBBefore = File.ReadAllText(songBPath, Encoding.UTF8);

            LoadLikeLoader(songAJson, "SongA");
            MakeDirtyByEditingNote();

            var prompt = new RecordingPrompt { Choice = SongSwitchChoice.SaveAndSwitch };
            var coordinator = new SongSwitchCoordinator(
                () => EditData.IsDirty.Value,
                () => ChartFileWriter.CurrentSongName(),
                prompt,
                () =>
                {
                    ChartFileWriter.SaveCurrentSong(tempDir);
                    return true;
                },
                (string path, out string error) =>
                {
                    error = "";
                    LoadLikeLoader(File.ReadAllText(path, Encoding.UTF8),
                        Path.GetFileNameWithoutExtension(path));
                    return true;
                });

            coordinator.RequestSwitch(songBPath);

            // A は保存された (A のノーツのみ)。
            var savedA = JsonUtility.FromJson<MusicDTO.EditData>(File.ReadAllText(songAPath, Encoding.UTF8));
            Assert.AreEqual("SongA", savedA.name);
            Assert.AreEqual(1, savedA.notes.Count);
            Assert.AreEqual(4, savedA.notes[0].num);

            // B は上書きされていない。
            Assert.AreEqual(songBBefore, File.ReadAllText(songBPath, Encoding.UTF8),
                "曲 B の JSON が上書きされました");

            // 編集中の曲とそのノーツは B のみ。
            Assert.AreEqual("SongB", EditData.Name.Value);
            Assert.AreEqual(1, EditData.Notes.Count, "曲 A のノーツ在新曲に残っています");
            Assert.AreEqual(8, EditData.Notes.Values.Single().note.position.num);
        }

        // 「保存せずに切り替え」を選んだ場合、旧曲的文件は書き換えない。
        [Test]
        public void SwitchWithDiscardAndSwitch_DoesNotWriteAnySongFile()
        {
            var songAJson = SongJson("SongA", 120, Tap(4, 0, 4));
            var songBJson = SongJson("SongB", 150, Tap(8, 0, 4));
            var songAPath = WriteSongFile("SongA", songAJson);
            var songBPath = WriteSongFile("SongB", songBJson);
            var songABefore = File.ReadAllText(songAPath, Encoding.UTF8);
            var songBBefore = File.ReadAllText(songBPath, Encoding.UTF8);

            LoadLikeLoader(songAJson, "SongA");
            MakeDirtyByEditingNote();

            var prompt = new RecordingPrompt { Choice = SongSwitchChoice.DiscardAndSwitch };
            var coordinator = new SongSwitchCoordinator(
                () => EditData.IsDirty.Value,
                () => ChartFileWriter.CurrentSongName(),
                prompt,
                () =>
                {
                    ChartFileWriter.SaveCurrentSong(tempDir);
                    return true;
                },
                (string path, out string error) =>
                {
                    error = "";
                    LoadLikeLoader(File.ReadAllText(path, Encoding.UTF8),
                        Path.GetFileNameWithoutExtension(path));
                    return true;
                });

            coordinator.RequestSwitch(songBPath);

            Assert.AreEqual(songABefore, File.ReadAllText(songAPath, Encoding.UTF8), "曲 A が保存されました");
            Assert.AreEqual(songBBefore, File.ReadAllText(songBPath, Encoding.UTF8), "曲 B が上書きされました");
            Assert.AreEqual("SongB", EditData.Name.Value);
            Assert.AreEqual(1, EditData.Notes.Count);
        }
    }
}
