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

    /// <summary>
    /// 描画される判定線 (GameScene "JudgeLine" LineRenderer) のワールド y / z。
    /// シーンの線はローカル座標 (-4, 0.5, 0) → (4, 0.5, 0) の水平線で、
    /// GameObject の Transform が (0, -0.02, 0)・scale (8, 1, 40) なので
    /// ワールドでは y = 0.5 - 0.02 = 0.48 / z = 0 に描かれる
    /// (x 方向は scale 8 で画面全幅へ伸びる)。ノーツが判定時刻に到達する
    /// 面 (z = 0) 上にある (Notes.cs / PlayfieldLaneGuides
    /// の「判定線 z = 0」と同じ面)。判定エフェクトの出現点
    /// (<see cref="EffectPosition"/> = y 0.76 / z 0.15) とは別物 —
    /// あちらは 3D 判定エフェクトが浮かぶ位置で、投影は描画判定線より
    /// 画面上で約 50px (FHD) ずれる (Issue #715)。v6 判定行のタイムラインは
    /// この線を原点に動く (<see cref="FlashBeat.UI.V6.Game.Hud.V6HudLogic.VerdictPoseAt"/>、
    /// Issue #731: 判定線上に出現 → 上昇 → 保持 → 下方退場)。
    /// </summary>
    public const float JudgeLineWorldY = 0.48f;
    public const float JudgeLineWorldZ = 0f;

    [SerializeField] private GameObject[] MessageObj;
    [SerializeField] private GameObject[] ParticleObj;
    [SerializeField] private NotesManager notesManager;
    [SerializeField] private TextMeshProUGUI comboText;
    [SerializeField] private TextMeshProUGUI scoreText;

    private AudioSource audioHit;
    [SerializeField] private AudioClip hitSound;

    /// <summary>
    /// Lane-key click sound (Issue #604). Played once per input frame on any
    /// valid lane-key press — including presses that hit no note — so the
    /// player gets audible feedback for every key press. It shares
    /// <see cref="audioHit"/> (volume = GManager.effectVolume, routed through
    /// the Effect mixer) but uses a different clip so it stays distinguishable
    /// from the judgement hit sound.
    /// </summary>
    [SerializeField] private AudioClip clickSound;

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
    /// 判定確定イベント (Issue #680)。<see cref="message"/> が呼ばれるたびに
    /// (判定, レーン) を積む。v6 HUD のレーン別判定行はこのキューを
    /// ドレインして表示する — シーンルートの判定クローン走査にも
    /// クローン名の規約にも依存しない。
    /// </summary>
    public struct VerdictEvent
    {
        /// <summary>判定種別 (<see cref="LongNote.Judgement"/> の int 値 = MessageObj の index)。</summary>
        public readonly int Judgement;

        /// <summary>判定されたノーツのレーン (0..LaneCount-1)。</summary>
        public readonly int Lane;

        public VerdictEvent(int judgement, int lane)
        {
            Judgement = judgement;
            Lane = lane;
        }
    }

    /// <summary>未消費の判定イベント。HUD が 1 フレームに 1 回ドレインする。</summary>
    private static readonly Queue<VerdictEvent> pendingVerdicts = new Queue<VerdictEvent>();

    /// <summary>未消費の判定イベント数 (テスト・計測用)。</summary>
    public static int PendingVerdictCount
    {
        get { return pendingVerdicts.Count; }
    }

    /// <summary>
    /// 判定イベントを 1 件取り出す。HUD の判定行が毎フレーム呼ぶ。
    /// イベントが無ければ false。
    /// </summary>
    public static bool TryDequeueVerdict(out int judgement, out int lane)
    {
        if (pendingVerdicts.Count == 0)
        {
            judgement = 0;
            lane = 0;
            return false;
        }

        VerdictEvent e = pendingVerdicts.Dequeue();
        judgement = e.Judgement;
        lane = e.Lane;
        return true;
    }

    /// <summary>
    /// 未消費の判定イベントを捨てる。曲のリトライ・シーン遷移・HUD 再構築で
    /// 前のプレイの残りを次の表示へ持ち越さないために使う。
    /// </summary>
    public static void ClearPendingVerdicts()
    {
        pendingVerdicts.Clear();
    }

    /// <summary>
    /// 3D 判定エフェクト (<see cref="MessageObj"/> の Instantiate) を抑制するか。
    ///
    /// v6 HUD のレーン別判定行 (<c>V6UiMode.VerdictRow</c>) が有効な間は
    /// HUD 側が true を書き、判定イベントだけをキューへ積んで見た目の
    /// クローンは作らない (3D フィールドに判定を二重表示しない)。
    /// 既定 false = 従来どおり 3D エフェクトを出す。
    /// </summary>
    public static bool SuppressJudgementEffects;

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

        // シーンをまたいだ未消費イベントと、前のプレイのエフェクト抑制を
        // 引き継がない (HUD が無い経路では従来どおり 3D エフェクトを出す)。
        ClearPendingVerdicts();
        SuppressJudgementEffects = false;

        // 保持中のロングノーツの先頭判定は「まだ無い」状態で始める。
        // 既定値 (0 = Perfect) だと、HOLD を持たないレーンが
        // DowngradeHoldToBad を受けたときに確定数を水増しする。
        Array.Fill(holdHead, LongNote.Judgement.None);
    }

    void Update()
    {
        if (!GManager.Start) return;

        // Issue #224: ポーズ中は判定・スコア加算を行わない。
        // Issue #383 / design §6.2: 終端フェードと「終了」表示の間も受け付けない。
        // Issue #604: クリック音も同じゲート (GameplayInputGate) を使う。
        if (!GameplayInputGate.CanAcceptInput(GManager.Start, PauseGate.IsPaused, SongEndGate.BlocksJudging)) return;

        // クリック音 (Issue #604): 有効なレーンキー押下 (空押しを含む) に
        // 1 フレーム 1 回だけ鳴らす。判定音 (hitSound) とは別クリップ。
        PlayLaneClickSound();

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
            JudgeQueue.HoldResult result = UpdateHoldStep(queue, lane, mediaTime, laneDown(lane), out JudgeQueue.NoteEntry holdEnd);
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
            if (TryJudgeMiss(queue, lane, mediaTime, out JudgeQueue.NoteEntry missed))
            {
                Debug.Log("Miss");
                board.ApplyMiss();
                message(3, missed.SourceIndex);
                FlushScore();
            }
        }
    }

    /// <summary>
    /// レーンキーがこのフレームで押されていればクリック音を 1 回鳴らす
    /// (Issue #604)。
    ///
    /// 押下の検出は判定と同じ <see cref="LanePressedPredicate"/>
    /// (GetKeyDownOrInjected のフレームエッジ) を使うので、HOLD の押しっぱなし
    /// では 1 回しか鳴らず、ポーズや曲終了後には鳴らない (Update のゲート済み)。
    /// 同時押し (複数レーン) でも 1 フレーム 1 回にまとめる: レーンごとに
    /// PlayOneShot すると和音押しで音が重なって割れるため。
    /// </summary>
    private void PlayLaneClickSound()
    {
        if (audioHit == null || clickSound == null) return;

        for (int lane = 0; lane < LaneKeys.Length; lane++)
        {
            if (LanePressedPredicate(lane))
            {
                PlayOneShotClip(clickSound);
                return;
            }
        }
    }

    /// <summary>
    /// <see cref="AudioSource.PlayOneShot"/> の呼び出し点 (Issue #682)。
    /// EditMode テストはサブクラスで差し替え、鳴らしたクリップと回数を
    /// 観測する (<see cref="JudgeStep"/> が判定ステップを引数で受け取るのと
    /// 同じ「テストシーム」の方針)。実機では audioHit へそのまま流し、
    /// audioHit が無いときは何もしない (呼び出し側のガードと同じ)。
    /// </summary>
    protected virtual void PlayOneShotClip(AudioClip clip)
    {
        if (audioHit != null)
        {
            audioHit.PlayOneShot(clip);
        }
    }

    /// <summary>
    /// 1 レーンの押下を判定する。入力 1 回は、そのレーンの押した時刻に
    /// 最も近い未判定ノーツ 1 つにだけ効く (Issue #182)。
    /// </summary>
    private void PressLane(int lane, float mediaTime)
    {
        // 押下 → JudgeHead → BeginHold/ConsumeTap の判定手順は
        // TryJudgePress に集約し、Option プレビュー (Issue #695) と共有する。
        if (!TryJudgePress(queue, lane, mediaTime, out JudgeQueue.NoteEntry hit, out LongNote.Judgement judgement)) return;

        // hitSound と clickSound が同一クリップのときは PlayLaneClickSound が
        // 同じフレームで既に鳴らしているため、ここでは重ねて鳴らさない (issue #672)。
        if (audioHit != null && hitSound != null && hitSound != clickSound)
        {
            PlayOneShotClip(hitSound);
        }

        // 先頭の押下で 1 回だけ加点する (Issue #164)。
        board.ApplyHead(judgement);
        message((int)judgement, hit.SourceIndex);

        if (hit.Type == LongNote.TYPE_HOLD)
        {
            // ロングノーツはここでは消さない: 保持が始まり、終端で離したか
            // 途中で離したかが UpdateHold で決まる。
            holdHead[lane] = judgement;
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

        // Issue #641: Add() は挿入位置を毎回線形スキャンするため、編集順が
        // 時刻順と一致しない譜面では全ノーツ追加で O(n²) のフレームスパイクに
        // なる。一旦リストへ集めて AddRange で一括追加・一括ソートする
        // (並び順は Add の繰り返しと同一: 時刻順・同時刻は追加順)。
        var entries = new List<JudgeQueue.NoteEntry>(notesManager.noteNum);
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

            entries.Add(new JudgeQueue.NoteEntry
            {
                Time = notesManager.NotesTime[i],
                EndTime = i < notesManager.NotesEndTime.Count ? notesManager.NotesEndTime[i] : 0f,
                Type = notesManager.NoteType[i],
                Lane = lane,
                SourceIndex = i,
            });
        }
        queue.AddRange(entries);
    }

    /// <summary>そのレーンのキーがこのフレームで押されたか。</summary>
    private static bool IsLaneKeyPressed(int lane)
    {
        if (lane < 0 || lane >= LaneKeys.Length) return false;

        // KeyBindings.IsLaneKeyDown は実キーのフレームエッジ
        // (GetKeyDownOrInjected) に加えて、EditMode テスト専用の
        // InjectLaneKeyDown (injectedLaneDowns) も見る。
        // 製品ビルドでは injectedLaneDowns は常に空なので、
        // 結果は従来の GetKeyDownOrInjected 走査と同じになる (Issue #682)。
        return KeyBindings.IsLaneKeyDown(lane);
    }

    /// <summary>そのレーンのキーが押されているか (保持判定用)。</summary>
    private static bool IsLaneKeyDown(int lane)
    {
        if (lane < 0 || lane >= LaneKeys.Length) return false;

        // IsLaneKeyHeld は「実キーの押下状態 (Input.GetKey)」と
        // 「検証ハーネスが注入した保持状態 (KeyBindings.InjectKeyHold)」の
        // **両方** を見る。製品ビルドでは注入されないので、
        // 結果は従来と同じ Input.GetKey と同じになる。
        return KeyBindings.IsLaneKeyHeld(lane);
    }

    // --- 判定ステップの共有ロジック (Issue #695) ---
    //
    // JudgeStep (実機) と V6 Option 画面のゲームプレビュー
    // (V6OptionPreviewRig) が同じ「どのノーツを・タップか保持か・
    // ミスか」の決定手順を通るよう、キュー操作だけを行う純粋な
    // static ヘルパーに切り出した。スコア・コンボ・エフェクト・
    // GManager への書き戻しは呼び出し側の責務のまま (プレビューは
    // それらを持たない)。

    /// <summary>
    /// 1 レーンの押下を判定キューへ適用する共通経路。
    /// TryPress (BAD_WINDOW) → JudgeHead → HOLD なら BeginHold /
    /// タップなら ConsumeTap、までを行う。判定が確定しなかった
    /// (窓外・<see cref="LongNote.Judgement.None"/>) ときは false を返し、
    /// キューは変えない。
    /// </summary>
    public static bool TryJudgePress(JudgeQueue queue, int lane, float mediaTime,
        out JudgeQueue.NoteEntry entry, out LongNote.Judgement judgement)
    {
        entry = default;
        judgement = LongNote.Judgement.None;
        if (queue == null) return false;
        if (!queue.TryPress(lane, mediaTime, LongNote.BAD_WINDOW, out entry, out float lag)) return false;

        judgement = LongNote.JudgeHead(lag);
        if (judgement == LongNote.Judgement.None) return false;

        if (entry.Type == LongNote.TYPE_HOLD)
        {
            // ロングノーツはここでは消さない: 保持が始まり、終端で離したか
            // 途中で離したかが UpdateHold で決まる。
            queue.BeginHold(entry);
        }
        else
        {
            queue.ConsumeTap(entry);
        }
        return true;
    }

    /// <summary>
    /// 保持中ロングノーツの状態を 1 ステップ進める共通経路
    /// (許容幅は <see cref="LongNote.GREAT_WINDOW"/> で固定)。
    /// </summary>
    public static JudgeQueue.HoldResult UpdateHoldStep(JudgeQueue queue, int lane,
        float mediaTime, bool keyDown, out JudgeQueue.NoteEntry entry)
    {
        if (queue == null)
        {
            entry = default;
            return JudgeQueue.HoldResult.None;
        }
        return queue.UpdateHold(lane, mediaTime, keyDown, LongNote.GREAT_WINDOW, out entry);
    }

    /// <summary>
    /// 時刻切れ (Miss) の検出とキューからの除去を行う共通経路。
    /// Miss が確定したときだけ true を返す。
    /// </summary>
    public static bool TryJudgeMiss(JudgeQueue queue, int lane, float mediaTime,
        out JudgeQueue.NoteEntry entry)
    {
        entry = default;
        if (queue == null) return false;
        if (!queue.TryPeekMiss(lane, mediaTime, LongNote.BAD_WINDOW, out entry)) return false;

        queue.Remove(entry);
        return true;
    }

    /// <summary>
    /// 集計を GManager の static フィールドへ写す。他シーン
    /// (ResultScene など) は従来どおり GManager を読む。
    /// </summary>
    private void FlushScore()
    {
        GManager.perfect = board.Perfect;
        GManager.great = board.Great;
        GManager.bad = board.Bad;
        GManager.miss = board.Miss;
        GManager.combo = board.Combo;
        GManager.maxCombo = board.MaxCombo;
        GManager.comboMilestones = board.MilestoneCrossings;

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

        // scoring v5 (ADR-0035 / Issue #797): 表示・送信する内訳は判定由来の
        // 基礎スコア (上限 1,000,000) と、その上に独立加算されるボーナス
        // スコア (上限 1,000,000) の 2 段構成。v5 には連続コンボボーナスが
        // 無く、コンボ報酬は節目到達 (comboMilestones) のみで確定する。
        // フルコンボ終了ボーナスは全ノーツの判定が確定した時点 (曲終了の
        // 判定条件と同じ settled >= noteCount) でのみ乗る。途中経過の
        // HUD スコアには含めない (Issue #618)。
        int noteCount = notesManager != null ? notesManager.noteNum : 0;
        bool judgmentsComplete = noteCount > 0 && board.Settled >= noteCount;
        GManager.judgmentsComplete = judgmentsComplete;
        Scoring.V5Breakdown v5 = Scoring.BreakdownV5(
            board.Perfect, board.Great, board.Bad, board.Miss, board.MaxCombo,
            board.MilestoneCrossings, noteCount, judgmentsComplete);
        GManager.ratioScore = v5.RatioScore;
        // v5 に連続コンボボーナスは無い (ADR-0035)。コンボ由来の獲得済み
        // ボーナスは milestoneBonus が保持する。
        GManager.comboBonus = 0;
        GManager.noMissBonus = v5.NoMissBonus;
        GManager.milestoneBonus = v5.MilestoneBonus;
        GManager.finalBonus = v5.FinalBonus;
        GManager.baseScore = v5.BaseScore;
        GManager.bonusScore = v5.BonusScore;
        GManager.maxBonusRatio = v5.MaxBonusRatio;

        // scoring v5 のスコアは整数の厳密な割り算で求める (Issue #631 と
        // 同じ決定的演算、ADR-0035)。GManager.score はこの値が HUD 表示と
        // スコア投稿の両方に使われ、サーバの再計算 (computeUnityScoreV5)
        // と全プラットフォームで一致する。上限は 2,000,000。
        UpdateScore(Scoring.ScoreV5(v5));
    }

    /// <summary>
    /// スコアの計算 (純ロジック)。
    ///
    /// <para>
    /// TEST / PARITY ONLY (Issue #641): production scoring is
    /// <see cref="Scoring.ScoreV4(Scoring.V4Breakdown)"/>, which uses exact
    /// integer arithmetic (Issue #631). This float path remains because the
    /// v1/v2 parity fixtures and the v2 scoring tests
    /// (<c>ParityFixtureTests</c>, <c>ScoringV2Tests</c>,
    /// <c>JudgeLogicTests</c>) pin the legacy rounding against the server
    /// recomputation. Do not call it from gameplay code.
    /// </para>
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

    /// <summary>
    /// スコア表示と内部スコアの更新。
    ///
    /// <paramref name="score"/> は呼び出し側が確定した値
    /// (<see cref="Scoring.ScoreV4(Scoring.V4Breakdown)"/> の整数演算、
    /// Issue #631)。ここで float の ratioScore / maxScore から再計算すると
    /// プラットフォームごとの丸め差が再び入るため、受け取った値をそのまま使う。
    /// </summary>
    private void UpdateScore(int score)
    {
        GManager.score = score;
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
        // Issue #680: HUD の判定行はシーンルートのクローン走査ではなく
        // このイベントキューから (判定, レーン) を受け取る。
        // エフェクトの Instantiate より先に積むので、
        // SuppressJudgementEffects で見た目を止めている間も届く。
        EnqueueVerdict(judge, JudgedLane(noteIndex));

        if (SuppressJudgementEffects) return;

        if (judge >= 0 && judge < MessageObj.Length && MessageObj[judge] != null)
        {
            Instantiate(MessageObj[judge], EffectPosition(notesManager.LaneNum, noteIndex), Quaternion.Euler(45, 0, 0));
        }
    }

    /// <summary>
    /// 判定イベントをキューへ積む。HUD 未配線の経路やテストで
    /// message だけが走り続けてもキューが無制限に育たないよう上限を設ける
    /// (通常は 1 フレームの判定数は数件なので、古い方から捨てる)。
    /// </summary>
    private static void EnqueueVerdict(int judge, int lane)
    {
        const int MaxPendingVerdicts = 256;
        while (pendingVerdicts.Count >= MaxPendingVerdicts)
        {
            pendingVerdicts.Dequeue();
        }

        pendingVerdicts.Enqueue(new VerdictEvent(judge, lane));
    }

    /// <summary>
    /// 判定したノーツのレーン。<see cref="EffectPosition"/> と同じ解決規則
    /// (laneNum が取れなければレーン 0)。
    /// </summary>
    private int JudgedLane(int noteIndex)
    {
        return LaneOfNote(notesManager != null ? notesManager.LaneNum : null, noteIndex);
    }

    /// <summary>
    /// noteIndex のノーツが属するレーン。<see cref="JudgedLane"/> と
    /// <see cref="EffectPosition"/> が共有する解決規則
    /// (laneNum が取れなければレーン 0)。
    /// </summary>
    public static int LaneOfNote(IList<int> laneNum, int noteIndex)
    {
        if (laneNum != null && noteIndex >= 0 && noteIndex < laneNum.Count)
        {
            return laneNum[noteIndex];
        }

        return 0;
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
        int lane = LaneOfNote(laneNum, noteIndex);

        // the notes use the same mapping, see NotesManager.Load
        return new Vector3(LaneWorldX(lane), 0.76f, 0.15f);
    }
}
