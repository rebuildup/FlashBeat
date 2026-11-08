using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using NoteEditor.DTO;
using NoteEditor.Model;
using NoteEditor.Notes;
using UnityEngine;

namespace FlashBeat.Tests.Editor
{
    // Issue #225: 曲切替で前の曲のノーツが混入し、次の曲の Resources JSON へ
    // 上書き保存されるデータ破壊の回帰テスト。
    //
    // FlashBeatSongLoader.OnFileSelected は MonoBehaviour (シーン依存) なので、
    // ロード本体である EditDataSerializer.Deserialize と、保存本体である
    // EditDataSerializer.Serialize の実物を直接検証する。
    // Name / IsDirty の切替はローダーが行うので、テストでは同じ代入を模倣する。
    public class NoteEditorSongSwitchTests
    {
        string tempDir;

        [SetUp]
        public void SetUp()
        {
            SingletonTestHelper.EnsureSingletons();
            EditData.Notes.Clear();
            EditData.Name.Value = "";
            EditData.IsDirty.Value = false;
            tempDir = Path.Combine(Path.GetTempPath(), "flashbeat_songswitch_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            SingletonTestHelper.TeardownSingletons();
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }

        static string SongJson(string name, int bpm, int offset, params MusicDTO.Note[] notes)
        {
            var dto = new MusicDTO.EditData
            {
                name = name,
                maxBlock = 8,
                BPM = bpm,
                offset = offset,
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

        static MusicDTO.Note Tap(int num, int block, int lpb)
        {
            return new MusicDTO.Note { type = 1, num = num, block = block, LPB = lpb, notes = new List<MusicDTO.Note>() };
        }

        static MusicDTO.Note Long(params MusicDTO.Note[] chain)
        {
            // Serialize 側の ToDTO と同じ契約: 連結全メンバーが type=2。
            foreach (var member in chain) member.type = 2;
            var head = chain[0];
            head.notes = chain.Skip(1).ToList();
            return head;
        }

        // ローダーの成功パスが行う Name/IsDirty 切替の模倣。
        static void LoadLikeLoader(string json, string songName)
        {
            EditDataSerializer.Deserialize(json);
            EditData.Name.Value = songName;
            EditData.IsDirty.Value = false;
        }

        [Test]
        public void Deserialize_AfterPreviousSong_LeavesOnlyCurrentSongNotes()
        {
            var songA = SongJson("SongA", 120, 0, Tap(4, 0, 4), Tap(5, 0, 4));
            var songB = SongJson("SongB", 150, 480, Tap(8, 0, 4));

            LoadLikeLoader(songA, "SongA");
            LoadLikeLoader(songB, "SongB");

            Assert.AreEqual(1, EditData.Notes.Count, "前の曲 (A) のノーツが残っています");
            var only = EditData.Notes.Values.Single();
            Assert.AreEqual(8, only.note.position.num);
            Assert.AreEqual(150, EditData.BPM.Value, "BPM が旧曲のままです");
            Assert.AreEqual(480, EditData.OffsetSamples.Value, "offset が旧曲のままです");
            Assert.AreEqual("SongB", EditData.Name.Value);
        }

        [Test]
        public void Serialize_AfterSongSwitch_WritesOnlyCurrentSongNotes()
        {
            var songA = SongJson("SongA", 120, 0, Tap(4, 0, 4));
            var songB = SongJson("SongB", 150, 0, Tap(8, 0, 4));

            LoadLikeLoader(songA, "SongA");
            LoadLikeLoader(songB, "SongB");

            // SavePresenter.Save の本体 (Serialize → ファイル書き込み) を
            // 一時出力先へ向けて再現する。本番 Resources は書き換えない。
            var path = Path.Combine(tempDir, "SongB.json");
            File.WriteAllText(path, EditDataSerializer.Serialize(), Encoding.UTF8);

            var saved = JsonUtility.FromJson<MusicDTO.EditData>(File.ReadAllText(path, Encoding.UTF8));
            Assert.AreEqual("SongB", saved.name);
            Assert.AreEqual(1, saved.notes.Count, "B の保存 JSON に A のノーツが混入しています");
            Assert.AreEqual(8, saved.notes[0].num);
            CollectionAssert.DoesNotContain(saved.notes.Select(n => n.num).ToArray(), 4);
        }

        [Test]
        public void Deserialize_SameSongReload_DoesNotDuplicateNotes()
        {
            var songA = SongJson("SongA", 120, 0, Tap(4, 0, 4));

            LoadLikeLoader(songA, "SongA");
            LoadLikeLoader(songA, "SongA");

            Assert.AreEqual(1, EditData.Notes.Count, "同じ曲の再読込でノーツが重複しています");
        }

        [Test]
        public void Deserialize_LongNotesOfPreviousSong_AreDiscarded()
        {
            var songA = SongJson("SongA", 120, 0,
                Long(Tap(4, 0, 4), Tap(4, 2, 4)));
            var songB = SongJson("SongB", 150, 0, Tap(8, 0, 4));

            LoadLikeLoader(songA, "SongA");
            LoadLikeLoader(songB, "SongB");

            Assert.AreEqual(1, EditData.Notes.Count);
            Assert.IsFalse(EditData.Notes.Values.Any(n => n.note.type == NoteTypes.Long),
                "旧曲のロングノーツが残っています");
            Assert.AreEqual(NotePosition.None, EditState.LongNoteTailPosition.Value);
        }

        [Test]
        public void Deserialize_InvalidJson_PreservesPreviousSongState()
        {
            var songA = SongJson("SongA", 120, 0, Tap(4, 0, 4));
            LoadLikeLoader(songA, "SongA");

            Assert.Throws<System.ArgumentException>(() => EditDataSerializer.Deserialize("{\"notes\":"));

            Assert.AreEqual(1, EditData.Notes.Count, "読込失敗時に旧曲のノーツが破棄されました");
            Assert.AreEqual(120, EditData.BPM.Value);
            Assert.AreEqual("SongA", EditData.Name.Value);
        }
    }
}
