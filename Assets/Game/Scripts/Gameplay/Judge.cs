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
    /// 判定キュー (Issue #182)。レーンごと・時刻順にノーツを持ち、
    /// 入力 1 回は最も近い未判定ノーツ 1 つにだけ効く。
    /// </summary>
    private JudgeQueue queue;

    /// <summary>スコアの集計 (1 ノーツ = 1 判定を保証する, Issue #164)。</summary>
    private readonly JudgeScoreboard board = new JudgeScoreboard();

    /// <summary>
    /// 保持中のロングノーツの先頭判定 (Issue #164)。途中で離した (中断)
    /// ときにこの判定を BAD へ差し替えるために覚えておく。
    /// </summary>
    private readonly LongNote.Judgement[] holdHead = new LongNote.Judgement[LaneKeys.Length];

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

        EnsureQueue();
        if (queue == null) return;

        // 時刻の基準は既存方式を維持する: Time.time と GManager.StartTime から
        // メディア時刻 (譜面上の秒) を出す。譜面の時刻はメディア時刻なので、
        // 判定はすべてこの mediaTime で行う。
        float mediaTime = Time.time - GManager.StartTime;

        // 1) 押下判定: レーンごとに独立に処理する (Issue #182)。2 レーンを
        //    同時に叩いても、それぞれのレーンのキューが別々に判定する。
        for (int lane = 0; lane < LaneKeys.Length; lane++)
        {
            if (IsLaneKeyPressed(lane))
            {
                PressLane(lane, mediaTime);
            }
        }

        // 2) ロングノーツの保持状態 (Issue #164)。離した / 押し続けた を
        //    毎フレーム見る。加点は先頭の押下で済んでいるので、ここでは
        //    中断の差し替えだけを行う。
        for (int lane = 0; lane < LaneKeys.Length; lane++)
        {
            JudgeQueue.HoldResult result = queue.UpdateHold(lane, mediaTime, IsLaneKeyDown(lane), LongNote.GREAT_WINDOW, out JudgeQueue.NoteEntry holdEnd);
            if (result == JudgeQueue.HoldResult.Broken)
            {
                // 途中で離した: 先頭判定を BAD に差し替える (追加加点しない)。
                Debug.Log("Hold broken");
                board.DowngradeHoldToBad(holdHead[lane]);
                message(2, holdEnd.SourceIndex);
                FlushScore();
            }
            else if (result == JudgeQueue.HoldResult.Released || result == JudgeQueue.HoldResult.Expired)
            {
                // 終端で離した / 押し続けたまま終端をすぎた: 成功。
                // スコアは先頭の押下で加算済みなので、ここでは何も加算しない。
                Debug.Log("Hold clear");
            }
        }

        // 3) 時刻切れ (Miss): レーンごとに見る。他レーンの未処理ノーツは
        //    邪魔にならない (Issue #182)。
        for (int lane = 0; lane < LaneKeys.Length; lane++)
        {
            if (queue.TryPeekMiss(lane, mediaTime, LongNote.BAD_WINDOW, out JudgeQueue.NoteEntry missed))
            {
                queue.Remove(missed);
                Debug.Log("Miss");
                board.ApplyMiss();
                message(3, missed.SourceIndex);
                FlushScore();
            }
        }

        if (fSize > 197) fSize -= 1;
        if (Fsize > 59) Fsize -= 1;

        if (comboText != null) comboText.fontSize = fSize;
        if (scoreText != null) scoreText.fontSize = Fsize;
    }

    /// <summary>
    /// 1 レーンの押下を判定する。入力 1 回は、そのレーンの押した時刻に
    /// 最も近い未判定ノーツ 1 つにだけ効く (Issue #182)。
    /// </summary>
    private void PressLane(int lane, float mediaTime)
    {
        if (!queue.TryPress(lane, mediaTime, LongNote.BAD_WINDOW, out JudgeQueue.NoteEntry hit, out float lag)) return;

        LongNote.Judgement judgement = LongNote.JudgeHead(lag);
        if (judgement == LongNote.Judgement.None) return;

        if (audioHit != null && hitSound != null)
        {
            audioHit.PlayOneShot(hitSound);
        }

        // 先頭の押下で 1 回だけ加点する (Issue #164)。
        board.ApplyHead(judgement);
        message((int)judgement, hit.SourceIndex);

        if (hit.Type == LongNote.TYPE_HOLD)
        {
            // ロングノーツはここでは消さない: 保持が始まり、終端で離したか
            // 途中で離したかが UpdateHold で決まる。
            holdHead[lane] = judgement;
            queue.BeginHold(hit);
        }
        else
        {
            queue.ConsumeTap(hit);
        }

        FlushScore();
    }

    /// <summary>
    /// NotesManager の譜面データから判定キューを 1 度だけ組み立てる。
    ///
    /// ノーツは譜面ファイルの編集順に並んでいることがあるので、キューの側で
    /// レーン別・時刻順に並べ替える (Issue #182)。NotesManager のリストは
    /// この後も書き換えない (エフェクト表示位置の参照に使う)。
    /// </summary>
    private void EnsureQueue()
    {
        if (queue != null) return;
        if (notesManager == null) return;
        if (notesManager.NoteType.Count == 0) return;

        queue = new JudgeQueue(LaneKeys.Length);
        for (int i = 0; i < notesManager.NoteType.Count; i++)
        {
            // 末尾の番兵 (時刻 10000) は判定の対象にしない。
            if (i >= notesManager.noteNum) break;

            int lane = notesManager.LaneNum[i];
            if (lane < 0 || lane >= LaneKeys.Length)
            {
                Debug.LogWarning($"[Judge] note {i} has invalid lane {lane} - skipped");
                continue;
            }

            queue.Add(new JudgeQueue.NoteEntry
            {
                Time = notesManager.NotesTime[i],
                EndTime = i < notesManager.NotesEndTime.Count ? notesManager.NotesEndTime[i] : 0f,
                Type = notesManager.NoteType[i],
                Lane = lane,
                SourceIndex = i,
            });
        }
    }

    /// <summary>そのレーンのキーがこのフレームで押されたか。</summary>
    private static bool IsLaneKeyPressed(int lane)
    {
        if (lane < 0 || lane >= LaneKeys.Length) return false;

        KeyCode[] keys = KeyBindings.GetKeys(lane);
        for (int i = 0; i < keys.Length; i++)
        {
            if (Input.GetKeyDown(keys[i])) return true;
        }
        return false;
    }

    /// <summary>そのレーンのキーが押されているか (保持判定用)。</summary>
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

    /// <summary>
    /// 集計を GManager の static フィールドへ写す。他シーン
    /// (ResultScene など) は従来どおり GManager を読む。
    /// </summary>
    private void FlushScore()
    {
        GManager.ratioScore = board.RatioScore;
        GManager.perfect = board.Perfect;
        GManager.great = board.Great;
        GManager.bad = board.Bad;
        GManager.miss = board.Miss;
        GManager.combo = board.Combo;
        GManager.maxCombo = board.MaxCombo;
        UpdateScore();
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
}
