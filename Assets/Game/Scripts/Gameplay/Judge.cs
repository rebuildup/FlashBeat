using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

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

    private static readonly KeyCode[][] LaneKeys = new KeyCode[][]
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

        if (notesManager.NotesTime.Count > 0)
        {
            int sameTimeCount = 1;
            float firstTime = notesManager.NotesTime[0];
            int maxCheck = Mathf.Min(8, notesManager.NotesTime.Count);

            for (int i = 1; i < maxCheck; i++)
            {
                if (Mathf.Approximately(notesManager.NotesTime[i], firstTime))
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
                CheckAll(i);
            }
        }

        if (notesManager.NotesTime.Count > 0 && Time.time > notesManager.NotesTime[0] + 0.2f + GManager.StartTime)
        {
            message(3);
            deleteData(0);
            Debug.Log("Miss");
            GManager.miss++;
            GManager.combo = 0;
        }

        if (fSize > 197) fSize -= 1;
        if (Fsize > 59) Fsize -= 1;

        if (comboText != null) comboText.fontSize = fSize;
        if (scoreText != null) scoreText.fontSize = Fsize;
    }

    private void Judgement(float timeLag, int numOffset)
    {
        if (audioHit != null && hitSound != null)
        {
            audioHit.PlayOneShot(hitSound);
        }

        if (timeLag <= 0.10f)
        {
            Debug.Log("Perfect");
            message(0);
            GManager.ratioScore += 5;
            GManager.perfect++;
            GManager.combo++;
            deleteData(numOffset);
        }
        else if (timeLag <= 0.15f)
        {
            Debug.Log("Great");
            message(1);
            GManager.ratioScore += 3;
            GManager.great++;
            GManager.combo++;
            deleteData(numOffset);
        }
        else if (timeLag <= 0.20f)
        {
            Debug.Log("Bad");
            message(2);
            GManager.ratioScore += 1;
            GManager.bad++;
            GManager.combo++;
            deleteData(numOffset);
        }
    }

    private void deleteData(int numOffset)
    {
        if (numOffset < 0 || numOffset >= notesManager.NotesTime.Count) return;

        notesManager.NotesTime.RemoveAt(numOffset);
        notesManager.LaneNum.RemoveAt(numOffset);
        notesManager.NoteType.RemoveAt(numOffset);

        GManager.score = (int)Math.Round(1000000 * Math.Floor(GManager.ratioScore / GManager.maxScore * 1000000) / 1000000);
        fSize = 220;
        Fsize = 67;

        if (comboText != null) comboText.text = GManager.combo.ToString();
        if (scoreText != null) scoreText.text = GManager.score.ToString();
    }

    private void message(int judge)
    {
        GManager.nextNotes++;
        if (judge >= 0 && judge < MessageObj.Length && MessageObj[judge] != null)
        {
            float laneX = (notesManager.LaneNum.Count > 0) ? notesManager.LaneNum[0] - 3.5f : 0f;
            Instantiate(MessageObj[judge], new Vector3(laneX, 0.76f, 0.15f), Quaternion.Euler(45, 0, 0));
        }
    }

    private void CheckAll(int LaneN)
    {
        if (LaneN < 0 || LaneN >= notesManager.LaneNum.Count) return;

        int targetLane = notesManager.LaneNum[LaneN];
        if (targetLane < 0 || targetLane >= LaneKeys.Length) return;

        KeyCode[] keys = LaneKeys[targetLane];
        for (int i = 0; i < keys.Length; i++)
        {
            if (Input.GetKeyDown(keys[i]))
            {
                float timeLag = Mathf.Abs(Time.time - (notesManager.NotesTime[LaneN] + GManager.StartTime));
                Judgement(timeLag, LaneN);
                break;
            }
        }
    }
}