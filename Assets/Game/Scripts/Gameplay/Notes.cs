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

    void Start()
    {
        noteTime = MediaTimeline.NoteTimeFromPositionZ(transform.position.z, GManager.noteSpeed);
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

}
