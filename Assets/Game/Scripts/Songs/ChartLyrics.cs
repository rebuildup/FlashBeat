using System;
using System.Collections.Generic;
using FlashBeat.UI;
using NoteEditor.DTO;
using UnityEngine;

/// <summary>
/// プレイする曲の歌詞データの解決状態 (Issue #789)。
///
/// 「読み込み未完了」「歌詞データなし確定」「歌詞データあり」を分ける。
/// 空行 (本文が空白の行) や表示区間外はここでは区別しない — それらは
/// <see cref="LyricsTimeline"/> が構築した行の側の状態で、HUD が処理する。
/// </summary>
public enum ChartLyricsState
{
    /// <summary>リモート曲の譜面 JSON がまだ固定されていない (到着待ち)。</summary>
    Pending,

    /// <summary>譜面は確定したが、表示区間を持つ歌詞行が無い (mondai 自体が無いか、startTime 欠落・配列不揃い・再生範囲外で本文を出せない。Issue #846)。</summary>
    NoLyrics,

    /// <summary>譜面に表示区間を持つ歌詞行がある (Issue #846: mondai 非空だけでは不十分)。</summary>
    HasLyrics,
}

/// <summary><see cref="ChartLyrics.Resolve"/> の結果。</summary>
public sealed class ChartLyricsResult
{
    public ChartLyricsState State { get; private set; }

    /// <summary>
    /// 正規化した歌詞データ。Pending のときだけ null。
    /// NoLyrics / HasLyrics では空配列を持つ <see cref="TextData"/> を返す
    /// (「データなし確定」と「未到着」を呼び出し側が null で区別できる)。
    /// </summary>
    public TextData Lyrics { get; private set; }

    public ChartLyricsResult(ChartLyricsState state, TextData lyrics)
    {
        State = state;
        Lyrics = lyrics;
    }
}

/// <summary>
/// プレイする曲の歌詞を、実際にノーツ生成へ使うのと同じ譜面ソースから
/// 解決する正規化ローダー (Issue #789)。
///
/// かつて GameSceneManager はローカル Resources の譜面 JSON しか
/// 読まず、クラウドカタログ曲 (リモート) では常に空の歌詞データを
/// 返していた (Issue #789)。リモート曲の譜面は
/// <see cref="PlaySessionSnapshot"/> / <see cref="CloudChartHolder"/> が
/// JSON ごと固定しており、その中の lyrics ブロックこそが正本なので、
/// ここで同じ優先順 (スナップショット → ホルダー) で読み、状態を
/// 明示的に確定する。Issue #823: ローカル実曲 Resources への
/// フォールバックは撤去した。
///
/// 結果は (プレイセッション, 曲) ごとにキャッシュする。HUD は毎フレーム
/// 参照するため、JSON の再パースはセッションごとに 1 回だけ。
/// </summary>
public static class ChartLyrics
{
    static PlaySessionSnapshot cachedSnapshot;
    static SongData cachedSong;
    static ChartLyricsResult cachedResult;

    /// <summary>
    /// 曲の歌詞データを解決する。song には
    /// <see cref="PlaySessionSnapshot.ResolveSong"/> が返した曲を渡すこと
    /// (スナップショット固定のインスタンスでないと固定 JSON を引けない)。
    /// </summary>
    public static ChartLyricsResult Resolve(SongData song)
    {
        if (song == null)
        {
            return new ChartLyricsResult(ChartLyricsState.Pending, null);
        }

        PlaySessionSnapshot snapshot = PlaySessionSnapshot.Current;
        if (cachedResult != null
            && ReferenceEquals(cachedSnapshot, snapshot)
            && ReferenceEquals(cachedSong, song))
        {
            return cachedResult;
        }

        ChartLyricsResult result = ResolveUncached(song);
        // Pending (リモート譜面の到着待ち) はキャッシュしない。
        // 同じ (セッション, 曲) のまま譜面 JSON が後から届く経路
        // (CloudChartHolder.Hold → GameScene 到達) で、古い Pending が
        // 残り続けて歌詞が永遠に出ない事故を防ぐ。到着後の Resolve は
        // 新しい状態を返してキャッシュされる。
        if (result.State != ChartLyricsState.Pending)
        {
            cachedSnapshot = snapshot;
            cachedSong = song;
            cachedResult = result;
        }
        return result;
    }

    /// <summary>キャッシュを捨てる (テスト・セッション境界で明示的に切る用)。</summary>
    public static void Invalidate()
    {
        cachedSnapshot = null;
        cachedSong = null;
        cachedResult = null;
    }

    static ChartLyricsResult ResolveUncached(SongData song)
    {
        // 譜面 JSON の優先順は NotesManager.ParseChart と同じ:
        // プレイセッションの固定 JSON → CloudChartHolder。
        // Issue #823: ローカル実曲 Resources へのフォールバックは撤去した。
        string chartJson = null;
        bool awaitingRemoteChart = false;
        if (PlaySessionSnapshot.TryGetChartJson(song, out string pinned))
        {
            chartJson = pinned;
        }
        else if (song.isRemote)
        {
            if (CloudChartHolder.TryGet(song.remoteId, song.chartVersion, out string held))
            {
                chartJson = held;
            }
            else
            {
                // リモート曲で譜面未取得 = 開始前の状態。到着待ちとして保留する
                // (ローカル Resources へは落ちない: クラウドカタログ専用)。
                awaitingRemoteChart = true;
            }
        }

        if (chartJson != null)
        {
            return FromChartJson(chartJson, song.duration);
        }

        // リモート曲で譜面未取得 = 開始前の状態。到着待ちとして保留する。
        if (awaitingRemoteChart || song.isRemote)
        {
            return new ChartLyricsResult(ChartLyricsState.Pending, null);
        }

        // ローカル譜面経路は撤去済み (Issue #823)。クラウド譜面を持たない
        // 曲は歌詞も存在しないものとして確定する (Pending にしない:
        // 到着する譜面が無いので待っても変わらない)。
        return new ChartLyricsResult(ChartLyricsState.NoLyrics, null);
    }

    /// <summary>
    /// 譜面 JSON (API エンベロープ {songId, chartVersion, chart} または
    /// 生のチャート文書) から lyrics ブロックを取り出す。
    /// </summary>
    static ChartLyricsResult FromChartJson(string chartJson, float songDuration)
    {
        LyricsProbe probe = null;
        try
        {
            probe = JsonUtility.FromJson<LyricsProbe>(chartJson);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ChartLyrics] chart JSON parse failed: {e.Message}");
        }

        MusicDTO.LyricsDTO dto = probe != null
            ? (probe.chart != null && probe.chart.lyrics != null
                ? probe.chart.lyrics
                : probe.lyrics)
            : null;
        TextData data = ToTextData(dto);
        // Issue #846: 「mondai がある」だけでは歌詞ありにしない。HUD が本文を
        // 出せるのは LyricsTimeline.Build が構築する表示区間つきの行だけなので、
        // startTime 欠落・配列不揃い・区間が再生範囲と不整合なデータを
        // HasLyrics にすると「枠だけ出て本文が永久に空」になる (v0.2.5 実機指摘)。
        bool has = HasLyricEntries(data)
            && HasDisplayableLyricLines(data, songDuration);
        return new ChartLyricsResult(
            has ? ChartLyricsState.HasLyrics : ChartLyricsState.NoLyrics, data);
    }

    /// <summary>
    /// HUD の本文表示が実際に使うのと同じ変換
    /// (<see cref="LyricsTimeline.Build"/>) で行を構築し、
    /// 「表示区間を持つ行が 1 つ以上あり、かつその区間が再生範囲
    /// (メディア 0〜曲長) と重なる」かを返す。
    /// 曲長が不明 (0 以下) のときは範囲チェックをせず行の有無だけを見る。
    /// </summary>
    static bool HasDisplayableLyricLines(TextData data, float songDuration)
    {
        List<LyricLine> lines = LyricsTimeline.Build(
            data.StartTime, data.EndTime, data.Mondai, null, null);
        if (!LyricsTimeline.HasLines(lines))
        {
            return false;
        }
        if (songDuration <= 0f)
        {
            return true;
        }
        for (int i = 0; i < lines.Count; i++)
        {
            LyricLine line = lines[i];
            // 区間 [Start, End) が再生範囲 [0, songDuration) と重なれば表示可能。
            if (line.End > 0f && line.Start < songDuration)
            {
                return true;
            }
        }
        return false;
    }

    static bool HasLyricEntries(TextData data)
    {
        return data != null
            && data.Mondai != null
            && data.Mondai.Length > 0;
    }

    static TextData ToTextData(MusicDTO.LyricsDTO dto)
    {
        if (dto == null)
        {
            return new TextData
            {
                StartTime = new float[0],
                Furigana = new string[0],
                Mondai = new string[0],
                romaji = new string[0],
                EndTime = new float[0],
            };
        }
        return new TextData
        {
            StartTime = dto.startTime ?? new float[0],
            Furigana = dto.furigana ?? new string[0],
            Mondai = dto.mondai ?? new string[0],
            romaji = dto.romaji ?? new string[0],
            EndTime = dto.endTime ?? new float[0],
        };
    }

    /// <summary>
    /// JsonUtility 用の読み取り専用プローブ。API のエンベロープ
    /// ({songId, chartVersion, chart:{...}}) と生のチャート文書
    /// (ChartJsonOverride 等) の両方を受ける。未知フィールドは無視される。
    /// </summary>
    [Serializable]
    class LyricsProbe
    {
        /// <summary>生チャート文書の lyrics (エンベロープでは null)。</summary>
        public MusicDTO.LyricsDTO lyrics;

        /// <summary>API エンベロープの chart 要素。</summary>
        public ChartNode chart;
    }

    [Serializable]
    class ChartNode
    {
        public MusicDTO.LyricsDTO lyrics;
    }
}
