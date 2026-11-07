using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>One card of the select carousel (design Unit 02 "Select").</summary>
[Serializable]
public class SongCardView
{
    public TextMeshProUGUI number;
    public TextMeshProUGUI title;
    public TextMeshProUGUI composer;
    public TextMeshProUGUI level;
    public Image thumbnail;
}

/// <summary>
/// Select scene (design Unit 02 "Select").
///
/// The five card carousel, the top bar and the bottom bar are uGUI objects
/// inside SelectScene.unity (see design/flashbeat-ui.pen). This script feeds
/// the song data into them and keeps the cloud catalog refresh alive.
/// </summary>
public class SelectSceneManager : MonoBehaviour
{
    private const int CardCount = 5;
    private static readonly int[] CardOffsets = { -2, -1, 0, 1, 2 };

    [SerializeField] private SongCardView[] cards = new SongCardView[CardCount];

    // Detail rows live on the centre card only (design: "詳細は中央カードのみ").
    [SerializeField] private TextMeshProUGUI bpmValue;
    [SerializeField] private TextMeshProUGUI totalHitsValue;
    [SerializeField] private TextMeshProUGUI songLevelValue;
    [SerializeField] private TextMeshProUGUI hiscoreValue;

    [SerializeField] private TextMeshProUGUI catalogStatus;

    [SerializeField] private RectTransform pageDots;
    [SerializeField] private GameObject dotOffTemplate;
    [SerializeField] private GameObject dotOnTemplate;

    [SerializeField] private AudioClip sound;

    private AudioSource sounds;
    private readonly List<Image> dots = new List<Image>();

    // Cloud Catalog (#17)
    Coroutine catalogRefresh;
    bool catalogWarned;

    void Start()
    {
        sounds = GetComponent<AudioSource>();
        if (sounds != null) sounds.volume = GManager.effectVolume;
        BuildPageDots();
        Refresh();
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
            BuildPageDots();
            Refresh();
            UpdateThumbnails();
        }
        else
        {
            if (!catalogWarned || GManager.HasRemoteCatalog)
            {
                catalogWarned = true;
                GManager.ClearRemoteCatalog();
                if (GManager.songID > GManager.totalSong) GManager.songID = 1;
                BuildPageDots();
                Refresh();
                UpdateThumbnails();
                Debug.LogWarning($"[SelectScene] catalog unavailable (status={status}) - using the built-in song list");
            }
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            playSound();
            GameScene();
        }

        if (KeyBindings.IsAnyLaneKeyDown(0, 4) || Input.GetKeyDown(KeyCode.LeftArrow)) // lanes 1-4 (custom bindings) = previous song
        {
            playSound();
            SongDown();
        }

        if (KeyBindings.IsAnyLaneKeyDown(4, 8) || Input.GetKeyDown(KeyCode.RightArrow)) // lanes 5-8 (custom bindings) = next song
        {
            playSound();
            SongUp();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            playSound();
            TitleScene();
        }

        // Quick settings entry (design Unit 02: "Tab でクイック設定を開く").
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            playSound();
            OptionScene();
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

    /// <summary>Pushes the current selection into the carousel, the detail rows and the status chip.</summary>
    private void Refresh()
    {
        for (int i = 0; i < CardCount; i++)
        {
            if (cards == null || i >= cards.Length || cards[i] == null) continue;
            int songID = GetSongIndexAtOffset(GManager.songID, CardOffsets[i]);
            SongData song = GManager.GetSong(songID);
            if (song == null) continue;

            SongCardView card = cards[i];
            if (card.number != null) card.number.text = "No." + songID;
            if (card.title != null) card.title.text = song.title;
            if (card.composer != null) card.composer.text = song.musician;
            if (card.level != null) card.level.text = song.level.ToString();
        }

        SongData current = GManager.GetSong(GManager.songID);
        if (current != null)
        {
            if (bpmValue != null) bpmValue.text = current.bpm.ToString();
            if (totalHitsValue != null) totalHitsValue.text = current.totalHits.ToString();
            if (songLevelValue != null) songLevelValue.text = current.level.ToString();
            if (hiscoreValue != null)
            {
                hiscoreValue.text = GManager.songID < GManager.Hiscore.Length
                    ? GManager.Hiscore[GManager.songID].ToString()
                    : "0";
            }
        }

        RefreshCatalogStatus();
        UpdatePageDots();
    }

    private void RefreshCatalogStatus()
    {
        if (catalogStatus == null) return;
        catalogStatus.text = GManager.HasRemoteCatalog
            ? $"同期済み · {GManager.totalSong}曲"
            : $"ローカル · {GManager.totalSong}曲";
    }

    private void UpdateThumbnails()
    {
        for (int i = 0; i < CardCount; i++)
        {
            if (cards == null || i >= cards.Length || cards[i] == null) continue;
            int songID = GetSongIndexAtOffset(GManager.songID, CardOffsets[i]);
            SongData song = GManager.GetSong(songID);
            if (song == null) continue;
            if (cards[i].thumbnail != null) cards[i].thumbnail.sprite = null;
            StartCoroutine(FetchThumbnail(song.videoId, i));
        }
    }

    /// <summary>Rebuilds the page dots (one per song) and marks the selected one.</summary>
    private void BuildPageDots()
    {
        if (pageDots == null || dotOffTemplate == null) return;

        for (int i = pageDots.childCount - 1; i >= 0; i--)
        {
            GameObject child = pageDots.GetChild(i).gameObject;
            if (child == dotOffTemplate || child == dotOnTemplate) continue;
            Destroy(child);
        }

        dots.Clear();
        int total = GManager.totalSong;
        for (int i = 1; i <= total; i++)
        {
            GameObject dot = Instantiate(dotOffTemplate, pageDots);
            dot.name = "Dot " + i;
            dot.SetActive(true);
            Image image = dot.GetComponent<Image>();
            if (image != null) dots.Add(image);
        }
        UpdatePageDots();
    }

    private void UpdatePageDots()
    {
        if (dots.Count == 0 || dotOffTemplate == null || dotOnTemplate == null) return;
        Image onImage = dotOnTemplate.GetComponent<Image>();
        Image offImage = dotOffTemplate.GetComponent<Image>();
        if (onImage == null || offImage == null) return;

        for (int i = 0; i < dots.Count; i++)
        {
            Image dot = dots[i];
            if (dot == null) continue;
            dot.sprite = (i + 1 == GManager.songID) ? onImage.sprite : offImage.sprite;
        }
    }

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

    /// <summary>Quick settings entry (design Unit 02). The panel itself is Unit 02b; until then it opens the option screen.</summary>
    public void OptionScene()
    {
        Initiate.Fade("OptionScene", Color.black, 1.0f);
    }

    public void SongUp()
    {
        GManager.songID++;
        if (GManager.songID > GManager.totalSong)
        {
            GManager.songID = 1;
        }
        Refresh();
        UpdateThumbnails();
    }

    public void SongDown()
    {
        GManager.songID--;
        if (GManager.songID <= 0)
        {
            GManager.songID = GManager.totalSong;
        }
        Refresh();
        UpdateThumbnails();
    }

    private IEnumerator FetchThumbnail(string songURL, int index)
    {
        if (cards == null || index < 0 || index >= cards.Length || cards[index] == null) yield break;
        if (cards[index].thumbnail == null) yield break;
        if (string.IsNullOrEmpty(songURL)) yield break;

        string url = "https://img.youtube.com/vi/" + songURL + "/0.jpg";
        UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
        yield return request.SendWebRequest();
        if (request.result == UnityWebRequest.Result.Success)
        {
            Texture2D texture = ((DownloadHandlerTexture)request.downloadHandler).texture;
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            cards[index].thumbnail.sprite = sprite;
        }
        else
        {
            Debug.Log(request.error);
        }
    }
}
