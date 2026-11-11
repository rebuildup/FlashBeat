using System.Collections;
using Debug = UnityEngine.Debug;
using Stopwatch = System.Diagnostics.Stopwatch;
using UnityEngine;
using UnityEngine.Video;

public class OpeningSceneManager : MonoBehaviour
{
    float Tcount = 0f;

    /// <summary>自動遷移までの待ち時間 (Update で参照)。Start で初期化され、
    /// 映像の準備後に正確な長さが分かれば更新される。</summary>
    float advanceSeconds = FlashBeat.UI.V5.OpeningView.BaseAdvanceSeconds;

    void Start()
    {
        var video = FindFirstObjectByType<VideoPlayer>();
        bool hasVideo = video != null && video.clip != null;

        if (hasVideo)
        {
            // オープニング映像があるシーンでは不透明な v5 背景を出さない
            // (ScreenSpaceOverlay の Canvas はカメラ描画より常に手前なので、
            //  背景があると映像を完全に隠してしまう)。ロゴとエッジ装飾のみ。
            var view = FlashBeat.UI.V5.OpeningView.Build(withBackdrop: false);

            if (!EnsureVideoRenders(video))
            {
                // どうしても映像を出せる状態にできないなら v5 の背景でフォールバック
                // (黒い画面のまま放置しない)。
                view.ShowBackdropFallback();
            }
            else
            {
                // 映像のデコード失敗時も背景でフォールバック。
                video.errorReceived += (_, msg) =>
                {
                    Debug.LogWarning("[OpeningSceneManager] video error: " + msg);
                    view.ShowBackdropFallback();
                };

                advanceSeconds = FlashBeat.UI.V5.OpeningView.ResolveAutoAdvanceDelay(
                    true, (float)video.clip.length);

                // (既存) PlayOnAwake の初回準備が最初のフレームを止めていた。
                // 動画を一度停止し、最初の描画の後に再再生することで、重い準備を
                // 1 フレーム目から外す。挙動 (動画が最初から流れる) は変わらない。
                if (video.isPlaying)
                {
                    video.Pause();
                }
                StartCoroutine(PlayAfterFirstRender(video));
            }
        }
        else
        {
            // 映像が無い場合は従来どおり v5 の背景 + 装飾。
            FlashBeat.UI.V5.OpeningView.Build();
        }

        Debug.Log("[Perf] opening scene start");
    }

    /// <summary>
    /// VideoPlayer の描画先が無効な場合に、確実に映る形へ直す (実行時のみ、
    /// シーン YAML は編集しない)。Opening.unity の VideoPlayer は
    /// MaterialOverride なのに Renderer 未指定のままなので、この状態では
    /// 映像がどこにも描画されない。カメラ描画 (CameraFarPlane) へ切り替え、
    /// 手前に来るスクリーン メッシュを隠す。
    /// </summary>
    /// <returns>映像が見える状態なら true。</returns>
    static bool EnsureVideoRenders(VideoPlayer video)
    {
        // design の背景は cover (画面全体を覆う)。16:9 の映像を 16:10 の
        // ディスプレイにそのまま出すと上下に帯が残るため、はみ出し側を切って
        // 全面に描く。FOV の変更はしないので歪みも出ない。
        video.aspectRatio = VideoAspectRatio.FitOutside;

        bool unusable =
            video.renderMode == VideoRenderMode.APIOnly ||
            (video.renderMode == VideoRenderMode.MaterialOverride &&
             video.targetMaterialRenderer == null) ||
            (video.renderMode == VideoRenderMode.CameraFarPlane &&
             video.targetCamera == null);

        if (!unusable)
        {
            return true; // RenderTexture 指定済み / MaterialOverride に Renderer 指定済みなど
        }

        var cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        if (cam == null)
        {
            return false;
        }

        video.renderMode = VideoRenderMode.CameraFarPlane;
        video.targetCamera = cam;

        // スクリーン メッシュ (無照明でほぼ黒) がフルスクリーン映像の手前に来るので隠す。
        var screen = video.GetComponentInChildren<MeshRenderer>();
        if (screen != null)
        {
            screen.enabled = false;
        }
        return true;
    }

    IEnumerator PlayAfterFirstRender(VideoPlayer video)
    {
        // LoadingOverlay は映像の上に重ねない (フルスクリーン オーバーレイが
        // 映像の始まりを覆ってしまうため、正常系では出さない)。
        // 1 フレーム待つ: 最初の描画が終わった後で動画の準備が走る。
        yield return null;
        var stopwatch = Stopwatch.StartNew();
        video.Play();
        while (video.isPlaying && video.frame < 0) yield return null;
        Debug.Log($"[Perf] opening video first frame {stopwatch.ElapsedMilliseconds}ms");

        // 準備後に正確な長さが取れるので、自動遷移の待ちを再計算 (上限内)。
        if (video.length > 0f)
        {
            advanceSeconds = FlashBeat.UI.V5.OpeningView.ResolveAutoAdvanceDelay(true, (float)video.length);
        }
    }

    void Update()
    {
        Tcount += Time.deltaTime;

        if (Tcount > advanceSeconds)
        {
            //SceneManager.LoadScene("TitleScene");
            Initiate.Fade("TitleScene", Color.black, 1.0f);
        }

    }
}
