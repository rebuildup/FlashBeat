using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using System;
using Unity.VisualScripting;

public class Judge : MonoBehaviour
{
    //変数の宣言
    [SerializeField] private GameObject[] MessageObj;//プレイヤーに判定を伝えるゲームオブジェクト
    [SerializeField] private GameObject[] ParticleObj;

    [SerializeField] NotesManager notesManager;//スクリプト「notesManager」を入れる変数

    [SerializeField] TextMeshProUGUI comboText;
    [SerializeField] TextMeshProUGUI scoreText;

    AudioSource audioHit;
    [SerializeField] AudioClip hitSound;

    int fSize;
    int Fsize;

    //public Animator anim;
    //public GameObject targetObject;

    void Start()
    {
        fSize = 197;
        Fsize = 59;
        //anim = targetObject.GetComponent<Animator>();
        audioHit = GetComponent<AudioSource>();
        comboText.text = "0";
        scoreText.text = "0";
        audioHit.volume = GManager.effectVolume;
    }

    void Update()
    {
        if (GManager.Start)
        {
            if (notesManager.NotesTime[0] == notesManager.NotesTime[1])
            {
                if (notesManager.NotesTime[0] == notesManager.NotesTime[2])
                {
                    if (notesManager.NotesTime[0] == notesManager.NotesTime[3])
                    {
                        if (notesManager.NotesTime[0] == notesManager.NotesTime[4])
                        {
                            if (notesManager.NotesTime[0] == notesManager.NotesTime[5])
                            {
                                if (notesManager.NotesTime[0] == notesManager.NotesTime[6])
                                {
                                    if (notesManager.NotesTime[0] == notesManager.NotesTime[7])
                                    {
                                        for(int i = 7; i >= 0; i--)
                                        {
                                            CheckAll(i);
                                        }
                                    }
                                    else
                                    {
                                        for (int i = 6; i >= 0; i--)
                                        {
                                            CheckAll(i);
                                        }
                                    }
                                }
                                else
                                {
                                    for (int i = 5; i >= 0; i--)
                                    {
                                        CheckAll(i);
                                    }
                                }
                            }
                            else
                            {
                                for (int i = 4; i >= 0; i--)
                                {
                                    CheckAll(i);
                                }
                            }
                        }
                        else
                        {
                            for (int i = 3; i >= 0; i--)
                            {
                                CheckAll(i);
                            }
                        }
                    }
                    else
                    {
                        for (int i = 2; i >= 0; i--)
                        {
                            CheckAll(i);
                        }
                    }
                }
                else
                {
                    for (int i = 1; i >= 0; i--)
                    {
                        CheckAll(i);
                    }
                }
            }
            else
            {
                CheckAll(0);
            }
            
            /*
            KeyCode[][] keys = new KeyCode[][]
            {
                 new KeyCode[] { KeyCode.Alpha1, KeyCode.Q, KeyCode.A, KeyCode.Z },
                 new KeyCode[] { KeyCode.Alpha2, KeyCode.W, KeyCode.S, KeyCode.X },
                 new KeyCode[] { KeyCode.Alpha3, KeyCode.E, KeyCode.D, KeyCode.C },
                 new KeyCode[] { KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.R, KeyCode.T, KeyCode.F, KeyCode.G, KeyCode.V, KeyCode.B },
                 new KeyCode[] { KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Y, KeyCode.U, KeyCode.H, KeyCode.J, KeyCode.N, KeyCode.M },
                 new KeyCode[] { KeyCode.Alpha8, KeyCode.I, KeyCode.K, KeyCode.Less, KeyCode.Comma },
                 new KeyCode[] { KeyCode.Alpha9, KeyCode.O, KeyCode.L, KeyCode.Greater, KeyCode.Period },
                 new KeyCode[]
                    {
                     KeyCode.Alpha0, KeyCode.Equals, KeyCode.Caret, KeyCode.Backslash, KeyCode.P, KeyCode.At, KeyCode.LeftBracket, KeyCode.Semicolon,
                     KeyCode.Colon, KeyCode.RightBracket, KeyCode.Slash, KeyCode.Underscore, KeyCode.Minus, KeyCode.Asterisk, KeyCode.BackQuote
                    }
            };
            

            for (int i = 0; i < keys.Length; i++)
            {
                if (CheckKey(keys[i]))
                {
                    for (int j = 0; j < notesManager.LaneNum.Count; j++)
                    {
                        if (notesManager.LaneNum[j] == i)
                        {
                            Judgement(GetABS(Time.time - (notesManager.NotesTime[j] + GManager.StartTime)), j);
                            break;
                        }
                    }
                }
            }
            bool CheckKey(KeyCode[] keyArray)
            {
                foreach (KeyCode key in keyArray)
                {
                    if (Input.GetKeyDown(key))
                    {
                        return true;
                    }
                }
                return false;
            }
            
            */

            if (notesManager.NotesTime.Count >= 0 && Time.time > notesManager.NotesTime[0] + 0.2f + GManager.StartTime)
            {
                message(3);
                if (notesManager.NotesTime.Count >= 0)
                {
                    deleteData(0);
                    Debug.Log("Miss");
                    PlayComboAnimation();
                    GManager.miss++;
                    GManager.combo = 0;
                    //ミス
                }
            }
            if (fSize > 197)
            {
                fSize -= 1;
            }
            if (Fsize > 59){
                Fsize -= 1;
            }
            comboText.fontSize = fSize;
            scoreText.fontSize = Fsize;
        }
        
    }
    void Judgement(float timeLag,int numOffset)
    {
        audioHit.PlayOneShot(hitSound);
        if (timeLag <= 0.10)
        {
            Debug.Log("Perfect");
            PlayComboAnimation();
            message(0);
            GManager.ratioScore += 5;
            GManager.perfect++;
            GManager.combo++;
            deleteData(numOffset);
        }
        else
        {
            if (timeLag <= 0.15)
            {
                Debug.Log("Great");
                PlayComboAnimation();
                message(1);
                GManager.ratioScore += 3;
                GManager.great++;
                GManager.combo++;
                deleteData(numOffset);
            }
            else
            {
                if (timeLag <= 0.20)
                {
                    Debug.Log("Bad");
                    PlayComboAnimation();
                    message(2);
                    GManager.ratioScore += 1;
                    GManager.bad++;
                    GManager.combo++;
                    deleteData(numOffset);
                }
            }
        }
    }
    float GetABS(float num)//引数の絶対値を返す関数
    {
        if (num >= 0)
        {
            return num;
        }
        else
        {
            return -num;
        }
    }
    void deleteData(int numOffset)//すでにたたいたノーツを削除する関数
    {
        notesManager.NotesTime.RemoveAt(numOffset);
        notesManager.LaneNum.RemoveAt(numOffset);
        notesManager.NoteType.RemoveAt(numOffset);
        GManager.score = (int)Math.Round(1000000 * Math.Floor(GManager.ratioScore / GManager.maxScore * 1000000) / 1000000);
        fSize = 220;
        Fsize = 67;
        comboText.text = GManager.combo.ToString();
        scoreText.text = GManager.score.ToString();
    }

    void message(int judge)//判定を表示する
    {
        GManager.nextNotes++;
        Instantiate(MessageObj[judge], new Vector3(notesManager.LaneNum[0] - 3.5f, 0.76f, 0.15f), Quaternion.Euler(45, 0, 0));
        
    }
    void PlayComboAnimation()
    {
        //if (anim != null)
        {
          //  anim.SetTrigger("comboText");  // "comboText"はトリガーの名前です。Animator Controller内で設定したものに対応しています。
        }/*
        Debug.Log("PlayComboAnimation method called.");
        if (anim != null)
        {
            Debug.Log("Animation component is not null.");
            anim.Play("comboText");
        }*/
    }
    private void CheckAll(int LaneN)
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.Z))
        {

            if (notesManager.LaneNum[LaneN] == 0)
            {
                Judgement(GetABS(Time.time - (notesManager.NotesTime[0] + GManager.StartTime)), 0);

            }

        }
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.X))
        {
            if (notesManager.LaneNum[LaneN] == 1)
            {
                Judgement(GetABS(Time.time - (notesManager.NotesTime[0] + GManager.StartTime)), 0);

            }
            
        }
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.C))
        {
            if (notesManager.LaneNum[LaneN] == 2)
            {
                Judgement(GetABS(Time.time - (notesManager.NotesTime[0] + GManager.StartTime)), 0);

            }
            
        }
        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.T) || Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.G) || Input.GetKeyDown(KeyCode.V) || Input.GetKeyDown(KeyCode.B))
        {
            if (notesManager.LaneNum[LaneN] == 3)
            {
                Judgement(GetABS(Time.time - (notesManager.NotesTime[0] + GManager.StartTime)), 0);

            }
            
        }
        if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Y) || Input.GetKeyDown(KeyCode.U) || Input.GetKeyDown(KeyCode.H) || Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.M))
        {
            if (notesManager.LaneNum[LaneN] == 4)
            {
                Judgement(GetABS(Time.time - (notesManager.NotesTime[0] + GManager.StartTime)), 0);

            }
            
        }
        if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.Less) || Input.GetKeyDown(KeyCode.Comma))
        {
            if (notesManager.LaneNum[LaneN] == 5)
            {
                Judgement(GetABS(Time.time - (notesManager.NotesTime[0] + GManager.StartTime)), 0);

            }
            
        }
        if (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.O) || Input.GetKeyDown(KeyCode.L) || Input.GetKeyDown(KeyCode.Greater) || Input.GetKeyDown(KeyCode.Period))
        {
            if (notesManager.LaneNum[LaneN] == 6)
            {
                Judgement(GetABS(Time.time - (notesManager.NotesTime[0] + GManager.StartTime)), 0);

            }
            
        }
        if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.Caret) || Input.GetKeyDown(KeyCode.Backslash) || Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.At) || Input.GetKeyDown(KeyCode.LeftBracket) || Input.GetKeyDown(KeyCode.Semicolon) || Input.GetKeyDown(KeyCode.Colon) || Input.GetKeyDown(KeyCode.RightBracket) || Input.GetKeyDown(KeyCode.Slash) || Input.GetKeyDown(KeyCode.Underscore) || Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.Asterisk) || Input.GetKeyDown(KeyCode.BackQuote))
        {
            if (notesManager.LaneNum[LaneN] == 7)
            {
                Judgement(GetABS(Time.time - (notesManager.NotesTime[0] + GManager.StartTime)), 0);

            }
        }

    }
}