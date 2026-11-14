using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.UI.V6.Option
{
    /// <summary>
    /// V6OptionView のデータ操作 API と uGUI ユーティリティ (Issue #385 Phase 2.5 で分割)。
    /// 設定値の書き戻し (Adjust*/Toggle*/Set*/Reset*/Clear*/MediaCache* /
    /// IsDefaultKeyLayout / CancelKeyConfigEdit) は既存の保存経路へ委譲する
    /// public static API で、EditMode テストからも同じ経路を呼べる。
    /// 末尾は design px 左上原点の Rect / DrawLine ヘルパー。partial class のため
    /// フィールド・メソッドは V6OptionView 本体と共有する。
    /// </summary>
    public sealed partial class V6OptionView
    {
        // --- データ操作 (既存 API へ委譲するだけ) ---
        //
        // ここで新しい保存経路は作らない。表示値・実動作・保存先は既存の
        // OptionSettings (PlayerPrefs) / ScorePersistence (安定キーのベストスコア) /
        // KeyBindings・KeyActions (キー設定) / PlaySession (セッション設定) /
        // MediaCacheManager (キャッシュ) がそのまま担う。
        // public static にしてあるので、EditMode テストから同じ経路を呼べる。

        /// <summary>NoteSpeed。既存の OptionSceneManager.AdjustNoteSpeed (clamp + 保存) へ委譲。</summary>
        public static void AdjustNoteSpeed(int delta)
        {
            OptionSceneManager.AdjustNoteSpeed(delta);
        }

        /// <summary>NoteTiming。既存の OptionSceneManager.AdjustNoteTiming (保存 + offset 書込) へ委譲。</summary>
        public static void AdjustNoteTiming(int delta)
        {
            OptionSceneManager.AdjustNoteTiming(delta);
        }

        /// <summary>Main Volume。即時反映 (ApplyVolumes) + 保存。</summary>
        public static void AdjustMainVolume(int delta)
        {
            GManager.mainVolume = V6OptionLogic.AdjustVolume(GManager.mainVolume, delta);
            OptionSceneManager.ApplyVolumes();
            OptionSettings.Save();
        }

        /// <summary>Effect Volume。即時反映 (ApplyVolumes) + 保存。</summary>
        public static void AdjustEffectVolume(int delta)
        {
            GManager.effectVolume = V6OptionLogic.AdjustVolume(GManager.effectVolume, delta);
            OptionSceneManager.ApplyVolumes();
            OptionSettings.Save();
        }

        /// <summary>BGM Volume。即時反映 (ApplyVolumes) + 保存。</summary>
        public static void AdjustBgmVolume(int delta)
        {
            GManager.BGMVolume = V6OptionLogic.AdjustVolume(GManager.BGMVolume, delta);
            OptionSceneManager.ApplyVolumes();
            OptionSettings.Save();
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
            GManager.FlashBG = V6OptionLogic.CycleFlashSpeed(GManager.FlashBG, delta);
            OptionSettings.Save();
        }

        /// <summary>キーガイドの表示形式 (セッション設定。保存はしない)。</summary>
        public static void SetKeyGuide(int delta)
        {
            PlaySession.SetKeyGuide(PlaySession.NextKeyGuide(PlaySession.KeyGuide, delta));
        }

        /// <summary>歌詞の表示 On/Off (セッション設定。保存はしない)。</summary>
        public static void ToggleLyrics()
        {
            PlaySession.SetLyricsEnabled(!PlaySession.LyricsEnabled);
        }

        /// <summary>レーンのキー割り当てを既定へ戻す (保存は KeyBindings が行う)。</summary>
        public static void ResetKeyLayout()
        {
            KeyBindings.ResetToDefaults();
        }

        /// <summary>キャッシュ容量の表示 (既存の MediaCacheManager の実測値)。</summary>
        public static string MediaCacheSizeLabel()
        {
            return Gameplay.MediaCacheManager.FormatSize(Gameplay.MediaCacheManager.GetCacheSizeBytes());
        }

        /// <summary>ダウンロード済みキャッシュを消す (既存の MediaCacheManager)。</summary>
        public static void ClearMediaCache()
        {
            Gameplay.MediaCacheManager.ClearAll();
        }

        /// <summary>
        /// ハイスコアとキー設定 (レーン・メニュー操作) を消去する。
        ///
        /// 消去対象は案内している既存データだけ:
        /// - ベストスコア (実体は安定キーの ScorePersistence / FlashBeat.ScoreStore)。
        ///   旧 PlayerPrefs の Hiscore だけ消すと、安定した保存先から記録が復活する
        ///   ので、既存の ScorePersistence.ResetAll() でまとめて消す。
        /// - キー設定 (レーン = KeyBindings / メニュー操作 = KeyActions)。
        ///
        /// 既存の API (ScorePersistence.ResetAll / KeyBindings.ResetToDefaults /
        /// KeyActions.ResetToDefaults) をそのまま使うので、新しい保存経路は増えない。
        /// </summary>
        public static void ClearSaveData()
        {
            FlashBeat.Persistence.ScorePersistence.ResetAll();

            if (GManager.Hiscore != null)
            {
                Array.Clear(GManager.Hiscore, 0, GManager.Hiscore.Length);
            }

            KeyBindings.ResetToDefaults();
            KeyActions.ResetToDefaults();
        }

        /// <summary>キー割当が既定と同じか (正本は KeyBindings.DefaultKeys)。</summary>
        public static bool IsDefaultKeyLayout()
        {
            return KeyConfigUI.IsDefaultLayout(KeyBindings.Current, KeyBindings.DefaultKeys);
        }

        /// <summary>
        /// キー設定 (KeyConfigUI) の編集をキャンセルして閉じる (Option の F1)。
        ///
        /// 保存済みの状態 (KeyBindings / KeyActions の保存先) へ戻してから閉じる。
        /// 閉じるときの保存は戻した内容を書き直すだけなので、保存値は変わらない
        /// (元の割当に戻る)。確定は従来どおり Esc / F2 で閉じる (= 保存)。
        ///
        /// 「開いた時点の状態 == 保存先」は既存の保存経路が保証する
        /// (KeyConfigUI の閉じる保存 / ResetToDefaults の保存 / ClearSaveData)。
        /// 未保存のまま残ることは無いので、Load() で開いた時点へ正確に戻れる。
        /// </summary>
        public static void CancelKeyConfigEdit()
        {
            KeyBindings.Load();
            KeyActions.Load();
            KeyConfigUI.Close();
        }

        // --- uGUI ユーティリティ (design px 左上原点) ---

        /// <summary>素の Image (角丸無し)。細線 / 矩形用 (共通部品に 1px 線は無いため最小限のローカル helper)。</summary>
        private static GameObject Rect(GameObject parent, string name, float w, float h, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent.transform, false);
            // サイズを必ず反映する。既定 (100×100) のまま回転すると、細線のつもりの
            // レーン線が 100px 幅の帯になり、プレビューが灰色のコーン状に潰れる。
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return go;
        }

        /// <summary>(x1,y1) → (x2,y2) へ細い線を引く (design px 左上原点)。</summary>
        private static void DrawLine(GameObject parent, string name, float x1, float y1, float x2, float y2,
            float width, Color color)
        {
            // 左上原点 → uGUI の下上原点へ変換 (Game Preview の高さ 280 基準)。
            float lx1 = x1, ly1 = GamePrevH - y1;
            float lx2 = x2, ly2 = GamePrevH - y2;
            float dx = lx2 - lx1;
            float dy = ly2 - ly1;
            float length = Mathf.Sqrt(dx * dx + dy * dy);
            var line = Rect(parent, name, width, length, color);
            var rt = line.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2((lx1 + lx2) * 0.5f, (ly1 + ly2) * 0.5f);
            rt.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(dy, dx) * Mathf.Rad2Deg - 90f);
        }
    }
}
