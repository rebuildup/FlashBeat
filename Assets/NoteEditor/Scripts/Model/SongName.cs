using System;
using System.IO;

namespace NoteEditor.Model
{
    // 曲名をファイル名として使うときの正規化。
    //
    // Issue #225 の独立レビュー N-3: `Path.GetFileNameWithoutExtension` は
    // 拡張子をすべて落とすので、ドットを含む曲名 (例: "Song.2") が
    // "Song" に丸められていた。すると「いま編集中の曲」と「保存先」の
    // 照合が別の曲と誤判定し、別曲の JSON を上書きしうる。
    public static class SongName
    {
        // 曲名をファイル名用の名前へ正規化する。
        // ディレクトリ部分が，如果有れば除去し、拡張子 (.json) だけを取り除く。
        public static string Normalize(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "";

            var name = Path.GetFileName(raw.Trim());
            if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - ".json".Length);

            return name;
        }
    }
}
