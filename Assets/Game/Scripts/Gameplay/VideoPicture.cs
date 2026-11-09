using UnityEngine;
using UnityEngine.Video;
using YoutubePlayer.Components;

namespace FlashBeat.Gameplay
{
    /// <summary>
    /// 映像プレーン (曲ごとの VideoPlayer を持つオブジェクト) の制御 (Issue #233)。
    ///
    /// 事実の訂正 (PR #263 レビュー B2):
    ///   * シーンファイル (GameScene.unity) の映像プレーン ("1".."29") は
    ///     すべて <c>m_IsActive: 0</c> だが、これは **シーンが映像表示を
    ///     ランタイムに委ねている** ためで、「プリミティブが一度も有効化
    ///     されなかった」ためではない。
    ///   * 実行時に <c>MusicManager.Start</c> が
    ///     <c>YPlayer[GManager.songID].SetActive(true)</c> で **選択した平面を
    ///     有効化する**。したがって <see cref="Activate"/> 内の SetActive は
    ///     実行時には no-op である。
    ///   * 映像が 1 フレームも描画されなかった **本当の原因** は、シーンの
    ///     VideoPlayer に target renderer も render texture も設定されて
    ///     いなかったこと (実行時に <c>MusicManager.WireVideoPicture</c> が
    ///     配線する) である。
    ///
    /// 板 (プレーンの renderer) は **フレームの準備とは独立に** 隠す:
    ///   * 平面が **選択された時点** (MusicManager.Start) で隠す。prepare が
    ///     成功しない環境 (Issue #13) では prepare が終わるまで板が出ないので、
    ///     prepare 完了を待つ方式では白い板が残る。
    ///   * 有効化は <see cref="ShowWhenFrameReady"/> **だけ** が行う。
    ///
    /// シーンファイルは編集せず、実行時に制御する。ここにロジックを
    /// 分けてあるのは、EditMode でそのまま検証できるようにするため
    /// (VideoPictureTests)。
    /// </summary>
    public static class VideoPicture
    {
        /// <summary>
        /// 映像プレーンの板 (renderer) を隠す。
        ///
        /// prepare 完了を待たずに隠すため、prepare が失敗する環境でも
        /// 白い板 (28.7 x 15.84 の quad) が画面を覆わない。
        /// </summary>
        /// <returns>板の renderer があるか (隠せたか)。</returns>
        public static bool HideBoard(VideoPlayer video)
        {
            if (video == null) return false;

            var renderer = video.GetComponent<Renderer>();
            if (renderer == null) return false;

            if (renderer.enabled) renderer.enabled = false;
            return true;
        }

        /// <summary>
        /// 映像プレーンを有効化する。
        ///
        ///   * プレーンに乗っている InvidiousVideoPlayer は機能しない経路
        ///     (Invidious の公開インスタンス一覧は減衰しており、応答する
        ///     ホストは 0/13) なので無効化して外す。映像は yt-dlp が
        ///     取得した URL / ローカルファイルをそのまま使う。
        ///   * 板 (renderer) は隠したままにする。有効化は
        ///     <see cref="ShowWhenFrameReady"/> だけが行う。
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

            RemoveDeadInvidiousPlayer(plane);

            // 有効化した直後はまだフレームが無いので、板は隠したままにする。
            HideBoard(video);

            return true;
        }

        /// <summary>
        /// 死んだ Invidious 経路のプレイヤーを平面から外す。
        ///
        /// 実装はここに一本化し、MusicManager 側
        /// (<c>MusicManager.DisableInvidiousPlayer</c>) はここを呼ぶだけにして、
        /// 同じ処理を二重に行わないようにする (PR #263 レビュー S6)。
        /// </summary>
        /// <returns>外したか (Invidious 経路が無かったら false)。</returns>
        public static bool RemoveDeadInvidiousPlayer(GameObject plane)
        {
            if (plane == null) return false;

            var invidious = plane.GetComponent<InvidiousVideoPlayer>();
            if (invidious == null) return false;

            invidious.enabled = false;
            SafeDestroy(invidious);
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
