using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FlashBeat.Gameplay;
using FlashBeat.UI.V6.Game.Playfield;

public class Notes : MonoBehaviour
{
    // Time of this note on the chart timeline. The note is created by the
    // NotesManager at z = noteTime * zScale, so the start position carries
    // the time.
    float noteTime;

    /// <summary>
    /// ロングノーツの長さ (秒) (Issue #164)。
    ///
    /// 0 ならタップ。NotesManager が生成時に設定する。
    /// </summary>
    public float holdSeconds;

    // --- v6 プレイフィールド (Issue #219) ---
    // ノーツの移動クロック (noteTime / step_time / zScale) は v5 と同じ。
    // 違うのは見た目だけ: 3D の板の代わりに、v6 遠近法 (V6LaneGeometry) の
    // レーン上を流れる uGUI Image として描く。V6PlayfieldView が無い環境
    // (EditMode テスト等) では従来どおり 3D transform を動かす。

    /// <summary>
    /// ワールド z の 1 秒あたりの移動量 (chart time -> world z)。
    ///
    /// NotesManager はノーツを z = noteTime * zScale に生成し、Notes はその z から
    /// 時刻を復元し、毎フレーム同じ zScale で動かす。3 つが同じ値なので、
    /// ノーツは判定線 (z = 0) に自分の判定時刻ちょうどに着く (Issue #233)。
    ///
    /// 値は <see cref="MediaTimeline.SessionZScale"/> 1 箇所から取る
    /// (生成・復元・移動で別の値を使うとずれるため)。セッションごとに
    /// 確定した値なので、曲中の速度変更でノーツが飛ぶことはない
    /// (PR #263 レビュー S7)。
    /// </summary>
    private static float ZScale
    {
        get { return MediaTimeline.SessionZScale; }
    }

    private int lane;
    private bool v6Attached;
    private Image headImage;
    private Image bodyImage;

    /// <summary>
    /// v6 プレイフィールド (2D) で描かれるノーツ本体の Image。
    /// レンダラが無いので、v6 有効時の可視判定はこれを使う。
    /// (PR #263 レビュー: 3D の Renderer.isVisible だけで数えると
    ///  v6 では常に 0 になる)
    /// </summary>
    public Image HeadImage { get { return headImage; } }
    private MeshRenderer meshRenderer;
    private BoxCollider boxCollider;

    void Start()
    {
        noteTime = MediaTimeline.NoteTimeFromPositionZ(transform.position.z, ZScale);

        // NotesManager が x = block - 3.5 で生成する (Judge.EffectPosition と同じ写像)。
        lane = Mathf.Clamp(Mathf.RoundToInt(transform.position.x - Judge.LaneFirstCenterX), 0, V6LaneGeometry.LaneCount - 1);

        meshRenderer = GetComponentInChildren<MeshRenderer>();
        boxCollider = GetComponent<BoxCollider>();

        // v6 プレイフィールド有効時は 3D のノーツは描かない (最初から隠す)。
        // uGUI のノーツは接続できたフレームから描かれる。
        if (FlashBeat.UI.V6.V6UiMode.Playfield)
        {
            SetMeshVisible(false);
        }

        ApplyHoldLength();
    }

    void Update()
    {
        if (KeyActions.IsDown(KeyActions.KeyAction.Confirm))
        {
            //start = true;
        }

        if (GManager.Start)
        {
            // Issue #224: ポーズ中は譜面を進めない。step_time が固定されていても
            // 明示的にガードする (remaining の再計算と v6 描画更新を止める)。
            if (PauseGate.IsPaused) return;

            // The notes follow the media clock (GManager.step_time), not the frame
            // clock: a buffering stall holds the notes instead of letting them run
            // ahead of the music, and the note reaches the hit line exactly when
            // the media reaches the note time - the same moment the judge uses.
            float remaining = noteTime - GManager.step_time;

            if (!v6Attached)
            {
                if (FlashBeat.UI.V6.V6UiMode.Playfield)
                {
                    // v6 プレイフィールド有効時は 3D には描かない
                    // (旧 3D レーンは隠れるので、3D ノーツだけが中途半端に見える)。
                    SetMeshVisible(false);
                }

                TryAttachV6();
            }

            if (v6Attached)
            {
                UpdateV6Visual(remaining);
            }
            else if (!FlashBeat.UI.V6.V6UiMode.Playfield)
            {
                float z = MediaTimeline.NotePositionZ(noteTime, GManager.step_time, ZScale);
                transform.position = new Vector3(transform.position.x, transform.position.y, z);
            }
        }

    }

    /// <summary>
    /// v6 プレイフィールドへ接続する。3D のメッシュを隠し、代わりに
    /// V6PlayfieldView.NoteLayer の下に head (ノーツ本体) と body (HOLD の体) の
    /// Image を作る。ビューがまだ無ければ何もせず、次のフレームに再試行する。
    /// </summary>
    private void TryAttachV6()
    {
        V6PlayfieldView view = V6PlayfieldView.Current;
        if (view == null || view.NoteLayer == null)
        {
            // ビューはシーン起動時に生成される。作り終わるまで毎フレーム再試行する。
            return;
        }

        // 3D の描画は uGUI に置き換わる (ノーツの判定は時刻だけで行うので
        // コライダーは不要)。
        SetMeshVisible(false);
        if (boxCollider != null && boxCollider.enabled)
        {
            boxCollider.enabled = false;
        }

        // 前回の接続が残っている場合 (ビューが作り直された) は作り直す。
        DestroyNoteImages();

        headImage = CreateNoteImage(view.NoteLayer, "Head");
        bodyImage = holdSeconds > 0f ? CreateNoteImage(view.NoteLayer, "Body") : null;
        v6Attached = true;
    }

    /// <summary>3D のメッシュの表示を切り替える。</summary>
    private void SetMeshVisible(bool visible)
    {
        if (meshRenderer != null && meshRenderer.enabled != visible)
        {
            meshRenderer.enabled = visible;
        }
    }

    /// <summary>uGUI のノーツ Image を破棄する (ビュー消滅時の付け直し用)。</summary>
    private void DestroyNoteImages()
    {
        if (headImage != null)
        {
            Destroy(headImage.gameObject);
            headImage = null;
        }

        if (bodyImage != null)
        {
            Destroy(bodyImage.gameObject);
            bodyImage = null;
        }
    }

    private static Image CreateNoteImage(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var img = go.AddComponent<Image>();
        img.color = V6LaneGeometry.NoteColor;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.gameObject.SetActive(false);
        return img;
    }

    /// <summary>
    /// v6 遠近法での配置とスケール。t01 = 残り時間 / アプローチ時間
    /// (0 = 判定線、1 = 奥。判定線通過後は負で画面下へ流れる)。
    /// 幾何は設計座標系で線形なので、HOLD の体も同じレーン直線上の
    /// head → tail の線分として描ける。
    /// </summary>
    private void UpdateV6Visual(float remaining)
    {
        V6PlayfieldView view = V6PlayfieldView.Current;
        if (view == null)
        {
            // ビューが消えた (シーン遷移等) ので、次フレームで付け直す。
            // v6 有効時は 3D に戻さない (旧 3D レーンは隠れている)。
            v6Attached = false;
            if (!FlashBeat.UI.V6.V6UiMode.Playfield)
            {
                SetMeshVisible(true);
            }

            return;
        }

        // アプローチ時間は **セッション固定の z スケール** から求める
        // (PR #263 レビュー: 曲中に noteSpeed を変えると、v6 側の表示だけが
        //  1.6 秒 → 0.64 秒と変わり、3D 側とずれる)。
        float approach = MediaTimeline.VisibleDepthZ / Mathf.Max(0.0001f, MediaTimeline.SessionZScale);
        float tHead = remaining / approach;

        // 奥 (t01 > 1) はまだ見えない。判定ラインを通ったあとは描かない
        // (design: ノーツは判定ラインより上だけ。下の判定行と重なると読めない)。
        bool visible = tHead <= 1f && tHead >= 0f;
        if (!visible)
        {
            SetImagesEnabled(false);
            return;
        }

        SetImagesEnabled(true);

        if (!V6LaneGeometry.TryLanePoint(lane, tHead, out Vector2 headDesign, out _))
        {
            return;
        }

        float widthDesign = V6LaneGeometry.NoteWidthAt(tHead);
        float thicknessDesign = V6LaneGeometry.NoteThicknessAt(tHead);
        float px = view.PixelsPerDesignPixel;
        Vector2 headLocal = view.DesignToLocal(headDesign);

        var headRt = headImage.rectTransform;
        headRt.pivot = new Vector2(0.5f, 0.5f);
        headRt.localRotation = Quaternion.identity;
        headRt.sizeDelta = new Vector2(widthDesign * px, thicknessDesign * px);
        headRt.anchoredPosition = headLocal;

        if (bodyImage != null)
        {
            // 体は head より奥 (時刻が大きい) 側に伸びる。奥は画面上端で打ち切る。
            float tTail = Mathf.Clamp01((remaining + holdSeconds) / approach);
            V6LaneGeometry.TryLanePoint(lane, tTail, out Vector2 tailDesign, out _);
            Vector2 tailLocal = view.DesignToLocal(tailDesign);
            Vector2 dir = tailLocal - headLocal;

            var bodyRt = bodyImage.rectTransform;
            bodyRt.pivot = new Vector2(0f, 0.5f);
            bodyRt.sizeDelta = new Vector2(dir.magnitude, thicknessDesign * px);
            bodyRt.anchoredPosition = headLocal;
            bodyRt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }
    }

    private void SetImagesEnabled(bool enabled)
    {
        if (headImage != null && headImage.gameObject.activeSelf != enabled)
        {
            headImage.gameObject.SetActive(enabled);
        }
        if (bodyImage != null && bodyImage.gameObject.activeSelf != enabled)
        {
            bodyImage.gameObject.SetActive(enabled);
        }
    }

    /// <summary>
    /// ロングノーツの体を長さぶん引き伸ばす (Issue #164)。
    ///
    /// ノーツは「始点の位置」を基準に伸ばす。プレイヤーが見ているのは
    /// 判定線に届く部分なので、始点が判定線に到着した瞬間に、長い体が
    /// 下へ伸びている。
    ///
    /// スケールは長さで決まるので、一度だけ設定すればよい。
    /// </summary>
    private void ApplyHoldLength()
    {
        if (holdSeconds <= 0f) return;

        float length = holdSeconds * ZScale;
        if (length <= 0f) return;

        Vector3 scale = transform.localScale;
        scale.z = length;
        transform.localScale = scale;

        // 中心が半ぶん先になるように、位置を補正する。始点はそのまま。
        Vector3 position = transform.position;
        position.z += length * 0.5f;
        transform.position = position;
    }
}
