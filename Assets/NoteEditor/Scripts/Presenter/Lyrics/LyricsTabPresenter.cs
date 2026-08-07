using NoteEditor.Model;
using UnityEngine;

namespace NoteEditor.Presenter.Lyrics
{
    public class LyricsTabPresenter : MonoBehaviour
    {
        // 純粋ロジック: テスト可能
        public static void AddLyricAtTime(float startTime, float endTime,
            string mondai, string furigana, string romaji)
        {
            EditData.Lyrics.StartTime.Value = Append(EditData.Lyrics.StartTime.Value, startTime);
            EditData.Lyrics.EndTime.Value   = Append(EditData.Lyrics.EndTime.Value, endTime);
            EditData.Lyrics.Mondai.Value    = Append(EditData.Lyrics.Mondai.Value, mondai ?? "");
            EditData.Lyrics.Furigana.Value  = Append(EditData.Lyrics.Furigana.Value, furigana ?? "");
            EditData.Lyrics.Romaji.Value    = Append(EditData.Lyrics.Romaji.Value, romaji ?? "");
            EditData.IsDirty.Value = true;
        }

        public static void RemoveLyricAt(int index)
        {
            EditData.Lyrics.StartTime.Value = RemoveAt(EditData.Lyrics.StartTime.Value, index);
            EditData.Lyrics.EndTime.Value   = RemoveAt(EditData.Lyrics.EndTime.Value, index);
            EditData.Lyrics.Mondai.Value    = RemoveAt(EditData.Lyrics.Mondai.Value, index);
            EditData.Lyrics.Furigana.Value  = RemoveAt(EditData.Lyrics.Furigana.Value, index);
            EditData.Lyrics.Romaji.Value    = RemoveAt(EditData.Lyrics.Romaji.Value, index);
            EditData.IsDirty.Value = true;
        }

        public static void UpdateLyricAt(int index, float startTime, float endTime,
            string mondai, string furigana, string romaji)
        {
            EditData.Lyrics.StartTime.Value = ReplaceAt(EditData.Lyrics.StartTime.Value, index, startTime);
            EditData.Lyrics.EndTime.Value   = ReplaceAt(EditData.Lyrics.EndTime.Value, index, endTime);
            EditData.Lyrics.Mondai.Value    = ReplaceAt(EditData.Lyrics.Mondai.Value, index, mondai ?? "");
            EditData.Lyrics.Furigana.Value  = ReplaceAt(EditData.Lyrics.Furigana.Value, index, furigana ?? "");
            EditData.Lyrics.Romaji.Value    = ReplaceAt(EditData.Lyrics.Romaji.Value, index, romaji ?? "");
            EditData.IsDirty.Value = true;
        }

        static T[] Append<T>(T[] arr, T value)
        {
            var copy = new T[arr.Length + 1];
            System.Array.Copy(arr, copy, arr.Length);
            copy[arr.Length] = value;
            return copy;
        }

        static T[] RemoveAt<T>(T[] arr, int index)
        {
            var copy = new T[arr.Length - 1];
            System.Array.Copy(arr, 0, copy, 0, index);
            System.Array.Copy(arr, index + 1, copy, index, arr.Length - 1 - index);
            return copy;
        }

        static T[] ReplaceAt<T>(T[] arr, int index, T value)
        {
            var copy = (T[])arr.Clone();
            copy[index] = value;
            return copy;
        }
    }
}