using System.Globalization;
using FlashBeat.UI.V6.Select.Data;

namespace FlashBeat.UI.V6
{
    /// <summary>
    /// プロセス内コンテンツ キャッシュの無効化を 1 箇所で定義する (Issue #377)。
    ///
    /// 対象は 2 つ。すべて Resources の中身 (ビルドに焼き込まれた譜面 JSON)
    /// から導かれ、プロセスの実行中には変わらない。
    ///
    ///   - 譜面メタ (BPM / ノーツ数) と譜面の有無: <see cref="V6SelectDataSource"/>
    ///
    /// Issue #823: ジャケットはローカル Resources から撤去し、YouTube
    /// サムネイルの遠隔キャッシュ (LRU 上限付き) に一本化した。遠隔キャッシュは
    /// ネットワーク由来でプロセス中に増減するため、この無効化の対象外とする。
    ///
    /// したがって有効期間はプロセスの寿命で、更新はアプリの再起動で反映される。
    /// これは「曲データを変えたのに古い表示が残る」が起こり得ない構成
    /// (Resources は実行時に書き換えられない) だからで、時間での失効は置かない。
    /// 実行中に差し替わる経路 (カタログの再取得でローカルの対応が変わる等) を
    /// 足すときは、その経路から <see cref="InvalidateAll"/> を呼ぶ。
    ///
    /// メモリ上限の見積り: 譜面メタは曲あたり 2 つの数値なので無視できる。
    /// ジャケットの遠隔キャッシュは <see cref="V6JacketProvider.RemoteCacheCapacity"/>
    /// 件までに制限される (Issue #416)。
    /// </summary>
    public static class V6ContentCaches
    {
        /// <summary>譜面メタを実際に読んだ回数 (キャッシュ ヒットは含まない)。</summary>
        public static int ChartMetaLoadCount
        {
            get { return V6SelectDataSource.ChartMetaLoadCount; }
        }

        /// <summary>ジャケットの遠隔 HTTP を実際に開始した回数。</summary>
        public static int JacketLoadCount
        {
            get { return V6JacketProvider.RemoteFetchCount; }
        }

        /// <summary>ジャケット遠隔キャッシュの現在のエントリ数。</summary>
        public static int JacketCount
        {
            get { return V6JacketProvider.RemoteCacheEntryCount; }
        }

        /// <summary>
        /// 計測中にクラウドカタログの再取得を止める監査スイッチ。
        /// カタログが計測の合間に適用されると曲が増減し、キャッシュの
        /// 実読込回数の比較が揺れる (AutoTest 専用。通常時は false)。
        /// </summary>
        public static bool SuspendCatalogRefresh;

        /// <summary>すべてのキャッシュを捨てる。次の読み込みで実ファイルから引き直す。</summary>
        public static void InvalidateAll()
        {
            V6SelectDataSource.ClearContentCaches();
            // Issue #823: ジャケットは遠隔キャッシュのみ (LRU 上限付き)。
            // Resources 由来ではないのでここでは触らない。
        }

        /// <summary>
        /// キャッシュの件数と実読込回数を 1 行にする。件数が伸びないことを
        /// 「再読込していない」の証拠として記録するためのもの。
        /// </summary>
        public static string Describe()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "chartMeta={0}(loads={1}) jacket={2}(loads={3})",
                V6SelectDataSource.ChartMetaCacheCount,
                V6SelectDataSource.ChartMetaLoadCount,
                V6JacketProvider.RemoteCacheEntryCount,
                V6JacketProvider.RemoteFetchCount);
        }
    }
}
