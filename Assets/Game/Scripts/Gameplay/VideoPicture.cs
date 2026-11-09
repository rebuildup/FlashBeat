using UnityEngine;
using UnityEngine.Video;
using YoutubePlayer.Components;

namespace FlashBeat.Gameplay
{
    /// <summary>
    /// 映像プレーン (曲ごとの VideoPlayer を持つオブジェクト) の有効化
    /// (Issue #233)。
    ///
    /// GameScene.unity の映像プレーン ("1".."29") は **すべて**
    /// <c>m_IsActive: 0</c> で置かれている。MusicManager が有効化していたのは
    /// YoutubePlayer の親だけなので、映像プレーンは一度も有効化されず、
    /// 映像が 1 フレームも描画されなかった。
    ///
    /// シーンファイルは編集せず、実行時に有効化する。ここにロジックを
    /// 分けてあるのは、EditMode でそのまま検証できるようにするため
    /// (VideoPictureTests)。
    /// </summary>
    public static class VideoPicture
    {
        /// <summary>
        /// 映像プレーンを有効化する。
        ///
        ///   * プレーンに乗っている InvidiousVideoPlayer は機能しない経路
        ///     (Invidious の公開インスタンス一覧は減衰しており、応答する
        ///     ホストは 0/13) なので無効化して外す。映像は yt-dlp が
        ///     取得した URL / ローカルファイルをそのまま使う。
        ///   * 映像プレーンのマテリアル (VideoPicture.mat) の _MainTex は空
        ///     (= 白) なので、映像が 1 フレームも出ていない間はレンダラを
        ///     無効にしておく。白い板が画面を覆わないため。
        /// </summary>
        /// <returns>プレーンが有効になったか (null なら false)。</returns>
        public static bool Activate(VideoPlayer video)
        {
            if (video == null) return false;

            var plane = video.gameObject;

            // プレーンは非アクティブなままなので、この時点で Invidious の
            // Awake は走っていない。先に有効化してから外す (OnDestroy が
            // VideoPlayer から購読を解除できるようにするため)。
            if (!plane.activeSelf) plane.SetActive(true);

            var invidious = plane.GetComponent<InvidiousVideoPlayer>();
            if (invidious != null)
            {
                invidious.enabled = false;
                SafeDestroy(invidious);
            }

            var renderer = plane.GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = false;

            return true;
        }

        /// <summary>
        /// 映像のフレームが出て初めて、レンダラを有効にする (板が見える)。
        ///
        /// 映像が出ないままなら板は出さない (白い板で画面を覆わない)。
        /// </summary>
        /// <returns>板が今見えている状態か。</returns>
        public static bool ShowWhenFrameReady(VideoPlayer video, bool hasFrame)
        {
            if (video == null) return false;
            if (!hasFrame) return false;

            var renderer = video.GetComponent<Renderer>();
            if (renderer == null) return false;

            if (!renderer.enabled) renderer.enabled = true;
            return renderer.enabled;
        }

        /// <summary>映像の板が今画面に出ていているか。</summary>
        public static bool IsVisible(VideoPlayer video)
        {
            if (video == null) return false;
            var renderer = video.GetComponent<Renderer>();
            return renderer != null
                   && renderer.enabled
                   && renderer.gameObject.activeInHierarchy;
        }

        static void SafeDestroy(Object target)
        {
            if (target == null) return;
            // EditMode (テスト) では Destroy が使えない。
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }
    }
}
