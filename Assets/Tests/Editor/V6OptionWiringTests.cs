using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FlashBeat.Persistence;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6.Option;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Issue #241: v6 Option 画面 (4 タブ) の「表示値・実動作・保存先」の結線を検証する。
    ///
    /// 検証すること:
    /// - Clear save data: 実体の保存先 (安定キーを鍵にする ScoreStore) まで消えること。
    ///   旧 PlayerPrefs の Hiscore だけ消すと記録が復活するが、その復活が起きないこと。
    /// - 4 タブの変更が GManager / OptionSettings (PlayerPrefs) / PlaySession へ届くこと。
    /// - Key Guide / Lyrics はセッション設定 (保存されない) のままであること。
    /// - マウス経路 (行クリック → 確認ダイアログ → 消去する / キャンセル) が
    ///   キーボード経路と同じ結果になり、キャンセルでは何も変わらないこと。
    /// </summary>
    public class V6OptionWiringTests
    {
        // --- 保存先 (PlayerPrefs) の退避と復元 ---

        struct PrefKey
        {
            public string Key;
            public bool IsFloat;
        }

        static readonly PrefKey[] PrefKeys =
        {
            new PrefKey { Key = OptionSettings.MainVolumeKey, IsFloat = true },
            new PrefKey { Key = OptionSettings.EffectVolumeKey, IsFloat = true },
            new PrefKey { Key = OptionSettings.BgmVolumeKey, IsFloat = true },
            new PrefKey { Key = OptionSettings.NoteSpeedKey, IsFloat = true },
            new PrefKey { Key = OptionSettings.NoteTimingKey, IsFloat = false },
            new PrefKey { Key = OptionSettings.FlashKey, IsFloat = false },
            new PrefKey { Key = OptionSettings.FlashTimingKey, IsFloat = false },
            new PrefKey { Key = OptionSettings.FlashSpeedKey, IsFloat = false },
        };

        struct PrefSnapshot
        {
            public bool Exists;
            public float Float;
            public int Int;
        }

        readonly Dictionary<string, PrefSnapshot> savedPrefs = new Dictionary<string, PrefSnapshot>();

        // --- GManager の退避と復元 ---

        float mainVolume, effectVolume, bgmVolume, noteSpeed;
        int noteTiming, flashT, flashBG;
        bool flash;

        // --- 保存先の差し替え (テストの間だけ) ---

        IScoreStorage originalScoreStorage;
        KeyBindings.IStorage originalLaneStorage;
        KeyActions.IStorage originalActionStorage;

        class FakeLaneStorage : KeyBindings.IStorage
        {
            public string Raw;

            public string LoadRaw()
            {
                return Raw;
            }

            public void SaveRaw(string json)
            {
                Raw = json;
            }
        }

        class FakeActionStorage : KeyActions.IStorage
        {
            public string Raw;

            public string LoadRaw()
            {
                return Raw;
            }

            public void SaveRaw(string json)
            {
                Raw = json;
            }
        }

        [SetUp]
        public void SetUp()
        {
            savedPrefs.Clear();
            for (int i = 0; i < PrefKeys.Length; i++)
            {
                PrefKey pref = PrefKeys[i];
                savedPrefs[pref.Key] = new PrefSnapshot
                {
                    Exists = PlayerPrefs.HasKey(pref.Key),
                    Float = PlayerPrefs.GetFloat(pref.Key, 0f),
                    Int = PlayerPrefs.GetInt(pref.Key, 0),
                };
            }

            mainVolume = GManager.mainVolume;
            effectVolume = GManager.effectVolume;
            bgmVolume = GManager.BGMVolume;
            noteSpeed = GManager.noteSpeed;
            noteTiming = GManager.noteTiming;
            flash = GManager.Flash;
            flashT = GManager.FlashT;
            flashBG = GManager.FlashBG;

            // スコア保存先はインメモリへ (テストが開発者のセーブデータへ触らないようにする)。
            originalScoreStorage = ScorePersistence.Storage;
            ScorePersistence.Storage = new MemoryScoreStorage();
            ScorePersistence.Unload();

            // キー設定の保存先もインメモリへ。
            originalLaneStorage = null;
            originalActionStorage = null;
            KeyBindings.SetStorage(new FakeLaneStorage());
            KeyActions.SetStorage(new FakeActionStorage());

            PlaySession.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            PlaySession.Reset();

            KeyBindings.SetStorage(originalLaneStorage);
            KeyActions.SetStorage(originalActionStorage);

            ScorePersistence.Unload();
            ScorePersistence.Storage = originalScoreStorage;

            GManager.mainVolume = mainVolume;
            GManager.effectVolume = effectVolume;
            GManager.BGMVolume = bgmVolume;
            GManager.noteSpeed = noteSpeed;
            GManager.noteTiming = noteTiming;
            GManager.Flash = flash;
            GManager.FlashT = flashT;
            GManager.FlashBG = flashBG;

            for (int i = 0; i < PrefKeys.Length; i++)
            {
                PrefKey pref = PrefKeys[i];
                PrefSnapshot snapshot = savedPrefs[pref.Key];
                if (!snapshot.Exists)
                {
                    PlayerPrefs.DeleteKey(pref.Key);
                    continue;
                }

                if (pref.IsFloat)
                {
                    PlayerPrefs.SetFloat(pref.Key, snapshot.Float);
                }
                else
                {
                    PlayerPrefs.SetInt(pref.Key, snapshot.Int);
                }
            }
            PlayerPrefs.Save();
        }

        // --- Clear save data (Data タブ) ---

        [Test]
        public void ClearSaveData_ErasesTheStableScoreStore_SoTheScoreDoesNotRevive()
        {
            ScorePersistence.SubmitForCatalogIndex(1, 12345);
            GManager.Hiscore[1] = 12345;
            Assert.AreEqual(12345, ScorePersistence.BestOf(1), "前提: 記録が入っている");

            V6OptionView.ClearSaveData();

            Assert.AreEqual(0, ScorePersistence.BestOf(1),
                "Clear save data で安定キーの保存先 (FlashBeat.ScoreStore) も消えること");
            for (int i = 0; i < GManager.Hiscore.Length; i++)
            {
                Assert.AreEqual(0, GManager.Hiscore[i], "旧 Hiscore 配列も 0 に戻ること");
            }

            // 再起動相当: 保存先から読み直しても復活しない。
            ScorePersistence.Unload();
            Assert.AreEqual(0, ScorePersistence.BestOf(1), "再起動後にスコアが復活してはいけない");

            // 旧形式 (Hiscore) が後から現れても、移行で復活しないこと。
            ScorePersistence.Storage.SetString(ScoreStore.LegacyStorageKey, "12345,0,0,0");
            ScorePersistence.Unload();
            Assert.AreEqual(0, ScorePersistence.BestOf(0),
                "旧形式から記録が移行されて復活してはいけない");
        }

        [Test]
        public void ClearSaveData_ResetsTheLaneKeysAndTheMenuActions()
        {
            // カスタムな割り当てを作る (レーン 0 の 1 番キーと、メニュー操作の 1 つ)。
            Assert.IsTrue(KeyBindings.SetLane(0, new[] { KeyCode.I }), "レーンの割り当てを変更できる");
            Assert.IsTrue(KeyActions.Set(KeyActions.KeyAction.Retry, new[] { KeyCode.Tilde }),
                "メニュー操作の割り当てを変更できる");
            Assert.IsFalse(V6OptionView.IsDefaultKeyLayout(), "前提: 標準ではない");

            V6OptionView.ClearSaveData();

            Assert.IsTrue(V6OptionView.IsDefaultKeyLayout(), "レーンの割当が既定へ戻る");
            for (int lane = 0; lane < KeyBindings.DefaultKeys.Length; lane++)
            {
                CollectionAssert.AreEqual(KeyBindings.DefaultKeys[lane], KeyBindings.GetKeys(lane),
                    "レーン " + lane + " が既定へ戻る");
            }

            CollectionAssert.AreEqual(new[] { KeyCode.R }, KeyActions.Get(KeyActions.KeyAction.Retry),
                "メニュー操作の割当も既定へ戻る");
        }

        // --- Play タブ ---

        [Test]
        public void PlayTab_ChangesReachTheRealApiAndAreSaved()
        {
            GManager.noteSpeed = 10f;
            GManager.noteTiming = 5;

            V6OptionView.AdjustNoteSpeed(1);

            Assert.AreEqual(10.1f, GManager.noteSpeed, 0.001f, "実動作へ反映される");
            Assert.AreEqual(GManager.noteSpeed, PlayerPrefs.GetFloat(OptionSettings.NoteSpeedKey), 0.001f,
                "保存先 (PlayerPrefs) へ書かれる");

            V6OptionView.AdjustNoteTiming(1);

            Assert.AreEqual(6, GManager.noteTiming, "実動作へ反映される");
            Assert.AreEqual(GManager.noteTiming, PlayerPrefs.GetInt(OptionSettings.NoteTimingKey),
                "保存先 (PlayerPrefs) へ書かれる");
        }

        // --- Display タブ ---

        [Test]
        public void DisplayTab_ChangesReachTheRealApiAndAreSaved()
        {
            GManager.Flash = false;
            GManager.FlashT = 3;
            GManager.FlashBG = 1;

            V6OptionView.ToggleFlash();

            Assert.IsTrue(GManager.Flash, "実動作へ反映される");
            Assert.AreEqual(1, PlayerPrefs.GetInt(OptionSettings.FlashKey), "保存先 (PlayerPrefs) へ書かれる");

            V6OptionView.AdjustFlashTiming(1);

            Assert.AreEqual(4, GManager.FlashT, "実動作へ反映される");
            Assert.AreEqual(GManager.FlashT, PlayerPrefs.GetInt(OptionSettings.FlashTimingKey),
                "保存先 (PlayerPrefs) へ書かれる");

            V6OptionView.AdjustFlashSpeed(1);

            Assert.AreEqual(2, GManager.FlashBG, "実動作へ反映される");
            Assert.AreEqual(GManager.FlashBG, PlayerPrefs.GetInt(OptionSettings.FlashSpeedKey),
                "保存先 (PlayerPrefs) へ書かれる");
        }

        // --- Sound タブ ---

        [Test]
        public void SoundTab_ChangesReachTheRealApiAndAreSaved()
        {
            GManager.mainVolume = 0.5f;
            GManager.effectVolume = 0.5f;
            GManager.BGMVolume = 0.5f;

            V6OptionView.AdjustMainVolume(1);

            Assert.AreEqual(0.6f, GManager.mainVolume, 0.001f, "実動作へ反映される");
            Assert.AreEqual(0.6f, AudioListener.volume, 0.001f, "出力へ即時反映される");
            Assert.AreEqual(GManager.mainVolume, PlayerPrefs.GetFloat(OptionSettings.MainVolumeKey), 0.001f,
                "保存先 (PlayerPrefs) へ書かれる");

            V6OptionView.AdjustEffectVolume(-1);

            Assert.AreEqual(0.4f, GManager.effectVolume, 0.001f, "実動作へ反映される");
            Assert.AreEqual(GManager.effectVolume, PlayerPrefs.GetFloat(OptionSettings.EffectVolumeKey), 0.001f,
                "保存先 (PlayerPrefs) へ書かれる");

            V6OptionView.AdjustBgmVolume(-1);

            Assert.AreEqual(0.4f, GManager.BGMVolume, 0.001f, "実動作へ反映される");
            Assert.AreEqual(GManager.BGMVolume, PlayerPrefs.GetFloat(OptionSettings.BgmVolumeKey), 0.001f,
                "保存先 (PlayerPrefs) へ書かれる");
        }

        // --- Display タブのセッション設定 (Key Guide / Lyrics) ---

        [Test]
        public void KeyGuideAndLyrics_AreSessionSettingsAndAreNotSaved()
        {
            KeyGuideMode keyGuideBefore = PlaySession.KeyGuide;
            bool lyricsBefore = PlaySession.LyricsEnabled;

            V6OptionView.SetKeyGuide(1);
            V6OptionView.ToggleLyrics();

            Assert.AreNotEqual(keyGuideBefore, PlaySession.KeyGuide, "セッション状態へ反映される");
            Assert.AreNotEqual(lyricsBefore, PlaySession.LyricsEnabled, "セッション状態へ反映される");

            // 保存先に Key Guide / Lyrics のキーが無い = 保存されない (セッション設定)。
            FieldInfo[] fields = typeof(OptionSettings).GetFields(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < fields.Length; i++)
            {
                if (!fields[i].IsLiteral || fields[i].FieldType != typeof(string)) { continue; }
                string name = (string)fields[i].GetRawConstantValue();
                Assert.IsFalse(name.Contains("KeyGuide") || name.Contains("Lyrics"),
                    "Key Guide / Lyrics は保存されない (セッション設定)  shouldn key: " + name);
            }

            // 設定を保存しても、復元しても、セッション内の値は変わらない。
            OptionSettings.Save();
            KeyGuideMode keyGuide = PlaySession.KeyGuide;
            bool lyrics = PlaySession.LyricsEnabled;
            OptionSettings.Load();
            Assert.AreEqual(keyGuide, PlaySession.KeyGuide, "保存・復元でセッション設定は変わらない");
            Assert.AreEqual(lyrics, PlaySession.LyricsEnabled, "保存・復元でセッション設定は変わらない");

            // セッションをやり直すと既定へ戻る (プロセス再起動と同じ扱い)。
            PlaySession.Reset();
            Assert.AreEqual(KeyGuideMode.Compact, PlaySession.KeyGuide, "セッション開始時の既定へ戻る");
            Assert.IsTrue(PlaySession.LyricsEnabled, "セッション開始時の既定へ戻る");
        }

        // --- Data タブ: マウス経路 (行クリック → 確認 → 消去する / キャンセル) ---

        [Test]
        public void TheClearSaveDataDialog_CancelsWithoutChangingAnything_AndClearsWithTheMouse()
        {
            ScorePersistence.SubmitForCatalogIndex(1, 4321);

            var parent = new GameObject("V6 Option Test Parent", typeof(RectTransform));
            V6OptionView view = null;
            try
            {
                view = V6OptionView.Create(parent.GetComponent<RectTransform>(), () => { });

                // Data タブを開く (タブはクリックで切り替えられる)。
                ClickButton(view.Root.gameObject, "Tab Data");

                // Clear save data の行クリック → 確認ダイアログが開く。
                ClickButton(view.Root.gameObject, "Set Clear save data");
                Assert.IsNotNull(FindButton(view.Root.gameObject, "Op Clear"),
                    "行のクリックで確認ダイアログが開く (マウスでも操作できる)");

                // キャンセル → 何も変わらない。
                ClickButton(view.Root.gameObject, "Op Cancel");
                Assert.AreEqual(4321, ScorePersistence.BestOf(1), "キャンセルでは消去されない");
                Assert.IsNull(FindButton(view.Root.gameObject, "Op Clear"), "キャンセルでダイアログが閉じる");

                // もう一度行をクリック → 「消去する」で消去される。
                ClickButton(view.Root.gameObject, "Set Clear save data");
                ClickButton(view.Root.gameObject, "Op Clear");

                Assert.AreEqual(0, ScorePersistence.BestOf(1), "「消去する」で実体の保存先が消える");
                Assert.IsNull(FindButton(view.Root.gameObject, "Op Clear"), "消去後はダイアログが閉じる");

                // 結果表示が出る (Data タブの行に「消去しました」)。
                TextMeshProUGUI value = FindText(view.Root.gameObject, "Set Clear save data", "Value");
                Assert.IsNotNull(value, "行の表示値を持つ Text がある");
                Assert.AreEqual("消去しました", value.text, "結果表示が出る");
            }
            finally
            {
                if (view != null)
                {
                    view.Dispose();
                }
                Object.DestroyImmediate(parent);
            }
        }

        // --- Play タブ: Key Layout のリセットは確認モーダルを経由する (issue-162 C-6) ---

        [Test]
        public void KeyLayoutReset_AskForConfirmationBeforeResetting()
        {
            Assert.IsTrue(KeyBindings.SetLane(0, new[] { KeyCode.I }), "前提: レーンの割当を変更できる");
            Assert.IsFalse(V6OptionView.IsDefaultKeyLayout(), "前提: カスタム配列");

            var parent = new GameObject("V6 Option KeyLayout Test Parent", typeof(RectTransform));
            V6OptionView view = null;
            try
            {
                view = V6OptionView.Create(parent.GetComponent<RectTransform>(), () => { });

                // Play タブの Key Layout 行の ◀ (標準へ戻す方向) を押す。
                GameObject row = FindChild(view.Root.gameObject, "Set Key Layout");
                Assert.IsNotNull(row, "Key Layout 行がある");
                Button prev = FindButton(row, "IconButton chevron-left");
                Assert.IsNotNull(prev, "◀ ステッパーがある");
                prev.onClick.Invoke();

                // 確認ダイアログが出て、まだリセットされていない。
                Assert.IsNotNull(FindButton(view.Root.gameObject, "Op Clear"),
                    "◀ で確認ダイアログが開く (確認なしでリセットしない)");
                Assert.IsFalse(V6OptionView.IsDefaultKeyLayout(), "確認中はまだ戻らない");

                // キャンセル → 何も変わらない。
                ClickButton(view.Root.gameObject, "Op Cancel");
                Assert.IsFalse(V6OptionView.IsDefaultKeyLayout(), "キャンセルでは戻らない");
                Assert.IsNull(FindButton(view.Root.gameObject, "Op Clear"), "キャンセルでダイアログが閉じる");

                // もう一度 ◀ → 「戻す」で標準へ戻る。
                row = FindChild(view.Root.gameObject, "Set Key Layout");
                FindButton(row, "IconButton chevron-left").onClick.Invoke();
                ClickButton(view.Root.gameObject, "Op Clear");
                Assert.IsTrue(V6OptionView.IsDefaultKeyLayout(), "「戻す」で標準配列に戻る");
                Assert.IsNull(FindButton(view.Root.gameObject, "Op Clear"), "実行後はダイアログが閉じる");

                // 既に標準のときは ◀ で確認ダイアログを出さない (する操作が無い)。
                row = FindChild(view.Root.gameObject, "Set Key Layout");
                FindButton(row, "IconButton chevron-left").onClick.Invoke();
                Assert.IsNull(FindButton(view.Root.gameObject, "Op Clear"),
                    "標準配列のときの ◀ は確認ダイアログを出さない");
            }
            finally
            {
                if (view != null)
                {
                    view.Dispose();
                }
                Object.DestroyImmediate(parent);
                KeyBindings.ResetToDefaults();
            }
        }

        // --- 保存先 (Resources フォルダに書かない) ---

        [Test]
        public void SavingPerSongSettings_DoesNotWriteIntoAnyResourcesFolder()
        {
            // 設定の書き出し先は Assets 配下 (Resources を含む) ではない。
            string directory = OptionSceneManager.SettingsDirectory().Replace('\\', '/');
            Assert.IsFalse(directory.Contains("/Assets/"),
                "設定は Assets 配下 (Resources を含む) へ書かない: " + directory);

            // 実動作: 判定幅の変更で全曲の offset を書き戻す (設定を保存する経路)。
            V6OptionView.AdjustNoteTiming(1);

            // Assets 配下の Resources フォルダに譜面 JSON が増えることは無い。
            // 正本 (Assets/Game/Resources) には譜面があるのが正しいので、
            // 正本以外の Resources フォルダを見て 「譜面と同じ形の JSON」 がないことを見る。
            string canonical = Path.Combine(Application.dataPath, "Game", "Resources").Replace('\\', '/');
            string[] roots = FindResourcesRoots();
            for (int r = 0; r < roots.Length; r++)
            {
                string root = roots[r].Replace('\\', '/');
                string[] files = Directory.GetFiles(roots[r], "*.json", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < files.Length; i++)
                {
                    Assert.IsFalse(files[i].EndsWith(".setting.json"),
                        "Resources フォルダへ設定ファイルを書かない: " + files[i]);
                    if (root == canonical) { continue; }
                    Assert.IsFalse(File.ReadAllText(files[i]).Contains("\"notes\""),
                        "Resources フォルダへ譜面と同じ形の JSON を書かない: " + files[i]);
                }
            }

            // 書き出した設定ファイルは譜面と区別できる名前 (…setting.json) になる。
            if (Directory.Exists(directory))
            {
                string[] written = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < written.Length; i++)
                {
                    Assert.IsTrue(written[i].EndsWith(".setting.json"),
                        "設定ファイルは .setting.json 名前である: " + written[i]);
                }
            }
        }

        /// <summary>Assets 配下の Resources フォルダを探す。</summary>
        static string[] FindResourcesRoots()
        {
            string assets = Application.dataPath.Replace('\\', '/');
            var found = new List<string>();
            string[] dirs = Directory.GetDirectories(assets, "Resources", SearchOption.AllDirectories);
            for (int i = 0; i < dirs.Length; i++)
            {
                found.Add(dirs[i]);
            }
            return found.ToArray();
        }

        // --- ヘルパ ---

        /// <summary>名前から Button を探す (階層を探索)。</summary>
        static Button FindButton(GameObject root, string name)
        {
            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].gameObject.name == name)
                {
                    return buttons[i];
                }
            }

            return null;
        }

        /// <summary>名前から TextMeshProUGUI を探す (親の名前で絞る)。</summary>
        static TextMeshProUGUI FindText(GameObject root, string parentName, string name)
        {
            GameObject parent = FindChild(root, parentName);
            if (parent == null)
            {
                return null;
            }

            TextMeshProUGUI[] texts = parent.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i].gameObject.name == name)
                {
                    return texts[i];
                }
            }

            return null;
        }

        static GameObject FindChild(GameObject root, string name)
        {
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == name)
                {
                    return children[i].gameObject;
                }
            }

            return null;
        }

        /// <summary>クリックする (実ハンドラを呼ぶ)。</summary>
        static void ClickButton(GameObject root, string name)
        {
            Button button = FindButton(root, name);
            Assert.IsNotNull(button, "ボタンが見つからない: " + name);
            button.onClick.Invoke();
        }
    }
}
