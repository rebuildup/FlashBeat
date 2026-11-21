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
    /// 曲名から歌詞データ (TextData) を読む。マージ済み譜面 JSON
    /// (NoteEditor DTO) を優先し、無ければ旧形式 *_text.json にフォールバックする。
    ///
    /// Issue #607: Option 画面の Game Preview も同じ経路で歌詞を読むため
    /// internal static にした (読み取りのみ・状態は持たない)。
    /// </summary>
    internal static TextData LoadTextData(string songName)
    {
        var mergedAsset = Resources.Load<TextAsset>(songName);
        if (mergedAsset != null)
        {
            var chart = JsonUtility.FromJson<NoteEditor.DTO.MusicDTO.EditData>(mergedAsset.text);
            if (chart != null && chart.lyrics != null && chart.lyrics.mondai != null && chart.lyrics.mondai.Length > 0)
            {
                return new TextData
                {
                    StartTime = chart.lyrics.startTime ?? new float[0],
                    Furigana  = chart.lyrics.furigana  ?? new string[0],
                    Mondai    = chart.lyrics.mondai    ?? new string[0],
                    romaji    = chart.lyrics.romaji    ?? new string[0],
                    EndTime   = chart.lyrics.endTime   ?? new float[0],
                };
            }
        }

        var legacyAsset = Resources.Load<TextAsset>(songName + "_text");
        if (legacyAsset != null)
        {
            return JsonUtility.FromJson<TextData>(legacyAsset.text);
        }

        // Cloud Catalog songs (#17) have no local lyrics resource; that is
        // expected, so only a warning. Fixed-catalog songs keep the error.
        SongData currentSong = GManager.GetSong(GManager.songID);
        if (currentSong != null && currentSong.isRemote)
        {
            Debug.LogWarning($"[GameSceneManager] no text data for remote song '{currentSong.remoteId}' - lyrics are empty");
        }
        else
        {
            Debug.LogError($"[GameSceneManager] No text data for {songName}");
        }
        return new TextData
        {
            StartTime = new float[0],
            Furigana  = new string[0],
            Mondai    = new string[0],
            romaji    = new string[0],
            EndTime   = new float[0],
        };
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
