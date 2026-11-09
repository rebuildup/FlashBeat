/// <summary>
/// HOLD (ロングノーツ) 入り譜面の実機検証用データ (Issue #278 / Issue #59)。
///
/// 商品カタログには HOLD 入り譜面が 1 つも無い (Assets/Game/Resources/*.json は
/// すべてタップのみ) ので、HOLD の実装 (Issue #164) を実機で確認する手段が
/// 無かった。このクラスは **ハーネス専用** の譜面を文字列として持つ。
///
/// 制約:
///   - GManager の 7 並列配列 (カタログ) は増やさない。
///   - Assets/Game/Resources/ にも譜面 JSON を置かない (カタログ整合テストが壊れる)。
///   - 谱面は <see cref="PlaySessionSnapshot.ChartJsonOverride"/> 経由で
///     プレイ時にだけ差し込む (通常起動には一切影響しない)。
///
/// JSON の形は **クラウド譜面の封筒** に合わせてある
/// (CloudCatalog.ParseChartResponse が読む
///  {"songId":…,"chartVersion":…,"chart":{…}})。
/// NotesManager.ParseChart は固定済み譜面 JSON を必ず ParseChartResponse を通すので、
/// 素の Data JSON ではなく封筒の形を渡さないと読めない。
///
/// 時刻: BPM 120 / LPB 1 なので 1 num = 0.5 秒 (kankaku = 60/(120*1) = 0.5)。
///   num = 8  →  4.0 秒
///   num = 16 →  8.0 秒
/// 終端 (endNum) も num と同じ単位 (LongNote.EndSeconds = kankaku * endNum)。
///
/// ノーツ (7 個 = タップ 4 + HOLD 3):
///   #0 タップ lane 0  num 8   →  4.0 秒
///   #1 HOLD  lane 1  num 16  →  8.0 秒 / 終端 num 26 → 13.0 秒 (5.0 秒保持)
///   #2 タップ lane 2  num 32  → 16.0 秒
///   #3 HOLD  lane 3  num 40  → 20.0 秒 / 終端 num 48 → 24.0 秒 (4.0 秒保持)
///   #4 タップ lane 4  num 56  → 28.0 秒
///   #5 HOLD  lane 5  num 64  → 32.0 秒 / 終端 num 70 → 35.0 秒 (3.0 秒保持)
///   #6 タップ lane 6  num 76  → 38.0 秒
///
/// 最後のノーツ (38 秒) は再生する曲の長さ (44 秒の曲 → 終了は 43 秒) より前に
/// あるので、曲終了が期限切れ強制完了 (ForcedByDeadline) になることはない。
/// </summary>
public static class HoldProbeChart
{
    /// <summary>ハーネスが差し込む譜面の曲名 (Resources には存在しないので衝突しない)。</summary>
    public const string ChartName = "HoldProbe";

    /// <summary>雲譜面の封筒の中の songId (曲名とは別物)。</summary>
    public const string SongId = "hold-probe";

    /// <summary>封筒の chartVersion。</summary>
    public const int ChartVersion = 1;

    /// <summary>譜面の BPM。</summary>
    public const int Bpm = 120;

    /// <summary>譜面が使うレーン数 (0..7 のうち 0..6)。</summary>
    public const int MaxBlock = 8;

    /// <summary>タップ (type = 1) の個数。</summary>
    public const int TapCount = 4;

    /// <summary>HOLD (type = 2) の個数。</summary>
    public const int HoldCount = 3;

    /// <summary>譜面の総ノーツ数 (タップ + HOLD)。</summary>
    public const int TotalNotes = TapCount + HoldCount;

    /// <summary>
    /// 封筒の中の譜面本体 (Data 相当)。
    /// CloudCatalog.ParseChartResponse に渡すのは <see cref="Json"/> (封筒)。
    /// </summary>
    public const string ChartJson =
        "{\"name\":\"" + ChartName + "\"," +
        "\"maxBlock\":" + MaxBlock + "," +
        "\"BPM\":" + Bpm + "," +
        "\"offset\":0," +
        "\"notes\":[" +
        // 0: タップ lane 0 @ 4.0 秒
        "{\"type\":1,\"num\":8,\"block\":0,\"LPB\":1,\"endNum\":0}," +
        // 1: HOLD lane 1 @ 8.0 秒 → 終端 13.0 秒 (5.0 秒保持)
        "{\"type\":2,\"num\":16,\"block\":1,\"LPB\":1,\"endNum\":26}," +
        // 2: タップ lane 2 @ 16.0 秒
        "{\"type\":1,\"num\":32,\"block\":2,\"LPB\":1,\"endNum\":0}," +
        // 3: HOLD lane 3 @ 20.0 秒 → 終端 24.0 秒 (4.0 秒保持)
        "{\"type\":2,\"num\":40,\"block\":3,\"LPB\":1,\"endNum\":48}," +
        // 4: タップ lane 4 @ 28.0 秒
        "{\"type\":1,\"num\":56,\"block\":4,\"LPB\":1,\"endNum\":0}," +
        // 5: HOLD lane 5 @ 32.0 秒 → 終端 35.0 秒 (3.0 秒保持)
        "{\"type\":2,\"num\":64,\"block\":5,\"LPB\":1,\"endNum\":70}," +
        // 6: タップ lane 6 @ 38.0 秒
        "{\"type\":1,\"num\":76,\"block\":6,\"LPB\":1,\"endNum\":0}" +
        "]}";

    /// <summary>
    /// 差し込み用の譜面 JSON (クラウド譜面の封筒そのまま)。
    /// PlaySessionSnapshot.ChartJsonOverride にそのまま渡す。
    /// </summary>
    public const string Json =
        "{\"songId\":\"" + SongId + "\"," +
        "\"chartVersion\":" + ChartVersion + "," +
        "\"chart\":" + ChartJson +
        "}";
}
