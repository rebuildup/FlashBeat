using System.Collections;
using UnityEngine;

/// <summary>
/// Select scene (design Unit 02 "Select", v5 rebuild #121).
///
/// データと遷移の所有者。見た目と入力は <see cref="FlashBeat.UI.V5.SelectView"/>
/// (コード生成 uGUI, design/flashbeat-ui.pen v5) が担い、このクラスは
/// - クラウドカタログの定期更新 (#17)
/// - プレイ開始パス (GManager.songID → GameScene / リモート譜面の確定)
/// - タイトルへの遷移と決定音
/// を持つ。旧シーン配置 UI の Canvas は SelectView.Create が非表示化する。
/// </summary>
public class SelectSceneManager : MonoBehaviour
{
    [SerializeField] private AudioClip sound;

    private AudioSource sounds;
    private FlashBeat.UI.V5.SelectView view;

    // Cloud Catalog (#17)
    Coroutine catalogRefresh;
    CloudCatalog.Config catalogConfig;
    bool catalogWarned;

    void Start()
    {
        sounds = GetComponent<AudioSource>();
        if (sounds != null) sounds.volume = GManager.effectVolume;
        view = FlashBeat.UI.V5.SelectView.Create(this);
        StartCatalogRefresh();
        KeyGuideUI.Create(null);
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
            yield break;
        }

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
