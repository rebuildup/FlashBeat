using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using UnityEngine.UI;

public class ResultSceneManager : MonoBehaviour
{
    [SerializeField] private AudioClip sound;
    AudioSource sounds;

    [SerializeField] private Canvas legacyCanvasV5;
    [SerializeField] private Canvas legacyCanvasV6;

    private FlashBeat.UI.V5.ResultView view;
    private FlashBeat.UI.V6.Result.V6ResultView v6View;
    private int prevHiscore;

    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI perfectText;
    [SerializeField] private TextMeshProUGUI greatText;
    [SerializeField] private TextMeshProUGUI badText;
    [SerializeField] private TextMeshProUGUI missText;

    [SerializeField] private TextMeshProUGUI Assessment;

    [SerializeField] private TextMeshProUGUI SongName;
    [SerializeField] private TextMeshProUGUI Musician;
    [SerializeField] private TextMeshProUGUI BPM;
    [SerializeField] private TextMeshProUGUI TotalHit;
    [SerializeField] private TextMeshProUGUI Songlevel;
    [SerializeField] private TextMeshProUGUI SongN;
    [SerializeField] private TextMeshProUGUI Hiscore;

    

    [SerializeField] private TextMeshProUGUI OKT;
    [SerializeField] private TextMeshProUGUI RetryT;
    
    public string[] songURLs;
    public Image[] imageObjects;

    void Awake()
    {
        // Issue #167: ベストスコアの更新は Awake で行う。
        // Start の実行順はコンポーネントの並びで決まるため、Start で更新すると
        // SaveLoadManager (Result シーンで Start 時に保存) より後になり、
        // 新記録が保存されないことがあった。Awake は全ての Start より先に走る。
        // 更新処理は EditMode テストから直接検証できるよう純粋関数に抽出した。
        prevHiscore = ApplyBestScore(GManager.songID, GManager.score);
    }

    /// <summary>
    /// ベストスコアを保存テーブル (<see cref="GManager.Hiscore"/>) へ反映する (Issue #167)。
    /// 返り値は更新前の値 (Result 画面の HISCORE 行の差分表示に使う)。
    /// 固定テーブル外の songId (cloud カタログで増えた曲) は 0 番スロットに
    /// 書かない (issue-162 A-6 再照合): スロット 0 は全 cloud 曲で共有され、
    /// 別曲のベストが「前回最高」として表示されていた。代わりに安定キー
    /// (remoteId@chartVersion) の保存先 (<see cref="FlashBeat.Persistence.ScorePersistence"/>)
    /// へ直接書く。
    /// </summary>
    public static int ApplyBestScore(int songId, int score)
    {
        if (songId >= 0 && songId < GManager.Hiscore.Length)
        {
            int prev = GManager.Hiscore[songId];
            if (score >= GManager.Hiscore[songId])
            {
                GManager.Hiscore[songId] = score;
            }
            return prev;
        }

        int stablePrev = FlashBeat.Persistence.ScorePersistence.BestOf(songId);
        if (score >= stablePrev)
        {
            FlashBeat.Persistence.ScorePersistence.SubmitForCatalogIndex(songId, score);
        }
        return stablePrev;
    }

    void Start()
    {
        GManager.BGMtime = 0f;   
        SongName.text = GManager.ActiveSongs[GManager.songID].title;
        Musician.text = GManager.ActiveSongs[GManager.songID].musician;
        BPM.text = GManager.ActiveSongs[GManager.songID].bpm.ToString();
        
       
        TotalHit.text = GManager.ActiveSongs[GManager.songID].totalHits.ToString();
        Songlevel.text = GManager.ActiveSongs[GManager.songID].level.ToString();
        SongN.text=GManager.songID.ToString();
        
        

        sounds = GetComponent<AudioSource>();
        sounds.volume = GManager.effectVolume;
        scoreText.text = GManager.score.ToString();
        perfectText.text = GManager.perfect.ToString();
        greatText.text = GManager.great.ToString();
        badText.text = GManager.bad.ToString();
        missText.text = GManager.miss.ToString();
        Assessment.text = FlashBeat.UI.V5.ResultView.RankFor(GManager.score);
        int hiIndex = GManager.songID;
        if (hiIndex >= GManager.Hiscore.Length) hiIndex = 0; // remote songs beyond the fixed table keep slot 0 unused
        Hiscore.text = GManager.Hiscore[hiIndex].ToString();
        // Issue #398: v6 有効時の二重取得経路を provider へ一本化。
        // hqdefault (pause と同じ) に揃えるため、/0.jpg より解像度は下がる
        // (v5 UI の背景用途ではカバー UV で同等に表示できる)。
        FlashBeat.UI.V6.V6JacketProvider.RequestRemote(GManager.ActiveSongs[GManager.songID].videoId, SetRemoteSprite);

        // v6 Result (#217 / Issue #240)。V6ResultView.Enabled で切替える
        // (統合担当は V6UiMode からこのフラグを設定する)。既定は true。
        if (FlashBeat.UI.V6.Result.V6ResultView.Enabled)
        {
            BuildV6View();
        }
        else
        {
            BuildV5View();
        }
    }

    /// <summary>
    /// v6 の Result 画面を組み立てる (#217)。データは GManager / ScorePersistence の
    /// 実値のみ。Create 内の初回 Refresh() が GManager から run スナップショットを
    /// 捕捉するので、以後 Retry でカウンタが 0 に戻っても表示は消えない。
    /// 遷移 (リトライ / 選曲へ / タイトルへ) はこのクラスの既存経路をコールバックで渡す。
    /// </summary>
    private void BuildV6View()
    {
        // 旧 Canvas は v5 / v6 両方とも非表示にする。v6 モードで legacyCanvasV5
        // が生き残り、v6 の Retry / Register の上に旧ヒントや OK ボタンが
        // 重なる不具合の修正 (片方だけ隠すと他方が残る)。null 許容。
        HideLegacyCanvases(legacyCanvasV5, legacyCanvasV6);

        // 旧オーバーレイの自動モーダル (結果画面入場 1.2 秒後の登録モーダル) を止める。
        // コードは消さず enabled 切替。v6 側は自前の登録モーダルを持つ
        // (SuspendLegacyOverlay が停止前の enabled 状態を覚えるので二重に止めて安全)。
        var overlay = FindFirstObjectByType<ResultRankingOverlay>();
        if (overlay != null)
        {
            overlay.enabled = false;
        }

        var canvas = FlashBeat.UI.V5.V5GameUi.NewScreenCanvas("V6 Result Canvas");
        var canvasGo = canvas.gameObject;
        v6View = FlashBeat.UI.V6.Result.V6ResultView.Create(
            canvasGo.GetComponent<RectTransform>(), Retry, SelectScene, TitleScene);
        v6View.SetPrevHiscore(prevHiscore);

        // v6 ビュー自身がキー入力を担うので、Unity の自動ナビゲーションは切る。
        FlashBeat.UI.V5.V5GameUi.ApplySingleInputModel(canvas);
    }

    /// <summary>
    /// 旧シーン配置 Canvas (v5 用 / v6 用の両方) を非表示にする。
    /// どちらかがシーンに無くても例外を出さない。フラグ分岐は呼び出し側
    /// (BuildV6View / BuildV5View) が担うので、ここでは常に非表示にするだけ。
    /// </summary>
    public static void HideLegacyCanvases(Canvas canvasA, Canvas canvasB)
    {
        if (canvasA != null) canvasA.gameObject.SetActive(false);
        if (canvasB != null) canvasB.gameObject.SetActive(false);
    }

    private void BuildV5View()
    {
        // v5 経路では legacyCanvasV5 を隠さない (従来どおり v5 のシーン配置 Canvas を
        // 使う)。v6 用の旧 Canvas のみ隠す (v5 ResultView はコード生成)。
        HideLegacyCanvases(null, legacyCanvasV6);

        var song = GManager.ActiveSongs[GManager.songID];
        var snapshot = new FlashBeat.UI.V5.ResultView.Snapshot
        {
            Score = GManager.score,
            Perfect = GManager.perfect,
            Great = GManager.great,
            Bad = GManager.bad,
            Miss = GManager.miss,
            Combo = GManager.combo,
            PrevHiscore = prevHiscore,
            Hiscore = GManager.Hiscore[GManager.songID < GManager.Hiscore.Length ? GManager.songID : 0],
            SongTitle = song.title,
            Musician = song.musician,
            Bpm = song.bpm,
            Level = song.level,
            TotalHits = song.totalHits
        };
        view = FlashBeat.UI.V5.ResultView.Build(snapshot, Retry, SelectScene);
    }

    void Update()
    {
        if (view != null || v6View != null)
        {
            return; // 入力は v5 ResultView / v6 ResultView が処理する (二重処理防止)
        }
        if (KeyActions.IsDown(KeyActions.KeyAction.Confirm))
        {
            SelectScene();
        }
        if (KeyActions.IsDown(KeyActions.KeyAction.Retry))
        {
            Retry();
        }
    }
    /// <summary>
    /// Result からのリトライ。カウンタのゼロ化は正本
    /// (<see cref="GManager.ResetSession"/>) を使う (issue-162 D-7 再照合)。
    /// 以前の自前実装は <c>settledNoteCount</c> とプレイセッションの固定
    /// (BeginPlaySession) を漏らしており、次のプレイに前回の状態が残った。
    /// </summary>
    public static void ResetRunCounters()
    {
        GManager.ResetSession();
    }

    public void Retry()
    {
        ResetRunCounters();
        Initiate.Fade("GameScene", Color.black, 1.0f);
    }
    public void SelectScene()
    {

        Initiate.Fade("SelectScene", Color.black, 1.0f);

    }

    /// <summary>v6 Result の「タイトルへ」(T キー / design 凡例) 用の遷移。</summary>
    public void TitleScene()
    {
        Initiate.Fade("TitleScene", Color.black, 1.0f);
    }
    public void playSound()
    {
        sounds.PlayOneShot(sound);
    }
    public void OKTUp()
    {
        OKT.fontSize = 55;
    }
    public void OKTDown()
    {
        OKT.fontSize = 50;
        
    }
    public void RetryTUp()
    {
        RetryT.fontSize = 55;
    }
    public void RetryTDown()
    {
        RetryT.fontSize = 50;
    }

    /// <summary>provider から届いた遠隔スプライトを v5 Result の背景へ反映する。</summary>
    void SetRemoteSprite(Sprite sprite)
    {
        // provider はシーンを知らない。Result 退出後 (Retry 等) に HTTP が返ると
        // 破棄済み Image へ書き込んで MissingReferenceException になる (#417)。
        if (this == null || imageObjects == null || imageObjects.Length == 0 || imageObjects[0] == null)
        {
            return;
        }

        if (sprite == null)
        {
            Debug.LogWarning("[ResultSceneManager] thumbnail 取得失敗 (V6JacketProvider)");
            return;
        }

        imageObjects[0].sprite = sprite;
        var texture = sprite.texture as Texture2D;
        if (texture != null && view != null)
        {
            view.SetBackdrop(texture);
        }    }

    // Issue #398: provider 一本化で使われなくなった旧経路。v5/v6 共通化の完了まで
    // 削除せずコメントアウトで残す (AGENTS.md: 削除時はコメントアウト)。
    //
    // IEnumerator FetchThumbnail(string songURL, int index)
    // {
    //     string url = "https://img.youtube.com/vi/" + songURL + "/0.jpg";
    //     UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
    //     yield return request.SendWebRequest();
    //     if (request.result == UnityWebRequest.Result.Success)
    //     {
    //         Texture2D texture = ((DownloadHandlerTexture)request.downloadHandler).texture;
    //         Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    //         imageObjects[index].sprite = sprite;
    //         if (view != null)
    //         {
    //             view.SetBackdrop(texture);
    //         }
    //     }
    //     else
    //     {
    //         Debug.Log(request.error);
    //     }
    // }

}
