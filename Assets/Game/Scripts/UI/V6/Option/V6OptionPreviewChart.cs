using System.Collections.Generic;
using FlashBeat.Gameplay;
using UnityEngine;

namespace FlashBeat.UI.V6.Option
{
    /// <summary>
    /// Option 画面 Game Preview (Issue #579 / #677) が流すチャート。
    ///
    /// <see cref="BuildForCurrentSong"/> は選択中の曲
    /// (<see cref="GManager.songID"/>) の実チャートを本編と同じ経路
    /// (<see cref="NotesManager.ParseChart"/>) で読み、時刻系も本編と同じ式
    /// (<see cref="NotesManager.NoteTimeSeconds"/> →
    /// <see cref="NotesManager.NoteTimeWithTiming"/> →
    /// <see cref="ChartTiming.BeatOffset"/>) で揃える。実チャートが無い・
    /// 読めない・<see cref="ChartValidator"/> を通らない場合は、従来の
    /// 決定的な疑似チャート (<see cref="Build"/>) へフォールバックする。
    ///
    /// 疑似チャートは 8 レーンへ決定的な間隔でタップ / ロングノーツを並べ、
    /// <see cref="LoopSeconds"/> 秒周期で折り返す。z への換算は
    /// <see cref="MediaTimeline.NotePositionZ"/> と同じ
    /// (noteTime - mediaTime) * zUnitsPerSecond。
    ///
    /// ループ長は「最も遅いノーツ速度 (1.0) でのアプローチ時間の 2 倍」
    /// より十分長く取る。短いと、折り返しの反対側にある未来のノーツが
    /// 「過去」として巻き戻り、画面途中にポップインする
    /// (速度 1 でアプローチ約 16 秒 → 40 秒で余裕を持たせる)。
    /// </summary>
    public static class V6OptionPreviewChart
    {
        /// <summary>疑似チャートのループ長 (秒)。</summary>
        public const float LoopSeconds = 40f;

        /// <summary>1 ノーツの定義 (時刻はループ先頭からの秒、タップは HoldSeconds = 0)。</summary>
        public struct Note
        {
            /// <summary>レーン (0..7)。</summary>
            public int Lane;
            /// <summary>判定時刻 (ループ内の秒)。NoteTiming 未適用の素の時刻。</summary>
            public float Time;
            /// <summary>ロングノーツの長さ (秒)。0 ならタップ。</summary>
            public float HoldSeconds;
        }

        /// <summary>
        /// 決定的な疑似チャートを生成する。乱数は使わず、レーンごとの
        /// 周期と位相だけで疎密を作る (実行ごとに同じ流れになる)。
        /// </summary>
        public static Note[] Build()
        {
            var notes = new List<Note>(160);
            for (int lane = 0; lane < Judge.LaneCount; lane++)
            {
                // レーンごとに周期と開始位相をずらし、同時押しを避けつつ
                // 全レーンが常にどこかでノーツを流している状態にする。
                float period = 1.6f + 0.12f * lane;
                float offset = 0.30f + 0.45f * lane;
                int index = 0;
                for (float t = offset; t < LoopSeconds; t += period, index++)
                {
                    // 5 個に 1 個をロングノーツにする (長さはレーンで変える)。
                    float hold = index % 5 == 4 ? 1.2f + 0.15f * lane : 0f;
                    notes.Add(new Note
                    {
                        Lane = lane,
                        Time = t,
                        // ループ末尾を越える HOLD は尻が巻き戻るので切り詰める
                        // (見た目だけの疑似チャートなので判定には関係しない)。
                        // 末尾すれすれでは切り詰め結果が負になるので 0 で止める。
                        HoldSeconds = Mathf.Max(0f, Mathf.Min(hold, LoopSeconds - t - 0.1f)),
                    });
                }
            }

            return notes.ToArray();
        }

        /// <summary>
        /// 選択中の曲 (<see cref="GManager.songID"/> / ActiveSongs) の実チャートを
        /// プレビュー用ノーツ列へ変換する (Issue #677)。曲の解決は
        /// <see cref="GManager.GetSong"/> (Option では現在の選択曲。
        /// <see cref="PlaySessionSnapshot.ResolveSong"/> は前プレイの
        /// pinned song を優先し得るため使わない)。読み込み経路は本編
        /// <see cref="NotesManager.ParseChart"/> と同一 (セッション固定
        /// スナップショット → Cloud Chart → Resources)。時刻系も本編と同じ:
        /// <see cref="NotesManager.NoteTimeSeconds"/> →
        /// <see cref="NotesManager.NoteTimeWithTiming"/> (GManager.noteTiming)
        /// → <see cref="ChartTiming.BeatOffset"/>、HOLD 終端は
        /// <see cref="LongNote.EndSeconds"/>。
        ///
        /// ループ長は「最後のノーツ/歌詞の終端 + 2 秒」。実チャートが無い曲・
        /// 読み込めない曲では従来の疑似チャート (<see cref="LoopSeconds"/>)
        /// へフォールバックする。戻り値の loopSeconds は必ず全ノーツ時刻より
        /// 大きい (時刻畳み込みの前提)。
        /// </summary>
        public static Note[] BuildForCurrentSong(out float loopSeconds)
        {
            loopSeconds = LoopSeconds;
            // Issue #677 review: Option 画面では「現在の選択曲」を読む。
            // PlaySessionSnapshot.ResolveSong は前プレイの pinned song を
            // 優先し得るため、Game/Result 以外では使わない。
            // GManager.GetSong はシーンに応じて pin を解決する正規経路。
            SongData song = GManager.GetSong(GManager.songID);
            if (song == null)
            {
                return Build();
            }

            Data data = NotesManager.ParseChart(song);
            // ChartValidator を通らない不正譜面は疑似チャートへフォールバック。
            if (data == null || ChartValidator.Validate(data) != null)
            {
                return Build();
            }

            // 本編 NotesManager.LoadRoutine と同じ時間設定の正規化
            // (beatOffset を全ノーツ時刻へ加算する)。
            ChartTiming timing = ChartTiming.Normalize(
                data.fadeOut, data.startTiming, data.beatOffset, song.duration, out _);

            var notes = new List<Note>(data.notes.Length);
            float lastEnd = 0f;
            for (int i = 0; i < data.notes.Length; i++)
            {
                // Data.notes の要素型はグローバル Note (NoteEditor 譜面)。
                // 同名のプレビュー用 struct を避けるため修飾する。
                global::Note source = data.notes[i];
                if (source.block < 0 || source.block >= Judge.LaneCount)
                {
                    continue; // ChartValidator を通らない壊れた譜面はレーン外を描かない
                }

                // Note.Time は「タイミング未適用」の生時刻で保持する。
                // rig 側の DeltaWithTiming / RebuildJudgement が本編と同じ
                // NoteTimeWithTiming を一度だけ適用する (疑似チャート経路と同じ)。
                // BeatOffset だけは譜面データ由来の定数なのでここで畳む。
                float start = NotesManager.NoteTimeSeconds(data.BPM, source) + timing.BeatOffset;
                float hold = 0f;
                if (source.type == LongNote.TYPE_HOLD)
                {
                    float end = LongNote.EndSeconds(data.BPM, source) + timing.BeatOffset;
                    // 本編と同じガード: 終端が開始以前なら 1 拍ぶん伸ばす。
                    if (end <= start)
                    {
                        end = start + (60f / data.BPM);
                    }
                    hold = end - start;
                }

                notes.Add(new Note
                {
                    Lane = source.block,
                    Time = start,
                    HoldSeconds = hold,
                });
                lastEnd = Mathf.Max(lastEnd, start + hold);
            }

            if (notes.Count == 0)
            {
                return Build();
            }

            // ループ末尾の余白: 最後の音が完全に抜けるまで + 歌詞の終端まで。
            float lyricEnd = 0f;
            var lines = V6OptionPreviewLyrics.LinesForCurrentSong();
            for (int i = 0; i < lines.Count; i++)
            {
                lyricEnd = Mathf.Max(lyricEnd, lines[i].End);
            }

            loopSeconds = Mathf.Max(lastEnd, lyricEnd) + 2f;
            return notes.ToArray();
        }

        /// <summary>
        /// メディア時刻 <paramref name="mediaTime"/> に対する、ノーツの
        /// 「最も近い出現」のチャート時刻差 (秒)。
        ///
        /// ループするので差は (-Loop/2, +Loop/2] に畳む。戻り値は
        /// <see cref="MediaTimeline.NotePositionZ"/> の (noteTime - mediaTime)
        /// に相当し、z = 戻り値 * zScale で本編と同じ換算になる。
        /// </summary>
        public static float DeltaToNearest(float chartTime, float mediaTime)
        {
            return DeltaToNearest(chartTime, mediaTime, LoopSeconds);
        }

        /// <summary><see cref="DeltaToNearest(float, float)"/> のループ長指定版。</summary>
        public static float DeltaToNearest(float chartTime, float mediaTime, float loopSeconds)
        {
            float delta = chartTime - mediaTime;
            return delta - loopSeconds * Mathf.Floor((delta + loopSeconds * 0.5f) / loopSeconds);
        }

        /// <summary>
        /// NoteTiming (0.01 秒単位のオフセット) を適用したチャート時刻差。
        /// 本編の <see cref="NotesManager.NoteTimeWithTiming"/> をそのまま使うので、
        /// オプション変更が即座に (毎フレーム) 反映される。
        /// </summary>
        public static float DeltaWithTiming(float chartTime, int noteTiming, float mediaTime)
        {
            return DeltaWithTiming(chartTime, noteTiming, mediaTime, LoopSeconds);
        }

        /// <summary><see cref="DeltaWithTiming(float, int, float)"/> のループ長指定版。</summary>
        public static float DeltaWithTiming(float chartTime, int noteTiming, float mediaTime, float loopSeconds)
        {
            float timed = NotesManager.NoteTimeWithTiming(chartTime, noteTiming);
            return DeltaToNearest(timed, mediaTime, loopSeconds);
        }
    }
}
