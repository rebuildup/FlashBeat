using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FlashBeat.Gameplay;

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

    void Start()
    {
        noteTime = MediaTimeline.NoteTimeFromPositionZ(transform.position.z, GManager.noteSpeed);
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
            float z = MediaTimeline.NotePositionZ(noteTime, GManager.step_time, GManager.noteSpeed);
            transform.position = new Vector3(transform.position.x, transform.position.y, z);
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
