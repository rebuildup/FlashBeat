using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 判定幅 (NoteTiming) の回帰テスト (Issue #226)。
///
/// NoteTiming は 0.01 秒単位の判定オフセットで、**負の値も有効**である
/// (OptionSettings.NoteTimingMin = -10 / NoteTimingMax = 10)。
/// 負の値は「少し早く判定する」ための設定で、0 に丸められない。
///
/// 以前は 0 / 4 / 5 しか試されていなかったので、負の値について
/// - 設定が負の値へ到達できること (下限で止まること)
/// - 保存 → 復元の往復で負の値が残ること
/// - ノーツの時刻へ設定値がそのまま (負の分だけ早く) 適用されること
/// - 各曲の offset へ設定値が書き戻されること
/// を固定する。
/// </summary>
public class NoteTimingRegressionTests
{
    /// <summary>テスト中に触る PlayerPrefs のキーと型 (保存 → 復元の往復に使う)。</summary>
    static readonly (string key, bool isFloat)[] PrefsKeys =
    {
        (OptionSettings.MainVolumeKey, true),
        (OptionSettings.EffectVolumeKey, true),
        (OptionSettings.BgmVolumeKey, true),
        (OptionSettings.NoteSpeedKey, true),
        (OptionSettings.NoteTimingKey, false),
        (OptionSettings.FlashKey, false),
        (OptionSettings.FlashTimingKey, false),
        (OptionSettings.FlashSpeedKey, false),
    };

    int originalNoteTiming;
    Dictionary<string, float> originalFloatPrefs = new Dictionary<string, float>();
    Dictionary<string, int> originalIntPrefs = new Dictionary<string, int>();

    /// <summary>各曲の offset (書き戻しの前後で元に戻す)。</summary>
    List<int> originalOffsets = new List<int>();

    /// <summary>テストの前に存在していた設定ファイル (テストが作るものは後で消す)。</summary>
    List<string> originalSettingFiles = new List<string>();

    [SetUp]
    public void RememberTheSettings()
    {
        originalNoteTiming = GManager.noteTiming;

        originalFloatPrefs.Clear();
        originalIntPrefs.Clear();
        foreach (var entry in PrefsKeys)
        {
            if (!PlayerPrefs.HasKey(entry.key)) continue;
            if (entry.isFloat) originalFloatPrefs[entry.key] = PlayerPrefs.GetFloat(entry.key);
            else originalIntPrefs[entry.key] = PlayerPrefs.GetInt(entry.key);
        }

        originalOffsets.Clear();
        originalSettingFiles.Clear();
        for (int i = 1; i <= GManager.totalSong; i++)
        {
            originalOffsets.Add(OptionSceneManager.LoadSettings(i).offset);

            string path = Path.Combine(OptionSceneManager.SettingsDirectory(),
                OptionSceneManager.SettingsFileName(GManager.ActiveSongs[i].title));
            if (File.Exists(path)) originalSettingFiles.Add(path);
        }
    }

    [TearDown]
    public void RestoreTheSettings()
    {
        GManager.noteTiming = originalNoteTiming;

        foreach (var pair in originalFloatPrefs)
        {
            PlayerPrefs.SetFloat(pair.Key, pair.Value);
        }
        foreach (var pair in originalIntPrefs)
        {
            PlayerPrefs.SetInt(pair.Key, pair.Value);
        }
        // テスト中に作られた、テスト前は存在しなかったキーは消す。
        foreach (var entry in PrefsKeys)
        {
            if (originalFloatPrefs.ContainsKey(entry.key)) continue;
            if (originalIntPrefs.ContainsKey(entry.key)) continue;
            PlayerPrefs.DeleteKey(entry.key);
        }
        PlayerPrefs.Save();

        // 各曲の offset を元に戻す。
        for (int i = 1; i <= GManager.totalSong && i - 1 < originalOffsets.Count; i++)
        {
            var setting = OptionSceneManager.LoadSettings(i);
            if (setting.offset == originalOffsets[i - 1]) continue;

            setting.offset = originalOffsets[i - 1];
            OptionSceneManager.saveSettings(i, setting);
        }

        // テストによって作られた設定ファイルは元が無かったので消す。
        for (int i = 1; i <= GManager.totalSong; i++)
        {
            string path = Path.Combine(OptionSceneManager.SettingsDirectory(),
                OptionSceneManager.SettingsFileName(GManager.ActiveSongs[i].title));
            if (File.Exists(path) && !originalSettingFiles.Contains(path))
            {
                try { File.Delete(path); } catch (IOException) { }
            }
        }
    }

    [Test]
    public void AdjustNoteTimingCanReachANegativeValue()
    {
        GManager.noteTiming = 0;

        OptionSceneManager.AdjustNoteTiming(-3);

        Assert.AreEqual(-3, GManager.noteTiming, "負の判定幅へ到達できる (0 に丸められない)");
        Assert.AreEqual(GManager.noteTiming, PlayerPrefs.GetInt(OptionSettings.NoteTimingKey),
            "保存先 (PlayerPrefs) へ書かれる");
    }

    [Test]
    public void AdjustNoteTimingIsClampedAtTheLowerBound()
    {
        GManager.noteTiming = OptionSettings.NoteTimingMin;

        OptionSceneManager.AdjustNoteTiming(-5);

        Assert.AreEqual(OptionSettings.NoteTimingMin, GManager.noteTiming,
            "下限より下には下がらない");
        Assert.Less(OptionSettings.NoteTimingMin, 0,
            "下限は 0 未満 (負の判定幅が有効であることを示す)");
    }

    [Test]
    public void AdjustNoteTimingIsClampedAtTheUpperBound()
    {
        GManager.noteTiming = OptionSettings.NoteTimingMax;

        OptionSceneManager.AdjustNoteTiming(5);

        Assert.AreEqual(OptionSettings.NoteTimingMax, GManager.noteTiming,
            "上限より上には上がらない");
    }

    [Test]
    public void ANegativeNoteTimingSurvivesSaveAndRestore()
    {
        GManager.noteTiming = -7;
        OptionSettings.Save();

        GManager.noteTiming = 5;
        OptionSettings.Load();

        Assert.AreEqual(-7, GManager.noteTiming, "保存 → 復元で負の判定幅が残る");
    }

    [Test]
    public void ANoteTimingBelowTheLowerBoundIsClampedOnRestore()
    {
        // OptionSettings.Load() は MainVolumeKey が無いと復元しないので、
        // 先に 1 つ存在させる (他のテストの実行順に依存しないため)。
        PlayerPrefs.SetFloat(OptionSettings.MainVolumeKey, GManager.mainVolume);
        PlayerPrefs.SetInt(OptionSettings.NoteTimingKey, -99);
        GManager.noteTiming = 5;

        OptionSettings.Load();

        Assert.AreEqual(OptionSettings.NoteTimingMin, GManager.noteTiming,
            "範囲外の値は復元時に下限へ寄る");
    }

    [Test]
    public void TheAppliedOffsetIsTheConfiguredNoteTiming()
    {
        // ノーツの時刻へ加算されるオフセットは、設定値そのもの (0.01 秒単位)。
        Assert.AreEqual(10.0f, NotesManager.NoteTimeWithTiming(10.0f, 0), 0.0001f,
            "0 のとき時刻は変わらない");
        Assert.AreEqual(10.05f, NotesManager.NoteTimeWithTiming(10.0f, 5), 0.0001f,
            "正の判定幅は時刻を遅くする");
        Assert.AreEqual(9.95f, NotesManager.NoteTimeWithTiming(10.0f, -5), 0.0001f,
            "負の判定幅は時刻を早くする");

        Assert.Less(NotesManager.NoteTimeWithTiming(10.0f, -1), 10.0f,
            "負の判定幅はノーツを早く判定する");
    }

    [Test]
    public void TheNegativeNoteTimingIsWrittenIntoThePerSongOffset()
    {
        GManager.noteTiming = 0;

        OptionSceneManager.AdjustNoteTiming(-4);

        Assert.AreEqual(-4, OptionSceneManager.LoadSettings(1).offset,
            "各曲の offset へ設定値が書き戻される (負の値も)");
    }
}
