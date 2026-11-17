using System.Collections.Generic;
using FlashBeat.Gameplay;
using UnityEngine;

namespace FlashBeat.UI.V6.Option
{
    /// <summary>
    /// Option 画面 Game Preview (Issue #579) が流す疑似チャート。
    ///
    /// 実際の譜面は読まない (設定画面に曲の文脈は無い)。8 レーンへ
    /// 決定的な間隔でタップ / ロングノーツを並べ、<see cref="LoopSeconds"/>
    /// 秒周期で折り返す。時刻は本編と同じ式
    /// (<see cref="NotesManager.NoteTimeWithTiming"/>) で NoteTiming を
    /// 反映し、z への換算は <see cref="MediaTimeline.NotePositionZ"/> と
    /// 同じ (noteTime - mediaTime) * zUnitsPerSecond。
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
        /// メディア時刻 <paramref name="mediaTime"/> に対する、ノーツの
        /// 「最も近い出現」のチャート時刻差 (秒)。
        ///
        /// ループするので差は (-Loop/2, +Loop/2] に畳む。戻り値は
        /// <see cref="MediaTimeline.NotePositionZ"/> の (noteTime - mediaTime)
        /// に相当し、z = 戻り値 * zScale で本編と同じ換算になる。
        /// </summary>
        public static float DeltaToNearest(float chartTime, float mediaTime)
        {
            float delta = chartTime - mediaTime;
            return delta - LoopSeconds * Mathf.Floor((delta + LoopSeconds * 0.5f) / LoopSeconds);
        }

        /// <summary>
        /// NoteTiming (0.01 秒単位のオフセット) を適用したチャート時刻差。
        /// 本編の <see cref="NotesManager.NoteTimeWithTiming"/> をそのまま使うので、
        /// オプション変更が即座に (毎フレーム) 反映される。
        /// </summary>
        public static float DeltaWithTiming(float chartTime, int noteTiming, float mediaTime)
        {
            float timed = NotesManager.NoteTimeWithTiming(chartTime, noteTiming);
            return DeltaToNearest(timed, mediaTime);
        }
    }
}
