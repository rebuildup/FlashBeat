using UnityEngine;
using FlashBeat.Gameplay;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>
    /// 音量設定の値モデル (Issue #733)。Option / Pause の Sound タブが共有する。
    ///
    /// - 値の実体は <see cref="GManager"/> (mainVolume / effectVolume / BGMVolume)。
    /// - 変更は即時に音出力へ反映する (<see cref="ApplyToOutputs"/>)。
    /// - 保存 (<see cref="OptionSettings.Save"/>) はキー操作では 0.5 秒まとめて
    ///   行い (連打で PlayerPrefs を書かない)、スライダーのドラッグ確定
    ///   (<c>final=true</c>) と画面を離れるときは即座に保存する。
    /// - 画面ごとに 1 インスタンスを持つ (Option / Pause は同時に開かない)。
    /// </summary>
    public sealed class V6VolumeSettings
    {
        /// <summary>キー操作を止めてから保存するまでの猶予 (秒)。</summary>
        public const float SaveDelaySeconds = 0.5f;

        /// <summary>保存予約時刻 (0 以下 = 予約なし。Time.unscaledTime 基準)。</summary>
        private float saveAt;

        /// <summary>音量 (0..1)。index は <see cref="V6SettingRow.VolumeIndex"/> と同じ対応。</summary>
        public static float Get(int index)
        {
            switch (index)
            {
                case 0: return GManager.mainVolume;
                case 1: return GManager.effectVolume;
                case 2: return GManager.BGMVolume;
                default: return 0f;
            }
        }

        /// <summary>キー / ステッパー 1 回分の値変更 (1% 刻み、Shift で 10%)。</summary>
        public void Nudge(int index, int delta, bool shift)
        {
            Set(index, V6VolumeSlider.Nudge(Get(index), delta, shift), false);
        }

        /// <summary>
        /// 値を書き、即時に音へ反映する。保存は final (ドラッグ終了) のときだけ行い、
        /// キー操作中は <see cref="SaveDelaySeconds"/> 後にまとめる
        /// (毎フレーム PlayerPrefs を書かない)。
        /// </summary>
        public void Set(int index, float value, bool final)
        {
            float clamped = Mathf.Clamp01(value);
            switch (index)
            {
                case 0:
                    GManager.mainVolume = clamped;
                    break;
                case 1:
                    GManager.effectVolume = clamped;
                    break;
                case 2:
                    GManager.BGMVolume = clamped;
                    break;
                default:
                    return;
            }

            ApplyToOutputs();
            if (final)
            {
                saveAt = 0f;
                OptionSettings.Save();
                return;
            }

            saveAt = Time.unscaledTime + SaveDelaySeconds;
        }

        /// <summary>予約された保存を、猶予が過ぎていれば実行する (毎フレーム呼ぶ)。</summary>
        public void Tick()
        {
            if (saveAt <= 0f || Time.unscaledTime < saveAt)
            {
                return;
            }

            FlushNow();
        }

        /// <summary>猶予を待たずに予約済みの音量保存を実行する (画面を離れるとき)。</summary>
        public void FlushNow()
        {
            if (saveAt <= 0f)
            {
                return;
            }

            saveAt = 0f;
            OptionSettings.Save();
        }

        /// <summary>保存が予約されているか (検証用)。</summary>
        public bool HasPendingSave
        {
            get { return saveAt > 0f; }
        }

        /// <summary>
        /// 音量を実際の出力へ即時反映する。音量は各所有者が起動時にしか読まないので、
        /// 値を変えた瞬間に反映する (v5 PauseMenuView.ApplyVolumes と同じ挙動)。
        /// </summary>
        public static void ApplyToOutputs()
        {
            AudioListener.volume = GManager.mainVolume;

            var music = Object.FindFirstObjectByType<MusicManager>();
            if (music != null)
            {
                // Issue #681 review: 演奏音源は audioS と streamAudioSource
                // (StreamSeparate) の 2 系統。片方だけ書き換えると
                // StreamSeparate 再生中に音量が追従しない。
                music.ApplyBgmVolume();
            }

            var judge = Object.FindFirstObjectByType<Judge>();
            if (judge != null)
            {
                var src = judge.GetComponent<AudioSource>();
                if (src != null)
                {
                    src.volume = GManager.effectVolume;
                }
            }

            // Option 画面側の AudioSource / AudioMixer も同じ値へ追従させる。
            OptionSceneManager.ApplyVolumes();
        }
    }
}
