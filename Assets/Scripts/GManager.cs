using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using UnityEngine;

public class GManager : MonoBehaviour
{
    public static GManager instance = null;

    public static float maxScore;
    public static float ratioScore;

    public static int songID = 1;
    public static string[] SongName = { "noSong","ロストアンブレラ","まにまに","Who","ツイッターランド","終焉逃避行","エウタナシア","Chartreuse","Psyched","Dogbite","チュートリアル","狂喜蘭舞","Poison","セルフィー","4th smile","Calamity","パノプティコン","GURU","阿吽のビーツ","混沌ブギ","春嵐","ビビビビ","灰Φ倶楽部","オーバーライド","イガク","テレキャスタービーボーイ","一龠"};
    public static string[] Musician = {"no","稲葉曇","r-906","Azari","STEAKA","柊マグネタイト","ど～ぱみん", "t+pazolite", "t+pazolite" , "t+pazolite" ,"LeaF","LeaF","LeaF","たぴぼ!!","LeaF","LeaF","r-906","ジン","羽生まゐご","jon-YAKITORY","john","フロクロ","煮ル果実","吉田夜世","原口沙輔","すりぃ", "ァネイロ" };
    public static string[] SongURL = {
        "VIjqWffacio",//00 https://www.youtube.com/watch?v=VIjqWffacio Major
        "DeKLpgzh-qQ",//01 https://www.youtube.com/watch?v=DeKLpgzh-qQ ロストアンブレラ
        "9O2VyUM5MlQ",//02 https://www.youtube.com/watch?v=9O2VyUM5MlQ まにまに
        "8JXiXt0D6tw",//03 https://www.youtube.com/watch?v=8JXiXt0D6tw Who?
        "e_qQEU_uGjw",//04 https://www.youtube.com/watch?v=e_qQEU_uGjw ツイッターランド
        "yVi3mhLr0uU",//05 https://www.youtube.com/watch?v=yVi3mhLr0uU 終焉逃避行
        "xqYOI7OD9aE",//06 https://www.youtube.com/watch?v=xqYOI7OD9aE エウタナシア
        "5BlSQpejMTw",//07 https://www.youtube.com/watch?v=5BlSQpejMTw Chartreuse Green
        "3mufQ1Tt844",//08 https://www.youtube.com/watch?v=3mufQ1Tt844 Psyched Fevereiro
        "3s4y8B6Je-4",//09 https://www.youtube.com/watch?v=3s4y8B6Je-4 Dogbite
        "NAeuRhLaqaQ",//10 https://www.youtube.com/watch?v=NAeuRhLaqaQ Evanescent
        "s-0HVBCEMZk",//11 https://www.youtube.com/watch?v=s-0HVBCEMZk 狂喜蘭舞
        "3C5zNU2JCdc",//12 https://www.youtube.com/watch?v=3C5zNU2JCdc Poison AND÷OR Affection
        "bUM5erw1vRs",//13 https://www.youtube.com/watch?v=bUM5erw1vRs セルフィ
        "zKbc-kVdtcI",//14 https://www.youtube.com/watch?v=zKbc-kVdtcI 4th smile
        "n-2GnXKvIOU",//15 https://www.youtube.com/watch?v=n-2GnXKvIOU Calamity Fortune
        "_-Vd0ZGB-lo",//16 https://www.youtube.com/watch?v=_-Vd0ZGB-lo パノプティコン
        "smYLMgfCD5o",//17 https://www.youtube.com/watch?v=smYLMgfCD5o GURU
        "SiqjnFhLq2U",//18 https://www.youtube.com/watch?v=SiqjnFhLq2U 阿吽のビーツ
        "1Swg-aBO9eY",//19 https://www.youtube.com/watch?v=1Swg-aBO9eY 混沌ブギ
        "pUH9vCsvq08",//20 https://www.youtube.com/watch?v=pUH9vCsvq08 春嵐
        "sWOvhZBS9IA",//21 https://www.youtube.com/watch?v=sWOvhZBS9IA ビビビビ
        "_qj9ftYCNyw",//22 https://www.youtube.com/watch?v=_qj9ftYCNyw 灰Φ倶楽部
        "LLjfal8jCYI",//23 https://www.youtube.com/watch?v=LLjfal8jCYI オーバーライド
        "F38EuG2dAyM",//24 https://www.youtube.com/watch?v=F38EuG2dAyM イガク
        "i-DZukWFR64",//25 https://www.youtube.com/watch?v=i-DZukWFR64 テレキャスタービーボーイ
        "iWzUxFQQAKY",//26 https://www.youtube.com/watch?v=iWzUxFQQAKY 一龠
    };
    public static int[] SBPM = { 100, 274, 174, 128, 142, 147, 127 ,180,150,195,95,176,120,195,140,200,174,138,206,95,140,175,143,102,170,182,176};
    public static int[] Slevel = { 10, 8, 13, 5, 15, 14, 18, 19, 19 ,19, 0,17,10,16,5,7,10,17,25,15,30,15,20,21,10,10,24};
    public static int[] Shit = { 1000, 268, 383, 139, 130, 296, 243, 349, 257, 603,53 ,360,82,254,105,111,316,295,262,292,315,184,275,250,196,163,193};
    public static float[] SongLong = { 10000.0f,89.0f,48f,68.0f,44.0f,103.0f,90.0f,80.0f,87.0f,121f,69f,79f,55f,80f,69f,52f,123f,85f,67f,86f,60f,74f,75f,68f,81f,69f,77f};
    public static float noteSpeed = 10f;

    public static int[] Hiscore = {0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0 ,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0};

    public static bool Start;
    public static float StartTime;

    public static bool played;

    public static int noteTiming = 5;

    public static int totalSong = 26;

    public static int combo;
    public static int score;

    public static int perfect;
    public static int great;
    public static int bad;
    public static int miss;

    public static float MainSound;
    public static float EffectSound;

    public static int FlashBG=4;//一小節ごとに何回点滅するか
    public static bool Flash = true;
    public static int FlashT = 0;//flashの開始位置 *100

    public static float step_time;

    public static int nextNotes;

    public static float mainVolume = 1.0f;
    public static float effectVolume = 1.0f;
    public static float BGMVolume = 0.3f;

    public static float BGMtime=0f;
    
    void Awake()
    {
        if(instance == null)
        {
            Debug.Log("Null");
            instance = this;
            DontDestroyOnLoad(this.gameObject);
            //NotesManager.NotesSpeed = noteSpeed;
        }
        else
        {
            Debug.Log("NotNull");
            Destroy(this.gameObject);
        }
    }
}
