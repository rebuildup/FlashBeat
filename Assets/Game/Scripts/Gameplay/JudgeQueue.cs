using System;
using System.Collections.Generic;
using UnityEngine;

namespace FlashBeat.Gameplay
{
    /// <summary>
    /// レーンごと・時刻順にノーツを保持する判定キュー (Issue #182)。
    ///
    /// 旧実装は「譜面ファイルの並び順 (編集順)」の単一リストの先頭だけを見て
    /// いたため、編集順と時刻順が食い違うと他レーンの未処理ノーツが
    /// 正常な入力を阻害していた。このクラスはノーツをレーン別に分け、
    /// 各レーン内で時刻順に並べる。入力 1 回は「そのレーンの、押した時刻に
    /// 最も近い未判定ノーツ 1 つ」にだけ効く。同時押しは別レーンの別入力と
    /// して独立に判定される。
    ///
    /// UnityEngine.Input / Time に依存しない純粋クラスなので、EditMode テスト
    /// (Assets/Tests/Editor/JudgeQueueTests.cs) から時刻を明示的に与えて
    /// 直接検証できる。
    /// </summary>
    public class JudgeQueue
    {
        /// <summary>判定対象のノーツ 1 つ分のデータ。</summary>
        public struct NoteEntry
        {
            /// <summary>開始時刻 (メディア時刻, 秒)。</summary>
            public float Time;

            /// <summary>終端時刻 (秒)。タップでは 0。</summary>
            public float EndTime;

            /// <summary>ノーツ種別 (<see cref="LongNote.TYPE_TAP"/> / <see cref="LongNote.TYPE_HOLD"/>)。</summary>
            public int Type;

            /// <summary>レーン番号 (0..7)。</summary>
            public int Lane;

            /// <summary>譜面ファイル上の行番号 (エフェクト表示位置の参照に使う)。</summary>
            public int SourceIndex;
        }

        /// <summary>保持状態の遷移結果。</summary>
        public enum HoldResult
        {
            /// <summary>変化なし (まだ保持中)。</summary>
            None = 0,

            /// <summary>終端の許容幅内で離した (成功)。</summary>
            Released = 1,

            /// <summary>終端より前に離した (中断)。</summary>
            Broken = 2,

            /// <summary>終端をすぎても離さなかった (押し続けたまま → 成功扱い)。</summary>
            Expired = 3,
        }

        private readonly List<NoteEntry>[] pending;
        private readonly NoteEntry?[] holding;

        public JudgeQueue(int laneCount)
        {
            if (laneCount < 1) laneCount = 1;
            pending = new List<NoteEntry>[laneCount];
            for (int i = 0; i < laneCount; i++) pending[i] = new List<NoteEntry>();
            holding = new NoteEntry?[laneCount];
        }

        /// <summary>未判定 (未処理) のノーツ数 (全レーン合計)。保持中のロングノーツは含まない。</summary>
        public int TotalPendingCount
        {
            get
            {
                int count = 0;
                for (int lane = 0; lane < pending.Length; lane++) count += pending[lane].Count;
                return count;
            }
        }

        /// <summary>指定レーンの未判定ノーツ数。</summary>
        public int PendingCount(int lane)
        {
            if (!IsValidLane(lane)) return 0;
            return pending[lane].Count;
        }

        /// <summary>そのレーンでロングノーツを保持中か。</summary>
        public bool IsHolding(int lane)
        {
            return IsValidLane(lane) && holding[lane].HasValue;
        }

        /// <summary>
        /// ノーツをキューに追加する。レーン内では時刻順に並ぶ (Issue #182)。
        /// 譜面ファイルの編集順で追加しても、判定は時刻順に行われる。
        /// 同一時刻のときは追加順 (譜面編集順) を保つ。
        /// </summary>
        public void Add(NoteEntry entry)
        {
            if (!IsValidLane(entry.Lane)) return;

            List<NoteEntry> laneQueue = pending[entry.Lane];
            // 時刻順 (同一時刻は追加順) を保つ位置に挿入する。
            int insertAt = laneQueue.Count;
            for (int i = 0; i < laneQueue.Count; i++)
            {
                if (laneQueue[i].Time > entry.Time)
                {
                    insertAt = i;
                    break;
                }
            }
            laneQueue.Insert(insertAt, entry);
        }

        /// <summary>
        /// 1 入力の押下を判定する (Issue #182)。
        ///
        /// そのレーンの未判定ノーツのうち、押した時刻に最も近いものを
        /// <see cref="LongNote.BAD_WINDOW"/> 内で探す。見つからなければ
        /// 何も起きない (空振りの入力は無視)。ロングノーツを保持中のレーンは
        /// 新しい判定を生まない (押し直しで救済しない — Issue #164)。
        /// </summary>
        public bool TryPress(int lane, float pressTime, float badWindow, out NoteEntry entry, out float lag)
        {
            entry = default;
            lag = 0f;
            if (!IsValidLane(lane)) return false;
            if (holding[lane].HasValue) return false;

            List<NoteEntry> laneQueue = pending[lane];
            int best = -1;
            float bestLag = float.MaxValue;
            for (int i = 0; i < laneQueue.Count; i++)
            {
                float candidateLag = Mathf.Abs(pressTime - laneQueue[i].Time);
                if (candidateLag > badWindow) continue;
                // 時刻順に並んでいるので、最初に見つけた同距離の候補 (より早い
                // 時刻のノーツ) を優先する。
                if (candidateLag < bestLag)
                {
                    bestLag = candidateLag;
                    best = i;
                }
            }
            if (best < 0) return false;

            entry = laneQueue[best];
            lag = bestLag;
            return true;
        }

        /// <summary>タップの判定を確定させてキューから取り除く。</summary>
        public void ConsumeTap(NoteEntry entry)
        {
            if (!IsValidLane(entry.Lane)) return;
            pending[entry.Lane].Remove(entry);
        }

        /// <summary>
        /// ロングノーツの保持を開始する。先頭判定のスコアは呼び出し側
        /// (Judge) が加算済み。ノーツは未判定キューから外れ、終端の離しが
        /// <see cref="UpdateHold"/> で確定するまで保持中として管理される。
        /// </summary>
        public void BeginHold(NoteEntry entry)
        {
            if (!IsValidLane(entry.Lane)) return;
            pending[entry.Lane].Remove(entry);
            holding[entry.Lane] = entry;
        }

        /// <summary>
        /// 保持中のロングノーツの状態を 1 ステップ進める。
        ///
        ///   - キーが離された: 終端の許容幅内なら <see cref="HoldResult.Released"/>
        ///     (成功)、より前なら <see cref="HoldResult.Broken"/> (中断)。
        ///   - キーを押したまま終端 + 許容幅をすぎた: <see cref="HoldResult.Expired"/>
        ///     (押し続けていたので成功扱い)。
        ///
        /// いずれの場合も保持状態は終了し、ノーツは再判定されない
        /// (押し直しでスコアが増えることはない — Issue #164)。
        /// </summary>
        public HoldResult UpdateHold(int lane, float mediaTime, bool keyDown, float greatWindow, out NoteEntry entry)
        {
            entry = default;
            if (!IsValidLane(lane) || !holding[lane].HasValue) return HoldResult.None;

            NoteEntry hold = holding[lane].Value;
            if (!keyDown)
            {
                holding[lane] = null;
                entry = hold;
                // HOLD の判定ルールは LongNote が単一の出所。
                // ここでは再実装しない (テストと実挙動が分離していた既知の不具合を閉じる)。
                return LongNote.IsBroken(mediaTime, hold.EndTime, greatWindow)
                    ? HoldResult.Broken
                    : HoldResult.Released;
            }
            if (LongNote.IsHoldExpired(mediaTime, hold.EndTime, greatWindow))
            {
                holding[lane] = null;
                entry = hold;
                return HoldResult.Expired;
            }
            return HoldResult.None;
        }

        /// <summary>
        /// そのレーンの先頭の未判定ノーツが時刻切れ (Miss) かどうか。
        ///
        /// 開始時刻 + badWindow をすぎても押されなかったノーツ (押されなかった
        /// ロングノーツを含む) を返す。保持中のロングノーツは Miss にならない。
        /// </summary>
        public bool TryPeekMiss(int lane, float mediaTime, float badWindow, out NoteEntry entry)
        {
            entry = default;
            if (!IsValidLane(lane)) return false;

            List<NoteEntry> laneQueue = pending[lane];
            if (laneQueue.Count == 0) return false;

            entry = laneQueue[0];
            return mediaTime > entry.Time + badWindow;
        }

        /// <summary>Miss として確定したノーツをキューから取り除く。</summary>
        public void Remove(NoteEntry entry)
        {
            if (!IsValidLane(entry.Lane)) return;
            pending[entry.Lane].Remove(entry);
        }

        private bool IsValidLane(int lane)
        {
            return lane >= 0 && lane < pending.Length;
        }
    }
}
