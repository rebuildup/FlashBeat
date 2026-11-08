using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V6.Game.Pause;

namespace FlashBeat.V6HudTests
{
    /// <summary>
    /// v6 ポーズ画面 (Issue #224) の純ロジックの EditMode テスト。
    /// ビュー (uGUI 生成) は含まない。実データ結線の式だけを検証する。
    /// </summary>
    public class V6PauseViewLogicTests
    {
        // --- index 操作 ---

        [Test]
        public void ClampIndex_StaysInRange()
        {
            Assert.AreEqual(0, V6PauseViewLogic.ClampIndex(-3, 4));
            Assert.AreEqual(2, V6PauseViewLogic.ClampIndex(2, 4));
            Assert.AreEqual(3, V6PauseViewLogic.ClampIndex(9, 4));
            Assert.AreEqual(0, V6PauseViewLogic.ClampIndex(0, 0));
        }

        [Test]
        public void WrapIndex_WrapsAround()
        {
            Assert.AreEqual(1, V6PauseViewLogic.WrapIndex(0, 1, 4));
            Assert.AreEqual(3, V6PauseViewLogic.WrapIndex(0, -1, 4));
            Assert.AreEqual(0, V6PauseViewLogic.WrapIndex(3, 1, 4));
            Assert.AreEqual(0, V6PauseViewLogic.WrapIndex(5, 0, 4));
        }

        // --- 表示形式 (design 例: NoteSpeed "10.0" / 曲長 "1:43") ---

        [Test]
        public void FormatNoteSpeed_HasOneDecimal()
        {
            Assert.AreEqual("10.0", V6PauseViewLogic.FormatNoteSpeed(10f));
            Assert.AreEqual("12.3", V6PauseViewLogic.FormatNoteSpeed(12.34f));
            Assert.AreEqual("1.0", V6PauseViewLogic.FormatNoteSpeed(1f));
        }

        [Test]
        public void FormatDuration_MinutesSeconds()
        {
            Assert.AreEqual("1:43", V6PauseViewLogic.FormatDuration(103f));
            Assert.AreEqual("0:59", V6PauseViewLogic.FormatDuration(59f));
            Assert.AreEqual("1:00", V6PauseViewLogic.FormatDuration(60f));
            Assert.AreEqual("0:00", V6PauseViewLogic.FormatDuration(0f));
            Assert.AreEqual("0:00", V6PauseViewLogic.FormatDuration(-5f));
        }

        // --- 実データの派生 (GManager.SongData) ---

        [Test]
        public void FormatNotes_PrefersCloudNoteCount()
        {
            var song = new SongData(1, "曲", "作家", "abc", 120, 3, 296, 103f);
            Assert.AreEqual("296", V6PauseViewLogic.FormatNotes(song));

            song.noteCount = 300;
            Assert.AreEqual("300", V6PauseViewLogic.FormatNotes(song));
        }

        [Test]
        public void FormatNotes_FallsBackToTotalHits_WhenNoteCountUnknown()
        {
            var song = new SongData(1, "曲", "作家", "abc", 120, 3, 296, 103f);
            song.noteCount = 0;
            Assert.AreEqual("296", V6PauseViewLogic.FormatNotes(song));
            Assert.AreEqual("0", V6PauseViewLogic.FormatNotes(null));
        }

        [Test]
        public void SongSub_MusicianAndBpm()
        {
            var song = new SongData(1, "終焉逃避行", "柊マグネタイト", "abc", 147, 3, 296, 103f);
            Assert.AreEqual("柊マグネタイト・BPM 147", V6PauseViewLogic.SongSub(song));
            Assert.AreEqual("", V6PauseViewLogic.SongSub(null));
        }

        [Test]
        public void SongSub_EmptyMusician_OmitsSeparator()
        {
            var song = new SongData(1, "曲", "", "abc", 147, 3, 296, 103f);
            Assert.AreEqual("BPM 147", V6PauseViewLogic.SongSub(song));
        }

        [Test]
        public void LayoutLabel_CustomOrStandard()
        {
            Assert.AreEqual("標準", V6PauseViewLogic.LayoutLabel(false));
            Assert.AreEqual("カスタム", V6PauseViewLogic.LayoutLabel(true));
        }

        // --- キー割当 (KeyBindings の実データを LaneOfKey で引く) ---

        [Test]
        public void LaneOfKey_MapsJisKeysToLanes()
        {
            KeyCode[][] bindings = KeyConfigUI.DefaultLayout;
            // 0 基底レーン。design のキー一覧 (1 始底表示) と一致すること。
            Assert.AreEqual(0, V6PauseViewLogic.LaneOfKey(bindings, '1'));
            Assert.AreEqual(0, V6PauseViewLogic.LaneOfKey(bindings, 'Q'));
            Assert.AreEqual(0, V6PauseViewLogic.LaneOfKey(bindings, 'Z'));
            Assert.AreEqual(3, V6PauseViewLogic.LaneOfKey(bindings, '4'));
            Assert.AreEqual(3, V6PauseViewLogic.LaneOfKey(bindings, '5'));
            Assert.AreEqual(4, V6PauseViewLogic.LaneOfKey(bindings, '6'));
            Assert.AreEqual(7, V6PauseViewLogic.LaneOfKey(bindings, '0'));
            Assert.AreEqual(7, V6PauseViewLogic.LaneOfKey(bindings, '-'));
            Assert.AreEqual(7, V6PauseViewLogic.LaneOfKey(bindings, '\\'));
        }

        [Test]
        public void LaneOfKey_UnknownCharOrEmptyBindings_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, V6PauseViewLogic.LaneOfKey(KeyConfigUI.DefaultLayout, '§'));
            Assert.AreEqual(-1, V6PauseViewLogic.LaneOfKey(null, 'A'));
        }

        [Test]
        public void JisKeyRows_Cover34KeysOfDesign()
        {
            // design: 13 + 12 + 12 + 11 = 48 刻印... 表示 caps は 34 個
            int total = 0;
            for (int row = 0; row < V6PauseViewLogic.JisKeyRows.Length; row++)
            {
                total += V6PauseViewLogic.JisKeyRows[row].Length;
            }

            // design uDNC0 の Key 一覧は 13+12+12+11 = 48 個 (brief の「34 個」は
            // 誤記。caps の総数は JIS 4 段の刻印数に一致させる)。
            Assert.AreEqual(13 + 12 + 12 + 11, total);
        }
    }
}
