using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FlashBeat.Gameplay;
using FlashBeat.UI.V6.Game.Playfield;

public class Notes : MonoBehaviour
{
    // Time of this note on the chart timeline. The note is created by the
    // NotesManager at z = noteTime * noteSpeed, so the start position carries
    // the time.
    float noteTime;

    /// <summary>
    /// ロングノーツの長さ (秒) (Issue #164)。
    ///
    /// 0 ならタップ。NotesManager が生成時に設定する。
    /// </summary>
    public float holdSeconds;

    // --- v6 プレイフィールド (Issue #219) ---
    // ノーツの移動クロック (noteTime / step_time / noteSpeed) は v5 と同じ。
    // 違うのは見た目だけ: 3D の板の代わりに、v6 遠近法 (V6LaneGeometry) の
    // レーン上を流れる uGUI Image として描く。V6PlayfieldView が無い環境
    // (EditMode テスト等) では従来どおり 3D transform を動かす。

    /// <summary>アプローチ時間の基準: noteSpeed 10 のとき 1.6 秒。速度オプションは
    /// 「見えてから判定線までの時間」が 1/speed に比例する形で効く (旧 z 速度と同じ向き)。</summary>
    private const float ApproachSecondsAtSpeed10 = 1.6f;

    private int lane;
    private bool v6Attached;
    private Image headImage;
    private Image bodyImage;

    void Start()
    {
        noteTime = MediaTimeline.NoteTimeFromPositionZ(transform.position.z, GManager.noteSpeed);

        // NotesManager が x = block - 3.5 で生成する (Judge.EffectPosition と同じ写像)。
        lane = Mathf.Clamp(Mathf.RoundToInt(transform.position.x + 3.5f), 0, V6LaneGeometry.LaneCount - 1);

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
            // The notes follow the media clock (GManager.step_time), not the frame
            // clock: a buffering stall holds the notes instead of letting them run
            // ahead of the music, and the note reaches the hit line exactly when
            // the media reaches the note time - the same moment the judge uses.
            float remaining = noteTime - GManager.step_time;

            if (!v6Attached)
            {
                TryAttachV6();
            }

            if (v6Attached)
            {
                UpdateV6Visual(remaining);
            }
            else
            {
                float z = MediaTimeline.NotePositionZ(noteTime, GManager.step_time, GManager.noteSpeed);
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
            return;
        }

        MeshRenderer meshRenderer = GetComponentInChildren<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.enabled = false;
        }

        BoxCollider collider = GetComponent<BoxCollider>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        headImage = CreateNoteImage(view.NoteLayer, "Head");
        bodyImage = holdSeconds > 0f ? CreateNoteImage(view.NoteLayer, "Body") : null;
        v6Attached = true;
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
            // ビューが消えた (シーン遷移等) ので 3D 描画に戻す。
            v6Attached = false;
            MeshRenderer meshRenderer = GetComponentInChildren<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.enabled = true;
            }
            return;
        }

        float approach = ApproachSecondsAtSpeed10 * 10f / Mathf.Max(0.1f, GManager.noteSpeed);
        float tHead = remaining / approach;

        // 奥 (t01 > 1) はまだ見えない。画面下端を出きったら非表示。
        bool visible = tHead <= 1f && tHead >= V6LaneGeometry.ExitT01 - 0.05f;
        if (!visible)
        {
            SetImagesEnabled(false);
            return;
        }

        SetImagesEnabled(true);

        if (!V6LaneGeometry.TryLanePoint(lane, tHead, out Vector2 headDesign, out float scale))
        {
            return;
        }

        float widthDesign = V6LaneGeometry.LaneCentreBottomStep * scale * V6LaneGeometry.NoteWidthRatio;
        float thicknessDesign = widthDesign * V6LaneGeometry.NoteThicknessRatio;
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

        float length = holdSeconds * GManager.noteSpeed;
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
