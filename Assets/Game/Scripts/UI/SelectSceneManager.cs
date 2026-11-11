using System.Collections;
using UnityEngine;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.History;
using FlashBeat.UI.V6.Ranking;
using FlashBeat.UI.V6.Select;
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
/// - v6 選曲 / 履歴 / ランキングの表示切替と入力振り分け
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

    // v6 履歴 / ランキング (#223 統合)
    private V6HistoryView historyView;
    private V6RankingView rankingView;

    // 表示モード: 選曲 / 履歴 / ランキング の 3 画面を排他で出す
    private enum V6Mode { Select, History, Ranking }
    private V6Mode currentMode = V6Mode.Select;

    // Cloud Catalog (#17)
    Coroutine catalogRefresh;
    CloudCatalog.Config catalogConfig;
    bool catalogWarned;

    void Start()
    {
        // 選曲画面に戻ったので、遷移ガードを解除する (#222)。
        // (ガードは static なので、シーンを跨いでも残る。選び直しはここからできる。)
        SelectSceneTransitionGuard.Reset();

        sounds = GetComponent<AudioSource>();
        if (sounds != null) sounds.volume = GManager.effectVolume;

        if (V6UiMode.SelectScreen)
        {
            CreateV6SelectView();
            ConsumeV6EntryRequest();
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
    /// v6 の 3 画面 (選曲 / 履歴 / ランキング) を組み立てる (#221, #223)。
    /// 遷移は既存の経路 (GameScene / TitleScene) をそのまま使う:
    /// Ready は「選択の確定 (CommitReady)」だけを行い、遷移は GameScene() が行う。
    /// 履歴 / ランキングの「戻る」は選曲画面に戻る。「この曲へ」は選曲画面に戻って選択を合わせる。
    /// </summary>
    private void CreateV6SelectView()
    {
        var canvas = FlashBeat.UI.V5.V5GameUi.NewScreenCanvas("V6 Select Canvas");
        var canvasGo = canvas.gameObject;

        v6Data = new V6SelectDataSource();
        v6View = V6SelectView.Create(canvasGo.GetComponent<RectTransform>(), v6Data,
            OnV6Ready, OnV6Back, OnV6OpenSettings);

        // v6 の履歴 / ランキング (03/04)。実データ供給と遷移は既存の経路を使う。
        // onBack は選曲画面への復帰、onGotoSong は選曲画面へ戻って選択を合わせる。
        historyView = FlashBeat.UI.V6.History.V6HistoryView.Create(
            canvasGo.GetComponent<RectTransform>(), OnV6BackFromSub, v6Data, OnV6OpenSettings, OnV6GotoSong);
        rankingView = FlashBeat.UI.V6.Ranking.V6RankingView.Create(
            canvasGo.GetComponent<RectTransform>(), OnV6BackFromSub, v6Data, OnV6OpenSettings, OnV6GotoSong);

        // 初期状態: 選曲画面のみ表示
        SetMode(V6Mode.Select);

        // レガシー (Issue #57 までのシーン配置 UI) を隠す。
        // 名前で 1 個だけ隠すと別 Canvas に乗った要素が残るので、
        // v6 の Canvas 以外の Canvas にあるグラフィックをすべて隠す。
        var legacy = GameObject.Find("Canvas");
        if (legacy != null && legacy != canvasGo) legacy.SetActive(false);
        HideLegacyGraphics(canvasGo);

        // v6 ビュー自身が矢印キー操作を担うので、Unity の自動ナビゲーションは切る。
        FlashBeat.UI.V5.V5GameUi.ApplySingleInputModel(canvas);
    }

    /// <summary>表示モードを切り替える (選曲 / 履歴 / ランキング)。</summary>
    private void SetMode(V6Mode mode)
    {
        currentMode = mode;
        bool isSelect = mode == V6Mode.Select;
        bool isHistory = mode == V6Mode.History;
        bool isRanking = mode == V6Mode.Ranking;

        if (v6View != null) v6View.SetVisible(isSelect);
        if (historyView != null) historyView.SetVisible(isHistory);
        if (rankingView != null) rankingView.SetVisible(isRanking);
    }

    /// <summary>Ready: 選択を確定してから、既存の開始経路でゲーム画面へ。</summary>
    private void OnV6Ready()
    {
        if (v6Data == null) return;

        // 未選択で Ready を押したとき、数据層の既定選択はしない (#222 の契約)。
        // ただし「曲が選択されていません」で詰ませないよう、**この操作のときだけ**
        // 先頭の曲 (クラウド曲よりローカル曲を優先) を選んでから確定する。
        if (v6Data.Selected == null)
        {
            V6SongEntry first = FirstPlayableSong(v6Data);
            if (first != null) v6Data.Select(first.SongId);
        }

        v6Data.CommitReady();
        if (v6Data.HasError) return; // 開始できない理由をデータ側に持たせる (選曲画面に留まる)
        GameScene();
    }

    /// <summary>選曲画面での「戻る」: タイトルへ遷移。</summary>
    private void OnV6Back()
    {
        TitleScene();
    }

    /// <summary>履歴 / ランキング画面での「戻る」: 選曲画面に戻る。</summary>
    private void OnV6BackFromSub()
    {
        SetMode(V6Mode.Select);
    }

    /// <summary>履歴 / ランキングの行から曲へ飛ぶ (選曲画面に戻って選択を合わせる)。</summary>
    private void OnV6GotoSong()
    {
        // 共有データソースの Selected が履歴/ランキング側の操作で既に更新されている。
        // 選曲画面に戻って中央パネルを更新するだけでよい。
        SetMode(V6Mode.Select);
    }

    /// <summary>設定パネルを開く (v6 クイック設定 = 右カラム差し替え)。</summary>
    private void OnV6OpenSettings()
    {
        // History / Ranking 画面の「設定」(フッターボタン / O キー) も有効にする (Issue #239)。
        // クイック設定は選曲画面の右カラム差し替えなので、一度選曲画面へ戻してから開く。
        if (currentMode != V6Mode.Select)
        {
            SetMode(V6Mode.Select);
        }

        if (v6View != null)
        {
            v6View.OpenQuickSettings();
        }
    }

    /// <summary>
    /// Title の History / Ranking タブ (H / R キー) からの入口要求を消費する
    /// (Issue #239)。TitleSceneManager が遷移前に積んだ V6TitleLinks.PendingEntry
    /// に従い、選曲画面の代わりに History / Ranking を初期表示する。
    /// </summary>
    private void ConsumeV6EntryRequest()
    {
        var entry = FlashBeat.UI.V6.Title.V6TitleLinks.PendingEntry;
        FlashBeat.UI.V6.Title.V6TitleLinks.PendingEntry =
            FlashBeat.UI.V6.Title.V6TitleLinks.EntryScreen.None;

        if (v6View == null)
        {
            return; // v6 無効時は History / Ranking の表示先が無い
        }

        if (entry == FlashBeat.UI.V6.Title.V6TitleLinks.EntryScreen.History)
        {
            SetMode(V6Mode.History);
        }
        else if (entry == FlashBeat.UI.V6.Title.V6TitleLinks.EntryScreen.Ranking)
        {
            SetMode(V6Mode.Ranking);
        }
    }

    /// <summary>絞り込み結果の先頭の曲 (演奏できるローカル Prefer 優先)。</summary>
    private static V6SongEntry FirstPlayableSong(IV6SelectDataSource data)
    {
        var songs = data.FilteredSongs;
        if (songs == null) return null;
        V6SongEntry firstLocal = null;
        for (int i = 0; i < songs.Count; i++)
        {
            V6SongEntry entry = songs[i];
            if (entry == null) continue;
            if (!entry.IsCloud) return entry; // ローカル曲を最優先 (オフライン展示でも playable)
            if (firstLocal == null) firstLocal = entry;
        }
        return firstLocal;
    }

    void Update()
    {
        if (view != null)
        {
            view.HandleInput();
        }
        else if (V6UiMode.SelectScreen)
        {
            // v6 モード時: アクティブなビューに入力を振り分ける
            HandleV6Input();
        }
    }

    /// <summary>v6 選曲 / 履歴 / ランキングの入力を処理する。</summary>
    private void HandleV6Input()
    {
        // V6SelectView は自前の Driver で Tick() を回しているため、ここでは触らない。
        // 履歴 / ランキングは Driver がスケール追従のみなので、ここでキーを拾う。

        if (currentMode == V6Mode.History && historyView != null)
        {
            HandleHistoryInput();
        }
        else if (currentMode == V6Mode.Ranking && rankingView != null)
        {
            HandleRankingInput();
        }

        // モード切替キー (選曲画面でのみ有効): L / R キーで左右の画面へ
        // (Tab は検索フォーカス、左右矢印は選択移動に使われるため、L/R を割り当て)
        if (currentMode == V6Mode.Select && v6View != null)
        {
            if (Input.GetKeyDown(KeyCode.L))
            {
                SetMode(V6Mode.History);
            }
            else if (Input.GetKeyDown(KeyCode.R))
            {
                SetMode(V6Mode.Ranking);
            }
        }
        // 履歴 / ランキングから選曲へ戻るのは Escape (TriggerBack) と「この曲へ」で行う。
    }

    private void HandleHistoryInput()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            historyView.MoveSelection(-1);
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            historyView.MoveSelection(1);
        }
        else if (Input.GetKeyDown(KeyCode.S)) // Sort 切替 (ビュー固有の固定ショートカット)
        {
            historyView.ToggleSort();
        }
        else if (KeyActions.IsDown(KeyActions.KeyAction.Back)) // 既定 Esc (#142)
        {
            historyView.TriggerBack();
        }
        else if (KeyActions.IsDown(KeyActions.KeyAction.Option)) // 既定 O (#142)
        {
            historyView.TriggerOpenSettings();
        }
        else if (KeyActions.IsDown(KeyActions.KeyAction.Confirm)
            || Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            historyView.TriggerGotoSong();
        }
    }

    private void HandleRankingInput()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            rankingView.MoveSelection(-1);
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            rankingView.MoveSelection(1);
        }
        else if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.S)) // Source 切替 (Local/Online)。design (S1TZD) の凡例は Tab、S は既存の固定ショートカットとして残す
        {
            rankingView.ToggleSource();
        }
        else if (KeyActions.IsDown(KeyActions.KeyAction.Back)) // 既定 Esc (#142)
        {
            rankingView.TriggerBack();
        }
        else if (KeyActions.IsDown(KeyActions.KeyAction.Option)) // 既定 O (#142)
        {
            rankingView.TriggerOpenSettings();
        }
        else if (KeyActions.IsDown(KeyActions.KeyAction.Confirm)
            || Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            rankingView.TriggerGotoSong();
        }
        else if (Input.GetKeyDown(KeyCode.F5)) // Retry fetch (再試行、ビュー固有の固定ショートカット)
        {
            rankingView.RetryFetch();
        }
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
        // Issue #222: Ready の連打 (クリック / キー連打) で同じ遷移が 2 回走ると
        // シーンの重複ロードや状態 (GManager.ResetSession) の二重化が起こる。
        // 遷移を要求できるのは 1 回だけにする (解除は Start / 開始失敗のとき)。
        if (!SelectSceneTransitionGuard.TryRequest()) return;

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
            SelectSceneTransitionGuard.Reset(); // 開始できなかったので、次の操作を許す
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
            SelectSceneTransitionGuard.Reset(); // 開始できなかったので、次の操作を許す
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
}

/// <summary>
/// 選曲画面からゲーム画面へ移るときの遷移ガード (Issue #222)。
///
/// Ready はマウス・キーの連打で複数回呼び出されうる。ガードが無いと
/// 同じ遷移が 2 回走ってシーンが重複ロードされ、プレイセッションの状態
/// (GManager.ResetSession) も二重にされる。ここでは 1 回の遷移だけを許し、
/// 選曲画面へ戻ったとき (SelectSceneManager.Start) か、開始に失敗したとき
/// に解除する。
///
/// static なのでシーンを跨いで残る。テストから直接呼べるよう、
/// 遷移の可否は TryRequest / Reset だけで操作する。
/// </summary>
public static class SelectSceneTransitionGuard
{
    /// <summary>遷移が既に要求されているか。</summary>
    static bool transitionRequested;

    /// <summary>遷移を要求してよいとき true (要求済みの 2 回目以降は false)。</summary>
    public static bool TryRequest()
    {
        if (transitionRequested) return false;
        transitionRequested = true;
        return true;
    }

    /// <summary>
    /// ガードを解除する (選曲画面に戻ったとき / 開始に失敗したとき)。
    /// 次の Ready で再び遷移できる。
    /// </summary>
    public static void Reset()
    {
        transitionRequested = false;
    }

    /// <summary>ガードが有効な状態か (診断・テスト用)。</summary>
    public static bool IsLocked
    {
        get { return transitionRequested; }
    }
}
