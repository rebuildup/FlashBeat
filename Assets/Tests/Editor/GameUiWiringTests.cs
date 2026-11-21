using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using FlashBeat.UI;
using FlashBeat.UI.V5;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Issue #167 / #740: GameScene の UI 配線が「実動作」していることを検証する
    /// EditMode テスト。
    ///
    /// かつては v5 View (Result / Select / Pause / HUD) の生成とボタン配線を
    /// 検証していたが、Issue #740 で v5 View 自体を削除したため、残るのは
    /// View に依存しない契約だけ:
    ///   - OptionSettings の保存/復元ラウンドトリップ
    ///   - レガシー Canvas の Raycaster 無効化 (GameHudController)
    ///   - ResultSceneManager.ApplyBestScore (F16)
    ///   - GameSceneManager にレガシー メニュー/歌詞パネルの経路が無いこと (F15 / #740 Stage B)
    /// </summary>
    public static class GameUiWiringTests
    {
        // --- 設定の保存と復元 ---

        [Test]
        public static void OptionSettings_SaveAndLoad_RoundTrips()
        {
            float main = GManager.mainVolume;
            float effect = GManager.effectVolume;
            float bgm = GManager.BGMVolume;
            float song = GManager.songVolume;
            float speed = GManager.noteSpeed;
            int timing = GManager.noteTiming;

            try
            {
                GManager.mainVolume = 0.42f;
                GManager.effectVolume = 0.31f;
                GManager.BGMVolume = 0.23f;
                GManager.songVolume = 0.77f;
                GManager.noteSpeed = 12f;
                GManager.noteTiming = 4;
                OptionSettings.Save();

                GManager.mainVolume = 1f;
                GManager.effectVolume = 1f;
                GManager.BGMVolume = 1f;
                GManager.songVolume = 1f;
                GManager.noteSpeed = 1f;
                GManager.noteTiming = 0;
                OptionSettings.Load();

                Assert.AreEqual(0.42f, GManager.mainVolume, 0.001f, "Main Volume が復元されていない。");
                Assert.AreEqual(0.31f, GManager.effectVolume, 0.001f, "Effect Volume が復元されていない。");
                Assert.AreEqual(0.23f, GManager.BGMVolume, 0.001f, "BGM Volume が復元されていない。");
                Assert.AreEqual(0.77f, GManager.songVolume, 0.001f, "Song Volume が復元されていない (#851)。");
                Assert.AreEqual(12f, GManager.noteSpeed, 0.001f, "NoteSpeed が復元されていない。");
                Assert.AreEqual(4, GManager.noteTiming, "NoteTiming が復元されていない。");
            }
            finally
            {
                GManager.mainVolume = main;
                GManager.effectVolume = effect;
                GManager.BGMVolume = bgm;
                GManager.songVolume = song;
                GManager.noteSpeed = speed;
                GManager.noteTiming = timing;
                OptionSettings.Save();
            }
        }

        // --- Findings F2 / F16 / F15 の直接検証 ---

        [Test]
        public static void LegacyCanvasRaycaster_IsDisabled()
        {
            // F2: GameScene のレガシー Canvas は sortingOrder 0 で v5 HUD (-1) より手前。
            // GraphicRaycaster が有効なままだとレガシー側が HUD のボタンを横取りする。
            // GameHudController.DisableLegacyRaycastersIn (Update と同じ処理をシーン単位で
            // 抽出した公開静的関数) を、実際に作ったシーンで検証する。
            // EditMode では NewScene(Additive) が「未保存の untitled シーンがあると
            // 追加シーンを作れない」で落ちる (batchmode では必ずそうなる) ため、
            // プレビューシーンを使う。GameObject はアクティブシーンに作ってから
            // EditorSceneManager.MoveGameObjectToScene で移す。
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                var canvasGo = new GameObject("Legacy Canvas", typeof(Canvas), typeof(GraphicRaycaster));
                UnityEditor.SceneManagement.EditorSceneManager.MoveGameObjectToScene(canvasGo, scene);

                Assert.AreEqual(1, GameHudController.DisableLegacyRaycastersIn(scene),
                    "レガシー Canvas の GraphicRaycaster が無効化されていない (F2)。");
                Assert.IsFalse(canvasGo.GetComponent<GraphicRaycaster>().enabled,
                    "GraphicRaycaster が enabled のまま (HUD のボタンが横取りされる)。");

                // 冪等: 2 回目は無効化するものが無い。
                Assert.AreEqual(0, GameHudController.DisableLegacyRaycastersIn(scene),
                    "2 回目の呼び出しで個数が増えた (冪等でない)。");

                // Raycaster を持たない Canvas は数えない。
                var plainGo = new GameObject("Plain Canvas", typeof(Canvas));
                UnityEditor.SceneManagement.EditorSceneManager.MoveGameObjectToScene(plainGo, scene);
                Assert.AreEqual(0, GameHudController.DisableLegacyRaycastersIn(scene),
                    "GraphicRaycaster の無い Canvas を数えている。");
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public static void GameSceneManager_HasNoLegacyMenuOrLyricPath()
        {
            // F15 / Issue #740 Stage B: かつての Esc → MenuPanel / 歌詞 TextPanel の
            // 経路は撤去済み。v6 ポーズと v6 HUD が唯一の UI なので、
            // GameSceneManager にレガシー UI を開く・表示する public API が
            // 復活していないことをリフレクションで固定する。
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;
            foreach (string name in new[]
            {
                "OpenMenu", "CloseMenu", "SwitchPanel",
                "TextPanelOn", "TextPanelOff", "ApplyLyricsVisibility",
                "LegacyBackOpensMenu", "playSound",
            })
            {
                Assert.IsNull(typeof(GameSceneManager).GetMethod(name, flags),
                    "GameSceneManager." + name + " が残っている (Stage B で撤去したはず)。");
            }
        }

        [Test]
        public static void ResultBestScore_IsAppliedInAwake()
        {
            // F16: ベストスコアの更新は Awake で確定させる (Start の実行順に依存しない)。
            // 抽出した ApplyBestScore を直接検証する。返り値は更新前の値。
            int[] backup = (int[])GManager.Hiscore.Clone();
            try
            {
                // 新記録: 更新前の値を返し、テーブルが上がる。
                GManager.Hiscore[1] = 100;
                Assert.AreEqual(100, ResultSceneManager.ApplyBestScore(1, 500),
                    "返り値が更新前の値でない (F16)。");
                Assert.AreEqual(500, GManager.Hiscore[1],
                    "新記録が保存テーブルに反映されていない (F16)。");

                // 下回るスコア: テーブルは変わらない。
                Assert.AreEqual(500, ResultSceneManager.ApplyBestScore(1, 50),
                    "下回るスコアで返り値が更新後の値になった (F16)。");
                Assert.AreEqual(500, GManager.Hiscore[1],
                    "下回るスコアで記録が書き換わった (F16)。");

                // 同点: 記録を置き換える (>= で更新)。
                Assert.AreEqual(500, ResultSceneManager.ApplyBestScore(1, 500), "同点の返り値が不正。");
                Assert.AreEqual(500, GManager.Hiscore[1], "同点で記録が変わった。");

                // 範囲外 songId (cloud カタログで増えた曲) は 0 番スロットを
                // 共有しない (issue-162 A-6 再照合)。安定キーの保存先へ書く。
                // 保存先はテスト用のメモリに差し替える (実セーブデータを汚さない)。
                var previousStorage = FlashBeat.Persistence.ScorePersistence.Storage;
                FlashBeat.Persistence.ScorePersistence.Storage =
                    new FlashBeat.Tests.Editor.MemoryScoreStorage();
                FlashBeat.Persistence.ScorePersistence.Unload();
                try
                {
                    GManager.Hiscore[0] = 0;
                    int outOfRange = GManager.Hiscore.Length + 3;
                    Assert.AreEqual(0, ResultSceneManager.ApplyBestScore(outOfRange, 700),
                        "範囲外 songId の返り値が安定キー保存先の前回値でない (F16)。");
                    Assert.AreEqual(0, GManager.Hiscore[0],
                        "範囲外 songId を 0 番スロットに書いてはいけない (issue-162 A-6)。");
                    Assert.AreEqual(700, FlashBeat.Persistence.ScorePersistence.BestOf(outOfRange),
                        "範囲外 songId のベストが安定キーの保存先に入っていない (issue-162 A-6)。");
                }
                finally
                {
                    FlashBeat.Persistence.ScorePersistence.Unload();
                    FlashBeat.Persistence.ScorePersistence.Storage = previousStorage;
                }
            }
            finally
            {
                for (int i = 0; i < backup.Length; i++) { GManager.Hiscore[i] = backup[i]; }
            }
        }
    }
}
