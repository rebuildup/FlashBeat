using System.Collections.Generic;
using UnityEngine;

namespace FlashBeat.UI.V6
{
    /// <summary>
    /// v6 のジャケット画像の唯一の解決経路。
    ///
    /// (1) Resources のパスに拡張子を含めない。`Resources.Load` は拡張子付きの
    ///     パスを受け付けず、null を返す (Result / Loading がこれで空枠になっていた)。
    /// (2) 対応表を 1 箇所に置く。以前は Select / Result / Loading が同じ表を
    ///     それぞれ写経していて、拡張子の扱いだけが食い違っていた。
    ///
    /// ジャケットが無い曲 (灰Φ倶楽部: 22) は空文字を返し、固定画像を流用しない。
    /// </summary>
    public static class V6JacketResources
    {
        private const string V6Dir = "V6/select/jacket/";
        private const string V5Dir = "V5/select/jacket/";

        /// <summary>カタログ index → v6 ジャケットのファイル名 (画像が無い index は空文字)。</summary>
        private static readonly Dictionary<int, string> V6Files = new Dictionary<int, string>
        {
            { 1, "01-rosen.jpg" }, { 2, "02-mani.jpg" }, { 3, "03-who.jpg" }, { 4, "04-tweet.jpg" },
            { 5, "05-shuumatsu.jpg" }, { 6, "06-euta.jpg" }, { 7, "07-chartreuse.jpg" }, { 8, "08-psyched.jpg" },
            { 9, "09-dogbite.jpg" }, { 10, "10-tutorial.jpg" }, { 11, "11-kyoukiranmai.jpg" },
            { 12, "12-poison.jpg" }, { 13, "13-selfie.jpg" }, { 14, "14-4thsmile.jpg" },
            { 15, "15-calamity.jpg" }, { 16, "16-panopticon.jpg" }, { 17, "17-guru.jpg" }, { 18, "18-abun.jpg" },
            { 19, "19-mushin.jpg" }, { 20, "20-haruarashi.jpg" }, { 21, "21-bibibibi.jpg" },
            // 22 = 灰Φ倶楽部。22-haihai.jpg の絵柄に曲名「灰Φ倶楽部」が入っており、
            // 1..26 の曲と 01..26 の画像が 1 対 1 で対応する (以前は「別の曲」として
            // 除外していたため、この曲だけサムネイルが空だった)。
            { 22, "22-haihai.jpg" },
            { 23, "23-override.jpg" }, { 24, "24-igaku.jpg" }, { 25, "25-tekkyu.jpg" }, { 26, "26-ikken.jpg" },
        };

        /// <summary>V6 に画像が無いときの V5 フォールバック (曲名 → ファイル名)。</summary>
        private static readonly Dictionary<string, string> V5Files = new Dictionary<string, string>
        {
            { "Poison", "Poison" },
            { "Calamity", "Calamity" },
            { "まにまに", "mani" },
            { "エウタナシア", "euta" },
            { "終焉逃避行", "syuen" },
            { "4th smile", "4th smile" },
        };

        private static readonly Dictionary<int, Sprite> SpriteCache = new Dictionary<int, Sprite>();

        /// <summary>
        /// カタログ index → Resources パス (拡張子なし)。無い曲は空文字。
        /// V5 フォールバックは曲名が必要なので <see cref="PathOf(int, string)"/> を使う。
        /// </summary>
        public static string PathOf(int songId)
        {
            string file;
            if (!V6Files.TryGetValue(songId, out file))
            {
                return "";
            }

            return V6Dir + StripExtension(file);
        }

        /// <summary>カタログ index (と曲名) → Resources パス (拡張子なし)。無い曲は空文字。</summary>
        public static string PathOf(int songId, string title)
        {
            string path = PathOf(songId);
            if (!string.IsNullOrEmpty(path))
            {
                return path;
            }

            if (string.IsNullOrEmpty(title))
            {
                return "";
            }

            string name;
            if (V5Files.TryGetValue(title, out name))
            {
                return V5Dir + name;
            }

            return "";
        }

        /// <summary>Resources.Load 用に拡張子を取り除く ("a/b.jpg" → "a/b")。</summary>
        public static string StripExtension(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return "";
            }

            int dot = fileName.LastIndexOf('.');
            if (dot <= 0)
            {
                return fileName;
            }

            return fileName.Substring(0, dot);
        }

        public static Texture2D LoadTexture(int songId, string title)
        {
            string path = PathOf(songId, title);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            return Resources.Load<Texture2D>(path);
        }

        /// <summary>
        /// ジャケットの Sprite。同じ曲には同じ Sprite を返す (毎フレーム生成しない)。
        /// 無い曲は null。
        /// </summary>
        public static Sprite LoadSprite(int songId, string title)
        {
            Sprite cached;
            if (SpriteCache.TryGetValue(songId, out cached))
            {
                return cached;
            }

            Texture2D tex = LoadTexture(songId, title);
            if (tex == null)
            {
                return null;
            }

            var sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "V6Jacket-" + songId;
            SpriteCache[songId] = sprite;
            return sprite;
        }

        /// <summary>EditMode テスト用。シーンを跨いだキャッシュを捨てる。</summary>
        public static void ClearCache()
        {
            SpriteCache.Clear();
        }
    }
}
