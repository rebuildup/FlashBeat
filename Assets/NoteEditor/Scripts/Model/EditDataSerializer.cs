using NoteEditor.DTO;
using NoteEditor.Notes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NoteEditor.Model
{
    public class EditDataSerializer
    {
        public static string Serialize()
        {
            var dto = new MusicDTO.EditData();
            dto.BPM = EditData.BPM.Value;
            dto.maxBlock = EditData.MaxBlock.Value;
            dto.offset = EditData.OffsetSamples.Value;
            dto.name = SongName.Normalize(EditData.Name.Value);
            dto.videoId = EditData.VideoId.Value;
            dto.lyrics = new MusicDTO.LyricsDTO
            {
                startTime = EditData.Lyrics.StartTime.Value ?? new float[0],
                furigana  = EditData.Lyrics.Furigana.Value ?? new string[0],
                mondai    = EditData.Lyrics.Mondai.Value ?? new string[0],
                romaji    = EditData.Lyrics.Romaji.Value ?? new string[0],
                endTime   = EditData.Lyrics.EndTime.Value ?? new float[0],
            };

            var sortedNoteObjects = EditData.Notes.Values
                .Where(note => !(note.note.type == NoteTypes.Long && EditData.Notes.ContainsKey(note.note.prev)))
                .OrderBy(note => note.note.position.ToSamples(EditData.AudioFrequency(), EditData.BPM.Value));

            dto.notes = new List<MusicDTO.Note>();

            foreach (var noteObject in sortedNoteObjects)
            {
                if (noteObject.note.type == NoteTypes.Single)
                {
                    dto.notes.Add(ToDTO(noteObject));
                }
                else if (noteObject.note.type == NoteTypes.Long)
                {
                    var current = noteObject;
                    var note = ToDTO(noteObject);

                    while (EditData.Notes.ContainsKey(current.note.next))
                    {
                        var nextObj = EditData.Notes[current.note.next];
                        note.notes.Add(ToDTO(nextObj));
                        current = nextObj;
                    }

                    dto.notes.Add(note);
                }
            }

            return UnityEngine.JsonUtility.ToJson(dto);
        }

        public static void Deserialize(string json)
        {
            Deserialize(json, CreateNoteObject);
        }

        // NoteObject の生成を差し替えられる版。
        // テストから「準備フェーズで失敗を注入」し、
        // 「失敗しても旧曲の状態が残る」ことを検証できる (Issue #225 B-3)。
        internal static void Deserialize(string json, Func<Note, NoteObject> noteObjectFactory)
        {
            // 曲切替は置換トランザクションとして扱う (Issue #225)。
            //
            // 1) 解析: 壊れた JSON はここで例外になり、旧状態はそのまま残る。
            // 2) 準備: 新曲の NoteObject を一時領域に作る。ここまでで例外が
            //    出ても旧曲の状態 (曲名 / ノーツ / BPM / 歌詞) は一切変化しない。
            // 3) 適用: ここから先は例外を投げない処理だけを行い、原子的に差し替える。
            //
            // 旧状態 (旧曲) を先に壊して例外を投げると、
            // 「Name=旧曲 / Notes=空 / BPM=新曲」という混在状態になり、
            // 次の保存で旧曲 JSON が空で上書きされる (B-3)。
            var editData = UnityEngine.JsonUtility.FromJson<MusicDTO.EditData>(json);
            var incomingNotes = editData.notes ?? new List<MusicDTO.Note>();

            var prepared = PrepareChart(incomingNotes, noteObjectFactory);

            ApplyChart(editData, prepared);
        }

        // 読み込み前の状態を作る。旧状態はまだ触らない。
        class PreparedChart
        {
            public readonly Dictionary<NotePosition, NoteObject> Notes = new Dictionary<NotePosition, NoteObject>();
        }

        // 新曲の NoteObject を一時領域に作る。
        // ここで例外が出た場合、旧曲の状態は一切変化しない。
        static PreparedChart PrepareChart(List<MusicDTO.Note> incomingNotes, Func<Note, NoteObject> noteObjectFactory)
        {
            var prepared = new PreparedChart();

            // 準備中は EditState を触る (NoteObject.SetState) ため、旧値を控えて
            // 失敗時に戻す。旧曲のロングノーツ編集途中状態を持ち込まないため、
            // 準備の入口で None に戻しておく。
            var savedLongNoteTailPosition = EditState.LongNoteTailPosition.Value;
            EditState.LongNoteTailPosition.Value = NotePosition.None;

            try
            {
                foreach (var note in incomingNotes)
                {
                    if (note.type == 1)
                    {
                        AddPreparedNote(prepared, ToNoteObject(note), noteObjectFactory);
                        continue;
                    }

                    var members = new List<MusicDTO.Note> { note };
                    if (note.notes != null)
                        members.AddRange(note.notes);

                    var longNoteObjects = members
                        .Select(member => AddPreparedNote(prepared, ToNoteObject(member), noteObjectFactory))
                        .ToList();

                    for (int i = 1; i < longNoteObjects.Count; i++)
                    {
                        longNoteObjects[i].note.prev = longNoteObjects[i - 1].note.position;
                        longNoteObjects[i - 1].note.next = longNoteObjects[i].note.position;
                    }
                }
            }
            catch
            {
                // 失敗したら作った NoteObject を破棄して、EditState を元に戻し、
                // 旧曲 (旧状態) をそのまま残す。
                foreach (var noteObject in prepared.Notes.Values)
                    noteObject.Dispose();
                EditState.LongNoteTailPosition.Value = savedLongNoteTailPosition;
                throw;
            }

            EditState.LongNoteTailPosition.Value = NotePosition.None;
            return prepared;
        }

        // 準備済みの NoteObject を一時領域へ追加する。
        static NoteObject AddPreparedNote(PreparedChart prepared, Note note, Func<Note, NoteObject> noteObjectFactory)
        {
            var noteObject = noteObjectFactory(note);

            if (prepared.Notes.ContainsKey(note.position))
            {
                // 同一座標が複数ある chart: 既存ノーツの状態を後勝ちで更新する
                // (EditNotesPresenter.AddNote と同じ挙動)。
                var existing = prepared.Notes[note.position];
                noteObject.Dispose();
                existing.SetState(note);
                return existing;
            }

            prepared.Notes.Add(note.position, noteObject);
            return noteObject;
        }

        // 新しい状態を適用する。旧状態を破棄する以降は例外を投げない。
        static void ApplyChart(MusicDTO.EditData editData, PreparedChart prepared)
        {
            // 旧曲の NoteObject をすべて破棄してから辞書をクリアする。
            // NoteObject は MonoBehaviour ではないが、Init() で購読した
            // UniRx subscriptions (GL 線描画を含む) を持つため、Dispose せずに
            // 捨てると旧曲ノーツ上に購読が漏れる。GameObject は保持しないので
            // Dispose + Clear で破棄は完了する。
            foreach (var noteObject in EditData.Notes.Values)
                noteObject.Dispose();
            EditData.Notes.Clear();

            EditData.BPM.Value = editData.BPM;
            EditData.MaxBlock.Value = editData.maxBlock;
            EditData.OffsetSamples.Value = editData.offset;
            EditData.VideoId.Value = string.IsNullOrEmpty(editData.videoId) ? "" : editData.videoId;

            if (editData.lyrics != null)
            {
                EditData.Lyrics.StartTime.Value = editData.lyrics.startTime ?? new float[0];
                EditData.Lyrics.Furigana.Value  = editData.lyrics.furigana  ?? new string[0];
                EditData.Lyrics.Mondai.Value    = editData.lyrics.mondai    ?? new string[0];
                EditData.Lyrics.Romaji.Value    = editData.lyrics.romaji    ?? new string[0];
                EditData.Lyrics.EndTime.Value   = editData.lyrics.endTime   ?? new float[0];
            }
            else
            {
                EditData.Lyrics.StartTime.Value = new float[0];
                EditData.Lyrics.Furigana.Value  = new string[0];
                EditData.Lyrics.Mondai.Value    = new string[0];
                EditData.Lyrics.Romaji.Value    = new string[0];
                EditData.Lyrics.EndTime.Value   = new float[0];
            }

            foreach (var note in prepared.Notes)
                EditData.Notes.Add(note.Key, note.Value);

            // 旧曲のロングノーツ編集途中状態を持ち込まない。
            EditState.LongNoteTailPosition.Value = NotePosition.None;

            // 読み込み後の状態は「保存済み」とみなす (Issue #225)。
            EditData.IsDirty.Value = false;
        }

        static MusicDTO.Note ToDTO(NoteObject noteObject)
        {
            var note = new MusicDTO.Note();
            note.num = noteObject.note.position.num;
            note.block = noteObject.note.position.block;
            note.LPB = noteObject.note.position.LPB;
            note.type = noteObject.note.type == NoteTypes.Long ? 2 : 1;
            note.notes = new List<MusicDTO.Note>();
            return note;
        }

        public static Note ToNoteObject(MusicDTO.Note musicNote)
        {
            return new Note(
                new NotePosition(musicNote.LPB, musicNote.num, musicNote.block),
                musicNote.type == 1 ? NoteTypes.Single : NoteTypes.Long);
        }

        // NoteObject の生成 (既定)。
        static NoteObject CreateNoteObject(Note note)
        {
            var noteObject = new NoteObject();
            noteObject.SetState(note);
            noteObject.Init();
            return noteObject;
        }
    }
}
