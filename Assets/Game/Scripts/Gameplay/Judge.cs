using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using FlashBeat.Gameplay;

public class Judge : MonoBehaviour
{
    [SerializeField] private GameObject[] MessageObj;
    [SerializeField] private GameObject[] ParticleObj;
    [SerializeField] private NotesManager notesManager;
    [SerializeField] private TextMeshProUGUI comboText;
    [SerializeField] private TextMeshProUGUI scoreText;

    private AudioSource audioHit;
    [SerializeField] private AudioClip hitSound;

    private int fSize;
    private int Fsize;

    /// <summary>
    /// 保持中のロングノーツ (Issue #164)。
    ///
    /// キーが押されている (または押されていた) ロングノーツのインデックスと
    /// レーンを保持する。離した瞬間にこの表から取り除き、終端で離したか /
    /// 途中で離したかを判定する。
    /// </summary>
    private readonly Dictionary<int, int> heldHolds = new Dictionary<int, int>();

    /// <summary>
    /// 押下判定済みのロングノーツのインデックス集合。
    ///
    /// 保持されなかった場合にも「開始は押された」ことを残す。押し直しで
    /// 救済しないため、押し直しは新しいロングノーツとして扱わない。
    /// </summary>
    private readonly HashSet<int> startedHolds = new HashSet<int>();

    // 正本 (single source of truth): 判定とキーガイド UI の両方がこの配列を参照する
    public static readonly KeyCode[][] LaneKeys = new KeyCode[][]
    {
        new KeyCode[] { KeyCode.Alpha1, KeyCode.Q, KeyCode.A, KeyCode.Z },
        new KeyCode[] { KeyCode.Alpha2, KeyCode.W, KeyCode.S, KeyCode.X },
        new KeyCode[] { KeyCode.Alpha3, KeyCode.E, KeyCode.D, KeyCode.C },
        new KeyCode[] { KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.R, KeyCode.T, KeyCode.F, KeyCode.G, KeyCode.V, KeyCode.B },
        new KeyCode[] { KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Y, KeyCode.U, KeyCode.H, KeyCode.J, KeyCode.N, KeyCode.M },
        new KeyCode[] { KeyCode.Alpha8, KeyCode.I, KeyCode.K, KeyCode.Less, KeyCode.Comma },
        new KeyCode[] { KeyCode.Alpha9, KeyCode.O, KeyCode.L, KeyCode.Greater, KeyCode.Period },
        new KeyCode[] { KeyCode.Alpha0, KeyCode.Equals, KeyCode.Caret, KeyCode.Backslash, KeyCode.P, KeyCode.At, KeyCode.LeftBracket, KeyCode.Semicolon, KeyCode.Colon, KeyCode.RightBracket, KeyCode.Slash, KeyCode.Underscore, KeyCode.Minus, KeyCode.Asterisk, KeyCode.BackQuote }
    };

    void Start()
    {
        fSize = 197;
        Fsize = 59;
        audioHit = GetComponent<AudioSource>();
        if (comboText != null) comboText.text = "0";
        if (scoreText != null) scoreText.text = "0";
        if (audioHit != null) audioHit.volume = GManager.effectVolume;
    }

    void Update()
    {
        if (!GManager.Start) return;

        // 判定対象の窓 (Issue #164)。
        //
        // 保持中のロングノーツはキューに残っている (終端まで消えない)。そのまま
        // だと、保持中のホールドの後ろにあるタップが判定されなくなるので、
        // 保持中のホールドを飛ばして次のノーツから窓を開く。
        int windowStart = FirstCheckableIndex();

        if (windowStart >= 0 && notesManager.NotesTime.Count > windowStart)
        {
            int sameTimeCount = 1;
            float firstTime = notesManager.NotesTime[windowStart];
            int maxCheck = Mathf.Min(8, notesManager.NotesTime.Count - windowStart);

            for (int i = 1; i < maxCheck; i++)
            {
                if (Mathf.Approximately(notesManager.NotesTime[windowStart + i], firstTime))
                {
                    sameTimeCount++;
                }
                else
                {
                    break;
                }
            }

            for (int i = sameTimeCount - 1; i >= 0; i--)
            {
                CheckAll(windowStart + i);
            }
        }

        // ロングノーツの保持状態 (Issue #164)。
        UpdateHolds();

        // 保持中のロングノーツは Miss の判定から外す。保持中に MISS に
        // ならないため (Issue #164)。
        int missTarget = FirstMissableIndex();

        if (missTarget >= 0 && Time.time > notesManager.NotesTime[missTarget] + 0.2f + GManager.StartTime)
        {
message(3, missTarget);
            deleteData(missTarget);
            Debug.Log("Miss");
            GManager.miss++;
            GManager.combo = 0;
        }

        if (fSize > 197) fSize -= 1;
        if (Fsize > 59) Fsize -= 1;

        if (comboText != null) comboText.fontSize = fSize;
        if (scoreText != null) scoreText.fontSize = Fsize;
    }

    /// <summary>
    /// 判定 (押下) の対象になる最初のノーツのインデックス。
    ///
    /// 保持中のロングノーツ (または押下判定済みのロングノーツ) は、保持が
    /// 終わるまで判定の対象から外す。-1 なら判定すべきノーツはない。
    /// </summary>
    private int FirstCheckableIndex()
    {
        for (int i = 0; i < notesManager.NotesTime.Count; i++)
        {
            if (heldHolds.ContainsKey(i) || startedHolds.Contains(i))
            {
                continue;
            }
            return i;
        }
        return -1;
    }

    /// <summary>
    /// Miss 判定の対象になる最初のノーツのインデックス。
    ///
    /// 保持中のロングノーツ (または押下判定済みのロングノーツ) は、保持が
    /// 終わるまでこの判定から外す。-1 なら Miss 判定すべきノーツはない。
    ///
    /// まだ押されていないロングノーツは、開始時刻をすぎた時点でこの判定に
    /// 入る (押されなかったロングノーツは Miss として数える)。
    /// </summary>
    private int FirstMissableIndex()
    {
        for (int i = 0; i < notesManager.NotesTime.Count; i++)
        {
            if (heldHolds.ContainsKey(i) || startedHolds.Contains(i))
            {
                continue;
            }
            if (i < notesManager.NoteType.Count && notesManager.NoteType[i] == LongNote.TYPE_HOLD)
            {
                // まだ押されていないロングノーツは、開始時刻をすぎた瞬間に
                // Miss 判定の対象になる。時刻の比較は下の Miss 判定と同じ
                // 式 (Time.time と StartTime) を使う。
                if (Time.time <= notesManager.NotesTime[i] + LongNote.BAD_WINDOW + GManager.StartTime)
                {
                    continue;
                }
            }
            return i;
        }
        return -1;
    }

    /// <summary>
    /// ロングノーツの保持状態を毎フレーム見る (Issue #164)。
    ///
    /// 押下は <see cref="CheckAll"/> が行うので、ここでは
    ///   - 保持中のロングノーツのキーを離した (中断) とき -> 即 BAD
    ///   - 終端まで保持して離した (成功) とき -> 確定 (スコアは開始で加算済み)
    /// の 2 つだけを見る。保持中の連続評価はしない。
    /// </summary>
    private void UpdateHolds()
    {
        if (heldHolds.Count == 0) return;

        float now = Time.time - GManager.StartTime;

        // 押され続けるキー (GetKey) をレーンごとに調べる。
        // 削除でインデックスがずれるので、大きいインデックスから処理する。
        List<int> released = new List<int>();
        List<int> expired = new List<int>();
        foreach (KeyValuePair<int, int> entry in heldHolds)
        {
            int index = entry.Key;
            int lane = entry.Value;
            if (index >= notesManager.NotesTime.Count) continue;

            if (!IsLaneKeyDown(lane))
            {
                released.Add(index);
                continue;
            }

            // 終端をすぎても離さないまま保持されている場合。放置すると
            // ノーツがキューに残ったままになるので、終端で確定させる
            // (押し続けていたので成功として扱う)。
            if (now > EndTimeOf(index) + LongNote.GREAT_WINDOW)
            {
                expired.Add(index);
            }
        }

        // 大きいインデックスから消すと、処理済みのノーツに影響しない。
        released.Sort((a, b) => b.CompareTo(a));
        expired.Sort((a, b) => b.CompareTo(a));

        // 終端をすぎた保持: 押し続けていたので成功。終端での離しと同じ扱い。
        for (int i = 0; i < expired.Count; i++)
        {
            int index = expired[i];
            if (!heldHolds.ContainsKey(index)) continue;
            heldHolds.Remove(index);
            Debug.Log("Hold clear");
            deleteData(index);
        }

        for (int i = 0; i < released.Count; i++)
        {
            int index = released[i];
            if (!heldHolds.ContainsKey(index)) continue;
            heldHolds.Remove(index);

            float endTime = EndTimeOf(index);
            float releaseTime = Time.time - GManager.StartTime;

            if (LongNote.JudgeRelease(releaseTime, endTime))
            {
                // 終端で離した: 成功。スコアは開始時に加算済みなので加点しない。
                // ノーツはここで消える: 保持が終わったので、Miss 判定の
                // 対象に残してはいけない。
                Debug.Log("Hold clear");
                deleteData(index);
            }
            else
            {
                // 途中で離した: 中断。BAD として確定する。押し直しは救済しない。
                Debug.Log("Hold broken");
                message(2, index);
                GManager.ratioScore += LongNote.SCORE_BAD;
                GManager.bad++;
                GManager.combo++;
                if (GManager.combo > GManager.maxCombo) GManager.maxCombo = GManager.combo;
                UpdateScore();
                deleteData(index);
            }
        }
    }

    /// <summary>ロングノーツの終端時刻 (秒)。</summary>
    private float EndTimeOf(int index)
    {
        if (index < 0 || index >= notesManager.NotesEndTime.Count) return 0f;
        return notesManager.NotesEndTime[index];
    }

    /// <summary>そのレーンのキーが押されているか。</summary>
    private static bool IsLaneKeyDown(int lane)
    {
        if (lane < 0 || lane >= LaneKeys.Length) return false;

        KeyCode[] keys = KeyBindings.GetKeys(lane);
        for (int i = 0; i < keys.Length; i++)
        {
            if (Input.GetKey(keys[i])) return true;
        }
        return false;
    }

    private void Judgement(float timeLag, int numOffset)
    {
        if (audioHit != null && hitSound != null)
        {
            audioHit.PlayOneShot(hitSound);
        }

        // 判定幅と配点は LongNote が正本 (Issue #164)。タップとロングノーツで
        // 同じ幅を使うので、どちらでも同じ結果になる。
        LongNote.Judgement judgement = LongNote.JudgeHead(timeLag);

        if (judgement == LongNote.Judgement.None) return;

        if (judgement == LongNote.Judgement.Perfect)
        {
            Debug.Log("Perfect");
message(0, numOffset);
            GManager.ratioScore += LongNote.SCORE_PERFECT;
            GManager.perfect++;
            GManager.combo++;
            if (GManager.combo > GManager.maxCombo) GManager.maxCombo = GManager.combo; // #134
            deleteData(numOffset);
        }
        else if (judgement == LongNote.Judgement.Great)
        {
            Debug.Log("Great");
message(1, numOffset);
            GManager.ratioScore += LongNote.SCORE_GREAT;
            GManager.great++;
            GManager.combo++;
            if (GManager.combo > GManager.maxCombo) GManager.maxCombo = GManager.combo; // #134
            deleteData(numOffset);
        }
        else if (judgement == LongNote.Judgement.Bad)
        {
            Debug.Log("Bad");
message(2, numOffset);
            GManager.ratioScore += LongNote.SCORE_BAD;
            GManager.bad++;
            GManager.combo++;
            if (GManager.combo > GManager.maxCombo) GManager.maxCombo = GManager.combo; // #134
            deleteData(numOffset);
        }
    }

    private void deleteData(int numOffset)
    {
        if (numOffset < 0 || numOffset >= notesManager.NotesTime.Count) return;

        notesManager.NotesTime.RemoveAt(numOffset);
        notesManager.LaneNum.RemoveAt(numOffset);
        notesManager.NoteType.RemoveAt(numOffset);
        if (numOffset < notesManager.NotesEndTime.Count)
        {
            notesManager.NotesEndTime.RemoveAt(numOffset);
        }
        // 消えたノーツは保持中ではない (Issue #164)。
        heldHolds.Remove(numOffset);
        startedHolds.Remove(numOffset);
        // インデックスがずれたので、保持中の表を補正する。
        ShiftHeldHolds(numOffset);
        UpdateScore();
    }

    /// <summary>
    /// ノーツが削除されたことで、保持中のロングノーツのインデックスがずれる
    /// (Issue #164)。削除位置より後ろのインデックスを 1 ずつ下げる。
    /// </summary>
    private void ShiftHeldHolds(int removedIndex)
    {
        if (heldHolds.Count > 0)
        {
            List<int> moved = new List<int>();
            foreach (KeyValuePair<int, int> entry in heldHolds)
            {
                if (entry.Key > removedIndex)
                {
                    moved.Add(entry.Key);
                }
            }
            for (int i = 0; i < moved.Count; i++)
            {
                int lane = heldHolds[moved[i]];
                heldHolds.Remove(moved[i]);
                heldHolds[moved[i] - 1] = lane;
            }
        }

        if (startedHolds.Count > 0)
        {
            List<int> moved = new List<int>();
            foreach (int index in startedHolds)
            {
                if (index > removedIndex)
                {
                    moved.Add(index);
                }
            }
            for (int i = 0; i < moved.Count; i++)
            {
                startedHolds.Remove(moved[i]);
                startedHolds.Add(moved[i] - 1);
            }
        }
    }

    /// <summary>スコア表示と内部スコアの更新。</summary>
    private void UpdateScore()
    {
        GManager.score = (int)Math.Round(1000000 * Math.Floor(GManager.ratioScore / GManager.maxScore * 1000000) / 1000000);
        fSize = 220;
        Fsize = 67;

        if (comboText != null) comboText.text = GManager.combo.ToString();
        if (scoreText != null) scoreText.text = GManager.score.ToString();
    }

    private void message(int judge, int noteIndex)
    {
        GManager.nextNotes++;
        if (judge >= 0 && judge < MessageObj.Length && MessageObj[judge] != null)
        {
            Instantiate(MessageObj[judge], EffectPosition(notesManager.LaneNum, noteIndex), Quaternion.Euler(45, 0, 0));
        }
    }

    /// <summary>
    /// World position of the judgement effect for the note that was judged.
    ///
    /// The effect has to appear on the lane that was actually hit. The lane of
    /// the first pending note is a different one as soon as two notes share a
    /// timestamp (a simultaneous press), so the lane of the judged note is used.
    /// A miss has no judged note and uses the first pending note (index 0).
    /// </summary>
    public static Vector3 EffectPosition(IList<int> laneNum, int noteIndex)
    {
        int lane = 0;
        if (laneNum != null && noteIndex >= 0 && noteIndex < laneNum.Count)
        {
            lane = laneNum[noteIndex];
        }

        // the notes use the same mapping (block - 3.5), see NotesManager.Load
        return new Vector3(lane - 3.5f, 0.76f, 0.15f);
    }

    private void CheckAll(int LaneN)
    {
        if (LaneN < 0 || LaneN >= notesManager.LaneNum.Count) return;

        int targetLane = notesManager.LaneNum[LaneN];
        if (targetLane < 0 || targetLane >= LaneKeys.Length) return;

        KeyCode[] keys = KeyBindings.GetKeys(targetLane);
        for (int i = 0; i < keys.Length; i++)
        {
            if (Input.GetKeyDown(keys[i]))
            {
                float timeLag = Mathf.Abs(Time.time - (notesManager.NotesTime[LaneN] + GManager.StartTime));

                // ロングノーツ (Issue #164): 開始の押下はタップと同じ幅で判定
                // するが、ノーツは消さない。保持を鳴らし、終端まで待ちます。
                if (LaneN < notesManager.NoteType.Count && notesManager.NoteType[LaneN] == LongNote.TYPE_HOLD)
                {
                    JudgementHold(timeLag, LaneN, targetLane);
                    break;
                }

                Judgement(timeLag, LaneN);
                break;
            }
        }
    }

    /// <summary>
    /// ロングノーツの開始時の押下を判定する (Issue #164)。
    ///
    /// タップと同じ幅で判定し、配点もタップと同じ (spec.md R3/R5)。ただし
    /// ノーツはここでは消さない: 保持されてから、終端で離したか途中で離したか
    /// が <see cref="UpdateHolds"/> で決まる。
    ///
    /// 押し直しは救済しない。押下判定は 1 note = 1 回だけ。
    /// </summary>
    private void JudgementHold(float timeLag, int numOffset, int lane)
    {
        if (audioHit != null && hitSound != null)
        {
            audioHit.PlayOneShot(hitSound);
        }

        LongNote.Judgement judgement = LongNote.JudgeHead(timeLag);
        if (judgement == LongNote.Judgement.None) return;

        if (judgement == LongNote.Judgement.Perfect)
        {
            Debug.Log("Perfect");
            message(0, numOffset);
            GManager.ratioScore += LongNote.SCORE_PERFECT;
            GManager.perfect++;
        }
        else if (judgement == LongNote.Judgement.Great)
        {
            Debug.Log("Great");
            message(1, numOffset);
            GManager.ratioScore += LongNote.SCORE_GREAT;
            GManager.great++;
        }
        else
        {
            Debug.Log("Bad");
            message(2, numOffset);
            GManager.ratioScore += LongNote.SCORE_BAD;
            GManager.bad++;
        }

        GManager.combo++;
        if (GManager.combo > GManager.maxCombo) GManager.maxCombo = GManager.combo; // #134

        // 保持中として記録する。押下判定は 1 回だけ (押し直しで救済しない)。
        heldHolds[numOffset] = lane;
        startedHolds.Add(numOffset);

        UpdateScore();
    }
}
