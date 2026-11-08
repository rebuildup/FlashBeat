using FlashBeat.Update;
using UnityEngine;

namespace FlashBeat.UI.V6.Title
{
    /// <summary>
    /// V6TitleView の表示文 / 表示条件の純ロジック (Unity API に依存しない)。
    /// V6SelectViewLogic / V6ResultLogic と同じ分離。EditMode テストは
    /// Assets/Tests/Editor/V6TitleLogicTests.cs。
    /// </summary>
    public static class V6TitleLogic
    {
        /// <summary>design フレームの基準サイズ (QJtTv)。</summary>
        public const float FrameW = 1728f;
        public const float FrameH = 972f;

        /// <summary>
        /// design px (1728x972) を画面へ等比拡大する倍率 (v6 共通: min 倍率)。
        /// 異常値 (0 以下) は 1 倍にフォールバックする (V6ResultLogic.DesignScale と同じ)。
        /// </summary>
        public static float DesignScale(int screenW, int screenH)
        {
            if (screenW <= 0 || screenH <= 0)
            {
                return 1f;
            }

            return Mathf.Min(screenW / FrameW, screenH / FrameH);
        }

        /// <summary>
        /// プレイ可能な曲数 (選曲画面 V6SelectDataSource.RebuildSongs と同じ基準):
        /// index 0 (noSong プレースホルダ) と unplayable (譜面のない曲) は数えない。
        /// </summary>
        public static int PlayableSongCount(SongData[] songs)
        {
            if (songs == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 1; i < songs.Length; i++)
            {
                SongData song = songs[i];
                if (song == null || song.unplayable)
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        /// <summary>ヘッダー右上に出すカタログ状態の文言 (実データからのみ作る)。</summary>
        public static string CatalogStatusText(bool hasRemoteCatalog, int songCount)
        {
            string state = hasRemoteCatalog ? "クラウドカタログ 統合済み" : "ローカルカタログ";
            return "楽曲 " + songCount + " 曲 · " + state;
        }

        /// <summary>バージョン表記 ("1.2.3" → "v1.2.3")。空なら空文字。</summary>
        public static string VersionLabel(string version)
        {
            return string.IsNullOrEmpty(version) ? "" : "v" + version;
        }

        /// <summary>
        /// 「更新して再起動」ボタンを出すか。更新フローが動いているときだけ
        /// (design lyK9E の更新ボタンは常在ノードではなく、更新があるときの状態)。
        /// </summary>
        public static bool ShowUpdateButton(UpdateFlowPhase phase)
        {
            return phase == UpdateFlowPhase.Available || phase == UpdateFlowPhase.Downloading
                || phase == UpdateFlowPhase.Ready;
        }

        /// <summary>更新ボタンのラベル (Downloading 中は進捗を出す)。</summary>
        public static string UpdateButtonLabel(UpdateFlowPhase phase, float progress)
        {
            if (phase == UpdateFlowPhase.Downloading)
            {
                return "更新を準備中... " + Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f) + "%";
            }

            return "更新して再起動";
        }

        /// <summary>新版のバージョン (→ の右側) を出すか。取得済みのときだけ。</summary>
        public static bool ShowNewVersion(UpdateFlowPhase phase, string latestVersion)
        {
            return ShowUpdateButton(phase) && !string.IsNullOrEmpty(latestVersion);
        }
    }
}
