using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Video;

namespace FlashBeat.UI
{
    /// <summary>
    /// GameScene の状態を既存 UI の上に表示する HUD (Issue #30 §4)。
    ///
    /// - 「準備中」(動画/音声のロード中) と「失敗」を文字で見せる
    /// - 準備中は Space 待ちの案内 (SpacetoStart) を隠す: Space はまだ効かないので
    /// - 失敗・準備中は Esc キーと「選曲にもどる」ボタンで SelectScene へ戻れる
    ///
    /// MusicManager (Gameplay) は変更しない。状態の入力はすべて観測系:
    ///   - GManager.played                (再生開始済みか)
    ///   - load/waitback インジケータ     (MusicManager が準備中に active にする既存 UI)
    ///   - VideoPlayer.errorReceived      (再生失敗)
    /// errorReceived は準備中 (direct URL probe が失敗を試すことがある) には無視し、
    /// 準備完了後のエラーだけを「失敗」として扱う。準備中の失敗はタイムアウトで拾う。
    /// </summary>
    public class HudPlayStatus : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject backButton;
        [SerializeField] private GameObject startHint;
        [SerializeField] private GameObject loadIndicator;
        [SerializeField] private GameObject waitbackIndicator;

        private readonly HashSet<VideoPlayer> subscribedPlayers = new HashSet<VideoPlayer>();

        private float loadingSeconds;
        private float nextVideoScan;
        private bool videoError;
        private bool leaving;

        void OnEnable()
        {
            loadingSeconds = 0f;
            nextVideoScan = 0f;
            videoError = false;
            leaving = false;
            if (statusText != null) statusText.text = "";
            if (backButton != null) backButton.SetActive(false);
        }

        void OnDestroy()
        {
            UnsubscribeAll();
        }

        void Update()
        {
            ScanForVideoPlayers();

            bool mediaLoading = IsLoading();
            if (mediaLoading) loadingSeconds += Time.deltaTime;

            HudPlayPhase phase = HudPlayState.Evaluate(videoError, GManager.played, mediaLoading, loadingSeconds);

            if (statusText != null) statusText.text = HudPlayState.Message(phase);
            if (backButton != null) backButton.SetActive(HudPlayState.ShowBackEscape(phase));
            if (startHint != null) startHint.SetActive(phase == HudPlayPhase.Hidden && !GManager.played);

            if (HudPlayState.ShowBackEscape(phase) && KeyActions.IsDown(KeyActions.KeyAction.Back))
            {
                BackToSelect();
            }
        }

        /// <summary>「選曲にもどる」ボタンの onClick から呼ばれる。</summary>
        public void BackToSelect()
        {
            if (leaving) return;
            leaving = true;
            UnsubscribeAll();
            Initiate.Fade("SelectScene", Color.black, 1.0f);
        }

        bool IsLoading()
        {
            return (loadIndicator != null && loadIndicator.activeInHierarchy) ||
                   (waitbackIndicator != null && waitbackIndicator.activeInHierarchy);
        }

        /// <summary>
        /// シーンに存在する (実行中に生成される Cloud Catalog 用のものも含む)
        /// VideoPlayer を購読する。1 秒ごとに掃くだけで十分。
        /// </summary>
        void ScanForVideoPlayers()
        {
            if (Time.unscaledTime < nextVideoScan) return;
            nextVideoScan = Time.unscaledTime + 1f;

            foreach (VideoPlayer player in FindObjectsByType<VideoPlayer>(FindObjectsSortMode.None))
            {
                if (subscribedPlayers.Add(player))
                {
                    player.errorReceived += OnVideoError;
                }
            }
        }

        void OnVideoError(VideoPlayer source, string message)
        {
            if (IsLoading())
            {
                // 準備中のエラーは direct URL probe の試行なども含むので無視し、
                // タイムアウト (HudPlayState.FailureTimeoutSeconds) に判定を任せる。
                return;
            }
            Debug.LogWarning($"[HudPlayStatus] VideoPlayer error: {message}");
            videoError = true;
        }

        void UnsubscribeAll()
        {
            foreach (VideoPlayer player in subscribedPlayers)
            {
                if (player != null) player.errorReceived -= OnVideoError;
            }
            subscribedPlayers.Clear();
        }
    }
}
