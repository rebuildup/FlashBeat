using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using FlashBeat.Gameplay;

/// <summary>
/// レーンのワールド配置。ノートはレーン i の中心 (LaneWorldX(i), 0.55, z) に置かれ、
/// 判定エフェクトも同じ x を使う (Judge.EffectPosition)。
/// </summary>
public class Judge : MonoBehaviour
{
    /// <summary>レーン数 (Judge.LaneKeys / NotesManager の block と同一)。</summary>
    public const int LaneCount = 8;

    /// <summary>レーン 0 の中心 x。レーン i の中心は LaneWorldX(i)。</summary>
    public const float LaneFirstCenterX = -3.5f;

    /// <summary>隣接レーンの中心間隔 (ワールド単位)。</summary>
    public const float LaneSpacing = 1f;

    /// <summary>レーン i の中心ワールド x (= i - 3.5)。</summary>
    public static float LaneWorldX(int lane)
    {
        return LaneFirstCenterX + lane * LaneSpacing;
    }

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

    /// <summary>
    /// 入力判定用のキャッシュ済みデリゲート (Issue #182 / PR #256 S2)。
    /// 毎フレームのデリゲート生成 (GC 圧力) を避けるため static readonly で保持。
    /// </summary>
    private static readonly Func<int, bool> LanePressedPredicate = IsLaneKeyPressed;
    private static readonly Func<int, bool> LaneDownPredicate = IsLaneKeyDown;

    /// <summary>スコアの集計 (1 ノーツ = 1 判定を保証する, Issue #164)。</summary>
    private readonly JudgeScoreboard board = new JudgeScoreboard();

    /// <summary>
    /// 直近の <see cref="JudgeStep"/> に渡されたメディア時刻 (Issue #182)。
    ///
    /// 実機 (Update) は「GManager.step_time」を渡す。EditMode テストは
    /// private な Update を直接呼んで、**実際に渡された値** が
    /// GManager.step_time と一致するかで確認する (観測点)。
    /// </summary>
    public float LastJudgeMediaTime { get; private set; }

    /// <summary>
    /// 保持中のロングノーツの先頭判定 (Issue #164)。途中で離した (中断)
    /// ときにこの判定を BAD へ差し替えるために覚えておく。
    ///
    /// 初期値は「まだ先頭判定が無い」= <see cref="LongNote.Judgement.None"/>。
    /// 既定値 (0 = Perfect) のままだと、HOLD を持たないレーンが
    /// <see cref="JudgeScoreboard.DowngradeHoldToBad"/> を受けたときに
    /// 確定したノーツ数を水増ししてしまう。
    /// </summary>
    private readonly LongNote.Judgement[] holdHead = CreateHoldHead();

    /// <summary>holdHead の初期配列 (None 埋め)。</summary>
    private static LongNote.Judgement[] CreateHoldHead()
    {
        LongNote.Judgement[] head = new LongNote.Judgement[LaneKeys.Length];
        Array.Fill(head, LongNote.Judgement.None);
        return head;
    }

    // 互換エイリアス: 正本は KeyBindings.DefaultKeys。
    // 判定 (Judge) とキーガイド UI の両方が同じ配列を参照する。
    // ここにもテーブルを書くと UI と判定が静かにずれる (Issue #30 §3)。
    public static readonly KeyCode[][] LaneKeys = KeyBindings.DefaultKeys;

    void Start()
    {
        fSize = 197;
        Fsize = 59;
        audioHit = GetComponent<AudioSource>();
        if (comboText != null) comboText.text = "0";
        if (scoreText != null) scoreText.text = "0";
        if (audioHit != null) audioHit.volume = GManager.effectVolume;

        // 保持中のロングノーツの先頭判定は「まだ無い」状態で始める。
        // 既定値 (0 = Perfect) だと、HOLD を持たないレーンが
        // DowngradeHoldToBad を受けたときに確定数を水増しする。
        Array.Fill(holdHead, LongNote.Judgement.None);
    }

    void Update()
    {
        if (!GManager.Start) return;

        // Issue #224: ポーズ中は判定・スコア加算を行わない。
        if (PauseGate.IsPaused) return;

        // 時刻の基準は既存方式を維持する: GManager.step_time は譜面上の
        // メディア時刻 (秒) なので、判定はすべてこの mediaTime で行う。
        // MusicManager は GManager.step_time を毎フレーム同じ式で更新するので、
        // ポーズ中はそこが固定される — Time.time を直接読むとポーズ中も
        // 判定時刻が進んでしまうため、ここでは GManager.step_time を基準にする。
        JudgeStep(GManager.step_time, LanePressedPredicate, LaneDownPredicate);

        // フォントサイズの縮退はキューが有効なときだけ実行 (main と同じ挙動)。
        // queue == null のときは譜面が無い / 未読み込みなので、フォント縮退をスキップする。
        if (queue != null)
        {
            if (fSize > 197) fSize -= 1;
            if (Fsize > 59) Fsize -= 1;

            if (comboText != null) comboText.fontSize = fSize;
            if (scoreText != null) scoreText.fontSize = Fsize;
        }
    }

    /// <summary>
    /// 判定の 1 ステップ (Issue #182)。
    ///
    /// 時刻と各レーンの入力状態を引数で受け取るので、EditMode テストから
    /// 実機と同じ経路 (EnsureQueue → PressLane → UpdateHold → TryPeekMiss)
    /// をそのまま再現できる。実機 (<see cref="Update"/>) は入力を
    /// UnityEngine.Input から読み、ここには渡さない。
    ///
    /// 1 ノーツ = 1 判定が保たれること:
    ///   - 押下 (タップ / HOLD の先頭) と Miss が「確定したノーツ」を数える。
    ///   - HOLD の中断は先頭判定の差し替えであり、確定数には数えない
    ///     (<see cref="JudgeScoreboard.Settled"/>)。
    ///
    /// virtual なのは、テスト側から判定ステップを差し替えられるようにするため
    /// (Issue #182 の S3)。実機では常にこのメソッドをそのまま使う。
    /// </summary>
    public virtual void JudgeStep(float mediaTime, Func<int, bool> lanePressed, Func<int, bool> laneDown)
    {
        // 渡されたメディア時刻を観測できるようにする (Issue #182 の S3)。
        // 実機の Update は「GManager.step_time」を JudgeStep に渡すので、
        // EditMode テストは private な Update を直接呼んで、
        // ここに残った値が GManager.step_time と一致するかで確認できる。
        LastJudgeMediaTime = mediaTime;

        EnsureQueue();
        if (queue == null) return;

        // 1) 押下判定: レーンごとに独立に処理する (Issue #182)。2 レーンを
        //    同時に叩いても、それぞれのレーンのキューが別々に判定する。
        for (int lane = 0; lane < LaneKeys.Length; lane++)
        {
            if (lanePressed(lane))
            {
                PressLane(lane, mediaTime);
            }
        }

        // 2) ロングノーツの保持状態 (Issue #164)。離した / 押し続けた を
        //    毎フレーム見る。加点は先頭の押下で済んでいるので、ここでは
        //    中断の差し替えだけを行う。
        for (int lane = 0; lane < LaneKeys.Length; lane++)
        {
            JudgeQueue.HoldResult result = queue.UpdateHold(lane, mediaTime, laneDown(lane), LongNote.GREAT_WINDOW, out JudgeQueue.NoteEntry holdEnd);
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
            // GetKeyDownOrInjected は「そのキーが押された (Input)」と「検証ハーネス
            // が注入した (KeyBindings.InjectKeyDown)」の両方を見る。製品ビルドでは
            // 注入されないので、結果は従来と同じ Input.GetKeyDown と同じになる。
            if (KeyBindings.GetKeyDownOrInjected(keys[i])) return true;
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

        // 「確定したノーツ数」を GManager へ書く (Issue #182)。
        //
        // 以前は message() が呼ばれた回数 (= 判定イベント数) を数えていた
        // ため、HOLD を途中で離して BAD に差し替えたノーツは 2 回分として
        // 数えられ、残りノーツ数が実際より早く 0 になっていた
        // (Issue #182 の「1 ノーツ = 1 判定」に反する)。
        // ここでは JudgeScoreboard.Settled (確定したノーツ数) を書く。
        //
        // 正は settledNoteCount (曲終了判定はこの値を直接読む)。
        // nextNotes は旧 API の互換値として「1 + 確定数」を保つ。
        GManager.settledNoteCount = board.Settled;
        GManager.nextNotes = 1 + board.Settled;

        UpdateScore();
    }

    /// <summary>
    /// スコアの計算 (純ロジック)。
    ///
    /// maxScore が 0 (譜面が無いセッション) のときは 0 で割って NaN になり、
    /// (int) キャストすると不定の値 (int.MinValue) になるため、0 を返す。
    /// </summary>
    public static int ComputeScore(float ratioScore, float maxScore)
    {
        if (maxScore <= 0f)
        {
            return 0;
        }

        return (int)Math.Round(1000000 * Math.Floor(ratioScore / maxScore * 1000000) / 1000000);
    }

    /// <summary>スコア表示と内部スコアの更新。</summary>
    private void UpdateScore()
    {
        GManager.score = ComputeScore(GManager.ratioScore, GManager.maxScore);
        fSize = 220;
        Fsize = 67;

        if (comboText != null) comboText.text = GManager.combo.ToString();
        if (scoreText != null) scoreText.text = GManager.score.ToString();
    }

    /// <summary>
    /// 判定エフェクトを出す。
    ///
    /// 「確定したノーツ数」はここでは数えない (数えるのは
    /// <see cref="JudgeScoreboard.Settled"/>)。HOLD の中断ではこの関数が
    /// 2 回目を呼ぶが、同じノーツなので 1 ノーツ = 1 判定を保つ必要がある。
    /// </summary>
    private void message(int judge, int noteIndex)
    {
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

        // the notes use the same mapping, see NotesManager.Load
        return new Vector3(LaneWorldX(lane), 0.76f, 0.15f);
    }
}
