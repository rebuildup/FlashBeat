using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SongData
{
    public int id;
    public string title;
    public string musician;
    public string videoId;
    public int bpm;
    public int level;
    public int totalHits;
    public float duration;

    // Cloud Catalog (#17): set for songs that came from the web API.
    public bool isRemote;
    public string remoteId;
    public int chartVersion;

    // Catalog integrity (#95): note count of the cloud chart (0 when unknown;
    // the local Shit values drift from the real chart, so they are not
    // copied in) and the placeholder rows that must not be playable.
    public int noteCount;
    public bool unplayable;

    public SongData(int id, string title, string musician, string videoId, int bpm, int level, int totalHits, float duration)
    {
        this.id = id;
        this.title = title;
        this.musician = musician;
        this.videoId = videoId;
        this.bpm = bpm;
        this.level = level;
        this.totalHits = totalHits;
        this.duration = duration;
    }
}

public class GManager : MonoBehaviour
{
    public static GManager instance = null;

    /// <summary>scoring v3 の満点 ratio (= MaxRatioV3(noteCount), ADR-0029)。NotesManager が譜面読込時に書く。v4 のスコア計算では使わない (情報用)。</summary>
    public static float maxScore;
    /// <summary>scoring v3/v4 の現在 ratio (判定 + コンボ + ノーミス + 節目 + フルコンボ、情報用)。Judge が毎フレーム書く。</summary>
    public static float ratioScore;

    public static int songID = 1;

    public static readonly string[] SongName = { "noSong", "ロストアンブレラ", "まにまに", "Who", "ツイッターランド", "終焉逃避行", "エウタナシア", "Chartreuse", "Psyched", "Dogbite", "チュートリアル", "狂喜蘭舞", "Poison", "セルフィー", "4th smile", "Calamity", "パノプティコン", "GURU", "阿吽のビーツ", "混沌ブギ", "春嵐", "ビビビビ", "灰Φ倶楽部", "オーバーライド", "イガク", "テレキャスタービーボーイ", "一龠", "ニセモノ", "ニセモノ"};
    public static readonly string[] Musician = { "no", "稲葉曇", "r-906", "Azari", "STEAKA", "柊マグネタイト", "ど～ぱみん", "t+pazolite", "t+pazolite", "t+pazolite", "LeaF", "LeaF", "LeaF", "たぴぼ!!", "LeaF", "LeaF", "r-906", "ジン", "羽生まゐご", "jon-YAKITORY", "john", "フロクロ", "煮ル果実", "吉田夜世", "原口沙輔", "すりぃ", "ァネイロ", "", ""};
    public static readonly string[] SongURL = {
        "VIjqWffacio", "DeKLpgzh-qQ", "9O2VyUM5MlQ", "8JXiXt0D6tw", "e_qQEU_uGjw", "yVi3mhLr0uU", "xqYOI7OD9aE", "5BlSQpejMTw", "3mufQ1Tt844", "3s4y8B6Je-4", "NAeuRhLaqaQ", "s-0HVBCEMZk", "3C5zNU2JCdc", "bUM5erw1vRs", "zKbc-kVdtcI", "n-2GnXKvIOU", "_-Vd0ZGB-lo", "smYLMgfCD5o", "SiqjnFhLq2U", "1Swg-aBO9eY", "pUH9vCsvq08", "sWOvhZBS9IA", "_qj9ftYCNyw", "LLjfal8jCYI", "F38EuG2dAyM", "i-DZukWFR64", "iWzUxFQQAKY", "MD2iO79MPp8", "MD2iO79MPp8"};
    // The numeric arrays have to stay in lockstep with SongName / Musician /
    // SongURL: one entry per song, in the same order. The last entry of the
    // numeric arrays was a leftover of a third cloud catalog placeholder and is
    // commented out (the catalog is built from SongName, so index 29 was never
    // reachable). ChartDataIntegrityTests keeps the four arrays in step.
    public static readonly int[] SBPM = { 100, 274, 174, 128, 142, 147, 127, 180, 150, 195, 95, 176, 120, 195, 140, 200, 174, 138, 206, 95, 140, 175, 143, 102, 170, 182, 176, 128, 120 /*, 120*/ };
    public static readonly int[] Slevel = { 10, 8, 13, 5, 15, 14, 18, 19, 19, 19, 0, 17, 10, 16, 5, 7, 10, 17, 25, 15, 30, 15, 20, 21, 10, 10, 24, 1, 1 /*, 1*/ };
    public static readonly int[] Shit = { 1000, 268, 383, 139, 130, 296, 243, 349, 257, 603, 53, 360, 82, 254, 105, 111, 316, 295, 262, 292, 315, 184, 275, 250, 196, 163, 193, 0, 0 /*, 0*/ };
    public static readonly float[] SongLong = { 10000.0f, 89.0f, 48f, 68f, 44f, 103.0f, 90.0f, 80.0f, 87f, 121f, 69f, 79f, 55f, 80f, 69f, 52f, 123f, 85f, 67f, 86f, 60f, 74f, 75f, 68f, 81f, 69f, 77f, 120.0f, 120.0f /*, 120.0f*/ };

    // Catalog integrity (#95): SongName[27]/[28] are both ニセモノ - leftovers
    // of the cloud catalog placeholders with no chart and no wav. The rows
    // stay in the parallel arrays (deleting them would shift every later
    // index), but they are marked unplayable so they are not selectable.
    // noSong (index 0) is the placeholder and has no chart either.
    static readonly HashSet<string> UnplayableTitles = new HashSet<string> { "noSong", "ニセモノ" };

    public static readonly SongData[] Songs = BuildSongs();

    // Cloud Catalog (#17): the fixed arrays above are the original song list of
    // the game. Issue #823: 実曲の譜面はクラウドカタログ経由でのみ供給
    // されるため、固定行は曲情報 (タイトル・アーティスト・動画 ID) の
    // フォールバックであり、ローカル譜面は持たない。リモートカタログの
    // 曲が取れた行は isRemote = true になり、そちらが実際に再生できる。
    // RemoteSongs is a snapshot built from GET /api/v1/catalog (index 0 = the
    // noSong placeholder, remote songs from index 1); it is null until a
    // catalog fetch succeeds. Everything that shows or plays a song indexes
    // ActiveSongs: the cloud snapshot only *updates* the songs it knows
    // (chart version, chart id and song info), the songs that are not
    // published in the cloud yet keep their local data (ただし譜面は無い)。
    public static SongData[] RemoteSongs;
    public static bool HasRemoteCatalog => RemoteSongs != null && RemoteSongs.Length > 1;

    static SongData[] mergedSongs = Songs;

    public static SongData[] ActiveSongs => mergedSongs;

    public static void ApplyRemoteCatalog(SongData[] remoteSongs)
    {
        RemoteSongs = remoteSongs;
        mergedSongs = Merge(Songs, remoteSongs);
        int remoteCount = remoteSongs == null ? 0 : System.Math.Max(0, remoteSongs.Length - 1);
        Debug.Log($"[GManager] cloud catalog applied: local={Songs.Length} merged={mergedSongs.Length} remote={remoteCount}");

        // Issue #189: the positions of the catalog changed, so the legacy
        // view of the best scores has to be rebuilt from the stable store.
        // Without this the record of a song would still be shown on the slot
        // of another one.
        FlashBeat.Persistence.ScorePersistence.SyncLegacyView();
    }

    public static void ClearRemoteCatalog()
    {
        RemoteSongs = null;
        mergedSongs = Songs;

        // Issue #189: the catalog went back to the built-in table, so the
        // legacy view has to follow.
        FlashBeat.Persistence.ScorePersistence.SyncLegacyView();
    }

    /// <summary>
    /// Merges the cloud catalog into the fixed catalog: every song of the
    /// original data stays, and the songs the cloud knows are replaced by the
    /// cloud version. A cloud song the original data does not know is added at
    /// the end of the list.
    ///
    /// Identity of a song (#95):
    ///  - two cloud entries are the same song only when their **remoteId**
    ///    (the stable id of the web API) is the same. The title and the YouTube
    ///    id are *not* identity: the API publishes easy / hard of the same video
    ///    and different songs with the same title as different songIds, and
    ///    those have to stay separate songs.
    ///  - a cloud entry takes over a built-in song when the title matches (the
    ///    published id is the title in lower case, e.g. "who" for "Who") or,
    ///    failing that, when the YouTube id matches. A built-in song is taken
    ///    over by at most one cloud entry, so two published charts of the same
    ///    song do not collapse into one row.
    /// </summary>
    public static SongData[] Merge(SongData[] localSongs, SongData[] remoteSongs)
    {
        if (localSongs == null || localSongs.Length == 0) return remoteSongs;
        if (remoteSongs == null || remoteSongs.Length <= 1) return localSongs;

        var merged = new SongData[localSongs.Length];
        Array.Copy(localSongs, merged, localSongs.Length);

        // Which rows of the merged catalog are already taken by a cloud entry.
        bool[] claimed = new bool[merged.Length];

        for (int i = 1; i < remoteSongs.Length; i++)
        {
            var remote = remoteSongs[i];
            if (remote == null) continue;

            // The same cloud song (same remoteId) -> the row is updated.
            int known = FindRemoteSong(merged, remote);
            if (known >= 0)
            {
                merged[known] = Overlay(merged[known], remote);
                continue;
            }

            // A built-in song -> the built-in row is updated.
            int local = FindLocalSong(merged, claimed, remote);
            if (local >= 0)
            {
                claimed[local] = true;
                merged[local] = Overlay(merged[local], remote);
                continue;
            }

            // An unknown cloud song -> added at the end of the list.
            merged = Append(merged, remote);
            claimed = Grow(claimed, merged.Length);
            claimed[merged.Length - 1] = true;
        }
        return merged;
    }

    /// <summary>
    /// Index of the cloud row that carries the same cloud song (same remoteId).
    /// Only the remote id is identity - the title and the YouTube id may be
    /// shared by different published songs.
    /// </summary>
    static int FindRemoteSong(SongData[] songs, SongData remote)
    {
        if (string.IsNullOrEmpty(remote.remoteId)) return -1;

        for (int i = 1; i < songs.Length; i++)
        {
            var song = songs[i];
            if (song == null || !song.isRemote) continue;
            if (string.IsNullOrEmpty(song.remoteId)) continue;
            if (string.Equals(song.remoteId, remote.remoteId, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }

    /// <summary>
    /// Index of the built-in song a cloud entry takes over: the title decides
    /// first (the published id is the title in lower case, e.g. "who" for
    /// "Who"), the YouTube id decides when no title matches. A built-in song
    /// that is already taken by another cloud entry is not taken over again.
    /// </summary>
    static int FindLocalSong(SongData[] songs, bool[] claimed, SongData remote)
    {
        for (int i = 1; i < songs.Length; i++)
        {
            var song = songs[i];
            if (song == null || song.isRemote || claimed[i]) continue;
            if (SameSong(song.title, remote.title)) return i;
            if (!string.IsNullOrEmpty(remote.remoteId) && SameSong(song.title, remote.remoteId)) return i;
        }

        if (!string.IsNullOrEmpty(remote.videoId))
        {
            for (int i = 1; i < songs.Length; i++)
            {
                var song = songs[i];
                if (song == null || song.isRemote || claimed[i]) continue;
                if (song.videoId == remote.videoId) return i;
            }
        }
        return -1;
    }

    /// <summary>Grows the "already taken" flags to the length of the catalog.</summary>
    static bool[] Grow(bool[] claimed, int length)
    {
        if (claimed.Length >= length) return claimed;
        var grown = new bool[length];
        Array.Copy(claimed, grown, claimed.Length);
        return grown;
    }

    static bool SameSong(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Cloud version of a song, with the local data as the fallback.</summary>
    static SongData Overlay(SongData local, SongData remote)
    {
        return new SongData(
            local.id,
            string.IsNullOrEmpty(remote.title) ? local.title : remote.title,
            string.IsNullOrEmpty(remote.musician) ? local.musician : remote.musician,
            string.IsNullOrEmpty(remote.videoId) ? local.videoId : remote.videoId,
            remote.bpm > 0 ? remote.bpm : local.bpm,
            remote.level > 0 ? remote.level : local.level,
            remote.totalHits > 0 ? remote.totalHits : local.totalHits,
            remote.duration > 0f ? remote.duration : local.duration)
        {
            isRemote = true,
            remoteId = remote.remoteId,
            chartVersion = remote.chartVersion,
            noteCount = remote.noteCount > 0 ? remote.noteCount : local.noteCount,
        };
    }

    /// <summary>Adds a cloud song the original data does not know.</summary>
    static SongData[] Append(SongData[] songs, SongData remote)
    {
        var grown = new SongData[songs.Length + 1];
        Array.Copy(songs, grown, songs.Length);
        int id = grown.Length - 1;
        grown[id] = new SongData(
            id,
            remote.title,
            remote.musician,
            remote.videoId,
            remote.bpm,
            remote.level,
            remote.totalHits,
            remote.duration)
        {
            isRemote = true,
            remoteId = remote.remoteId,
            chartVersion = remote.chartVersion,
            noteCount = remote.noteCount,
        };
        return grown;
    }

    // Catalog integrity (#95): the parallel arrays have to stay in lockstep
    // with SongName (ChartDataIntegrityTests pins that), but an editing
    // mistake must not take the game down with an IndexOutOfRangeException.
    // These reads return the value when it exists, a zero/empty fallback
    // otherwise, and report the mismatch once per session.
    static bool parallelArrayMismatchLogged;

    public static int ParallelValue(int[] values, int i, string name)
    {
        if (values != null && i >= 0 && i < values.Length) return values[i];
        WarnParallelArrayOnce(values, i, name);
        return 0;
    }

    public static float ParallelValue(float[] values, int i, string name)
    {
        if (values != null && i >= 0 && i < values.Length) return values[i];
        WarnParallelArrayOnce(values, i, name);
        return 0f;
    }

    public static string ParallelValue(string[] values, int i, string name)
    {
        if (values != null && i >= 0 && i < values.Length) return values[i];
        WarnParallelArrayOnce(values, i, name);
        return "";
    }

    static void WarnParallelArrayOnce(Array values, int i, string name)
    {
        if (parallelArrayMismatchLogged) return;
        parallelArrayMismatchLogged = true;
        Debug.LogWarning(
            $"[GManager] parallel array '{name}' is out of step with SongName " +
            $"(length={values?.Length ?? 0}, song index={i}) - the song gets fallback values");
    }

    private static SongData[] BuildSongs()
    {
        var built = new SongData[SongName.Length];
        for (int i = 0; i < SongName.Length; i++)
        {
            built[i] = new SongData(
                i,
                SongName[i],
                ParallelValue(Musician, i, "Musician"),
                ParallelValue(SongURL, i, "SongURL"),
                ParallelValue(SBPM, i, "SBPM"),
                ParallelValue(Slevel, i, "Slevel"),
                ParallelValue(Shit, i, "Shit"),
                ParallelValue(SongLong, i, "SongLong"))
            {
                unplayable = UnplayableTitles.Contains(SongName[i]),
            };
        }

        // Issue #189: the built-in table is the fixed part of the catalog, so
        // its order is a stable identity. The score store uses it to map a
        // song to its stable key, independently of the position the song
        // happens to have in the merged catalog.
        FlashBeat.Persistence.ScorePersistence.BindCatalog(built);

        return built;
    }

    public static float noteSpeed = 10f;
    public static readonly int[] Hiscore = new int[45];

    /// <summary>
    /// The stable key of a song of the catalog (issue #189).
    ///
    /// The best score used to be stored in <see cref="Hiscore"/>, indexed by
    /// the position of the song in the catalog. The catalog is dynamic (the
    /// cloud catalog appends unknown songs, and the API orders the songs by
    /// id, so a song published later can land *before* an existing one), so
    /// the position is not an identity: adding a song moved the record of one
    /// song onto another one.
    ///
    /// The stable key of a song is its **cloud song id** (`remoteId`) or, for
    /// the songs that ship with the game, its slot in the fixed built-in
    /// table (a constant of the game). The key never depends on the order of
    /// the catalog.
    ///
    /// The key also carries the **chart version**: a new chart version is a
    /// new chart, so its best score starts at 0 instead of inheriting the
    /// record of the old one. The old version stays in the store as history.
    /// </summary>
    public static FlashBeat.Persistence.ScoreKey ScoreKeyOf(int catalogIndex)
    {
        SongData song = GetSong(catalogIndex);
        return FlashBeat.Persistence.ScorePersistence.KeyOf(song, catalogIndex);
    }

    /// <summary>
    /// The stable key of a song of the catalog.
    /// </summary>
    public static FlashBeat.Persistence.ScoreKey ScoreKeyOf(SongData song, int catalogIndex)
    {
        return FlashBeat.Persistence.ScorePersistence.KeyOf(song, catalogIndex);
    }

    /// <summary>
    /// The best score of the song at the given position of the catalog, read
    /// from the stable store (issue #189).
    ///
    /// This is the replacement for reading <see cref="Hiscore"/> by index.
    /// The UI still reads the legacy array (it belongs to another owner);
    /// this method is the way forward.
    /// </summary>
    public static int BestScoreOf(int catalogIndex)
    {
        return FlashBeat.Persistence.ScorePersistence.BestOf(catalogIndex);
    }

    /// <summary>
    /// Submit a score for the song at the given position of the catalog.
    ///
    /// Returns true when the record was beaten.
    /// </summary>
    public static bool SubmitScore(int catalogIndex, int score)
    {
        return FlashBeat.Persistence.ScorePersistence.SubmitForCatalogIndex(catalogIndex, score);
    }

    public static bool Start;
    public static float StartTime;
    public static bool played;
    public static int noteTiming = 5;
    public static int totalSong => ActiveSongs.Length - 1;

    public static int combo;
    public static int maxCombo; // festival ranking (#134): longest combo of the current play session
    public static int score;
    public static int perfect;
    public static int great;
    public static int bad;
    public static int miss;

    /// <summary>
    /// scoring v2–v6 (ADR-0028 / ADR-0029 / ADR-0033 / ADR-0035 / ADR-0037)
    /// の加点内訳。Judge が毎フレーム書き、結果画面の内訳表示とローカル
    /// 検証が読む。comboBonus / noMissBonus / milestoneBonus / finalBonus
    /// は送信フィールドには含めない (サーバは P/G/B/M/maxCombo/
    /// comboMilestones から再計算する)。comboMilestones は節目到達回数で、
    /// 送信フィールド comboMilestones と同じ値。
    /// scoring v5 では連続コンボ・ノーミスのボーナスが無く comboBonus /
    /// noMissBonus は常に 0、コンボ由来の獲得済みは milestoneBonus が保持
    /// する (ADR-0035)。scoring v6 (ADR-0037) ではこれらの欄は
    /// **絶対スコア点** を保持する: milestoneBonus = 節目点の合計
    /// (comboMilestones * 5000)、finalBonus = フルクリア点 (25,000)。
    /// </summary>
    public static int comboBonus;
    public static int noMissBonus;
    /// <summary>節目到達回数 (scoring v3–v6)。ランキング送信の comboMilestones。</summary>
    public static int comboMilestones;
    /// <summary>
    /// 節目ボーナス合計。v5 までは ratio points (comboMilestones * 250)、
    /// v6 では絶対スコア点 (comboMilestones * 5000、ADR-0037)。
    /// </summary>
    public static int milestoneBonus;
    /// <summary>
    /// フルコンボ終了ボーナス。v3–v5 は ratio points (miss == 0 かつ全
    /// ノーツ判定確定のとき 100)、v6 は絶対スコア点 (同条件で 25,000、
    /// ADR-0037)。確定は曲終了時に一度だけ (Issue #618)。
    /// </summary>
    public static int finalBonus;

    /// <summary>
    /// scoring v4/v5 (ADR-0033 / ADR-0035) の基礎スコアと加算ボーナス、
    /// および scoring v6 (ADR-0037) の基礎スコアと絶対追加スコア。
    /// score = baseScore + bonusScore で、baseScore は判定由来のみ (上限
    /// 1,000,000)。v4/v5 の bonusScore はボーナス ratio points を
    /// 1,000,000 で正規化した値 (上限 1,000,000)、v6 の bonusScore は
    /// 実プレイで獲得した絶対スコア点 (品質点 + 節目 + フルクリア、
    /// 上限は scoreMaxV6 - 1,000,000)。結果画面の内訳表示が読む。
    /// Judge が FlushScore のたびに書く。
    /// </summary>
    public static int baseScore;
    public static int bonusScore;
    /// <summary>
    /// scoring v4/v5 (ADR-0033 / ADR-0035) のボーナス分母
    /// (Scoring.MaxBonusRatioV4/V5(noteCount))。scoring v6 (ADR-0037)
    /// では追加スコアが絶対点なので、この欄には
    /// <see cref="Scoring.MaxAdditionalV6"/> と同じ値
    /// (<see cref="maxAdditionalScore"/>) が入る。HUD のボーナス獲得点
    /// 表示が ratio points → スコア点の換算に使う。
    /// </summary>
    public static float maxBonusRatio;

    /// <summary>
    /// scoring v6 (ADR-0037) の譜面ごとの追加スコア上限
    /// (Scoring.MaxAdditionalV6(chartNoteCount))。Judge が FlushScore の
    /// たびに書く。v6 のグレード・ゲージ正規化は
    /// <see cref="scoreMaxV6"/> を使う。
    /// </summary>
    public static int maxAdditionalScore;

    /// <summary>
    /// scoring v6 (ADR-0037) の譜面ごとのスコア上限
    /// (Scoring.ScoreMaxV6(chartNoteCount) = 1,000,000 + maxAdditionalScore)。
    /// v6 には固定の合計上限が無いため、ゲージ・グレードはこの値で
    /// 正規化する。Judge が FlushScore のたびに書く。
    /// </summary>
    public static int scoreMaxV6;

    /// <summary>
    /// 全ノーツの判定が確定したか (perfect + great + bad + miss == 譜面
    /// ノーツ数)。Judge が FlushScore のたびに書く。scoring v3 の
    /// フルコンボ終了ボーナスの確定条件で、結果画面の内訳表示もこれを読む
    /// (Issue #618)。
    /// </summary>
    public static bool judgmentsComplete;

    /// <summary>
    /// 現在の譜面のノーツ数 (NotesManager.noteNum)。Judge が FlushScore の
    /// たびに書く。HUD が「次の節目がまだ到達可能か」を判定するのに使う
    /// (ADR-0035)。
    /// </summary>
    public static int chartNoteCount;

    public static float MainSound;
    public static float EffectSound;

    public static int FlashBG = 4;
    public static bool Flash = true;
    public static int FlashT = 0;

    public static float step_time;

    /// <summary>
    /// 確定したノーツ数 (Issue #182)。
    ///
    /// Judge が 1 ノーツ = 1 判定を保証しながら書き換える「正」の値で、
    /// 曲終了判定 (MusicManager → SongEndStateMachine) はこの値を読む。
    /// ノーツ数と等しければ全ノーツ確定済み。
    /// </summary>
    public static int settledNoteCount;

    /// <summary>
    /// 旧 API の互換値 (Issue #182)。<c>1 + settledNoteCount</c> を保つ。
    ///
    /// 「1 + N」 / 「- 1」 という規約は読み手に誤解を招くので、
    /// 曲終了判定などの**新しい**コードは
    /// <see cref="settledNoteCount"/> を直接読むこと。
    /// </summary>
    public static int nextNotes;

    public static float mainVolume = 1.0f;
    public static float effectVolume = 1.0f;
    /// <summary>Non-gameplay BGM volume (Title/Select/Option background
    /// video audio). GameScene video audio follows <see cref="mainVolume"/>
    /// instead — the Option slider labeled BGM therefore does not affect
    /// in-game audio (accepted semantics, issue #651).</summary>
    public static float BGMVolume = 0.3f;
    public static float BGMtime = 0f;

    public static SongData GetSong(int id)
    {
        if (id >= 0 && id < ActiveSongs.Length)
        {
            // Issue #183: プレイセッションで固定した曲は、プレイ中 (GameScene) と
            // 結果 (ResultScene) では開始時の内容 (版を含む) を優先して返す。
            // 選曲画面などでは最新カタログの行を返す。
            SongData pinned = PlaySessionSnapshot.PinnedSongFor(id);
            if (pinned != null) return pinned;
            return ActiveSongs[id];
        }
        return null;
    }

    public static void ResetSession()
    {
        perfect = 0;
        great = 0;
        bad = 0;
        miss = 0;
        score = 0;
        combo = 0;
        maxCombo = 0;
        maxScore = 0;
        ratioScore = 0;
        comboBonus = 0;
        noMissBonus = 0;
        comboMilestones = 0;
        milestoneBonus = 0;
        finalBonus = 0;
        baseScore = 0;
        bonusScore = 0;
        maxBonusRatio = 0f;
        maxAdditionalScore = 0;
        scoreMaxV6 = 0;
        judgmentsComplete = false;
        chartNoteCount = 0;
        settledNoteCount = 0;
        BeginPlaySession();
    }

    /// <summary>
    /// 遷移開始時に 1 プレイの identity を固定する (Issue #183)。
    ///
    /// SelectSceneManager は
    ///   - ローカル曲: GameScene() → ResetSession → Fade
    ///   - リモート曲: 譜面 GET → CloudChartHolder.Hold → ResetSession → Fade
    /// の順で遷移を開始する。ここでは現在の選択行と CloudChartHolder の保持内容
    /// から、このプレイで使う曲と譜面を確定させる。この呼び出し以降にカタログが
    /// 更新されても (ApplyRemoteCatalog / ClearRemoteCatalog) や選択が動いても
    /// (songID)、プレイする譜面・版・曲情報は変わらない。
    /// </summary>
    static void BeginPlaySession()
    {
        SongData row = songID >= 0 && songID < ActiveSongs.Length ? ActiveSongs[songID] : null;

        if (row != null && row.isRemote && !string.IsNullOrEmpty(row.remoteId)
            && CloudChartHolder.TryGetForSong(row.remoteId, out int heldVersion, out string heldJson))
        {
            // リモート曲: 実際に取得した版 (holder の保持版) で固定する。
            // 遷移中のカタログ入れ替えで行の chartVersion が新しくなっても、
            // 取得済みの譜面と版がずれない。
            PlaySessionSnapshot.Begin(row, heldVersion, heldJson);
            return;
        }
        if (row != null)
        {
            // Issue #823: 譜面はクラウドカタログ経由でのみ供給される。
            // リモート化されていない行 (カタログ未取得の固定曲) は
            // ローカル譜面を持たず、譜面 JSON 無しで固定する
            // (NotesManager.ParseChart は null を返し、開始経路は
            // 選曲画面側で既にエラーになる)。
            PlaySessionSnapshot.Begin(row, row.chartVersion, null);
            return;
        }

        // 遷移中にカタログが縮小/初期化されて行が消えた: 固定できる identity が
        // ない。遷移自体の中止は UI 側 (SelectSceneManager) の開始処理で防ぐ。
        PlaySessionSnapshot.Clear();
        Debug.LogWarning($"[GManager] play session cannot be pinned: song row {songID} " +
                         "vanished (the catalog changed) - falling back to the live catalog");
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }
    }
}

