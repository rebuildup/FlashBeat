using System;
using System.IO;
using System.Text;

namespace NoteEditor.Model
{
    // 譜面 JSON の書き出しを 1 箇所にまとめる。
    // SavePresenter (保存ボタン / 終了ダイアログ) と
    // 曲切替ダイアログ (保存して切り替え) が同じ手順で保存する。
    public static class ChartFileWriter
    {
        // 本番の保存先。Resources/<曲名>.json。
        public const string ResourcesDir = "Assets/Game/Resources";

        // 現在の譜面を directory 配下へ保存し、書き出し先のパスを返す。
        // 保存が成功したら dirty フラグを落とす (保存済み = 未保存変更なし)。
        //
        // Issue #225: 曲切替の直後に保存すると「いま編集中の曲」ではない
        // 別の曲の JSON を上書きしてしまうことがあるため、songName が
        // いま編集中の曲名と一致しない場合は保存を中止する。
        public static string Save(string songName, string directory)
        {
            var name = Path.GetFileNameWithoutExtension(songName ?? "");
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("曲名が空のため保存できません", nameof(songName));

            if (string.IsNullOrEmpty(directory))
                throw new ArgumentException("保存先ディレクトリが空のため保存できません", nameof(directory));

            var current = CurrentSongName();
            if (!string.IsNullOrEmpty(current) && !string.Equals(current, name, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"編集中の曲 ({current}) と保存先 ({name}) が一致しません。"
                    + "別曲の JSON を上書きするため保存を中止しました。");

            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            var path = Path.Combine(directory, name + ".json");
            File.WriteAllText(path, EditDataSerializer.Serialize(), Encoding.UTF8);
            EditData.IsDirty.Value = false;
            return path;
        }

        // いま編集中の曲を directory 配下へ保存する。
        // 保存先は必ず「いま編集中の曲」の JSON になる。
        public static string SaveCurrentSong(string directory)
        {
            return Save(CurrentSongName(), directory);
        }

        // いま編集中の曲名 (拡張子なし)。未読込なら空文字。
        public static string CurrentSongName()
        {
            return Path.GetFileNameWithoutExtension(EditData.Name.Value ?? "");
        }
    }
}
