using System;
using UnityEngine;
using FlashBeat.UI.V6;

namespace FlashBeat.Gameplay
{
    /// <summary>
    /// Issue #824: ゲーム開始前 (ローディング / Space 待ち / 開始カウントダウン)
    /// の背景に、再生対象動画のサムネイルを静止画として表示する。
    ///
    /// 対象はシーンの <c>kakusi</c> (動画面を覆うカバー板)。MusicManager が
    /// 曲の開始時に有効化し、<c>StartPlayback</c> で無効化するので、
    /// この板にテクスチャを貼れば「静止画 → 動画フレーム」の切り替えは
    /// 既存の板の ON/OFF と同じタイミングで起き、黒画面のフラッシュも
    /// フェードも発生しない。VideoPlayer の準備状態・再生時計には触れない。
    ///
    /// 取得は <see cref="V6JacketProvider.RequestBackdropTexture"/> に委ねる
    /// (dedupe / 負キャッシュ / LRU を共有)。リポジトリに画像アセットは
    /// 追加しない — YouTube のサムネイル CDN から実行時に取る。
    ///
    /// サムネイルが取れない / 曲が変わった場合は既定の面 (Background.mat の
    /// 暗色) のままにし、開始を阻害しない。曲の切り替え・リトライでは
    /// <see cref="Begin"/> が世代番号を進めて古い非同期結果を捨て、
    /// 板の材質を既定へ戻すので、前の曲のサムネイルは残らない。
    /// </summary>
    public sealed class SongBackdrop
    {
        /// <summary>動画面 (16:9) に合わせた cover-crop の基準アスペクト。</summary>
        private const float ContentAspect = 16f / 9f;

        private const string ShaderName = "FlashBeat/FlatUnlit";
        private const string FallbackShaderName = "Unlit/Texture";
        private const string TextureProperty = "_MainTex";

        private readonly GameObject cover;
        private readonly Renderer renderer;
        private readonly Material originalMaterial;
        private Material backdropMaterial;
        private string videoId = "";
        private int generation;
        private bool disposed;

        /// <summary>
        /// <paramref name="coverObject"/> (kakusi) の現在の材質を既定として記録する。
        /// 材質は実行時インスタンスを別途作り、シーンの共有アセット
        /// (Background.mat) は書き換えない。
        /// </summary>
        public SongBackdrop(GameObject coverObject)
        {
            cover = coverObject;
            renderer = coverObject != null ? coverObject.GetComponent<Renderer>() : null;
            originalMaterial = renderer != null ? renderer.sharedMaterial : null;
        }

        /// <summary>
        /// 新しい曲の背景取得を始める。前の曲の結果は世代番号で捨て、
        /// 板はいったん既定の面へ戻す (サムネイル未到着の間に前の曲が
        /// 一瞬見えることを防ぐ)。
        /// </summary>
        /// <param name="newVideoId">再生対象の YouTube 動画 ID。空なら取得しない。</param>
        public void Begin(string newVideoId)
        {
            if (disposed)
            {
                return;
            }

            generation++;
            videoId = newVideoId ?? "";
            RestoreOriginal();

            if (renderer == null || string.IsNullOrEmpty(videoId))
            {
                return;
            }

            int gen = generation;
            string vid = videoId;
            V6JacketProvider.RequestBackdropTexture(vid, texture => OnTextureReady(texture, vid, gen));
        }

        /// <summary>シーンを離れるときに実行時材質を破棄する。</summary>
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            RestoreOriginal();
            if (backdropMaterial != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(backdropMaterial);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(backdropMaterial);
                }

                backdropMaterial = null;
            }
        }

        private void OnTextureReady(Texture2D texture, string vid, int gen)
        {
            // 遅れて届いた別の曲の結果・破棄後の結果は捨てる。
            if (disposed || gen != generation ||
                !string.Equals(vid, videoId, StringComparison.Ordinal))
            {
                return;
            }

            if (texture == null || renderer == null || cover == null || !cover.activeInHierarchy)
            {
                return;
            }

            EnsureMaterial();
            if (backdropMaterial == null)
            {
                return;
            }

            backdropMaterial.SetTexture(TextureProperty, texture);

            // 動画は 16:9。サムネイルのアスペクト比を保ったまま動画面全体を
            // 覆うよう、短辺を合わせはみ出た側を中央でクロップする
            // (V6BackdropFit.CoverUV — uGUI 側と同じ規約)。
            var uv = V6BackdropFit.CoverUV(texture.width, texture.height, ContentAspect, 1f);
            backdropMaterial.SetTextureScale(TextureProperty, uv.size);
            backdropMaterial.SetTextureOffset(TextureProperty, uv.position);

            renderer.sharedMaterial = backdropMaterial;
        }

        private void EnsureMaterial()
        {
            if (backdropMaterial != null)
            {
                return;
            }

            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                shader = Shader.Find(FallbackShaderName);
            }

            if (shader == null)
            {
                Debug.LogWarning("[SongBackdrop] no unlit shader found - thumbnail background stays off");
                return;
            }

            backdropMaterial = new Material(shader) { name = "SongBackdrop" };
            backdropMaterial.hideFlags = HideFlags.HideAndDontSave;
            if (backdropMaterial.HasProperty("_Color"))
            {
                backdropMaterial.SetColor("_Color", Color.white);
            }
        }

        /// <summary>板の材質をシーン既定 (Background.mat) へ戻す。</summary>
        private void RestoreOriginal()
        {
            if (renderer == null)
            {
                return;
            }

            if (backdropMaterial != null || renderer.sharedMaterial != originalMaterial)
            {
                renderer.sharedMaterial = originalMaterial;
            }
        }
    }
}
