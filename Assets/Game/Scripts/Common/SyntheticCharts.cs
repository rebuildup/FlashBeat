using System.Text;

/// <summary>
/// Issue #823: 権利クリアな合成譜面 fixture。
///
/// 実曲の譜面 JSON は HEAD から撤去し、製品版の譜面はクラウドカタログ
/// 経由でのみ供給される。EditMode テストと実機 AutoTest がオフラインで
/// 譜面を必要とする場合は、このクラスの合成 JSON を
/// <see cref="CloudChartHolder"/> / <see cref="PlaySessionSnapshot"/> 経由で
/// 固定する。
///
/// 制約 (HoldProbeChart と同じ):
///   - GManager のカタログ配列は増やさない。
///   - Assets/Game/Resources/ に譜面 JSON を置かない。
///   - 合成曲はリモートカタログの合成行としてだけ現れ、実在の songId
///     (remoteId) を名乗らない。本番カタログ・ランキングとは混ざらない
///     (remoteId は "synthetic-*" で、サーバ側に存在しない)。
///
/// JSON の形はクラウド譜面の封筒
/// ({"songId":…,"chartVersion":…,"chart":{…}}) に合わせる。
/// NotesManager.ParseChart は固定済み譜面 JSON を必ず
/// CloudCatalog.ParseChartResponse を通すので、素の Data JSON ではなく
/// 封筒の形を渡す。
/// </summary>
public static class SyntheticCharts
{
    /// <summary>合成曲の remoteId 接頭辞 (本番 ID と衝突しない)。</summary>
    public const string RemoteIdPrefix = "synthetic-";

    /// <summary>封筒の chartVersion (テストでは 1 で固定)。</summary>
    public const int ChartVersion = 1;

    // --- JSON 組み立て ----------------------------------------------------

    /// <summary>ノーツ 1 個分の JSON。type: 1=タップ / 2=HOLD。</summary>
    public static string NoteJson(int type, int num, int block, int lpb, int endNum)
    {
        return "{\"type\":" + type + ",\"num\":" + num + ",\"block\":" + block +
               ",\"LPB\":" + lpb + ",\"endNum\":" + endNum + "}";
    }

    /// <summary>タップノーツ 1 個分の JSON (LPB=4, endNum=0)。</summary>
    public static string TapJson(int num, int block, int lpb = 4)
    {
        return NoteJson(1, num, block, lpb, 0);
    }

    /// <summary>
    /// 譜面本体 (Data 相当) の JSON。notesJson / lyricsJson は
    /// 呼び出し側が組み立てた配列要素の連結 (無ければ省略)。
    /// </summary>
    public static string ChartDocJson(int bpm, string name, string notesJson, string lyricsJson = null)
    {
        StringBuilder sb = new StringBuilder(256);
        sb.Append("{\"name\":\"").Append(name).Append("\",");
        sb.Append("\"maxBlock\":8,\"BPM\":").Append(bpm).Append(",\"offset\":0,");
        sb.Append("\"notes\":[").Append(notesJson ?? "").Append("]");
        if (!string.IsNullOrEmpty(lyricsJson))
        {
            sb.Append(",\"lyrics\":").Append(lyricsJson);
        }
        sb.Append("}");
        return sb.ToString();
    }

    /// <summary>歌詞ブロック (EditData.lyrics 相当) の JSON。null の配列は空配列。</summary>
    public static string LyricsJson(string[] startTime, string[] endTime, string[] mondai)
    {
        StringBuilder sb = new StringBuilder(128);
        sb.Append("{\"startTime\":[").Append(JoinNum(startTime)).Append("],");
        sb.Append("\"endTime\":[").Append(JoinNum(endTime)).Append("],");
        sb.Append("\"mondai\":[").Append(JoinCsv(mondai)).Append("],");
        sb.Append("\"furigana\":[").Append(JoinCsv(mondai)).Append("],");
        sb.Append("\"romaji\":[").Append(JoinCsv(mondai)).Append("]}");
        return sb.ToString();
    }

    /// <summary>数値配列 (引用符なし)。JsonUtility は文字列を float へ読まない。</summary>
    static string JoinNum(string[] values)
    {
        return values == null ? "" : string.Join(",", values);
    }

    /// <summary>文字列配列 (各要素を引用符で囲む)。</summary>
    static string JoinCsv(string[] values)
    {
        return values == null || values.Length == 0
            ? ""
            : "\"" + string.Join("\",\"", values) + "\"";
    }

    /// <summary>クラウド譜面の封筒 JSON (GET /api/v1/charts/{id} の応答形)。</summary>
    public static string Envelope(string remoteId, int chartVersion, string chartDocJson)
    {
        return "{\"songId\":\"" + remoteId + "\",\"chartVersion\":" + chartVersion +
               ",\"chart\":" + chartDocJson + "}";
    }

    // --- カタログ / ホルダーへの差し込み ------------------------------------

    /// <summary>
    /// 固定カタログの行を覆うリモート行を作る。Merge はタイトル一致で
    /// ローカル行へ重ねるので、id・曲順はそのまま remoteId 付きになる。
    /// </summary>
    public static SongData RemoteRow(SongData local, string remoteId, int chartVersion, int noteCount)
    {
        return new SongData(
            local.id,
            local.title,
            local.musician,
            local.videoId,
            local.bpm,
            local.level,
            local.totalHits,
            local.duration)
        {
            isRemote = true,
            remoteId = remoteId,
            chartVersion = chartVersion,
            noteCount = noteCount,
        };
    }

    /// <summary>
    /// 指定した固定カタログ行をリモート曲としてカタログへ重ねる
    /// (本番の ApplyRemoteCatalog と同じ経路)。remoteSongs は index 0 が
    /// プレースホルダ、index 1 が対象行。
    /// </summary>
    public static void ApplyRemoteOverlay(SongData local, string remoteId, int chartVersion, int noteCount)
    {
        SongData[] remote = new SongData[2];
        remote[0] = new SongData(0, "noSong", "", "", 0, 0, 0, 0f);
        remote[1] = RemoteRow(local, remoteId, chartVersion, noteCount);
        GManager.ApplyRemoteCatalog(remote);
    }

    /// <summary>合成譜面を CloudChartHolder へ保持させる (遷移前の取得済み状態)。</summary>
    public static void Hold(string remoteId, int chartVersion, string envelopeJson)
    {
        CloudChartHolder.Hold(remoteId, chartVersion, envelopeJson);
    }

    /// <summary>
    /// 合成 fixture が残した状態をすべて戻す。テストの TearDown で呼ぶ。
    /// </summary>
    public static void Reset()
    {
        GManager.ClearRemoteCatalog();
        CloudChartHolder.Clear();
        PlaySessionSnapshot.Clear();
        ChartLyrics.Invalidate();
    }

    // --- 既定 fixture -------------------------------------------------------

    /// <summary>
    /// 歌詞つきタップ譜面 (Option Preview / 歌詞 HUD / 描画 parity 用)。
    /// BPM 120 / LPB 4 → 1 num = 0.125 秒。2 秒刻みで 2 レーンのコード
    /// (i%8 と (i+4)%8) を 15 回、末尾は 30 秒 (ループ長 ≈ 32 秒)。
    /// 同時に複数レーンへノーツがあるので、描画 parity が「同じ mediaTime で
    /// 2 レーン以上にノーツが見える」前提を満たす。歌詞は 3 行で、
    /// 先頭行は従来テストが使っていた「僕を連れてって」(0.946–2.3 秒)。
    /// </summary>
    public static string LyricsChartEnvelope(string remoteId)
    {
        StringBuilder notes = new StringBuilder(1024);
        for (int i = 0; i < 15; i++)
        {
            // 時刻 = num/8 秒 (BPM120 LPB4)。2 秒刻み = num 16 刻み。
            int num = 16 * (i + 1);
            notes.Append(TapJson(num, i % 8)).Append(',')
                 .Append(TapJson(num, (i + 4) % 8));
            if (i < 14) notes.Append(',');
        }
        string lyrics = LyricsJson(
            new[] { "0.946", "5.0", "12.0" },
            new[] { "2.3", "7.0", "14.0" },
            new[] { "僕を連れてって", "ふたつめの行", "みっつめの行" });
        return Envelope(remoteId, ChartVersion,
            ChartDocJson(120, "SyntheticLyrics", notes.ToString(), lyrics));
    }

    /// <summary>歌詞つきタップ譜面のノーツ数 (LyricsChartEnvelope と一致)。</summary>
    public const int LyricsChartNoteCount = 30;

    /// <summary>歌詞なしタップ譜面 (NoLyrics 経路の確認用)。</summary>
    public static string NoLyricsChartEnvelope(string remoteId)
    {
        StringBuilder notes = new StringBuilder(64);
        for (int i = 0; i < 8; i++)
        {
            if (i > 0) notes.Append(',');
            notes.Append(TapJson(16 * (i + 1), i % 8));
        }
        return Envelope(remoteId, ChartVersion,
            ChartDocJson(120, "SyntheticNoLyrics", notes.ToString()));
    }

    /// <summary>
    /// 曲終了ハーネス用のタップ譜面。ノーツは 30 秒まで (HOLD 無し)。
    /// ハーネス曲 (ツイッターランド ≒ 44 秒) の「末尾ノーツ確定 &lt; 曲長−1」
    /// を満たす。
    /// </summary>
    public static string SongEndChartEnvelope(string remoteId)
    {
        StringBuilder notes = new StringBuilder(512);
        for (int i = 0; i < 15; i++)
        {
            if (i > 0) notes.Append(',');
            notes.Append(TapJson(16 * (i + 1), i % 8));
        }
        return Envelope(remoteId, ChartVersion,
            ChartDocJson(120, "SyntheticSongEnd", notes.ToString()));
    }
}
