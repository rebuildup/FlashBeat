using System.Collections;
using Debug = UnityEngine.Debug;
using Stopwatch = System.Diagnostics.Stopwatch;
using UnityEngine;
using UnityEngine.Video;

public class OpeningSceneManager : MonoBehaviour
{
    float Tcount = 0f;

    /// <summary>LoadingOverlay でのオーナーキー。</summary>
    internal const string OverlayOwner = "opening";

    void Start()
    {
        // v5 UI (Issue #120): 背景 + エッジ装飾 + ロゴをコードで構築。
        FlashBeat.UI.V5.OpeningView.Build();

        // オープニング動画 (opening1.mp4, PlayOnAwake) の初回準備が最初のフレームを
        // 止めていた。動画を一度停止し、最初の描画の後に再再生することで、
        // 重い準備を 1 フレーム目から外す。挙動 (動画が最初から流れる) は変わらない。
        var video = FindFirstObjectByType<VideoPlayer>();
        if (video != null && video.isPlaying)
        {
            video.Pause();
            StartCoroutine(PlayAfterFirstRender(video));
        }
        Debug.Log("[Perf] opening scene start");
    }

    IEnumerator PlayAfterFirstRender(VideoPlayer video)
    {
        LoadingOverlay.Show("オープニング…", OverlayOwner);
        // 1 フレーム待つ: 最初の描画が終わった後で動画の準備が走る。
        yield return null;
        var stopwatch = Stopwatch.StartNew();
        video.Play();
        while (video.isPlaying && video.frame < 0) yield return null;
        Debug.Log($"[Perf] opening video first frame {stopwatch.ElapsedMilliseconds}ms");
        LoadingOverlay.Hide(OverlayOwner);
    }

    void Update()
    {
        Tcount += Time.deltaTime;

        if (Tcount > 6.031f)
        {
            //SceneManager.LoadScene("TitleScene");
            Initiate.Fade("TitleScene", Color.black, 1.0f);
        }

    }
}