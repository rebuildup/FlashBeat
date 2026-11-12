using System.Globalization;
using FlashBeat.UI.V6.Select.Data;

namespace FlashBeat.UI.V6
{
    /// <summary>
    /// プロセス内コンテンツ キャッシュの無効化を 1 箇所で定義する (Issue #377)。
    ///
    /// 対象は 3 つ。すべて Resources の中身 (ビルドに焼き込まれた譜面 JSON と
    /// ジャケット画像) から導かれ、プロセスの実行中には変わらない。
    ///
    ///   - 譜面メタ (BPM / ノーツ数) と譜面の有無: <see cref="V6SelectDataSource"/>
    ///   - ジャケットの Sprite: <see cref="V6JacketResources"/>
    ///
    /// したがって有効期間はプロセスの寿命で、更新はアプリの再起動で反映される。
    /// これは「曲データを変えたのに古い表示が残る」が起こり得ない構成
    /// (Resources は実行時に書き換えられない) だからで、時間での失効は置かない。
    /// 実行中に差し替わる経路 (カタログの再取得でローカルの対応が変わる等) を
    /// 足すときは、その経路から <see cref="InvalidateAll"/> を呼ぶ。
    ///
    /// メモリ上限の見積り: ジャケット 1 枚は 320×180 px の非圧縮 RGBA で約 220 KB。
    /// キャッシュに載るのは対応表にある曲だけ (v6 26 + V5 フォールバック 6) なので
    /// 上限は約 7 MB。曲が増えても「曲数 × 220 KB」を超えない。譜面メタは曲あたり
    /// 2 つの数値なので無視できる。
    /// </summary>
    public static class V6ContentCaches
    {
        /// <summary>譜面メタを実際に読んだ回数 (キャッシュ ヒットは含まない)。</summary>
        public static int ChartMetaLoadCount
        {
            get { return V6SelectDataSource.ChartMetaLoadCount; }
        }

        /// <summary>譜面の有無を実際に確かめた回数。</summary>
        public static int ChartExistsLoadCount
        {
            get { return V6SelectDataSource.ChartExistsLoadCount; }
        }

        /// <summary>ジャケットを実際に Resources から読んだ回数。</summary>
        public static int JacketLoadCount
        {
            get { return V6JacketResources.TextureLoadCount; }
        }

        /// <summary>ジャケット キャッシュの現在の枚数。</summary>
        public static int JacketCount
        {
            get { return V6JacketResources.CachedSpriteCount; }
        }

        /// <summary>すべてのキャッシュを捨てる。次の読み込みで実ファイルから引き直す。</summary>
        public static void InvalidateAll()
        {
            V6SelectDataSource.ClearContentCaches();
            V6JacketResources.ClearCache();
        }

        /// <summary>
        /// キャッシュの件数と実読込回数を 1 行にする。件数が伸びないことを
        /// 「再読込していない」の証拠として記録するためのもの。
        /// </summary>
        public static string Describe()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "chartMeta={0}(loads={1}) chartExists={2}(loads={3}) jacket={4}(loads={5})",
                V6SelectDataSource.ChartMetaCacheCount,
                V6SelectDataSource.ChartMetaLoadCount,
                V6SelectDataSource.ChartExistsCacheCount,
                V6SelectDataSource.ChartExistsLoadCount,
                V6JacketResources.CachedSpriteCount,
                V6JacketResources.TextureLoadCount);
        }
    }
}
