using System;
using System.Collections.Generic;
using UnityEngine;

namespace FlashBeat.UI.V6
{
    /// <summary>
    /// V6JacketProvider のローカル (Resources) 経路 (Issue #385 Phase 2.11 で分割)。
    /// songId → Resources パス解決 (<see cref="V6JacketResources.PathOf"/>)、原 Sprite の
    /// キャッシュ、解決済みパスの「無い」負キャッシュを持つ。
    /// partial class のためフィールド・メソッドは V6JacketProvider 本体と共有する。
    /// </summary>
    public static partial class V6JacketProvider
    {
        // --- ローカル (Resources) ---

        /// <summary>songId で引く Sprite (原画像の全面)。キーはローカル songId。</summary>
        private static readonly Dictionary<string, Sprite> localSpriteCache =
            new Dictionary<string, Sprite>(StringComparer.Ordinal);

        /// <summary>解決したパスごとに「Resources に無い」ことを覚える (毎回 Load しない)。</summary>
        private static readonly HashSet<string> localMissing = new HashSet<string>(StringComparer.Ordinal);

        // ------------------------------------------------------------------
        // ローカル (Resources) 経路
        // ------------------------------------------------------------------

        /// <summary>
        /// ローカルのジャケット Sprite (原画像の全面)。
        /// 無い曲は null。null も覚える (毎回 Resources.Load しない)。
        ///
        /// 戻り値は **provider が所有** する。呼び出し側は Destroy しない。
        /// </summary>
        public static Sprite Local(int songId, string title)
        {
            string resolved = V6JacketResources.PathOf(songId, title);
            if (string.IsNullOrEmpty(resolved))
            {
                // 曲に画像が無いことが確定している (毎回表を引かないよう覚える)。
                return null;
            }

            Sprite cached;
            if (localSpriteCache.TryGetValue(resolved, out cached))
            {
                // Unity の偽 null (実体が外部で破棄された) は作り直す。
                if (cached != null)
                {
                    return cached;
                }

                localSpriteCache.Remove(resolved);
            }

            if (localMissing.Contains(resolved))
            {
                return null;
            }

            Texture2D texture = Resources.Load<Texture2D>(resolved);
            if (texture == null)
            {
                localMissing.Add(resolved);
                return null;
            }

            LocalLoadCount++;
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "V6Jacket-" + resolved;
            localSpriteCache[resolved] = sprite;
            return sprite;
        }

        /// <summary>
        /// ローカルのジャケット Texture (原画像)。
        /// 無い曲は null。<see cref="V6LoadingView"/> のように Texture そのものが要る
        /// 呼び出し向け。**Unity が所有する** ので破棄しない。
        /// </summary>
        public static Texture2D LocalTexture(int songId, string title)
        {
            string resolved = V6JacketResources.PathOf(songId, title);
            return string.IsNullOrEmpty(resolved) ? null : Resources.Load<Texture2D>(resolved);
        }
    }
}
