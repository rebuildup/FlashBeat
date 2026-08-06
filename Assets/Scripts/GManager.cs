using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SongData
{
    public int id;
    public string title;
    public string musician;
    public string videoId;
    public int bpm;
    public int level;
    public int totalHits;
    public float duration;

    public SongData(int id, string title, string musician, string videoId, int bpm, int level, int totalHits, float duration)
    {
        this.id = id;
        this.title = title;
        this.musician = musician;
        this.videoId = videoId;
        this.bpm = bpm;
        this.level = level;
        this.totalHits = totalHits;
        this.duration = duration;
    }
}

public class GManager : MonoBehaviour
{
    public static GManager instance = null;

    public static float maxScore;
    public static float ratioScore;

    public static int songID = 1;

    public static readonly string[] SongName = { "noSong", "ロストアンブレラ", "まにまに", "Who", "ツイッターランド", "終焉逃避行", "エウタナシア", "Chartreuse", "Psyched", "Dogbite", "チュートリアル", "狂喜蘭舞", "Poison", "セルフィー", "4th smile", "Calamity", "パノプティコン", "GURU", "阿吽のビーツ", "混沌ブギ", "春嵐", "ビビビビ", "灰Φ倶楽部", "オーバーライド", "イガク", "テレキャスタービーボーイ", "一龠" };
    public static readonly string[] Musician = { "no", "稲葉曇", "r-906", "Azari", "STEAKA", "柊マグネタイト", "ど～ぱみん", "t+pazolite", "t+pazolite", "t+pazolite", "LeaF", "LeaF", "LeaF", "たぴぼ!!", "LeaF", "LeaF", "r-906", "ジン", "羽生まゐご", "jon-YAKITORY", "john", "フロクロ", "煮ル果実", "吉田夜世", "原口沙輔", "すりぃ", "ァネイロ" };
    public static readonly string[] SongURL = {
        "VIjqWffacio", "DeKLpgzh-qQ", "9O2VyUM5MlQ", "8JXiXt0D6tw", "e_qQEU_uGjw", "yVi3mhLr0uU", "xqYOI7OD9aE", "5BlSQpejMTw", "3mufQ1Tt844", "3s4y8B6Je-4", "NAeuRhLaqaQ", "s-0HVBCEMZk", "3C5zNU2JCdc", "bUM5erw1vRs", "zKbc-kVdtcI", "n-2GnXKvIOU", "_-Vd0ZGB-lo", "smYLMgfCD5o", "SiqjnFhLq2U", "1Swg-aBO9eY", "pUH9vCsvq08", "sWOvhZBS9IA", "_qj9ftYCNyw", "LLjfal8jCYI", "F38EuG2dAyM", "i-DZukWFR64", "iWzUxFQQAKY"
    };
    public static readonly int[] SBPM = { 100, 274, 174, 128, 142, 147, 127, 180, 150, 195, 95, 176, 120, 195, 140, 200, 174, 138, 206, 95, 140, 175, 143, 102, 170, 182, 176 };
    public static readonly int[] Slevel = { 10, 8, 13, 5, 15, 14, 18, 19, 19, 19, 0, 17, 10, 16, 5, 7, 10, 17, 25, 15, 30, 15, 20, 21, 10, 10, 24 };
    public static readonly int[] Shit = { 1000, 268, 383, 139, 130, 296, 243, 349, 257, 603, 53, 360, 82, 254, 105, 111, 316, 295, 262, 292, 315, 184, 275, 250, 196, 163, 193 };
    public static readonly float[] SongLong = { 10000.0f, 89.0f, 48f, 68.0f, 44.0f, 103.0f, 90.0f, 80.0f, 87.0f, 121f, 69f, 79f, 55f, 80f, 69f, 52f, 123f, 85f, 67f, 86f, 60f, 74f, 75f, 68f, 81f, 69f, 77f };

    public static readonly SongData[] Songs = BuildSongs();

    private static SongData[] BuildSongs()
    {
        var built = new SongData[SongName.Length];
        for (int i = 0; i < SongName.Length; i++)
        {
            built[i] = new SongData(
                i,
                SongName[i],
                Musician[i],
                SongURL[i],
                SBPM[i],
                Slevel[i],
                Shit[i],
                SongLong[i]
            );
        }
        return built;
    }

    public static float noteSpeed = 10f;
    public static int[] Hiscore = new int[42];

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

    public static int FlashBG = 4;
    public static bool Flash = true;
    public static int FlashT = 0;

    public static float step_time;
    public static int nextNotes;

    public static float mainVolume = 1.0f;
    public static float effectVolume = 1.0f;
    public static float BGMVolume = 0.3f;
    public static float BGMtime = 0f;

    public static SongData GetSong(int id)
    {
        if (id >= 0 && id < Songs.Length)
        {
            return Songs[id];
        }
        return null;
    }

    public static void ResetSession()
    {
        perfect = 0;
        great = 0;
        bad = 0;
        miss = 0;
        score = 0;
        combo = 0;
        maxScore = 0;
        ratioScore = 0;
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }
    }
}

