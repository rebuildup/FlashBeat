using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using FlashBeat.UI.V6.History;
using FlashBeat.UI.V6.Select;

namespace FlashBeat.V6Tests
{
    /// <summary>
    /// v6 履歴画面の中央パネル Controls (skip-back / play / skip-forward) の
    /// 実動作結線を検証する (Issue #167)。
    ///
    /// 実 View を生成して確認する:
    /// - 3 ボタンが存在すること。
    /// - skip-back / skip-forward が履歴リストの選択を動かすこと (V6HistoryView.MoveSelection と同じ経路)。
    /// - play がフッターの「この曲へ」と同じ経路 (onGotoSong) を通ること。
    /// プレビュー再生のような新しい操作は作らない (V6SelectView と同じ規約)。
    /// </summary>
    public sealed class V6HistoryControlsTests
    {
        // --- テスト用の最小 V6SongEntry ファクトリ (V6HistoryRankingTests と同じ形) ---

        private static V6SongEntry MakeSong(int songId, string title, int bestScore)
        {
            return new V6SongEntry
            {
                SongId = songId,
                Title = title,
                Artist = "Artist",
                Level = 10,
                JacketResourcePath = "",
                BestScore = bestScore,
                BestGrade = bestScore > 0 ? "S" : "",
                Bpm = 120.0,
                NoteCount = 500,
                IsCloud = false,
            };
        }

        // --- テスト用データソース (ビューが使うメンバーだけを実値で返す) ---

        private sealed class FakeHistoryDataSource : IV6SelectDataSource
        {
            public readonly List<V6SongEntry> Songs = new List<V6SongEntry>();
            private int selectedSongId = -1;

            public event System.Action Changed;

            public IReadOnlyList<V6SongEntry> AllSongs { get { return Songs; } }

            public IReadOnlyList<V6SongEntry> FilteredSongs { get { return Songs; } }

            public string Query { get { return ""; } set { } }

            public V6SongEntry Selected
            {
                get
                {
                    return Songs.Find(s => s.SongId == selectedSongId);
                }
            }

            public bool IsLoading { get { return false; } }

            public bool HasError { get { return false; } }

            public string StatusMessage { get { return ""; } }

            public void SetQuery(string query) { }

            public void Select(int songId)
            {
                if (Songs.Find(s => s.SongId == songId) == null)
                {
                    return;
                }
                selectedSongId = songId;
            }

            public void MoveSelection(int delta) { }

            public void RefreshAsync() { }

            public IReadOnlyList<V6RankingRow> RankingFor(V6SongEntry song)
            {
                return new List<V6RankingRow>();
            }

            public void CommitReady() { }
        }

        // --- テストの準備と後片付け ---

        private GameObject canvasGo;
        private FakeHistoryDataSource data;
        private V6HistoryView view;
        private int gotoCalls;

        [SetUp]
        public void SetUp()
        {
            // スコアを降順にして、スコア順ソートの履歴順を 1 → 2 → 3 に固定する。
            data = new FakeHistoryDataSource
            {
                Songs =
                {
                    MakeSong(1, "Song 1", 300000),
                    MakeSong(2, "Song 2", 200000),
                    MakeSong(3, "Song 3", 100000),
                },
            };

            canvasGo = new GameObject("V6HistoryControlsTests Canvas", typeof(Canvas));
            var canvasRect = (RectTransform)canvasGo.transform;
            canvasRect.sizeDelta = new Vector2(1920f, 1080f);

            gotoCalls = 0;
            view = V6HistoryView.Create(canvasRect, null, data, null, () => gotoCalls++);
        }

        [TearDown]
        public void TearDown()
        {
            if (view != null)
            {
                view.Dispose();
            }
            if (canvasGo != null)
            {
                Object.DestroyImmediate(canvasGo);
            }
            view = null;
            data = null;
            canvasGo = null;
        }

        private Button ControlButton(string name)
        {
            // Controls 行のボタンは V6Controls.IconButton が "IconButton <icon名>" で作る。
            Button found = null;
            foreach (var b in view.Root.GetComponentsInChildren<Button>(true))
            {
                if (b.gameObject.name == "IconButton " + name)
                {
                    found = b;
                    break;
                }
            }
            return found;
        }

        // =====================================================================
        // Controls (skip-back / play / skip-forward)
        // =====================================================================

        [Test]
        public void Controls_ButtonsExist()
        {
            Assert.IsNotNull(ControlButton("skip-back"), "skip-back ボタンが見つからない");
            Assert.IsNotNull(ControlButton("play"), "play ボタンが見つからない");
            Assert.IsNotNull(ControlButton("skip-forward"), "skip-forward ボタンが見つからない");
        }

        [Test]
        public void Controls_SkipForward_MovesSelectionToNextSong()
        {
            // Create 直後は履歴先頭 (スコア順 → Song 1) がハイライトされる。
            // ただし表示しただけではデータ層を選ばない (#222 の「既定選択はしない」)。
            Assert.IsNull(data.Selected, "前提: 起動直後にデータ層の選択を作ってはいけない");

            ControlButton("skip-forward").onClick.Invoke();

            Assert.IsNotNull(data.Selected, "skip-forward がデータ層の選択を作っていない");
            Assert.AreEqual(2, data.Selected.SongId, "skip-forward で次の曲へ選択が移っていない");
        }

        [Test]
        public void Controls_SkipBack_MovesSelectionToPreviousSong()
        {
            // 末尾から戻る方向も確認する (初期選択 → skip-forward ×2 → skip-back)。
            ControlButton("skip-forward").onClick.Invoke();
            ControlButton("skip-forward").onClick.Invoke();
            Assert.AreEqual(3, data.Selected.SongId, "前提: skip-forward ×2 で履歴末尾にいる");

            ControlButton("skip-back").onClick.Invoke();

            Assert.AreEqual(2, data.Selected.SongId, "skip-back で前の曲へ選択が戻っていない");
        }

        [Test]
        public void Controls_Play_TriggersGotoSong()
        {
            Assert.AreEqual(0, gotoCalls, "前提: まだ遷移要求は無い");

            ControlButton("play").onClick.Invoke();

            Assert.AreEqual(1, gotoCalls, "play が「この曲へ」(onGotoSong) を通っていない");
        }

        [Test]
        public void Controls_Play_AppliesTheHighlightedSongBeforeLeaving()
        {
            // 曲送りしていない状態でも、ハイライト中の曲がデータ層に入ってから遷移する。
            // 空のまま選曲画面へ戻ると「この曲へ」を押したのに何も選ばれていない、になる。
            Assert.IsNull(data.Selected, "前提: 起動直後は未選択");

            ControlButton("play").onClick.Invoke();

            Assert.IsNotNull(data.Selected, "「この曲へ」がデータ層の選択を作っていない");
            Assert.AreEqual(1, data.Selected.SongId, "ハイライト中の曲 (Song 1) がデータ層に入っていない");
            Assert.AreEqual(1, gotoCalls);
        }

        [Test]
        public void Controls_Play_UsesSamePathAsFooterGotoButton()
        {
            // フッターの「この曲へ」と同じ経路であること: 両方押すと onGotoSong が 2 回届く。
            ControlButton("play").onClick.Invoke();

            Button found = null;
            foreach (var b in view.Root.GetComponentsInChildren<Button>(true))
            {
                // フッターの主ボタンは V6Controls.ButtonPrimary が "Button <label>" で作る。
                if (b.gameObject.name == "Button この曲へ")
                {
                    found = b;
                    break;
                }
            }
            Assert.IsNotNull(found, "フッターの「この曲へ」ボタンが見つからない");
            found.onClick.Invoke();

            Assert.AreEqual(2, gotoCalls, "play とフッター「この曲へ」が同じ onGotoSong 経路になっていない");
        }
    }
}
