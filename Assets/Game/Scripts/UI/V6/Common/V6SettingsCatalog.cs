using System;
using System.Collections.Generic;
using UnityEngine;
using FlashBeat.Gameplay;
using FlashBeat.UI.V5;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>
    /// 設定画面 (Option / Pause) のホスト側が提供する差分 (Issue #733)。
    /// 行スキーマ・値の読み書き・保存経路は <see cref="V6SettingsCatalog"/> が
    /// 単一ソースとして持ち、ここには「その画面固有の文脈」だけを置く。
    /// </summary>
    public interface IV6SettingsHost
    {
        /// <summary>
        /// ゲーム進行中の画面 (Pause) から host されているか。
        /// 現在は行スキーマの出し分けには使っていない (v0.2.1 で Sound タブは
        /// 両画面同一になった)。ホスト固有の将来の出し分け用に契約として残す。
        /// </summary>
        bool IsGameplayHost { get; }

        /// <summary>音量行の値モデル (遅延保存つき)。両画面で同じ実装を使う。</summary>
        V6VolumeSettings Volumes { get; }

        /// <summary>キー設定モーダル (design vo4kB) を開く。</summary>
        void OpenKeyConfig();

        /// <summary>Key Layout の標準リセット確認を出す (破壊的なので即実行しない)。</summary>
        void RequestResetKeyLayout();

        /// <summary>
        /// Display タブのセッション設定 (Key Guide / Lyrics) が変わったあとに呼ぶ。
        /// Pause は歌詞表示へ即時反映するため <c>ApplyLyricsVisibility</c> を呼ぶ。
        /// Option はゲーム外なので no-op でよい。
        /// </summary>
        void OnDisplaySettingChanged();
    }

    /// <summary>
    /// 設定 1 行の定義 (Issue #733)。Option / Pause の両方がこの記述子から行を
    /// 組み立てる。値の読み取り (<see cref="Value"/>)・調整 (<see cref="Adjust"/>)・
    /// 決定 (<see cref="Activate"/>) はすべて実データへの委譲で、
    /// 保存経路は <see cref="OptionSettings"/> / <see cref="KeyBindings"/> /
    /// <see cref="PlaySession"/> / <see cref="V6VolumeSettings"/> に一本化する。
    /// </summary>
    public sealed class V6SettingRow
    {
        public readonly string Label;
        public readonly Func<string> Value;
        /// <summary>←→ / ステッパー 1 回分の調整 (第 2 引数は Shift 併用)。</summary>
        public readonly Action<int, bool> Adjust;
        /// <summary>Enter / 行クリックでの決定。null なら決定しても何も起きない。</summary>
        public readonly Action Activate;
        public readonly string DescTitle;
        public readonly string Desc;
        /// <summary>破壊的な操作の行 (ラベルを danger 色にする)。</summary>
        public readonly bool Danger;
        /// <summary>ステッパーを持たない行 (キー設定 / Data の操作行)。</summary>
        public readonly bool IsAction;
        /// <summary>音量行の対象 (0=Main / 1=Effect / 2=BGM、それ以外は -1)。</summary>
        public readonly int VolumeIndex;
        /// <summary>値の右に出す導線アイコン (<c>Resources/Icons</c> の名前。不要なら null)。</summary>
        public readonly string ValueIcon;
        /// <summary>
        /// アクション行の右端に出す小型ボタンのラベル (例: "開く")。
        /// 設定すると値テキストの代わりに、共通ボタンと同じ面・アイコン・
        /// 押下フィードバックを持つボタンとして描画する (Issue #798)。
        /// null なら従来どおり値テキスト (+ <see cref="ValueIcon"/>) を出す。
        /// </summary>
        public readonly string ActionButtonLabel;

        public V6SettingRow(string label, Func<string> value, Action<int, bool> adjust,
            Action activate, string descTitle, string desc,
            bool danger = false, bool isAction = false, int volumeIndex = -1,
            string valueIcon = null, string actionButtonLabel = null)
        {
            Label = label;
            Value = value;
            Adjust = adjust;
            Activate = activate;
            DescTitle = descTitle;
            Desc = desc;
            Danger = danger;
            IsAction = isAction;
            VolumeIndex = volumeIndex;
            ValueIcon = valueIcon;
            ActionButtonLabel = actionButtonLabel;
        }
    }

    /// <summary>
    /// Option / Pause 共通の設定行カタログ (Issue #733)。
    /// 「どの行が・どの順で・どんなラベルと値書式で存在するか」の単一ソース。
    /// ホスト固有の行 (Option の Data 操作 / Pause の実行時統計) はカタログの
    /// 対象外で、各ビューが同じ <see cref="V6SettingRow"/> 記述子で追加する。
    /// </summary>
    public static class V6SettingsCatalog
    {
        /// <summary>Play / Display / Sound の 3 タブ分の行を返す。Data (index 3) は空。</summary>
        public static List<V6SettingRow> RowsForTab(int tab, IV6SettingsHost host)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            switch (tab)
            {
                case 0: return PlayRows(host);
                case 1: return DisplayRows(host);
                case 2: return SoundRows(host);
                default: return new List<V6SettingRow>();
            }
        }

        // --- Play タブ ---

        private static List<V6SettingRow> PlayRows(IV6SettingsHost host)
        {
            return new List<V6SettingRow>
            {
                new V6SettingRow("NoteSpeed",
                    () => FormatNoteSpeed(GManager.noteSpeed),
                    (d, _) => OptionSceneManager.AdjustNoteSpeed(d),
                    null,
                    "NoteSpeed",
                    "ノーツが出現してから判定線に届くまでの時間。数値が大きいほど速く落ちます。"),
                new V6SettingRow("NoteTiming",
                    () => FormatNoteTiming(GManager.noteTiming),
                    (d, _) => OptionSceneManager.AdjustNoteTiming(d),
                    null,
                    "NoteTiming",
                    "判定タイミングのずれを調整します。変更は全曲の譜面設定 (offset) に書き込まれます。"),
                new V6SettingRow("Key Layout",
                    () => KeyLayoutLabel(),
                    (d, _) =>
                    {
                        if (d > 0)
                        {
                            host.OpenKeyConfig(); // カスタムはキー設定で編集する
                        }
                        else if (IsCustomKeyLayout())
                        {
                            host.RequestResetKeyLayout(); // 破壊的なリセットは確認してから
                        }
                    },
                    host.OpenKeyConfig,
                    "Key Layout",
                    "キー配列を選びます。標準は既定の配列、カスタムはキー設定で編集します。左キーで標準に戻します (確認ダイアログが出ます)。"),
                new V6SettingRow("キー設定",
                    () => "開く",
                    null,
                    host.OpenKeyConfig,
                    "キー設定",
                    "8 レーンの代表キーを変更します。行を選んで Enter を押し、次に押したキーが割り当てられます。",
                    isAction: true,
                    actionButtonLabel: "開く"),
            };
        }

        // --- Display タブ ---

        private static List<V6SettingRow> DisplayRows(IV6SettingsHost host)
        {
            return new List<V6SettingRow>
            {
                new V6SettingRow("Flash",
                    () => GManager.Flash ? "On" : "Off",
                    (d, _) =>
                    {
                        if (d != 0)
                        {
                            ToggleFlash();
                        }
                    },
                    ToggleFlash,
                    "Flash",
                    "ノーツ判定時の背景フラッシュの On/Off。"),
                new V6SettingRow("FlashTiming",
                    () => GManager.FlashT.ToString(),
                    (d, _) => AdjustFlashTiming(d),
                    null,
                    "FlashTiming",
                    "フラッシュのタイミングを調整します。"),
                new V6SettingRow("FlashSpeed",
                    () => GManager.FlashBG.ToString(),
                    (d, _) => AdjustFlashSpeed(d),
                    null,
                    "FlashSpeed",
                    "フラッシュの速さ。1 が最も速く、8 が最も遅い。"),
                new V6SettingRow("Key Guide",
                    () => PlaySession.KeyGuideLabel(PlaySession.KeyGuide),
                    (d, _) =>
                    {
                        PlaySession.SetKeyGuide(PlaySession.NextKeyGuide(PlaySession.KeyGuide, d));
                        host.OnDisplaySettingChanged();
                    },
                    () =>
                    {
                        PlaySession.SetKeyGuide(PlaySession.NextKeyGuide(PlaySession.KeyGuide, 1));
                        host.OnDisplaySettingChanged();
                    },
                    "Key Guide",
                    "プレイ中の下部キーガイドの表示形式。"),
                new V6SettingRow("Lyrics",
                    () => PlaySession.LyricsLabel(PlaySession.LyricsEnabled),
                    (d, _) =>
                    {
                        if (d != 0)
                        {
                            ToggleLyrics(host);
                        }
                    },
                    () => ToggleLyrics(host),
                    "Lyrics",
                    "プレイ中の歌詞表示の On/Off。"),
            };
        }

        // --- Sound タブ ---

        private static List<V6SettingRow> SoundRows(IV6SettingsHost host)
        {
            // 音量行は VolumeIndex を持ち、←→ / ステッパー / スライダーが
            // 同じ値モデル (<see cref="V6VolumeSettings"/>) を編集する
            // (Issue #375)。Adjust はホストの Volumes へ委譲する。
            var rows = new List<V6SettingRow>
            {
                VolumeRow(host, 0, "Main Volume",
                    "全体の音量。スライダーをドラッグするか左右キーで調整します (Shift で 10% 刻み)。"),
                // Issue #851: プレイ楽曲はメニュー BGM と別チャンネル。
                // リズムゲームの主体なので既定は十分に聞こえる値。
                VolumeRow(host, 3, "Song Volume",
                    "プレイ中の楽曲の音量。スライダーをドラッグするか左右キーで調整します (Shift で 10% 刻み)。"),
                // v0.2.1 (#733 実機レビュー + PR #787 review): BGM Volume は
                // Pause にも出す。消費者はメニュー画面 (Title/Select/Option/
                // Result) の背景動画音声 (VideoTime → GManager.BGMVolume)。
                // プレイ楽曲は Song Volume 側 (MusicManager →
                // GManager.songVolume) で、こちらとは独立。
                // Pause と Option で Sound タブの項目を一致させる。
                VolumeRow(host, 2, "BGM Volume",
                    "メニュー画面の背景動画の音量。スライダーをドラッグするか左右キーで調整します (Shift で 10% 刻み)。"),
                VolumeRow(host, 1, "Effect Volume",
                    "効果音の音量。スライダーをドラッグするか左右キーで調整します (Shift で 10% 刻み)。"),
            };

            return rows;
        }

        private static V6SettingRow VolumeRow(IV6SettingsHost host, int index, string label, string desc)
        {
            return new V6SettingRow(label,
                () => FormatVolume(V6VolumeSettings.Get(index)),
                (d, shift) => host.Volumes.Nudge(index, d, shift),
                null,
                label,
                desc,
                volumeIndex: index);
        }

        // --- 共有の値書式・操作 (両画面で同じ表示・同じ保存経路) ---

        /// <summary>
        /// キー割当が既定と同じか (正本は KeyBindings.DefaultKeys)。
        /// Option 側の旧 <c>IsDefaultKeyLayout</c> と同じ比較を使う
        /// (current == null は既定ではない = カスタム扱い)。
        /// </summary>
        public static bool IsCustomKeyLayout()
        {
            return !KeyConfigUI.IsDefaultLayout(KeyBindings.Current, KeyBindings.DefaultKeys);
        }

        /// <summary>Key Layout 行の表示 ("標準" / "カスタム")。</summary>
        public static string KeyLayoutLabel()
        {
            return IsCustomKeyLayout() ? "カスタム" : "標準";
        }

        /// <summary>NoteSpeed の表示 (F1)。</summary>
        public static string FormatNoteSpeed(float value)
        {
            return value.ToString("F1");
        }

        /// <summary>NoteTiming の表示。</summary>
        public static string FormatNoteTiming(int value)
        {
            return value.ToString();
        }

        /// <summary>音量 (0..1) の表示 ("50%")。</summary>
        public static string FormatVolume(float value)
        {
            float clamped = value < 0f ? 0f : (value > 1f ? 1f : value);
            return (int)Math.Round(clamped * 100f) + "%";
        }

        /// <summary>背景フラッシュの On/Off を切り替えて保存する。</summary>
        public static void ToggleFlash()
        {
            GManager.Flash = !GManager.Flash;
            OptionSettings.Save();
        }

        /// <summary>フラッシュのタイミング調整。負の値 (計算が成立しない) は 0 へ寄せる。</summary>
        public static void AdjustFlashTiming(int delta)
        {
            GManager.FlashT = Mathf.Max(0, GManager.FlashT + delta);
            OptionSettings.Save();
        }

        /// <summary>フラッシュの速さ (1 → 2 → 4 → 8 → 1)。</summary>
        public static void AdjustFlashSpeed(int delta)
        {
            int[] steps = { 1, 2, 4, 8 };
            int index = Array.IndexOf(steps, GManager.FlashBG);
            if (index < 0)
            {
                index = 0;
            }

            GManager.FlashBG = steps[(index + delta % steps.Length + steps.Length) % steps.Length];
            OptionSettings.Save();
        }

        /// <summary>歌詞の表示 On/Off (セッション設定。保存はしない)。</summary>
        public static void ToggleLyrics(IV6SettingsHost host)
        {
            PlaySession.SetLyricsEnabled(!PlaySession.LyricsEnabled);
            if (host != null)
            {
                host.OnDisplaySettingChanged();
            }
        }
    }
}
