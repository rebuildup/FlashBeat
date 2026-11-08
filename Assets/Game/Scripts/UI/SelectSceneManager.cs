using System.Collections;
using UnityEngine;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Select.Data;
using FlashBeat.UI.V6.Select.View;

/// <summary>
/// Select scene (design Unit 02 "Select", v5 rebuild #121 → v6 移植 #221/#222).
///
/// データと遷移の所有者。見た目と入力は
/// <see cref="FlashBeat.UI.V6.Select.View.V6SelectView"/> (v6, コード生成 uGUI) が担い、
/// 実データは <see cref="V6SelectDataSource"/> が持つ。このクラスは
/// - クラウドカタログの定期更新 (#17)
/// - プレイ開始パス (GManager.songID → GameScene / リモート譜面の確定)
/// - タイトルへの遷移と決定音
/// を持つ。旧シーン配置 UI の Canvas は View の Create が非表示化する。
///
/// v6UiMode.SelectScreen が false のときは旧 v5 SelectView を使う (未移植画面の退避先)。
/// </summary>
public class SelectSceneManager : MonoBehaviour
{
    [SerializeField] private AudioClip sound;

    private AudioSource sounds;
    private FlashBeat.UI.V5.SelectView view;

    // v6 選曲画面 (#221 ビュー / #222 データ)
    private V6SelectView v6View;
    private V6SelectDataSource v6Data;

    // Cloud Catalog (#17)
    Coroutine catalogRefresh;
    CloudCatalog.Config catalogConfig;
    bool catalogWarned;

    void Start()
    {
        sounds = GetComponent<AudioSource>();
        if (sounds != null) sounds.volume = GManager.effectVolume;

        if (V6UiMode.SelectScreen)
        {
            CreateV6SelectView();
        }
        else
        {
            view = FlashBeat.UI.V5.SelectView.Create(this);
        }

        StartCatalogRefresh();

        // v6 の画面にはキーガイド行 (画面下) があるので、旧キーガイドパネルは作らない。
        if (V6UiMode.SelectScreen)
        {
            V6UiMode.HideLegacyKeyGuide();
        }
        else
        {
            KeyGuideUI.Create(null);
        }
    }

    /// <summary>
    /// v6 の 3 カラム選曲画面を組み立てる (#221)。
    /// 遷移は既存の経路 (GameScene / TitleScene) をそのまま使う:
    /// Ready は「選択の確定 (CommitReady)」だけを行い、遷移は GameScene() が行う。
    /// </summary>
    private void CreateV6SelectView()
    {
        var canvas = FlashBeat.UI.V5.V5GameUi.NewScreenCanvas("V6 Select Canvas");
        var canvasGo = canvas.gameObject;

        v6Data = new V6SelectDataSource();
        v6View = V6SelectView.Create(canvasGo.GetComponent<RectTransform>(), v6Data,
            OnV6Ready, TitleScene, OnV6OpenSettings);

        // レガシー (Issue #57 までのシーン配置 UI) を隠す。
        // 名前で 1 個だけ隠すと別 Canvas に乗った要素が残るので、
        // v6 の Canvas 以外の Canvas にあるグラフィックをすべて隠す。
        var legacy = GameObject.Find("Canvas");
        if (legacy != null && legacy != canvasGo) legacy.SetActive(false);
        HideLegacyGraphics(canvasGo);

        // v6 ビュー自身が矢印キー操作を担うので、Unity の自動ナビゲーションは切る。
        FlashBeat.UI.V5.V5GameUi.ApplySingleInputModel(canvas);
    }

    /// <summary>v6 の Canvas 以外にあるグラフィック (旧 UI) を隠す。</summary>
    private static void HideLegacyGraphics(GameObject keep)
    {
        var canvases = FindObjectsOfType<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null) continue;
            if (canvas.gameObject == keep) continue;
            if (canvas.name.StartsWith("V6")) continue;

            var graphics = canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
            for (int g = 0; g < graphics.Length; g++)
            {
                if (graphics[g] != null) graphics[g].enabled = false;
            }
            var raycaster = canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster != null) raycaster.enabled = false;
        }
    }

    /// <summary>Ready: 選択を確定してから、既存の開始経路でゲーム画面へ。</summary>
    private void OnV6Ready()
    {
        if (v6Data == null) return;
        v6Data.CommitReady();
        if (v6Data.HasError) return; // 開始できない理由をデータ側に持たせる (選曲画面に留まる)
        GameScene();
    }

    /// <summary>設定パネルは P1 (未移植)。今は選曲画面に留まったままにする。</summary>
    private void OnV6OpenSettings()
    {
        Debug.LogWarning("[SelectScene] v6 の設定パネルは未移植です (Issue #224 相当)。");
    }

    void Update()
    {
        if (view != null) view.HandleInput();
    }

    void OnEnable()
    {
        // Issue #167: オーバーレイ (ランキングボード等) の SuspendScene で
        // 無効化されるとコルーチンも止まる。有効に戻ったら更新ループを再開する。
        if (catalogConfig != null && catalogRefresh == null)
        {
            StartCatalogRefresh();
        }
    }

    void OnDisable()
    {
        // 無効化でコルーチンが止まるので、OnEnable の再開判定用にハンドルを手放す。
        catalogRefresh = null;
    }

    // Cloud Catalog (#17): fetch GET /api/v1/catalog on entry and refresh it
    // while the player is idle on the select screen. A usable response
    // switches the shown songs over (GManager.ActiveSongs); any failure -
    // offline, HTTP error, empty or malformed body - only logs a warning and
    // keeps the built-in fixed catalog, so the game never stops here.
    void StartCatalogRefresh()
    {
        CloudCatalog.Config config = CloudCatalog.LoadConfig();
        if (config == null) return; // no API configured: stay on the fixed catalog
        if (catalogRefresh != null) StopCoroutine(catalogRefresh);
        catalogConfig = config;
        catalogRefresh = StartCoroutine(RefreshCatalogLoop(config));
    }

    IEnumerator RefreshCatalogLoop(CloudCatalog.Config config)
    {
        while (true)
        {
            yield return RefreshCatalogOnce(config);
            float wait = config.refreshSeconds > 1f ? config.refreshSeconds : 8f;
            yield return new WaitForSeconds(wait);
        }
    }

    IEnumerator RefreshCatalogOnce(CloudCatalog.Config config)
    {
        string body = null;
        int status = 0;
        yield return CloudCatalog.GetText(CloudCatalog.CatalogUrl(config.baseUrl), (code, text) => { status = code; body = text; });

        SongData[] songs = status == 200 ? CloudCatalog.ParseCatalog(body) : null;
        if (songs != null)
        {
            catalogWarned = false;
            GManager.ApplyRemoteCatalog(songs);
            if (GManager.songID > GManager.totalSong) GManager.songID = 1;
            OnCatalogChanged();
        }
        else
        {
            if (!catalogWarned || GManager.HasRemoteCatalog)
            {
                catalogWarned = true;
                GManager.ClearRemoteCatalog();
                if (GManager.songID > GManager.totalSong) GManager.songID = 1;
                OnCatalogChanged();
                Debug.LogWarning($"[SelectScene] catalog unavailable (status={status}) - using the built-in song list");
            }
        }
    }

    private void OnCatalogChanged()
    {
        if (view != null) view.OnCatalogChanged();
        if (v6Data != null) v6Data.RefreshAsync();
    }

    /// <summary>選択中の曲でプレイを開始する (リモート曲は譜面を確定してから)。</summary>
    public void GameScene()
    {
        SongData song = GManager.GetSong(GManager.songID);
        if (song != null && song.isRemote)
        {
            // Cloud Catalog (#17): pin the chart version now and fetch the
            // chart snapshot once; it is kept for the whole play (retries
            // included). A failure only logs a warning and stays here.
            StartCoroutine(StartRemoteSong(song));
            return;
        }
        GManager.ResetSession();
        Initiate.Fade("GameScene", Color.black, 1.0f);
    }

    IEnumerator StartRemoteSong(SongData song)
    {
        CloudCatalog.Config config = CloudCatalog.LoadConfig();
        if (config == null)
        {
            Debug.LogWarning("[SelectScene] remote song selected but no API is configured - staying on the select screen");
            v6Data?.ReportStartFailure("クラウドカタログが設定されていないため、この曲は開始できません");
            yield break;
        }

        int version = song.chartVersion > 0 ? song.chartVersion : config.chartVersion;
        string body = null;
        int status = 0;
        yield return CloudCatalog.GetText(CloudCatalog.ChartUrl(config.baseUrl, song.remoteId, version), (code, text) => { status = code; body = text; });

        Data chart = status == 200 ? CloudCatalog.ParseChartResponse(body) : null;
        if (chart == null)
        {
            // 404 / network error / malformed body: warn and stay; the fixed
            // catalog and the rest of the game keep working.
            Debug.LogWarning($"[SelectScene] chart for '{song.remoteId}' v{version} unavailable (status={status}) - start cancelled");
            v6Data?.ReportStartFailure("譜面を取得できなかったため開始できませんでした (HTTP " + status + ")");
            yield break;
        }

        // The song entry carries the version the snapshot was taken with, so
        // that the game scene finds exactly this snapshot (NotesManager looks
        // it up by song.chartVersion) even when the catalog entry itself did
        // not carry a version.
        song.chartVersion = version;
        CloudChartHolder.Hold(song.remoteId, version, body);
        GManager.ResetSession();
        Initiate.Fade("GameScene", Color.black, 1.0f);
    }

    public void playSound()
    {
        if (sounds == null) sounds = GetComponent<AudioSource>();
        if (sounds == null || sound == null) return;
        sounds.PlayOneShot(sound);
    }

    public void TitleScene()
    {
        Initiate.Fade("TitleScene", Color.black, 1.0f);
    }
}
