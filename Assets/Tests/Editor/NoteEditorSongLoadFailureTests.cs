using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NoteEditor.DTO;
using NoteEditor.Model;
using NoteEditor.Notes;
using EditorNote = NoteEditor.Notes.Note;
using NoteEditor.Presenter.FlashBeatSong;
using UnityEngine;
using UnityEngine.TestTools;

namespace FlashBeat.Tests.Editor
{
    // Issue #225 の独立レビュー B-3 / S-1:
    //
    // B-3: ロード失敗時に旧曲 (いま編集中の曲) のデータが壊れ、
    //      次の保存で旧曲 JSON が空で上書きされうる。
    // S-1: 本番のロード経路 (FlashBeatSongLoader.LoadSongFile) のテストが 0 件。
    //
    // ここでは本番と同じ MonoBehaviour (FlashBeatSongLoader) を直接操作して
    // ロード経路を検証する。
    public class NoteEditorSongLoadFailureTests
    {
        string tempDir;
        GameObject audioGo;
        GameObject loaderGo;

        [SetUp]
        public void SetUp()
        {
            SingletonTestHelper.EnsureSingletons();
            audioGo = new GameObject("NoteEditorSongLoadFailureAudio");
            var source = audioGo.AddComponent<AudioSource>();
            typeof(Audio).GetField("source_", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(Audio.Instance, source);

            EditData.Notes.Clear();
            EditData.Name.Value = "";
            EditData.IsDirty.Value = false;

            tempDir = Path.Combine(Path.GetTempPath(), "flashbeat_loadfailure_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            loaderGo = new GameObject("FlashBeatSongLoader");
        }

        [TearDown]
        public void TearDown()
        {
            SingletonTestHelper.TeardownSingletons();
            if (loaderGo != null) UnityEngine.Object.DestroyImmediate(loaderGo);
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

        string WriteSongFile(string songName, string json)
        {
            var path = Path.Combine(tempDir, songName + ".json");
            File.WriteAllText(path, json, Encoding.UTF8);
            return path;
        }

        FlashBeatSongLoader CreateLoader()
        {
            return loaderGo.AddComponent<FlashBeatSongLoader>();
        }

        // S-1: 本番のロード経路は曲を読み込み、旧曲の再生位置・再生状態を
        // 持ち込まない。
        [Test]
        public void LoadSongFile_ProductionPath_LoadsSongAndResetsPlaybackState()
        {
            var loader = CreateLoader();
            var path = WriteSongFile("SongA", SongJson("SongA", 150, Tap(4, 0, 4)));

            // 旧曲の再生位置・再生状態を立てておく。
            Audio.TimeSamples.Value = 12345;
            Audio.SmoothedTimeSamples.Value = 0.5f;
            Audio.IsPlaying.Value = true;

            string error;
            Assert.IsTrue(loader.LoadSongFile(path, out error), "ロードに失敗しました: " + error);

            Assert.AreEqual("SongA", EditData.Name.Value);
            Assert.AreEqual(150, EditData.BPM.Value);
            Assert.AreEqual(1, EditData.Notes.Count);
            Assert.AreEqual(0, Audio.TimeSamples.Value, "旧曲の再生位置を持ち込んでいます");
            Assert.AreEqual(0f, Audio.SmoothedTimeSamples.Value, "旧曲の再生位置 (平滑) を持ち込んでいます");
            Assert.IsFalse(Audio.IsPlaying.Value, "旧曲の再生状態を持ち込んでいます");
            Assert.IsFalse(EditData.IsDirty.Value, "読み込み後に dirty が残っています");
        }

        // B-3: ロードに失敗しても、いま編集中の曲の状態は壊れない。
        [Test]
        public void LoadSongFile_ProductionPath_Failure_KeepsCurrentSongIntact()
        {
            var loader = CreateLoader();
            var songAPath = WriteSongFile("SongA", SongJson("SongA", 150, Tap(4, 0, 4)));
            var brokenPath = WriteSongFile("BrokenSong", "{\"notes\":");

            string error;
            Assert.IsTrue(loader.LoadSongFile(songAPath, out error), "曲 A のロードに失敗しました: " + error);
            Assert.AreEqual("SongA", EditData.Name.Value);
            Assert.AreEqual(1, EditData.Notes.Count);

            // 壊れた JSON のロードは失敗する。
            LogAssert.Expect(LogType.Error, new Regex("Failed to load .*BrokenSong\\.json"));
            Assert.IsFalse(loader.LoadSongFile(brokenPath, out error), "壊れた JSON が成功扱いになっています");
            Assert.IsNotEmpty(error, "失敗の理由が返っていません");

            // 旧曲の状態はそのまま。
            Assert.AreEqual("SongA", EditData.Name.Value, "曲名が変わっています (混在状態)");
            Assert.AreEqual(1, EditData.Notes.Count, "ノーツが失われています (混在状態)");
            Assert.AreEqual(150, EditData.BPM.Value, "BPM が壊れた曲に置き換わっています (混在状態)");
        }

        // B-3: ロード失敗後に保存しても、旧曲 JSON は壊れない。
        [Test]
        public void SaveAfterFailedLoad_WritesCurrentSongUnchanged()
        {
            var loader = CreateLoader();
            var songAPath = WriteSongFile("SongA", SongJson("SongA", 150, Tap(4, 0, 4)));
            var brokenPath = WriteSongFile("BrokenSong", "{\"notes\":");

            string error;
            Assert.IsTrue(loader.LoadSongFile(songAPath, out error));
            LogAssert.Expect(LogType.Error, new Regex("Failed to load .*BrokenSong\\.json"));
            Assert.IsFalse(loader.LoadSongFile(brokenPath, out error));

            // 失敗後も「いま編集中の曲」は曲 A。保存は曲 A へ行う。
            var savedPath = ChartFileWriter.SaveCurrentSong(tempDir);
            Assert.AreEqual(songAPath, savedPath);

            var saved = JsonUtility.FromJson<MusicDTO.EditData>(File.ReadAllText(savedPath, Encoding.UTF8));
            Assert.AreEqual("SongA", saved.name);
            Assert.AreEqual(1, saved.notes.Count, "保存した JSON のノーツが空です (旧曲が壊された)");
            Assert.AreEqual(4, saved.notes[0].num);
        }

        // B-3: 解析は成功するが準備フェーズで失敗した場合でも、
        // 旧曲の状態 (曲名 / ノーツ / BPM / 歌詞) は一切壊されない。
        [Test]
        public void Deserialize_PreparePhaseFailure_KeepsCurrentSongIntact()
        {
            EditDataSerializer.Deserialize(SongJson("SongA", 150, Tap(4, 0, 4)));
            EditData.Name.Value = "SongA";
            EditData.IsDirty.Value = false;

            var failingFactory = new Func<EditorNote, NoteObject>(note =>
            {
                throw new InvalidOperationException("注入した失敗");
            });

            Assert.Throws<InvalidOperationException>(() =>
                EditDataSerializer.Deserialize(SongJson("SongB", 200, Tap(8, 0, 4)), failingFactory));

            Assert.AreEqual("SongA", EditData.Name.Value, "曲名が変わっています");
            Assert.AreEqual(1, EditData.Notes.Count, "旧曲のノーツが失われています");
            Assert.AreEqual(150, EditData.BPM.Value, "BPM が新曲に置き換わっています");
            Assert.AreEqual(4, EditData.Notes.Values.First().note.position.num);
        }

        // B-3: 準備フェーズで失敗した後に保存しても、旧曲 JSON は壊れない。
        [Test]
        public void SaveAfterPreparePhaseFailure_WritesCurrentSongUnchanged()
        {
            EditDataSerializer.Deserialize(SongJson("SongA", 150, Tap(4, 0, 4)));
            EditData.Name.Value = "SongA";

            var failingFactory = new Func<EditorNote, NoteObject>(note =>
            {
                throw new InvalidOperationException("注入した失敗");
            });

            Assert.Throws<InvalidOperationException>(() =>
                EditDataSerializer.Deserialize(SongJson("SongB", 200, Tap(8, 0, 4)), failingFactory));

            var savedPath = ChartFileWriter.SaveCurrentSong(tempDir);

            Assert.AreEqual(Path.Combine(tempDir, "SongA.json"), savedPath);
            var saved = JsonUtility.FromJson<MusicDTO.EditData>(File.ReadAllText(savedPath, Encoding.UTF8));
            Assert.AreEqual("SongA", saved.name);
            Assert.AreEqual(1, saved.notes.Count, "保存した JSON のノーツが空です (旧曲が壊された)");
            Assert.AreEqual(4, saved.notes[0].num);
        }

        // 曲読み込み (Deserialize) は「読み込み後の状態は保存済み」として
        // dirty を残さない。読み込み中に dirty が誤検知されないこと。
        [Test]
        public void LoadingSong_DoesNotLeaveSongDirty()
        {
            var presenter = NoteEditor.Presenter.EditNotesPresenter.Instance;
            presenter.Init();
            presenter.RequestForAddNote.OnNext(new EditorNote(new NotePosition(4, 8, 0), NoteTypes.Single));
            Assert.IsTrue(EditData.IsDirty.Value);

            EditDataSerializer.Deserialize(SongJson("Loaded", 120));

            Assert.IsFalse(EditData.IsDirty.Value, "読み込み後に dirty が残っています");
        }
    }
}
