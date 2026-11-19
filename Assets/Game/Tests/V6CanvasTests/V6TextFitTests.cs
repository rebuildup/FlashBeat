using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using FlashBeat.Update;
using FlashBeat.UI.V6.Common;
using FlashBeat.UI.V6.Game.Pause;
using FlashBeat.UI.V6.History;
using FlashBeat.UI.V6.Ranking;
using FlashBeat.UI.V6.Result;
using FlashBeat.UI.V6.Select;
using FlashBeat.UI.V6.Select.View;
using FlashBeat.UI.V6.Title;
using FlashBeat.Update;

namespace FlashBeat.V6CanvasTests
{
    /// <summary>
    /// Issue #749: 「テキストが design 矩形に収まる」共通規約
    /// (<see cref="V6TextFit"/> = Ellipsis / AutoFit) の回帰検証。
    ///
    /// ここで固定する契約:
    /// - ランキング行の Name は長いニックネームでも 1 行 + 末尾省略で Name 列に収まる
    /// - History 中央 Title は長い曲名でも 1 行 (auto-size) で Artist/Stats と重ならない
    /// - 空状態・エラー文は Normal 折返しで 2 行化せず矩形内に収まる
    /// - Result モーダルの Status / Register ラベルは全文を読める範囲で縮む
    /// - Pause 行の値 ("P1234 G123 B12 M8" / "9,999,999") は 120px 列に収まる
    /// - 要視覚確認だった境界ケース (Title 更新ボタン / KeyConfig Status /
    ///   CasualNote) も実測で矩形内であることを固定する
    ///
    /// 判定は V6ResultGradeFitTests と同じく TMP の実メッシュ頂点 (インク) を
    /// 設計矩形と比較する。Unity を起動しない compile-check では実行できないため、
    /// 実機/CI の EditMode 実行で最終確認する。
    /// </summary>
    public sealed class V6TextFitTests
    {
        private GameObject canvasGo;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("V6TextFitTests Canvas", typeof(RectTransform), typeof(Canvas));
        }

        [TearDown]
        public void TearDown()
        {
            if (canvasGo != null)
            {
                Object.DestroyImmediate(canvasGo);
            }
            canvasGo = null;
        }

        // --- テスト用データソース ---

        private sealed class FakeSource : IV6SelectDataSource
        {
            public readonly List<V6SongEntry> Songs = new List<V6SongEntry>();
            public readonly List<V6RankingRow> OnlineRows = new List<V6RankingRow>();
            public V6SongEntry SelectedValue;
            public bool Error;
            public string Message = "";

            public IReadOnlyList<V6SongEntry> AllSongs { get { return Songs; } }
            public IReadOnlyList<V6SongEntry> FilteredSongs { get { return Songs; } }
            public string Query { get { return ""; } set { } }
            public V6SongEntry Selected { get { return SelectedValue; } }
            public bool IsLoading { get { return false; } }
            public bool HasError { get { return Error; } }
            public string StatusMessage { get { return Message; } }
            public event System.Action Changed;

            public void SetQuery(string query) { }
            public void Select(int songId) { SelectedValue = Songs.Find(s => s.SongId == songId); }
            public void MoveSelection(int delta) { }
            public void RefreshAsync() { }
            public void CommitReady() { }

            public IReadOnlyList<V6RankingRow> RankingFor(V6SongEntry song)
            {
                return song != null && song.IsCloud ? OnlineRows : new List<V6RankingRow>();
            }
        }

        private static V6SongEntry Song(int id, string title)
        {
            return new V6SongEntry
            {
                SongId = id,
                Title = title,
                Artist = "稲葉曇",
                Level = 8,
                Bpm = 274.0,
                NoteCount = 268,
                JacketResourcePath = "",
                BestScore = 1234567,
                BestGrade = "S",
            };
        }

        // --- ヘルパ ---

        /// <summary>TMP textInfo のメッシュ頂点からインク外接矩形 (V6ResultGradeFitTests と同じ走査)。</summary>
        private static void InkSize(TextMeshProUGUI tmp, out float width, out float height)
        {
            tmp.ForceMeshUpdate(true, true);
            var info = tmp.textInfo;
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            if (info != null && info.meshInfo != null)
            {
                int quadLimit = info.characterCount * 4;
                for (int m = 0; m < info.meshInfo.Length; m++)
                {
                    Vector3[] verts = info.meshInfo[m].vertices;
                    if (verts == null)
                    {
                        continue;
                    }

                    int limit = Mathf.Min(verts.Length, quadLimit);
                    for (int i = 0; i + 3 < limit; i += 4)
                    {
                        for (int k = 0; k < 4; k++)
                        {
                            Vector3 v = verts[i + k];
                            if (v.x < minX) { minX = v.x; }
                            if (v.x > maxX) { maxX = v.x; }
                            if (v.y < minY) { minY = v.y; }
                            if (v.y > maxY) { maxY = v.y; }
                        }
                    }
                }
            }

            width = maxX >= minX ? maxX - minX : 0f;
            height = maxY >= minY ? maxY - minY : 0f;
        }

        /// <summary>名前一致の TMP を深さ優先で探す (parentName 直下の childName 優先)。</summary>
        private static TextMeshProUGUI FindText(Transform root, string parentName, string childName)
        {
            var texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i].name == childName
                    && texts[i].transform.parent != null
                    && texts[i].transform.parent.name == parentName)
                {
                    return texts[i];
                }
            }

            Assert.Fail("TMP '" + parentName + "/" + childName + "' not found");
            return null;
        }

        private static Transform FindTransform(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            return null;
        }

        private static TextMeshProUGUI FindTextByName(Transform root, string name)
        {
            var texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i].name == name)
                {
                    return texts[i];
                }
            }

            Assert.Fail("TMP '" + name + "' not found");
            return null;
        }

        /// <summary>
        /// 1 行・インクが矩形内・(auto-size 期待時は) fontSizeMin/Max の契約を確認する。
        /// </summary>
        private static void AssertFitting(TextMeshProUGUI tmp, float boxW, float boxH, string label,
            bool expectAutoFit = false, bool expectEllipsis = false)
        {
            Assert.AreEqual(TextWrappingModes.NoWrap, tmp.textWrappingMode, label + " must not wrap");
            if (expectAutoFit)
            {
                Assert.IsTrue(tmp.enableAutoSizing, label + " must use TMP auto-size");
                Assert.Greater(tmp.fontSizeMax, tmp.fontSizeMin, label + " fontSizeMax above min");
            }

            if (expectEllipsis)
            {
                Assert.AreEqual(TextOverflowModes.Ellipsis, tmp.overflowMode, label + " must ellipsize");
            }

            tmp.ForceMeshUpdate(true, true);
            Assert.Greater(tmp.textInfo.characterCount, 0, label + " must render glyphs (0-line regression)");
            Assert.LessOrEqual(tmp.textInfo.lineCount, 1, label + " must stay on a single line");

            InkSize(tmp, out float inkW, out float inkH);
            Assert.Greater(inkW, 0f, label + " ink width must be measurable");
            Assert.Greater(inkH, 0f, label + " ink height must be measurable");
            Assert.LessOrEqual(inkW, boxW + 0.5f, label + " ink width must fit its box");
            Assert.LessOrEqual(inkH, boxH + 0.5f, label + " ink height must fit its box");
        }

        // --- 1. ランキング行 Name の省略 (V6Lists) ---

        /// <summary>ニックネーム最長 16 文字級の長い名前でも Name 列からはみ出さない。</summary>
        [Test]
        public void RankRows_LongName_EllipsizesInsideNameColumn()
        {
            const string longName = "VeryLongPlayerName"; // 18 文字 > Name 160/140px
            var parent = new GameObject("rows", typeof(RectTransform));
            parent.transform.SetParent(canvasGo.transform, false);
            try
            {
                var rank = V6Lists.RankRow(parent, "1", longName, "1,234,567", "SS",
                    Vector2.zero, new Vector2(V6Lists.RankRowW, V6Lists.RankRowH));
                var compact = V6Lists.CompactRankRow(parent, "2", longName, "987654",
                    "A", new Vector2(0f, 60f));
                var player = V6Lists.PlayerRow(parent, "3", longName, "1,000,000", "12",
                    new Vector2(0f, 120f), new Vector2(V6Lists.RankRowW, V6Lists.RankRowH));

                AssertFitting(rank.transform.Find("Name").GetComponent<TextMeshProUGUI>(),
                    V6Lists.RankNameW, V6Lists.RankTextH, "RankRow/Name", expectEllipsis: true);
                AssertFitting(compact.transform.Find("Name").GetComponent<TextMeshProUGUI>(),
                    V6Lists.CompactRankNameW, V6Lists.CompactRankTextH, "CompactRankRow/Name",
                    expectEllipsis: true);
                AssertFitting(player.transform.Find("Name").GetComponent<TextMeshProUGUI>(),
                    V6Lists.RankNameW, V6Lists.RankTextH, "PlayerRow/Name", expectEllipsis: true);
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        // --- 2. History 中央 Title ---

        [Test]
        public void HistoryCenter_LongTitle_ShrinksToSingleLine()
        {
            var source = new FakeSource();
            var song = Song(1, "とても長い曲名がここに入ります〜フルタイトルedition〜");
            source.Songs.Add(song);
            source.SelectedValue = song;

            var view = V6HistoryView.Create((RectTransform)canvasGo.transform, null, source, null, null);
            try
            {
                var title = FindText(view.Root, "Meta", "Title");
                Assert.AreEqual(song.Title, title.text);
                AssertFitting(title, 512f, 50f, "History Center Title", expectAutoFit: true);
                Assert.AreEqual(30f, title.fontSizeMin, 0.01f, "design fs48 の下限は Select と同じ 30");
                Assert.AreEqual(48f, title.fontSizeMax, 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }

        // --- 3. 空状態・エラー文 ---

        private const string LongError =
            "ネットワークに接続できないためランキングを取得できませんでした";

        [Test]
        public void ResultRankingEmpty_LongError_StaysInsideRect()
        {
            var view = V6ResultView.Create((RectTransform)canvasGo.transform, null, null, null);
            try
            {
                var songEmpty = FindText(view.Root, "Song Ranking", "No Data");
                var playerEmpty = FindText(view.Root, "Player Ranking", "No Data");
                songEmpty.text = LongError;
                playerEmpty.text = LongError;

                AssertFitting(songEmpty, 496f, 44f, "Song Ranking/No Data", expectAutoFit: true);
                AssertFitting(playerEmpty, 496f, 44f, "Player Ranking/No Data", expectAutoFit: true);
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }

        [Test]
        public void SelectHeader_Status_LongError_Ellipsizes()
        {
            var source = new FakeSource
            {
                Error = true,
                Message = "カタログの取得に失敗しました: " + LongError + " (detail: server timeout after 30s)",
            };

            var view = V6SelectView.Create((RectTransform)canvasGo.transform, source, null, null, null);
            try
            {
                var status = FindText(view.Root, "Header", "Status");
                AssertFitting(status, 792f, 20f, "Select Header/Status", expectEllipsis: true);
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }

        [Test]
        public void RankingScoreEmpty_LongError_StaysInsideRect()
        {
            var source = new FakeSource();
            var view = V6RankingView.Create((RectTransform)canvasGo.transform, null, source, null, null);
            try
            {
                var noData = FindText(view.Root, "Score Ranking", "No Data");
                noData.text = LongError;
                AssertFitting(noData, 496f, 44f, "Score Ranking/No Data", expectAutoFit: true);

                // 監査の要視覚確認ケース: CasualNote は既定文言で 496×26 に収まること。
                var note = FindTextByName(view.Root, "CasualNote");
                AssertFitting(note, V6RankingLogic.CasualNoteW, V6RankingLogic.CasualNoteH,
                    "Score Ranking/CasualNote");
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }

        [Test]
        public void HistoryList_EmptyNote_Texts_StayInsidePanel()
        {
            var source = new FakeSource(); // 曲 0 件 → Empty Note 表示
            var view = V6HistoryView.Create((RectTransform)canvasGo.transform, null, source, null, null);
            try
            {
                Transform empty = FindTransform(view.Root, "Empty Note");
                Assert.IsNotNull(empty, "Empty Note が表示される");
                var title = FindText(empty, "Inner", "Title");
                var hint = FindText(empty, "Inner", "Hint");
                Assert.AreEqual(432f, title.rectTransform.sizeDelta.x, 0.01f,
                    "Empty Note の Title は明示幅を持つ");
                AssertFitting(title, 432f, 40f, "History Empty/Title", expectEllipsis: true);
                AssertFitting(hint, 432f, 32f, "History Empty/Hint", expectEllipsis: true);
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }

        // --- 4. Result モーダル ---

        [Test]
        public void ResultModal_StatusAndRegister_FitTheirRects()
        {
            var view = V6ResultView.Create((RectTransform)canvasGo.transform, null, null, null);
            try
            {
                var status = FindText(view.Root, "Dialog", "Status");
                status.text = "登録に失敗しました: " + LongError;
                AssertFitting(status, 592f, 26f, "Modal/Status", expectAutoFit: true);

                var register = FindText(view.Root, "Button Register", "Label");
                register.text = "Register";
                AssertFitting(register, 101f, 38f, "Modal/Register Label", expectAutoFit: true);
                Assert.AreEqual(20f, register.fontSizeMin, 0.01f);
                Assert.AreEqual(26f, register.fontSizeMax, 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }

        // --- 5. Pause 行の値 ---

        [Test]
        public void PauseRowValues_LongStrings_StayInsideValueColumn()
        {
            var view = V6PauseView.Create((RectTransform)canvasGo.transform);
            try
            {
                Canvas.ForceUpdateCanvases();
                // Issue #733: 設定行は V6SettingRowView (ルート名 "Set <label>")
                // + 共有カタログ (V6SettingsCatalog) 構成。値テキストは行ルート直下の
                // "Value" で、編集可能な行 (IsAction でない行) は中央揃え +
                // TMP auto-size (fontSizeMin 16 / max 24)。アクション行
                // (キー設定の「開く」等) は値列ではないので対象外。
                var values = new List<TextMeshProUGUI>();
                foreach (var tmp in view.Root.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (tmp.name == "Value" && tmp.transform.parent != null
                        && tmp.transform.parent.name.StartsWith("Set "))
                    {
                        values.Add(tmp);
                    }
                }

                Assert.Greater(values.Count, 0, "Pause の設定行 Value が見つかる");
                int autoFitCount = 0;
                foreach (var value in values)
                {
                    string label = value.transform.parent.name + "/Value";
                    if (!value.enableAutoSizing)
                    {
                        // アクション行の固定ラベル (「開く」等) は値列ではない。
                        Assert.AreEqual(TextWrappingModes.NoWrap, value.textWrappingMode,
                            label + " must not wrap");
                        continue;
                    }

                    autoFitCount++;
                    Assert.AreEqual(16f, value.fontSizeMin, 0.01f,
                        label + " auto-size の下限は共通規約の 16");
                    Assert.AreEqual(24f, value.fontSizeMax, 0.01f,
                        label + " auto-size の上限は design の 24");

                    foreach (string text in new[] { "P1234 G123 B12 M8", "9,999,999" })
                    {
                        value.text = text;
                        AssertFitting(value, value.rectTransform.rect.width,
                            value.rectTransform.rect.height, label + " '" + text + "'");
                    }
                }

                Assert.Greater(autoFitCount, 0,
                    "auto-size 付きの編集可能な値列が 1 つ以上ある");
            }
            finally
            {
                if (view != null)
                {
                    view.Dispose();
                }
            }
        }

        // --- 6. 監査の要視覚確認ケース ---

        [Test]
        public void KeyConfigModal_Status_LongKeyList_Ellipsizes()
        {
            var modal = V6KeyConfigModal.Build(canvasGo);
            try
            {
                var status = FindTextByName(modal.transform, "Status");
                // 「他: A / B / …」の使用中キー一覧が最長級になった状態を再現。
                status.text = "Lane 3 で使用中 / 他: " +
                    "A / S / D / F / J / K / L / ; / ' / Z / X / C / V / B / N / M";
                AssertFitting(status, 784f, 26f, "KeyConfig/Status", expectEllipsis: true);
            }
            finally
            {
                Object.DestroyImmediate(modal.gameObject);
            }
        }

        [Test]
        public void TitleUpdateButton_LongLabel_StaysInsideButton()
        {
            var checkerGo = new GameObject("UpdateChecker");
            checkerGo.transform.SetParent(canvasGo.transform, false);
            var checker = checkerGo.AddComponent<UpdateChecker>();
            // EditMode では Awake/Coroutine は走らないので Model を直接進める。
            checker.Model.StartCheck();
            checker.Model.CheckFoundNewVersion("v9.9.9-test", "9.9.9");
            checker.Model.BeginDownload();
            checker.Model.SetDownloadProgress(1f); // "更新を準備中... 100%"

            V6TitleView view = null;
            try
            {
                view = V6TitleView.Create((RectTransform)canvasGo.transform, null, null, null);
                // Header 内の更新ボタン ("Button 更新を準備中... 100%")。
                var button = FindButton(view.Root, "更新");
                Assert.IsNotNull(button, "更新ボタンが見つかる");
                var label = button.Find("Label");
                Assert.IsNotNull(label, "Button/Label");
                var tmp = label.GetComponent<TextMeshProUGUI>();
                Assert.IsTrue(tmp.enableAutoSizing,
                    "長い更新ラベルは auto-size でボタン内へ収める");
                tmp.ForceMeshUpdate(true, true);
                Assert.LessOrEqual(tmp.textInfo.lineCount, 1);
                InkSize(tmp, out float inkW, out _);
                Assert.LessOrEqual(inkW, tmp.rectTransform.rect.width + 0.5f,
                    "Label のインクは自身の矩形 (利用可能幅へ詰めたもの) に収まる");
                // ボタン全体の内容幅を超えないこと: Label 矩形 + 左右 padding +
                // icon/cap/gap がボタン幅に収まる (FitButtonLabel の契約)。
                Assert.LessOrEqual(tmp.rectTransform.rect.width,
                    V6TitleLogic.UpdateButtonSize.x - V6Controls.ButtonPaddingX * 2f + 0.5f);
            }
            finally
            {
                if (view != null)
                {
                    view.Dispose();
                }

                Object.DestroyImmediate(checkerGo);
            }
        }

        private static Transform FindButton(Transform root, string namePart)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("Button ") && t.name.Contains(namePart))
                {
                    return t;
                }
            }

            return null;
        }
    }
}
