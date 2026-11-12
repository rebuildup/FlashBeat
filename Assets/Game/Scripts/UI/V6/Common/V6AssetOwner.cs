using System.Collections.Generic;
using UnityEngine;

namespace FlashBeat.UI.V6
{
    /// <summary>
    /// 実行時に生成した native オブジェクト (Sprite / Texture2D / Material) の
    /// **所有者**を 1 箇所に集める (Issue #398 / #401 / #403)。
    ///
    /// 何が問題だったか:
    ///   Resources.Load が返す Texture2D は Unity が所有する (破棄してはいけない)。
    ///   一方 Sprite.Create / new Texture2D / new Material が作る実体は **誰も所有しない**。
    ///   GameObject を Destroy してもこれらは残るので、画面を作り直すたびに
    ///   native メモリが積み上がる。各 View が思い思いに Sprite.Create し、
    ///   破棄は「実装した View」と「忘れた View」が混在していた (#398 の一次監査)。
    ///
    /// 何をするか:
    ///   - プロセス全体で共有する派生アセット (角丸 9-slice、アイコン、ロゴ等) は
    ///     所有者を 1 つにし、キーで引く。同じ見た目のものを 2 度作らない。
    ///   - View 固有の派生アセット (ローディングの正方形 crop 等) は
    ///     スコープ (<see cref="V6AssetScope"/>) に預け、スコープの破棄でまとめて消す。
    ///
    /// 何をしないか:
    ///   - Resources.Load が返した Texture2D は登録しない (Unity 所有なので破棄禁止)。
    ///   - 画面ごとに見た目・切り抜き・優先順位が違うものは統合しない
    ///     (crop の違いは「同じ原Texture + variant」で表現する)。
    /// </summary>
    public static class V6AssetOwner
    {
        /// <summary>共有派生アセットの種類。デバッグと台帳の分類に使う。</summary>
        public enum Kind
        {
            /// <summary>角丸 9-slice (半径がキー)。</summary>
            RoundedSprite,

            /// <summary>アイコン (Resources の Sprite をそのまま使うので通常は生成しない)。</summary>
            Icon,

            /// <summary>ロゴ等の単発画像。</summary>
            Logo,

            /// <summary>原Texture からの切り抜き Sprite (キーに原Texture と crop を含む)。</summary>
            CroppedSprite,

            /// <summary>グラデーション等の実行時生成テクスチャ。</summary>
            GeneratedTexture,
        }

        // 共有アセットはプロセスの寿命。キーは「種類 + 生成条件」で作る。
        private static readonly Dictionary<string, Object> shared = new Dictionary<string, Object>();

        /// <summary>このプロセスで生成した共有アセットの数 (リーク監査用)。</summary>
        public static int SharedCount
        {
            get { return shared.Count; }
        }

        /// <summary>共有アセットを実際に生成した回数 (キャッシュ ヒットは含まない)。</summary>
        public static int SharedCreateCount { get; private set; }

        /// <summary>
        /// 共有アセットを取り出す。無ければ <paramref name="create"/> で作って登録する。
        ///
        /// <paramref name="create"/> が null を返した場合はキャッシュしない
        /// (「まだ無い」を固定しないため。素材の追加・差し替えで回復できる)。
        /// </summary>
        public static T Shared<T>(string key, System.Func<T> create) where T : Object
        {
            Object cached;
            if (shared.TryGetValue(key, out cached))
            {
                // Unity の偽 null (実体が外部で破棄された) は作り直す。
                if (cached != null)
                {
                    return cached as T;
                }

                shared.Remove(key);
            }

            if (create == null)
            {
                return null;
            }

            T created = create();
            if (created == null)
            {
                return null;
            }

            shared[key] = created;
            SharedCreateCount++;
            return created;
        }

        /// <summary>既に共有アセットがあるか (生成せずに確認する)。</summary>
        public static bool HasShared(string key)
        {
            Object cached;
            return shared.TryGetValue(key, out cached) && cached != null;
        }

        /// <summary>
        /// スコープを開く。スコープに預けたアセットは <see cref="V6AssetScope.Dispose"/> で
        /// まとめて破棄される。共有アセットはスコープに入れない (プロセス寿命)。
        /// </summary>
        public static V6AssetScope OpenScope(string owner)
        {
            return new V6AssetScope(owner);
        }

        /// <summary>
        /// 共有アセットをすべて破棄する。**アプリ終了時と EditMode テストのみ**。
        /// 実行中に呼ぶと、生きている Image のスプライトが消えて白くなる。
        /// </summary>
        public static void DestroyShared()
        {
            foreach (var pair in shared)
            {
                if (pair.Value != null)
                {
                    DestroyObject(pair.Value);
                }
            }

            shared.Clear();
            SharedCreateCount = 0;
        }

        /// <summary>テスト用: 生成回数のみ戻す (アセットは残す)。</summary>
        public static void ResetCountersForAudit()
        {
            SharedCreateCount = 0;
        }

        internal static void DestroyObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }

    /// <summary>
    /// 1 つの View / 画面が所有する派生アセットの束 (Issue #398)。
    ///
    /// View は Sprite.Create や new Texture2D をしたら <see cref="Track"/> に渡す。
    /// <see cref="Dispose"/> で「その View が作ったものだけ」を破棄する。
    /// 共有アセット (<see cref="V6AssetOwner.Shared{T}"/>) はここに入れない。
    /// </summary>
    public sealed class V6AssetScope : System.IDisposable
    {
        private readonly string owner;
        private readonly List<Object> owned = new List<Object>();

        internal V6AssetScope(string owner)
        {
            this.owner = string.IsNullOrEmpty(owner) ? "unnamed" : owner;
        }

        /// <summary>このスコープの名前 (ログ・台帳用)。</summary>
        public string Owner
        {
            get { return owner; }
        }

        /// <summary>このスコープが現在保持しているアセット数。</summary>
        public int Count
        {
            get { return owned.Count; }
        }

        /// <summary>破棄済みか。破棄後に生成したものを取りこぼさないための状態。</summary>
        public bool IsDisposed { get; private set; }

        /// <summary>このスコープが所有するアセットとして登録する。</summary>
        public T Track<T>(T asset) where T : Object
        {
            if (asset == null)
            {
                return null;
            }

            if (IsDisposed)
            {
                // 破棄後に作られたものは誰も消さないので、その場で消して
                // 「Dispose 済みのスコープに足した」バグを表面化させる。
                Debug.LogWarning("[V6AssetScope] " + owner + ": Dispose 後に生成されたアセットを破棄しました: " + asset.name);
                V6AssetOwner.DestroyObject(asset);
                return null;
            }

            owned.Add(asset);
            return asset;
        }

        /// <summary>
        /// 所有権を手放す (他のスコープやシーンへ渡すとき)。
        /// 破棄はしない。
        /// </summary>
        public void Release(Object asset)
        {
            if (asset == null)
            {
                return;
            }

            owned.Remove(asset);
        }

        /// <summary>このスコープが作ったアセットをすべて破棄する。</summary>
        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            for (int i = 0; i < owned.Count; i++)
            {
                V6AssetOwner.DestroyObject(owned[i]);
            }

            owned.Clear();
        }
    }
}
