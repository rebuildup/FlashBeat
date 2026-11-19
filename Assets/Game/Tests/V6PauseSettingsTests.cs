using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using FlashBeat.Gameplay;
using FlashBeat.UI.V6.Game.Pause;

namespace FlashBeat.V6HudTests
{
    /// <summary>
    /// v6 ポーズ画面の音量変更・再開・PauseGate の実挙動 (Issue #681) の EditMode テスト。
    ///
    /// 既存の <see cref="V6PauseRuntimeTests"/> / <see cref="V6PauseViewLogicTests"/> は
    /// index / format / key mapping の純ロジックだけを見ており、設定値が実際の
    /// 音量 (<see cref="AudioListener.volume"/> / MusicManager / Judge の
    /// AudioSource) と保存 (<see cref="OptionSettings"/>) へ届くこと、
    /// Open → RequestResume → V6ResumeCountdown → PauseGate.Exit の状態遷移を
    /// 検証していなかった。このテストは実際の <see cref="V6PauseView"/> を
    /// 生成してその経路を固定する。
    ///
    /// 検証する実挙動:
    ///   - Sound タブの Main / SE 行を ←→ (AdjustSelected) で動かすと
    ///     GManager.mainVolume / effectVolume が 1% 刻みで変わり、
    ///     AudioListener.volume・MusicManager の AudioSource・Judge の
    ///     AudioSource へ即時反映され、OptionSettings.Save で PlayerPrefs に届く。
    ///   - 音量スライダーのドラッグ経路 (SetSelectedPauseVolume) は
    ///     ドラッグ中 (final=false) は適用のみで保存せず、確定時 (final=true) に保存する。
    ///   - Open → PauseGate.Enter、RequestResume → カウントダウン中も
    ///     PauseGate は閉じたまま、カウントダウン終了で PauseGate.Exit、
    ///     カウントダウン中の Esc (CancelBackToPause) はポーズへ戻る、
    ///     Close は即座に PauseGate.Exit する。
    /// </summary>
    public class V6PauseSettingsTests
    {
        private GameObject parentGo;
        private V6PauseView view;

        private GameObject musicGo;
        private GameObject judgeGo;
        private AudioSource musicSource;
        private AudioSource judgeSource;

        private float savedMainVolume;
        private float savedEffectVolume;
        private float savedListenerVolume;
        private int savedSongId;
        private bool savedMainVolumePrefExists;
        private float savedMainVolumePref;
        private bool savedEffectVolumePrefExists;
        private float savedEffectVolumePref;

        private static readonly MethodInfo SetSelectedPauseVolumeMethod =
            typeof(V6PauseView).GetMethod("SetSelectedPauseVolume",
                BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo CountdownUpdateMethod =
            typeof(V6ResumeCountdown).GetMethod("Update",
                BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo CancelBackToPauseMethod =
            typeof(V6ResumeCountdown).GetMethod("CancelBackToPause",
                BindingFlags.NonPublic | BindingFlags.Instance);

        [SetUp]
        public void SetUp()
        {
            Assert.IsNotNull(SetSelectedPauseVolumeMethod, "SetSelectedPauseVolume が見つからない");
            Assert.IsNotNull(CountdownUpdateMethod, "V6ResumeCountdown.Update が見つからない");
            Assert.IsNotNull(CancelBackToPauseMethod, "CancelBackToPause が見つからない");

            savedMainVolume = GManager.mainVolume;
            savedEffectVolume = GManager.effectVolume;
            savedListenerVolume = AudioListener.volume;
            savedSongId = GManager.songID;
            savedMainVolumePrefExists = PlayerPrefs.HasKey(OptionSettings.MainVolumeKey);
            savedMainVolumePref = PlayerPrefs.GetFloat(OptionSettings.MainVolumeKey, 0f);
            savedEffectVolumePrefExists = PlayerPrefs.HasKey(OptionSettings.EffectVolumeKey);
            savedEffectVolumePref = PlayerPrefs.GetFloat(OptionSettings.EffectVolumeKey, 0f);

            GManager.mainVolume = 0.5f;
            GManager.effectVolume = 0.5f;
            AudioListener.volume = 0.5f;
            // songID = -1 で GetSong が null を返すようにし、RefreshHeader の
            // ジャケット取得 (ネットワーク) を走らせない。
            GManager.songID = -1;

            // ApplyVolumes が FindFirstObjectByType で見つける対象。
            // 非アクティブだと見つからないのでアクティブな GameObject に載せる
            // (EditMode では Start / Update は走らない)。
            musicGo = new GameObject("V6PauseSettingsTests Music");
            musicGo.AddComponent<MusicManager>();
            musicSource = musicGo.AddComponent<AudioSource>();

            judgeGo = new GameObject("V6PauseSettingsTests Judge");
            judgeGo.AddComponent<Judge>();
            judgeSource = judgeGo.AddComponent<AudioSource>();

            parentGo = new GameObject("V6PauseSettingsTests Parent", typeof(RectTransform));
            view = V6PauseView.Create(parentGo.GetComponent<RectTransform>());
            Assert.IsNotNull(view, "V6PauseView.Create がビューを返さない");
        }

        [TearDown]
        public void TearDown()
        {
            V6ResumeCountdown.Abort();
            if (view != null)
            {
                view.Dispose();
            }
            if (parentGo != null)
            {
                Object.DestroyImmediate(parentGo);
            }
            if (musicGo != null)
            {
                Object.DestroyImmediate(musicGo);
            }
            if (judgeGo != null)
            {
                Object.DestroyImmediate(judgeGo);
            }

            PauseGate.HardReset();
            GManager.mainVolume = savedMainVolume;
            GManager.effectVolume = savedEffectVolume;
            AudioListener.volume = savedListenerVolume;
            GManager.songID = savedSongId;

            RestorePref(OptionSettings.MainVolumeKey, savedMainVolumePrefExists, savedMainVolumePref);
            RestorePref(OptionSettings.EffectVolumeKey, savedEffectVolumePrefExists, savedEffectVolumePref);
        }

        // ------------------------------------------------------------------
        // 音量変更 → 実音量・保存への反映
        // ------------------------------------------------------------------

        /// <summary>
        /// Sound タブの Main 行を → で動かすと、GManager.mainVolume が 1% 上がり、
        /// AudioListener.volume と MusicManager の AudioSource へ即時反映され、
        /// OptionSettings.Save で PlayerPrefs にも届く。SE 側と Judge の
        /// AudioSource はこの操作では変わらない。
        /// </summary>
        [Test]
        public void AdjustMainVolume_ReachesListenerMusicSourceAndPrefs()
        {
            view.Open();
            Assert.IsTrue(view.IsOpen);

            view.SelectRowForAudit(2, 0); // Sound タブ / MainVolume 行
            view.AdjustSelectedForAudit(1);

            Assert.AreEqual(0.51f, GManager.mainVolume, 0.0001f, "Main は 1% 刻みで増える");
            Assert.AreEqual(GManager.mainVolume, AudioListener.volume, 0.0001f,
                "AudioListener.volume が mainVolume に追従する");
            Assert.AreEqual(GManager.mainVolume, musicSource.volume, 0.0001f,
                "MusicManager の AudioSource.volume が mainVolume に追従する");
            Assert.AreEqual(0.5f, judgeSource.volume, 0.0001f,
                "Judge の AudioSource (SE) は Main 変更では変わらない");
            Assert.AreEqual(0.5f, GManager.effectVolume, 0.0001f, "SE は変わらない");
            // Issue #733: キー操作の保存は Option と同じく遅延 (0.5 秒まとめ)。
            // 画面を離れる / 猶予経過で Flush されるため、ここでは明示的に flush する。
            view.FlushPendingVolumeSaveForAudit();
            Assert.AreEqual(0.51f, PlayerPrefs.GetFloat(OptionSettings.MainVolumeKey, -1f), 0.0001f,
                "OptionSettings.Save で PlayerPrefs に保存される");
        }

        /// <summary>
        /// Sound タブの SE 行を ← で動かすと、GManager.effectVolume が 1% 下がり、
        /// Judge の AudioSource へ即時反映され、PlayerPrefs にも届く。
        /// AudioListener / MusicManager (Main 側) は変わらない。
        /// </summary>
        [Test]
        public void AdjustSeVolume_ReachesJudgeSourceAndPrefs()
        {
            view.Open();

            view.SelectRowForAudit(2, 1); // Sound タブ / SEVolume 行
            view.AdjustSelectedForAudit(-1);

            Assert.AreEqual(0.49f, GManager.effectVolume, 0.0001f, "SE は 1% 刻みで減る");
            Assert.AreEqual(GManager.effectVolume, judgeSource.volume, 0.0001f,
                "Judge の AudioSource.volume が effectVolume に追従する");
            Assert.AreEqual(0.5f, musicSource.volume, 0.0001f,
                "MusicManager の AudioSource (Main) は SE 変更では変わらない");
            Assert.AreEqual(0.5f, AudioListener.volume, 0.0001f,
                "AudioListener.volume は mainVolume のまま");
            // Issue #733: キー操作の保存は Option と同じく遅延。flush して確認する。
            view.FlushPendingVolumeSaveForAudit();
            Assert.AreEqual(0.49f, PlayerPrefs.GetFloat(OptionSettings.EffectVolumeKey, -1f), 0.0001f,
                "OptionSettings.Save で PlayerPrefs に保存される");
        }

        /// <summary>
        /// 音量スライダーのドラッグ経路 (SetSelectedPauseVolume):
        /// ドラッグ中 (final=false) は値を適用するが保存しない。
        /// 確定 (final=true) で初めて OptionSettings.Save が走る。
        /// 値は 0〜1 にクランプされる。
        /// </summary>
        [Test]
        public void SetSelectedPauseVolume_AppliesImmediately_AndSavesOnlyOnFinal()
        {
            view.Open();
            view.SelectRowForAudit(2, 0); // selectedRow = 0 (Main)

            // ドラッグ中: 値は適用されるが PlayerPrefs はまだ古い値のまま。
            PlayerPrefs.SetFloat(OptionSettings.MainVolumeKey, 0.25f);
            SetSelectedPauseVolumeMethod.Invoke(view, new object[] { 0.8f, false });

            Assert.AreEqual(0.8f, GManager.mainVolume, 0.0001f, "ドラッグ中も値は適用される");
            Assert.AreEqual(0.8f, AudioListener.volume, 0.0001f, "即時反映される");
            Assert.AreEqual(0.8f, musicSource.volume, 0.0001f);
            Assert.AreEqual(0.25f, PlayerPrefs.GetFloat(OptionSettings.MainVolumeKey, -1f), 0.0001f,
                "ドラッグ中 (final=false) は保存しない");

            // 確定: 保存される。
            SetSelectedPauseVolumeMethod.Invoke(view, new object[] { 0.8f, true });
            Assert.AreEqual(0.8f, PlayerPrefs.GetFloat(OptionSettings.MainVolumeKey, -1f), 0.0001f,
                "確定 (final=true) で保存される");

            // クランプ: 1.0 を超える値は 1.0、負は 0 に丸められる。
            SetSelectedPauseVolumeMethod.Invoke(view, new object[] { 1.5f, false });
            Assert.AreEqual(1f, GManager.mainVolume, 0.0001f, "上限は 1.0 にクランプ");
            Assert.AreEqual(1f, AudioListener.volume, 0.0001f);

            SetSelectedPauseVolumeMethod.Invoke(view, new object[] { -0.2f, false });
            Assert.AreEqual(0f, GManager.mainVolume, 0.0001f, "下限は 0 にクランプ");
        }

        /// <summary>
        /// Issue #681 review: StreamSeparate 経路の演奏音源
        /// (MusicManager.streamAudioSource) も mainVolume に追従する。
        /// 以前は AudioSource を 1 つだけ書き換えていたため、
        /// streamAudioSource が残って音量が変わらなかった。
        /// </summary>
        [Test]
        public void AdjustMainVolume_ReachesStreamAudioSource()
        {
            var streamGo = new GameObject("V6PauseSettingsTests StreamAudio");
            var streamSource = streamGo.AddComponent<AudioSource>();
            streamSource.volume = 0.5f;

            var musicManager = musicGo.GetComponent<MusicManager>();
            FieldInfo streamField = typeof(MusicManager).GetField("streamAudioSource",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(streamField, "MusicManager.streamAudioSource が見つからない");
            streamField.SetValue(musicManager, streamSource);
            try
            {
                view.Open();
                view.SelectRowForAudit(2, 0); // Sound タブ / MainVolume 行
                view.AdjustSelectedForAudit(1);

                Assert.AreEqual(GManager.mainVolume, streamSource.volume, 0.0001f,
                    "StreamSeparate の AudioSource も mainVolume に追従する");
            }
            finally
            {
                streamField.SetValue(musicManager, null);
                Object.DestroyImmediate(streamGo);
            }
        }

        // ------------------------------------------------------------------
        // Open → RequestResume → V6ResumeCountdown → PauseGate.Exit
        // ------------------------------------------------------------------

        /// <summary>
        /// Open で PauseGate.Enter、RequestResume でポーズ画面は閉じるが
        /// PauseGate はカウントダウン終了まで閉じたまま。カウントダウンが
        /// 終わると PauseGate.Exit で再開する。
        /// </summary>
        [Test]
        public void Open_RequestResume_CountdownFinish_ReleasesPauseGate()
        {
            view.Open();
            Assert.IsTrue(PauseGate.IsPaused, "Open で PauseGate.Enter される");

            view.RequestResume();
            Assert.IsFalse(view.IsOpen, "RequestResume でポーズ画面は閉じる");
            Assert.IsTrue(V6ResumeCountdown.IsRunning, "再開カウントダウンが始まる");
            Assert.IsTrue(PauseGate.IsPaused,
                "カウントダウン中も PauseGate は閉じたまま (メディア時計・判定は止まったまま)");

            // カウントダウンを終了直前まで進めて Update を 1 回回す
            // (elapsed は private。実時間を待たずに終了分岐へ入れる)。
            V6ResumeCountdown countdown = Object.FindObjectOfType<V6ResumeCountdown>();
            Assert.IsNotNull(countdown, "V6ResumeCountdown が生成されていない");
            FieldInfo elapsedField = typeof(V6ResumeCountdown).GetField("elapsed",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(elapsedField, "V6ResumeCountdown.elapsed が見つからない");
            elapsedField.SetValue(countdown, V6ResumeCountdownLogic.DurationSeconds);

            CountdownUpdateMethod.Invoke(countdown, null);

            Assert.IsFalse(V6ResumeCountdown.IsRunning, "カウントダウンが終了する");
            Assert.IsFalse(PauseGate.IsPaused,
                "カウントダウン終了で PauseGate.Exit → 演奏が再開する");
        }

        /// <summary>
        /// カウントダウン中の Esc (CancelBackToPause) は数字を消して
        /// ポーズ画面へ戻す。PauseGate は閉じたまま (演奏は再開しない)。
        /// </summary>
        [Test]
        public void RequestResume_BackDuringCountdown_ReturnsToPause()
        {
            view.Open();
            view.RequestResume();
            Assert.IsTrue(V6ResumeCountdown.IsRunning);

            V6ResumeCountdown countdown = Object.FindObjectOfType<V6ResumeCountdown>();
            Assert.IsNotNull(countdown);

            CancelBackToPauseMethod.Invoke(countdown, null);

            Assert.IsFalse(V6ResumeCountdown.IsRunning, "カウントダウンは破棄される");
            Assert.IsTrue(view.IsOpen, "ポーズ画面へ戻る");
            Assert.IsTrue(PauseGate.IsPaused, "演奏は再開しない (PauseGate は閉じたまま)");
        }

        /// <summary>Close はカウントダウンを介さず即座に PauseGate.Exit する (リトライ / 終了経路)。</summary>
        [Test]
        public void Close_WhileOpen_ReleasesPauseGateImmediately()
        {
            view.Open();
            Assert.IsTrue(PauseGate.IsPaused);

            view.Close();

            Assert.IsFalse(view.IsOpen);
            Assert.IsFalse(PauseGate.IsPaused, "Close で PauseGate.Exit される");
            Assert.IsFalse(V6ResumeCountdown.IsRunning, "カウントダウンは始まらない");
        }

        private static void RestorePref(string key, bool existed, float value)
        {
            if (existed)
            {
                PlayerPrefs.SetFloat(key, value);
            }
            else
            {
                PlayerPrefs.DeleteKey(key);
            }
        }
    }
}
