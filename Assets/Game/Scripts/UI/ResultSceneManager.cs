using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ResultSceneManager : MonoBehaviour
{
    [SerializeField] private AudioClip sound;
    AudioSource sounds;

    [SerializeField] private Canvas legacyCanvasV5;
    [SerializeField] private Canvas legacyCanvasV6;

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

        // Issue #651: BGMtime の頭出しも Awake で行う。
        // VideoTime.Start は GManager.BGMtime を読んで背景動画の再生位置を
        // 決めるが、Start の実行順は未定義。Start で 0 に戻すと、VideoTime
        // が先に走った場合に旧値で seek したあと Update が BGMtime を旧値へ
        // 書き戻し、Result の背景動画が途中から再開される。Awake は全ての
        // Start より先に走るため、ここで確定させれば順序に依存しない。
        GManager.BGMtime = 0f;
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
        // Issue #657: 曲情報はプレイセッションの固定値を優先する
        // (GetSong は PlaySessionSnapshot.PinnedSongFor を先に見る)。
        // ActiveSongs[songID] の直接参照はカタログ更新で別曲にずれうる。
        var playedSong = GManager.GetSong(GManager.songID);
        SongName.text = playedSong.title;
        Musician.text = playedSong.musician;
        BPM.text = playedSong.bpm.ToString();


        TotalHit.text = playedSong.totalHits.ToString();
        Songlevel.text = playedSong.level.ToString();
        SongN.text=GManager.songID.ToString();
        
        

        sounds = GetComponent<AudioSource>();
        sounds.volume = GManager.effectVolume;
        scoreText.text = GManager.score.ToString();
        perfectText.text = GManager.perfect.ToString();
        greatText.text = GManager.great.ToString();
        badText.text = GManager.bad.ToString();
        missText.text = GManager.miss.ToString();
        Assessment.text = FlashBeat.UI.V6.Result.V6ResultRules.RankFor(GManager.score);
        // Issue #657: 固定テーブル外の曲 (cloud カタログ) は slot 0 を表示せず、
        // ScorePersistence の安定キー側のベストを出す (slot 0 は別曲の記録)。
        int displayedHiscore = GManager.songID >= 0 && GManager.songID < GManager.Hiscore.Length
            ? GManager.Hiscore[GManager.songID]
            : FlashBeat.Persistence.ScorePersistence.BestOf(GManager.songID);
        Hiscore.text = displayedHiscore.ToString();

        // Issue #740: 旧 v5 ResultView のフォールバック経路は撤去。
        // v6 Result が唯一の実装。
        BuildV6View();
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
    /// どちらかがシーンに無くても例外を出さない。
    /// </summary>
    public static void HideLegacyCanvases(Canvas canvasA, Canvas canvasB)
    {
        if (canvasA != null) canvasA.gameObject.SetActive(false);
        if (canvasB != null) canvasB.gameObject.SetActive(false);
    }

    void Update()
    {
        if (v6View != null)
        {
            return; // 入力は v6 ResultView が処理する
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
        // Issue #738: Result → Select は同じ人の続きの操作なので、直前に
        // プレイした曲と選曲時の検索語を次の選曲画面へ引き継ぐ。
        // Title へ戻る導線 (TitleScene) ではコンテキストを捨てるので、
        // 新規セッション相当の Title → Select では従来の初期選択のまま。
        FlashBeat.UI.V6.Select.SelectReturnContext.RequestRestore();
        Initiate.Fade("SelectScene", Color.black, 1.0f);

    }

    /// <summary>v6 Result の「タイトルへ」(T キー / design 凡例) 用の遷移。
    /// 来場者の交代境界なので、Title へ戻る前にセッション設定
    /// (Key Guide / Lyrics, Issue #241) を既定値へ戻す。</summary>
    public void TitleScene()
    {
        PlaySession.Reset();
        // Issue #738: 来場者交代の境界。選曲復帰コンテキストも捨て、
        // 次の選曲画面 (Title → Select) は初期状態から始める。
        FlashBeat.UI.V6.Select.SelectReturnContext.Clear();
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


}
