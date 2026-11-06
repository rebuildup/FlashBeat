using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class SelectSceneManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI BackT;
    [SerializeField] private TextMeshProUGUI StartT;
    [SerializeField] private TextMeshProUGUI SongUpT;
    [SerializeField] private TextMeshProUGUI SongDownT;

    [SerializeField] private AudioClip sound;
    private AudioSource sounds;

    [SerializeField] private TextMeshProUGUI SongNameOne;
    [SerializeField] private TextMeshProUGUI SongNameTwo;
    [SerializeField] private TextMeshProUGUI SongNameThree;
    [SerializeField] private TextMeshProUGUI SongNameFour;
    [SerializeField] private TextMeshProUGUI SongNameFive;

    [SerializeField] private TextMeshProUGUI MusicianOne;
    [SerializeField] private TextMeshProUGUI MusicianTwo;
    [SerializeField] private TextMeshProUGUI MusicianThree;
    [SerializeField] private TextMeshProUGUI MusicianFour;
    [SerializeField] private TextMeshProUGUI MusicianFive;

    [SerializeField] private TextMeshProUGUI BPMOne;
    [SerializeField] private TextMeshProUGUI BPMTwo;
    [SerializeField] private TextMeshProUGUI BPMThree;
    [SerializeField] private TextMeshProUGUI BPMFour;
    [SerializeField] private TextMeshProUGUI BPMFive;

    [SerializeField] private TextMeshProUGUI HitOne;
    [SerializeField] private TextMeshProUGUI HitTwo;
    [SerializeField] private TextMeshProUGUI HitThree;
    [SerializeField] private TextMeshProUGUI HitFour;
    [SerializeField] private TextMeshProUGUI HitFive;

    [SerializeField] private TextMeshProUGUI LevelOne;
    [SerializeField] private TextMeshProUGUI LevelTwo;
    [SerializeField] private TextMeshProUGUI LevelThree;
    [SerializeField] private TextMeshProUGUI LevelFour;
    [SerializeField] private TextMeshProUGUI LevelFive;

    [SerializeField] private TextMeshProUGUI noOne;
    [SerializeField] private TextMeshProUGUI noTwo;
    [SerializeField] private TextMeshProUGUI noThree;
    [SerializeField] private TextMeshProUGUI noFour;
    [SerializeField] private TextMeshProUGUI noFive;

    [SerializeField] private TextMeshProUGUI HiOne;
    [SerializeField] private TextMeshProUGUI HiTwo;
    [SerializeField] private TextMeshProUGUI HiThree;
    [SerializeField] private TextMeshProUGUI HiFour;
    [SerializeField] private TextMeshProUGUI HiFive;

    [SerializeField] private TextMeshProUGUI dottt;
    [SerializeField] private Image[] samune;

    public Image[] imageObjects;
    private string dottT;

    // Cloud Catalog (#17)
    Coroutine catalogRefresh;
    bool catalogWarned;

    void Start()
    {
        sounds = GetComponent<AudioSource>();
        if (sounds != null) sounds.volume = GManager.effectVolume;
        UpdateThumbnails();
        StartCatalogRefresh();
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
            UpdateUI();
            UpdateThumbnails();
        }
        else
        {
            if (!catalogWarned || GManager.HasRemoteCatalog)
            {
                catalogWarned = true;
                GManager.ClearRemoteCatalog();
                if (GManager.songID > GManager.totalSong) GManager.songID = 1;
                UpdateUI();
                UpdateThumbnails();
                Debug.LogWarning($"[SelectScene] catalog unavailable (status={status}) - using the built-in song list");
            }
        }
    }

    void Update()
    {
        UpdateUI();

        if (Input.GetKeyDown(KeyCode.Space))
        {
            playSound();
            GameScene();
        }

        if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            playSound();
            SongDown();
        }

        if (Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.L) || Input.GetKeyDown(KeyCode.Semicolon) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            playSound();
            SongUp();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            playSound();
            TitleScene();
        }
    }

    private int GetSongIndexAtOffset(int currentSongID, int offset)
    {
        int total = GManager.totalSong;
        if (total <= 0) return 1;
        int zeroBased = (currentSongID - 1 + offset) % total;
        if (zeroBased < 0) zeroBased += total;
        return zeroBased + 1;
    }

    private void UpdateUI()
    {
        dottDis(GManager.songID);

        int[] offsets = new int[] { -2, -1, 0, 1, 2 };

        TextMeshProUGUI[] songNames = { SongNameOne, SongNameTwo, SongNameThree, SongNameFour, SongNameFive };
        TextMeshProUGUI[] musicians = { MusicianOne, MusicianTwo, MusicianThree, MusicianFour, MusicianFive };
        TextMeshProUGUI[] bpms = { BPMOne, BPMTwo, BPMThree, BPMFour, BPMFive };
        TextMeshProUGUI[] hits = { HitOne, HitTwo, HitThree, HitFour, HitFive };
        TextMeshProUGUI[] levels = { LevelOne, LevelTwo, LevelThree, LevelFour, LevelFive };
        TextMeshProUGUI[] nos = { noOne, noTwo, noThree, noFour, noFive };
        TextMeshProUGUI[] hiscores = { HiOne, HiTwo, HiThree, HiFour, HiFive };

        for (int i = 0; i < 5; i++)
        {
            int sID = GetSongIndexAtOffset(GManager.songID, offsets[i]);
            var song = GManager.ActiveSongs[sID];
            if (songNames[i] != null) songNames[i].text = song.title;
            if (musicians[i] != null) musicians[i].text = song.musician;
            if (bpms[i] != null) bpms[i].text = song.bpm.ToString();
            if (hits[i] != null) hits[i].text = song.totalHits.ToString();
            if (levels[i] != null) levels[i].text = song.level.ToString();
            if (nos[i] != null) nos[i].text = sID.ToString();
            if (hiscores[i] != null) hiscores[i].text = sID < GManager.Hiscore.Length ? GManager.Hiscore[sID].ToString() : "0";
        }
    }

    private void UpdateThumbnails()
    {
        int[] offsets = new int[] { -2, -1, 0, 1, 2 };
        for (int i = 0; i < 5; i++)
        {
            int sID = GetSongIndexAtOffset(GManager.songID, offsets[i]);
            StartCoroutine(FetchThumbnail(GManager.ActiveSongs[sID].videoId, i));
        }
    }

    public void dottDis(int N)
    {
        if (dottt == null) return;
        StringBuilder sb = new StringBuilder();
        for (int i = 1; i <= GManager.totalSong; i++)
        {
            if (i > 1) sb.Append(" ");
            sb.Append(i == N ? "●" : "○");
        }
        dottt.text = sb.ToString();
    }

    public void GameScene()
    {
        SongData song = GManager.ActiveSongs[GManager.songID];
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
        if (sounds != null && sound != null)
        {
            sounds.PlayOneShot(sound);
        }
    }

    public void TitleScene()
    {
        Initiate.Fade("TitleScene", Color.black, 1.0f);
    }

    public void SongUp()
    {
        GManager.songID++;
        if (GManager.songID > GManager.totalSong)
        {
            GManager.songID = 1;
        }
        UpdateThumbnails();
    }

    public void SongDown()
    {
        GManager.songID--;
        if (GManager.songID <= 0)
        {
            GManager.songID = GManager.totalSong;
        }
        UpdateThumbnails();
    }

    public void BackTUp()
    {
        if (BackT != null) BackT.fontSize = 19;
    }
    public void BackTDown()
    {
        if (BackT != null) BackT.fontSize = 17;
    }
    public void StartTUp()
    {
        if (StartT != null) StartT.fontSize = 16.5f;
    }
    public void StartTDown()
    {
        if (StartT != null) StartT.fontSize = 15;
    }
    public void SongUpTUp()
    {
        if (SongUpT != null) SongUpT.fontSize = 33;
    }
    public void SongUpTDown()
    {
        if (SongUpT != null) SongUpT.fontSize = 30;
    }
    public void SongDownTUp()
    {
        if (SongDownT != null) SongDownT.fontSize = 33;
    }
    public void SongDownTDown()
    {
        if (SongDownT != null) SongDownT.fontSize = 30;
    }

    public void PanelOne()
    {
        GManager.songID -= 2;
        if (GManager.songID <= 0)
        {
            GManager.songID += GManager.totalSong;
        }
        UpdateThumbnails();
    }
    public void PanelTwo()
    {
        SongDown();
    }
    public void PanelFour()
    {
        SongUp();
    }
    public void PanelFive()
    {
        GManager.songID += 2;
        if (GManager.songID > GManager.totalSong)
        {
            GManager.songID -= GManager.totalSong;
        }
        UpdateThumbnails();
    }

    private IEnumerator FetchThumbnail(string songURL, int index)
    {
        if (imageObjects == null || index < 0 || index >= imageObjects.Length || imageObjects[index] == null)
            yield break;

        string url = "https://img.youtube.com/vi/" + songURL + "/0.jpg";
        UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
        yield return request.SendWebRequest();
        if (request.result == UnityWebRequest.Result.Success)
        {
            Texture2D texture = ((DownloadHandlerTexture)request.downloadHandler).texture;
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            imageObjects[index].sprite = sprite;
        }
        else
        {
            Debug.Log(request.error);
        }
    }
}

