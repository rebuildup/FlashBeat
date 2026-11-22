using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class TextData
{
    public float[] StartTime;
    public string[] Furigana;
    public string[] Mondai;
    public string[] romaji;
    public float[] EndTime;

}

/// <summary>
/// GameScene の進行管理 (曲データの読み込み・ゲージ/フラッシュ表示・シーン遷移)。
///
/// Issue #740 Stage B: 旧 (v5) の歌詞パネル (TextPanel) とメニュー
/// (MenuPanel / MenuButton) はシーンに残るが、v6 HUD が歌詞とポーズを
/// 自前で描くため、開く経路も表示経路も持たない死に UI になった。
/// かつての Esc → OpenMenu 経路と歌詞パネルの表示制御は撤去した
/// (シーン側のオブジェクトは GamePlayPhaseService.HideLegacyHud が
/// 引き続き非表示に保つ)。歌詞データ自体は <see cref="InputJson"/> 経由で
/// v6 HUD (V6HudLyrics) が読む。
/// </summary>
public class GameSceneManager : MonoBehaviour
{
    [SerializeField] public Image gauge;

    public static List<float> SaveNotesTime = new List<float>();

    [SerializeField] public GameObject Flash;

    private string songName;
    private SongData sessionSong;

    // Issue #881: 歌詞が Pending に張り付いたとき、原因を Player.log から
    // 復元できるようにする診断。Pending が続く間だけ時間を数え、
    // 閾値を超えたら 1 回だけ状態を吐く (常時ログは出さない)。
    private float lyricPendingSeconds;
    private bool lyricPendingWarned;

    private TextData inputJson; // メンバ変数として TextData オブジェクトを宣言

    /// <summary>
    /// Issue #789: 歌詞データの解決結果。<see cref="Load"/> で
    /// <see cref="ChartLyrics.Resolve"/> の結果を保持する。
    /// テスト等で <see cref="InputJson"/> が直接差し込まれた場合は null のまま
    /// (その場合 HUD は行データの有無から状態を推測する従来経路を使う)。
    /// </summary>
    public ChartLyricsResult LyricsResult { get; private set; }

    // 他の関数で使用するためのプロパティ
    public TextData InputJson
    {
        get { return inputJson; }
    }


    private void Start()
    {
        // v6 HUD が唯一の UI。旧キーガイドパネルは作らない。
        FlashBeat.UI.V6.V6UiMode.HideLegacyKeyGuide();
        Flash.SetActive(true);
        gauge.fillAmount = 0;
        // Issue #657: GetSong はプレイセッション固定の曲を優先する。
        SongData song = PlaySessionSnapshot.ResolveSong(GManager.songID);
        SongData fallback = song ?? GManager.GetSong(GManager.songID);
        songName = fallback != null ? fallback.title : string.Empty;
        sessionSong = song;
        Load(songName, song);

    }
    private void Load(string SongName, SongData song)
    {
        // Issue #789: 歌詞は実際に使う譜面ソース (プレイセッションの固定 JSON /
        // CloudChartHolder / ローカル Resources) から ChartLyrics が解決する。
        // 以前はローカル Resources しか読まず、リモート曲では常に空を返して
        // いた (クラウド譜面の lyrics ブロックが HUD へ届かない根本原因)。
        LyricsResult = ChartLyrics.Resolve(song);
        this.inputJson = LyricsResult.Lyrics;
    }

    /// <summary>
    /// Issue #789: Pending (リモート譜面の到着待ち) の間だけ毎フレーム
    /// 再解決する。通常は Start 時点で譜面が確定しているが、万が一
    /// 到着が遅れた場合でも、ChartLyrics が Pending をキャッシュしない
    /// ためここで到着を拾って歌詞が出るようになる。
    /// </summary>
    void RefreshPendingLyrics()
    {
        if (LyricsResult == null || LyricsResult.State != ChartLyricsState.Pending)
        {
            lyricPendingSeconds = 0f;
            return;
        }
        LyricsResult = ChartLyrics.Resolve(sessionSong);
        inputJson = LyricsResult.Lyrics;

        if (LyricsResult.State == ChartLyricsState.Pending)
        {
            // 開始待ち (Space 待ち) の間の Pending は正常なので、
            // 実際に演奏が始まってからの滞留だけを数える。
            if (GManager.played)
            {
                lyricPendingSeconds += Time.unscaledDeltaTime;
            }
            if (GManager.played && !lyricPendingWarned
                && lyricPendingSeconds >= 8f)
            {
                lyricPendingWarned = true;
                SongData s = sessionSong;
                bool holderHas = s != null && s.isRemote
                    && CloudChartHolder.TryGetForSong(s.remoteId, out _, out _);
                bool snapshotHas = s != null
                    && PlaySessionSnapshot.TryGetChartJson(s, out _);
                Debug.LogWarning($"[GameSceneManager] lyrics stuck Pending for " +
                                 $"{lyricPendingSeconds:F1}s during playback: " +
                                 $"song='{(s != null ? s.title : "null")}' " +
                                 $"remoteId='{(s != null ? s.remoteId : null)}' " +
                                 $"chartVersion={(s != null ? s.chartVersion : 0)} " +
                                 $"snapshotChart={snapshotHas} holderChart={holderHas} " +
                                 $"session={(PlaySessionSnapshot.Current != null ? "pinned" : "none")}");
            }
        }
        else
        {
            lyricPendingSeconds = 0f;
        }
    }

    public void RetireGame()
    {
        Initiate.Fade("SelectScene", Color.black, 1.0f);

    }
    public void RetryGame()
    {
        GManager.ResetSession();
        Initiate.Fade("GameScene", Color.black, 1.0f);
    }
    public void GameEnd()
    {
        Initiate.Fade("ResultScene", Color.black, 1.0f);
    }

    void Update()
    {
        RefreshPendingLyrics();
        if (!GManager.played)
        {
            return;
        }

        if (GManager.Flash)
        {
            Flash.SetActive(true);
        }
        else
        {
            Flash.SetActive(false);
        }

        gauge.fillAmount = (GManager.score / 1000000f);
    }
}
