using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>
    /// 判定ポップの表示インスタンスプール (Issue #793)。
    ///
    /// 旧実装はレーンごとに 1 つの TMP_Text を持ち、同一レーンで次の判定が
    /// 来ると文字・開始時刻を上書きして前の判定を即座に消していた。
    /// 連打中に判定情報が失われるため、v0.2.3 実機レビューで
    /// 「判定イベント 1 件につき独立した表示インスタンス」が要求された。
    ///
    /// 本プールはレーン × スロット数ぶんの TMP_Text を構築時に全て生成し、
    /// Spawn/Tick で有効・無効を切り替えるだけにする (実行時の
    /// Instantiate/Destroy を発生させない)。同一レーンでスロットが
    /// 尽きた場合は最も古い表示中インスタンスを回収する — 表示数は
    /// 有界で、寿命 <see cref="FlashBeat.UI.V6.Game.Hud.V6HudLogic.VerdictTotalSeconds"/>
    /// を過ぎたものから順に Tick で解放される。
    ///
    /// 位置・透明度・スケールの適用は利用側 (V6HudVerdict / V6GamePreview)
    /// が <see cref="ActiveEntries"/> を走査して行う。タイムラインの
    /// 正本は <see cref="FlashBeat.UI.V6.Game.Hud.V6HudLogic.VerdictPoseAt"/>。
    /// </summary>
    public sealed class V6VerdictPopPool
    {
        /// <summary>レーンあたりの同時表示スロット数。</summary>
        public const int SlotsPerLane = 4;

        /// <summary>1 スロット分の表示インスタンス。</summary>
        public sealed class Entry
        {
            public TextMeshProUGUI Text;
            public RectTransform Rt;
            public int Lane;
            public int Slot;
            /// <summary>Spawn 時の unscaled 時刻。タイムラインの t=0。</summary>
            public float StartTime;
            public bool Active;
        }

        private readonly Entry[][] slots;      // [lane][slot]
        private readonly List<Entry> activeList = new List<Entry>(32);

        /// <summary>
        /// テキスト生成は利用側のファクトリに委ねる (本編 HUD と
        /// Option プレビューでフォント・アンカーが異なるため)。
        /// 生成直後の GameObject はプールが非アクティブ化する。
        /// </summary>
        /// <param name="laneCount">レーン数 (通常 <see cref="Judge.LaneCount"/>)。</param>
        /// <param name="factory">(lane, slot) → 設定済み TMP_Text。</param>
        public V6VerdictPopPool(int laneCount, System.Func<int, int, TextMeshProUGUI> factory)
        {
            slots = new Entry[laneCount][];
            for (int lane = 0; lane < laneCount; lane++)
            {
                slots[lane] = new Entry[SlotsPerLane];
                for (int slot = 0; slot < SlotsPerLane; slot++)
                {
                    var tmp = factory(lane, slot);
                    var entry = new Entry
                    {
                        Text = tmp,
                        Rt = tmp != null ? tmp.rectTransform : null,
                        Lane = lane,
                        Slot = slot,
                        StartTime = 0f,
                        Active = false,
                    };
                    if (tmp != null)
                    {
                        tmp.gameObject.SetActive(false);
                    }
                    slots[lane][slot] = entry;
                }
            }
        }

        /// <summary>表示中エントリの走査用リスト (毎フレーム位置・α・Scale を適用する)。</summary>
        public IReadOnlyList<Entry> ActiveEntries
        {
            get { return activeList; }
        }

        /// <summary>全スロット (レイアウト一括更新用。非表示分も含む)。</summary>
        public IEnumerable<Entry> AllEntries
        {
            get
            {
                for (int lane = 0; lane < slots.Length; lane++)
                {
                    for (int i = 0; i < slots[lane].Length; i++)
                    {
                        yield return slots[lane][i];
                    }
                }
            }
        }

        /// <summary>レーンの全スロット (グリッド更新時の x 座標適用用)。</summary>
        public Entry[] EntriesOfLane(int lane)
        {
            return lane >= 0 && lane < slots.Length ? slots[lane] : null;
        }

        /// <summary>レーンの全スロット数 (= SlotsPerLane)。</summary>
        public int SlotCount
        {
            get { return SlotsPerLane; }
        }

        /// <summary>現在表示中のエントリ数。</summary>
        public int ActiveCount
        {
            get { return activeList.Count; }
        }

        /// <summary>
        /// 判定イベントを表示へ登録する。同一レーンで空きスロットが無い
        /// 場合は最も古い表示中インスタンスを回収して再利用する
        /// (連打が上限を超えても Instantiate/GC は発生しない)。
        /// </summary>
        /// <returns>使用したエントリ。lane が不正なら null。</returns>
        public Entry Spawn(int lane, string label, Color color, float now)
        {
            if (lane < 0 || lane >= slots.Length)
            {
                return null;
            }

            var laneSlots = slots[lane];
            Entry entry = null;

            // 空きスロットを優先。全て使用中なら最も古い表示中を回収する。
            float oldestStart = float.MaxValue;
            Entry oldest = null;
            for (int i = 0; i < laneSlots.Length; i++)
            {
                var e = laneSlots[i];
                if (!e.Active)
                {
                    entry = e;
                    break;
                }
                if (e.StartTime < oldestStart)
                {
                    oldestStart = e.StartTime;
                    oldest = e;
                }
            }

            if (entry == null)
            {
                entry = oldest;
            }

            if (entry == null)
            {
                return null; // レーンのスロット配列が空 (構築異常)
            }

            if (entry.Active)
            {
                // 最古の表示中を回収: activeList から外してから再利用する。
                activeList.Remove(entry);
            }

            entry.Text.text = label ?? string.Empty;
            entry.Text.color = color;
            entry.StartTime = now;
            entry.Active = true;
            entry.Text.gameObject.SetActive(true);
            // 新しい判定ほど前面に描く (同レーン連打での重なり順)。
            entry.Rt.SetAsLastSibling();
            activeList.Add(entry);
            return entry;
        }

        /// <summary>
        /// 寿命を過ぎたエントリを非表示へ戻す。寿命の正本は呼び出し側が
        /// 渡す <see cref="FlashBeat.UI.V6.Game.Hud.V6HudLogic.VerdictTotalSeconds"/>。
        /// </summary>
        public void Tick(float now, float lifetimeSeconds)
        {
            for (int i = activeList.Count - 1; i >= 0; i--)
            {
                var e = activeList[i];
                if (now - e.StartTime >= lifetimeSeconds)
                {
                    e.Active = false;
                    e.Text.gameObject.SetActive(false);
                    activeList.RemoveAt(i);
                }
            }
        }

        /// <summary>表示中の全エントリを解放する (カウンタリセット等)。</summary>
        public void Clear()
        {
            for (int i = 0; i < activeList.Count; i++)
            {
                var e = activeList[i];
                e.Active = false;
                e.Text.gameObject.SetActive(false);
            }
            activeList.Clear();
        }

        /// <summary>
        /// レーンの「最新の表示中エントリ」を返す (監査・テスト用。
        /// 表示中が無ければ null)。
        /// </summary>
        public Entry LatestActiveOnLane(int lane)
        {
            Entry latest = null;
            for (int i = 0; i < activeList.Count; i++)
            {
                var e = activeList[i];
                if (e.Lane == lane && (latest == null || e.StartTime > latest.StartTime))
                {
                    latest = e;
                }
            }
            return latest;
        }
    }
}
