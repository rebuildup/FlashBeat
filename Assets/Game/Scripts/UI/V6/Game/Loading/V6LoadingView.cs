using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.UI.V6.Game.Loading
{
    /// <summary>
    /// v6 曲ローディング画面 (Issue #224, design/flashbeat-ui.pen の `g4Q3F`)。
    ///
    /// - design: タイトル欄は空、右上にジャケット 56×56 + 曲名 fs22 / 作家 fs16、
    ///   フッターの凡例は Esc「戻る」のみ、補助 / 主操作は非表示、メインは空。
    /// - 進捗の表現は design に無い → この実装が決める: 中央に「準備中…」+ バー。
    ///   実進捗 (譜面準備, Issue #224) が渡されたら確定バー + 割合表示、
    ///   不明のときだけ不定バーへフォールバックする。
    /// - 失敗時はメッセージ + 「再試行」(RetryGame) + 「選曲にもどる」(実遷移)。
    /// - 状態は SetLoading / SetError で受け取る (統合担当が MusicManager /
    ///   HudPlayState の状態から呼ぶ)。キー入力は持たない (Esc は既存の
    ///   HudPlayStatus が「戻る」を担う)。
    /// - すべてコードから uGUI を生成。寸法は design px (1728x972)。
    ///   共通フレームを 1:1 (等倍) で画面中央に置く
    ///   (<see cref="V6LoadingViewLogic.FrameOrigin"/>、ポーズと同じ規約)。
    ///   等比拡大 (min(W/1728, H/972)) は行わない — design は各解像度ごとに
    ///   フレーム原点 ((w-1728)/2, (h-972)/2) を固定している。
    ///
    /// Issue #385 Phase 2.8: 領域ごとに partial class へ分割した
    /// (uGUI 構築 = V6LoadingViewBuild.cs、ローディング映像 = V6LoadingViewVideo.cs、
    /// 表示状態・操作 API = V6LoadingViewState.cs、1:1 配置・毎フレーム =
    /// V6LoadingViewPlacement.cs、毎フレーム駆動 = V6LoadingViewDriver.cs)。
    /// このファイルはフィールド・ライフサイクル (Create / Dispose / SetVisible)・
    /// 検証ハーネス用プロパティ (*ForAudit) を持つ。
    /// </summary>
    public sealed partial class V6LoadingView
    {
        /// <summary>現在生成されている v6 ローディング画面。統合側・検証から参照する。</summary>
        public static V6LoadingView Current { get; private set; }

        // --- design px 定数 (flashbeat-v6-nodes.json g4Q3F / rw7zx) ---
        private const float FrameW = 1728f;
        private const float FrameH = 972f;

        // ヘッダー帯の寸法は V6Header (Width/Height/FramePos) が正本。
        private const float FooterX = 24f, FooterY = 876f;

        private const float TrackW = 464f;
        private const float TrackH = 8f;
        private const float FillW = 160f;

        private GameObject rootGo;
        private VideoPlayer videoPlayer;
        private RawImage videoRaw;
        private Image bgImage;
        private bool videoErrorLogged;
        private bool videoErrored;
        private bool shown;
        private V6LoadingVideoSurface? lastSurface;
        private RectTransform frame;
        private V6LoadingViewDriver driver;
        private TextMeshProUGUI headline;
        private TextMeshProUGUI detail;
        private RectTransform fill;
        private GameObject progressGroup;
        private GameObject errorGroup;
        private TextMeshProUGUI songTitle;
        private TextMeshProUGUI songArtist;
        private Image jacketImage;
        private TextMeshProUGUI noJacketLabel;
        private float? progress;            // null = 不明 (不定インジケータ)
        private Texture2D jacketTexture;    // スプライトの生成元 (変化時だけ再生成)
        private Sprite jacketSprite;        // ApplySong で作ったスプライト (Dispose で破棄)
        private RenderTexture videoTexture; // RenderTexture モードの出力先 (Dispose で破棄)
        private bool errorShown;
        private bool leaving;
        private GameSceneManager sceneManager;

        private V6LoadingView()
        {
        }

        /// <summary>View を生成する (非表示のまま)。統合担当から呼ぶ。</summary>
        public static V6LoadingView Create(RectTransform parent)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            var view = new V6LoadingView();
            view.Build(parent);
            Current = view;
            return view;
        }

        public RectTransform Root
        {
            get { return frame; }
        }

        /// <summary>生成したビューを破棄する。</summary>
        public void Dispose()
        {
            // Issue #398: jacketSprite は V6JacketProvider が所有する (原 Texture +
            // crop 矩形で共有)。ここで Destroy すると、同じ画像を使っている他の画面が
            // 白くなる。手放すだけにする。
            jacketSprite = null;
            jacketTexture = null;

            if (videoTexture != null)
            {
                if (videoPlayer != null)
                {
                    videoPlayer.targetTexture = null;
                }

                if (videoTexture.IsCreated())
                {
                    videoTexture.Release();
                }

                UnityEngine.Object.Destroy(videoTexture);
                videoTexture = null;
            }

            if (rootGo != null)
            {
                V5GameUi.DestroyNow(rootGo);
            }

            if (ReferenceEquals(Current, this))
            {
                Current = null;
            }
        }

        /// <summary>検証ハーネス用: ローディング画面が表示状態か。</summary>
        internal bool IsShownForAudit
        {
            get { return shown; }
        }

        /// <summary>検証ハーネス用: ローディング映像が準備できたか。</summary>
        internal bool VideoPreparedForAudit
        {
            get { return videoPlayer != null && videoPlayer.isPrepared; }
        }

        /// <summary>検証ハーネス用: 映像に使えるクリップがあるか。</summary>
        internal bool VideoClipForAudit
        {
            get { return videoPlayer != null && videoPlayer.clip != null; }
        }

        /// <summary>検証ハーネス用: 映像面 (RawImage) が実際に表示されているか。</summary>
        internal bool VideoVisibleForAudit
        {
            get { return videoRaw != null && videoRaw.enabled; }
        }

        /// <summary>検証ハーネス用: 直近に選んだ映像面の種類。</summary>
        internal string VideoSurfaceForAudit
        {
            get { return lastSurface.HasValue ? lastSurface.Value.ToString() : "none"; }
        }

        public void SetVisible(bool visible)
        {
            bool wasShown = shown;
            shown = visible;
            if (rootGo != null)
            {
                rootGo.SetActive(visible);
            }

            if (visible)
            {
                UpdatePlacement();
                // 画面に出始めた瞬間だけ初期化する。SetVisible(true) は毎フレーム
                // 呼ばれるので、ここで Play を呼び直したり警告フラグを戻したり
                // すると、途中で届いた映像エラーが毎回鳴り直す (review Finding A)。
                if (!wasShown)
                {
                    videoErrorLogged = false;
                    videoErrored = false;
                    // 映像面を作り直す (テクスチャをreleasedした状態から戻す)。
                    lastSurface = null;
                    StartVideo();
                }
            }
            else
            {
                if (wasShown)
                {
                    StopVideo();
                }
            }

            ApplyVideoSurface();
        }

        // --- ユーティリティは V6Layout に集約 (Issue #385) ---
    }
}
