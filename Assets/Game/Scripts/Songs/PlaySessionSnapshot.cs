using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 1 プレイ (GameScene 遷移 1 回 + そのリトライ) の identity (Issue #183)。
///
/// 選曲 → リモート譜面取得 → GameScene 遷移の間にカタログの更新
/// (GManager.ApplyRemoteCatalog / ClearRemoteCatalog) や選択変更
/// (GManager.songID) が入ると、取得済みの譜面と実際に使う曲情報・版がずれる。
/// そこで遷移開始が確定した瞬間 (GManager.ResetSession = 両遷移パスの直前に
/// 呼ばれる) に、
///   - songId (GManager の行インデックス)
///   - chartVersion (実際にプレイする譜面の版)
///   - 曲情報 (SongData) とリモート譜面 JSON のスナップショット
/// をここへ原子的に固定する。GameScene 以降 (Notes / Music / BGFlash /
/// Result / ランキング) は、可変な ActiveSongs[songID] の代わりにこの
/// スナップショットを読む。
///
/// スナップショットは次の遷移開始 (次の ResetSession) まで保持される:
///   - リトライ (GameSceneManager.RetryGame → ResetSession) は同じ内容で
///     再固定するので、同じ譜面をプレイし続ける。
///   - Select へ戻って次に開始したとき (次の ResetSession) に初めて、
///     新しいカタログの版を採用する。
/// </summary>
public sealed class PlaySessionSnapshot
{
    /// <summary>遷移開始時に確定させた曲情報 (複製。chartVersion は確定版で上書き済み)。</summary>
    public SongData Song { get; private set; }

    /// <summary>このプレイで使うリモート譜面の JSON (ローカル譜面のときは null)。</summary>
    public string ChartJson { get; private set; }

    /// <summary>固定した曲の行インデックス (GManager.songID と同じ単位)。</summary>
    public int SongId { get { return Song != null ? Song.id : -1; } }

    /// <summary>固定した譜面の版 (ローカル譜面は 0)。</summary>
    public int ChartVersion { get { return Song != null ? Song.chartVersion : 0; } }

    /// <summary>固定した曲のクラウド上の id (ローカル譜面は null)。</summary>
    public string RemoteId { get { return Song != null ? Song.remoteId : null; } }

    PlaySessionSnapshot(SongData song, string chartJson)
    {
        Song = song;
        ChartJson = chartJson;
    }

    // --- 現在のセッション ----------------------------------------------------

    static PlaySessionSnapshot current;

    /// <summary>いまのプレイセッション (遷移開始時に固定)。なければ null。</summary>
    public static PlaySessionSnapshot Current { get { return current; } }

    /// <summary>
    /// プレイセッションを確定させる。
    ///
    /// song はカタログの行でも呼び出し元が持つ SongData でもよい (内容を複製する
    /// ので、あとからカタログの行が置き換えられても影響を受けない)。
    /// chartVersion には「実際にプレイする譜面の版」を渡す。遷移開始の時点で
    /// カタログの行が新しい版に更新されていても、取得済みの版を渡すことで、
    /// 譜面と曲情報・版のずれが起きない。
    /// </summary>
    public static PlaySessionSnapshot Begin(SongData song, int chartVersion, string chartJson)
    {
        if (song == null) return null;

        SongData pinned = new SongData(
            song.id,
            song.title,
            song.musician,
            song.videoId,
            song.bpm,
            song.level,
            song.totalHits,
            song.duration)
        {
            isRemote = song.isRemote,
            remoteId = song.remoteId,
            chartVersion = chartVersion,
            noteCount = song.noteCount,
            unplayable = song.unplayable,
        };
        current = new PlaySessionSnapshot(pinned, chartJson);
        Debug.Log($"[PlaySession] session fixed: songId={pinned.id} title='{pinned.title}' " +
                  $"remoteId='{pinned.remoteId}' chartVersion={pinned.chartVersion} " +
                  $"chart={(chartJson != null ? "snapshot" : "local resources")}");
        return current;
    }

    /// <summary>セッションを破棄する (固定をやめて現在のカタログに従う)。</summary>
    public static void Clear()
    {
        current = null;
    }

    /// <summary>
    /// GameScene 側が読む曲。プレイセッションが確定していればその曲
    /// (fallbackSongId が動いていても無視する)、なければ現在の選択行
    /// (従来の読み方) を返す。
    /// </summary>
    public static SongData ResolveSong(int fallbackSongId)
    {
        if (current != null && current.Song != null) return current.Song;
        return GManager.GetSong(fallbackSongId);
    }

    /// <summary>
    /// この曲の譜面がスナップショットで固定されているとき、その JSON を返す。
    /// song は ResolveSong が返したインスタンス (開始時に確定した曲) でなければ
    /// ならない。ローカル譜面 (Resources) の曲は固定 JSON を持たない。
    /// </summary>
    public static bool TryGetChartJson(SongData song, out string json)
    {
        if (current != null && current.Song == song && !string.IsNullOrEmpty(current.ChartJson))
        {
            json = current.ChartJson;
            return true;
        }
        json = null;
        return false;
    }

    /// <summary>
    /// 遷移の待ち時間 (フェード中の選択操作など) に書き換わったかもしれない
    /// GManager.songID を、固定した曲の行へ戻す。GameScene の読み込み時に
    /// 呼ぶ。スナップショットがないとき、すでに一致しているとき、固定した行が
    /// 現在のカタログに存在しない (縮小した) ときは何もしない。
    /// </summary>
    public void RestoreSongId()
    {
        if (Song == null || GManager.songID == Song.id) return;
        if (Song.id < 0 || Song.id >= GManager.ActiveSongs.Length) return;
        Debug.LogWarning($"[PlaySession] songID drifted during the transition " +
                         $"({GManager.songID} -> {Song.id}) - restored to the session's song");
        GManager.songID = Song.id;
    }

    // --- GManager.GetSong のスナップショット優先 -------------------------------

    /// <summary>
    /// スナップショットを行の代わりに返してよいシーンか。プレイ中 (GameScene) と
    /// 結果 (ResultScene) では開始時の内容を優先し、選曲画面などでは最新カタログを
    /// 見せる。編集モード (テスト) ではアクティブシーンがないため常に false。
    /// </summary>
    public static bool SnapshotPreferredScene
    {
        get
        {
            string scene = SceneManager.GetActiveScene().name;
            return scene == "GameScene" || scene == "ResultScene";
        }
    }

    /// <summary>行 id の曲がプレイセッションで固定されていれば、その曲を返す。</summary>
    public static SongData PinnedSongFor(int songId)
    {
        return PinnedSongFor(songId, SnapshotPreferredScene);
    }

    /// <summary>テスト用: シーン判定を明示的に渡せる本体。</summary>
    public static SongData PinnedSongFor(int songId, bool snapshotPreferredScene)
    {
        if (!snapshotPreferredScene) return null;
        if (current == null || current.Song == null || current.Song.id != songId) return null;
        return current.Song;
    }
}
