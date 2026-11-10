using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using FlashBeat.UI.V6.Ranking;
using FlashBeat.UI.V6.Select;

namespace FlashBeat.V6Tests
{
    /// <summary>
    /// v6 ランキング画面の空状態表示 (Issue #239)。
    /// - 未選択のときは「記録がありません」ではなく未選択であることを出す。
    /// - Local セグメントは通信状態 (読込中/失敗) を出さない。
    /// 実 View を生成して、SCORE RANKING パネルに表示されるテキストで検証する。
    /// </summary>
    public sealed class V6RankingEmptyStateTests
    {
        // --- テスト用データソース (ビューが使うメンバーだけを実値で返す) ---

        private sealed class FakeRankingDataSource : IV6SelectDataSource
        {
            public readonly List<V6SongEntry> Songs = new List<V6SongEntry>();
            public bool HasErrorValue;
            public string StatusMessageValue = "";

            public IReadOnlyList<V6SongEntry> AllSongs { get { return Songs; } }

            public event System.Action Changed;

            public IReadOnlyList<V6SongEntry> FilteredSongs { get { return Songs; } }

            public string Query { get { return ""; } set { } }

            public V6SongEntry SelectedValue;

            public V6SongEntry Selected { get { return SelectedValue; } }

            public bool IsLoading { get { return false; } }

            public bool HasError { get { return HasErrorValue; } }

            public string StatusMessage { get { return StatusMessageValue; } }

            public void SetQuery(string query) { }

            public void Select(int songId) { SelectedValue = Songs.Find(song => song.SongId == songId); }

            public void MoveSelection(int delta) { }

            public void RefreshAsync() { }

            public IReadOnlyList<V6RankingRow> RankingFor(V6SongEntry song)
            {
                return new List<V6RankingRow>();
            }

            public void CommitReady() { }
        }

        private GameObject canvasGo;
        private FakeRankingDataSource data;

        [SetUp]
        public void SetUp()
        {
            data = new FakeRankingDataSource();
            data.Songs.Add(MakeSong(1, "Song 1"));
            data.Songs.Add(MakeSong(2, "Song 2"));

            canvasGo = new GameObject("V6RankingEmptyStateTests Canvas", typeof(Canvas));
        }

        [TearDown]
        public void TearDown()
        {
            if (canvasGo != null)
            {
                Object.DestroyImmediate(canvasGo);
            }
            canvasGo = null;
            data = null;
        }

        private static string[] PanelTexts(V6RankingView view)
        {
            // SCORE RANKING パネル配下のテキストだけを集める (他のテストの残骸を読まない)。
            var texts = new List<string>();
            Transform panel = view.Root.Find("Main/Score Ranking");
            foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>(true))
            {
                texts.Add(text.text);
            }
            return texts.ToArray();
        }

        private static bool ContainsText(string[] texts, string value)
        {
            return System.Array.IndexOf(texts, value) >= 0;
        }

        [Test]
        public void NoSelection_ShowsUnselectedMessage_NotEmptyRecordMessage()
        {
            // 未選択 (Selected == null): 「記録がありません」ではなく未選択であることを出す。
            var view = V6RankingView.Create((RectTransform)canvasGo.transform, null, data, null, null);
            try
            {
                Assert.Contains("曲が選択されていません", PanelTexts(view));
            }
            finally
            {
                view.Dispose();
            }
        }

        [Test]
        public void LocalSong_ShowsNoRecordMessage_EvenWhenErrorWouldBeAvailable()
        {
            // Local (ローカル記録) は通信状態と無関係。エラー状態があっても
            // 「記録がありません」を出す (Issue #239)。
            // 未選択だと NoSongSelectedMessage 側の分岐になるため、先に曲を選ぶ。
            data.Select(1);
            Assert.IsNotNull(data.Selected, "前提: ローカル曲が選ばれている");
            data.HasErrorValue = true;
            data.StatusMessageValue = "カタログの更新に失敗しました (HTTP 500)";
            var view = V6RankingView.Create((RectTransform)canvasGo.transform, null, data, null, null);
            try
            {
                string[] texts = PanelTexts(view);
                Assert.Contains("記録がありません", texts);
                Assert.IsFalse(ContainsText(texts, "カタログの更新に失敗しました (HTTP 500)"));
            }
            finally
            {
                view.Dispose();
            }
        }

        // --- テスト用の最小 V6SongEntry ファクトリ ---

        private static V6SongEntry MakeSong(int songId, string title)
        {
            return new V6SongEntry
            {
                SongId = songId,
                Title = title,
                Artist = "Artist",
                Level = 10,
                JacketResourcePath = "",
                BestScore = -1,
                BestGrade = "",
                Bpm = 120.0,
                NoteCount = 500,
                IsCloud = false,
            };
        }
    }
}
