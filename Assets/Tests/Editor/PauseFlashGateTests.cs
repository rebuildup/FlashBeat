using System.Collections.Generic;
using System.Reflection;
using FlashBeat.Gameplay;
using FlashBeat.UI.V6.Game.Playfield;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// ポーズ中の発光フィードバック停止 (Issue #580) の EditMode テスト。
    ///
    /// LaneFlash / BGFlash / V6PlayfieldView の private Update をリフレクションで
    /// 駆動し、PauseGate.IsPaused 中は
    /// - レーンキー押下で新規発火しない
    /// - 点灯中の α が 0 に戻る (消灯)
    /// - BGFlash の拍タイマーが発火しない (BPMCount も壁時計で進まない)
    /// こと、および PauseGate.Exit 後に通常復帰することを検証する。
    ///
    /// キー押下は Input を使えないので KeyBindings.InjectLaneKeyDown
    /// (検証ハーネス専用の注入シーム) で再現する。
    /// </summary>
    public class PauseFlashGateTests
    {
        private const float Epsilon = 0.001f;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Material> materials = new List<Material>();
        private bool savedPlayed;
        private float savedStepTime;
        private float savedStartTime;

        [SetUp]
        public void SetUp()
        {
            savedPlayed = GManager.played;
            savedStepTime = GManager.step_time;
            savedStartTime = GManager.StartTime;
            PauseGate.HardReset();
            KeyBindings.ClearInjectedLaneKeyDowns();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null) { Object.DestroyImmediate(spawned[i]); }
            }
            spawned.Clear();
            for (int i = 0; i < materials.Count; i++)
            {
                if (materials[i] != null) { Object.DestroyImmediate(materials[i]); }
            }
            materials.Clear();

            KeyBindings.ClearInjectedLaneKeyDowns();
            PauseGate.HardReset();
            GManager.played = savedPlayed;
            GManager.step_time = savedStepTime;
            GManager.StartTime = savedStartTime;
        }

        // --- helpers ---

        private GameObject Track(GameObject go)
        {
            spawned.Add(go);
            return go;
        }

        private static void InvokeLifecycle(MonoBehaviour behaviour, string method)
        {
            MethodInfo m = behaviour.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(m, behaviour.GetType().Name + "." + method + " が見つからない");
            m.Invoke(behaviour, null);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo f = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, target.GetType().Name + "." + name + " が見つからない");
            f.SetValue(target, value);
        }

        private static float MaterialAlpha(Renderer rend)
        {
            return rend.material.color.a;
        }

        private LaneFlash CreateLaneFlash(int laneNumber)
        {
            var go = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            var material = new Material(Shader.Find("Standard"));
            materials.Add(material);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;

            var flash = go.AddComponent<LaneFlash>();
            SetField(flash, "laneNumber", laneNumber);
            InvokeLifecycle(flash, "Start");
            return flash;
        }

        private float LaneFlashAlpha(LaneFlash flash)
        {
            return MaterialAlpha(flash.GetComponent<Renderer>());
        }

        private BGFlash CreateBGFlash()
        {
            var go = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            var material = new Material(Shader.Find("Standard"));
            materials.Add(material);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;

            var flash = go.AddComponent<BGFlash>();
            InvokeLifecycle(flash, "Start");
            // Start が読む曲データに依存しないよう、拍タイマーは直接固定する。
            SetField(flash, "Beat", 0f);
            SetField(flash, "BPMCount", 0f);
            GManager.played = true;
            return flash;
        }

        private float BGFlashAlpha(BGFlash flash)
        {
            return MaterialAlpha(flash.GetComponent<Renderer>());
        }

        /// <summary>設計フレーム 1728×972 の親を持つ v6 プレイフィールドを作る。</summary>
        private V6PlayfieldView CreateView()
        {
            var parentGo = Track(new GameObject("PlayfieldParent", typeof(RectTransform)));
            var prt = parentGo.GetComponent<RectTransform>();
            prt.sizeDelta = new Vector2(V6LaneGeometry.DesignWidth, V6LaneGeometry.DesignHeight);
            var view = V6PlayfieldView.Create(prt);
            Track(view.gameObject);
            return view;
        }

        private static void InvokeUpdate(MonoBehaviour behaviour)
        {
            InvokeLifecycle(behaviour, "Update");
        }

        private static Image LaneFlashImage(V6PlayfieldView view, int lane)
        {
            Transform child = view.transform.Find("LaneFlash " + lane);
            Assert.IsNotNull(child, "v6 プレイフィールドに LaneFlash " + lane + " が無い");
            return child.GetComponent<Image>();
        }

        // LaneFlash / BGFlash read renderer.material, which Unity rejects in
        // edit mode ("leak materials into the scene"). Play mode is
        // unaffected; the error is an edit-mode artifact of driving the
        // production Update loop from a test. LogAssert scopes are per
        // command, so the flag must be set inside each test method.
        private static void IgnoreMaterialEditModeError()
        {
            LogAssert.ignoreFailingMessages = true;
        }

        // --- LaneFlash ---

        [Test]
        public void LaneFlash_LightsOnLaneKey_WhenNotPaused()
        {
            IgnoreMaterialEditModeError();
            LaneFlash flash = CreateLaneFlash(laneNumber: 1);
            KeyBindings.InjectLaneKeyDown(0);

            InvokeUpdate(flash);

            Assert.Greater(LaneFlashAlpha(flash), 0.2f,
                "非ポーズ中はレーンキー押下で発光する");
        }

        [Test]
        public void LaneFlash_Paused_DoesNotFire_AndExtinguishes_ThenResumes()
        {
            IgnoreMaterialEditModeError();
            LaneFlash flash = CreateLaneFlash(laneNumber: 1);
            KeyBindings.InjectLaneKeyDown(0);

            // まず点灯させる。
            InvokeUpdate(flash);
            Assert.Greater(LaneFlashAlpha(flash), 0.2f, "前提: 非ポーズ中に点灯している");

            // ポーズ突入: 押下が続いていても発火せず、点灯中の α は消える。
            PauseGate.Enter();
            InvokeUpdate(flash);
            Assert.AreEqual(0f, LaneFlashAlpha(flash), Epsilon,
                "ポーズ中は押下があっても発光せず、点灯中の α は 0 に戻る");

            // 再開: 注入押下が続くので通常どおり再点灯する。
            PauseGate.Exit();
            InvokeUpdate(flash);
            Assert.Greater(LaneFlashAlpha(flash), 0.2f,
                "再開後はレーンキー押下で通常どおり発光する");
        }

        [Test]
        public void LaneFlash_PausedWithoutPriorLight_DoesNotLightOnKeyDown()
        {
            IgnoreMaterialEditModeError();
            LaneFlash flash = CreateLaneFlash(laneNumber: 1);

            PauseGate.Enter();
            KeyBindings.InjectLaneKeyDown(0);
            InvokeUpdate(flash);

            Assert.AreEqual(0f, LaneFlashAlpha(flash), Epsilon,
                "消灯状態でポーズ中に押しても発光しない");
        }

        // --- BGFlash ---

        [Test]
        public void BGFlash_BeatFlashFires_WhenPlayedAndNotPaused()
        {
            IgnoreMaterialEditModeError();
            BGFlash flash = CreateBGFlash();

            InvokeUpdate(flash);

            Assert.Greater(BGFlashAlpha(flash), 0.2f,
                "非ポーズ中は拍タイマー到達で発光する");
        }

        [Test]
        public void BGFlash_Paused_BeatDoesNotFire_AndExtinguishes_ThenResumes()
        {
            IgnoreMaterialEditModeError();
            BGFlash flash = CreateBGFlash();

            // まず拍フラッシュを点ける (Beat=0 なので毎 Update 発火する)。
            InvokeUpdate(flash);
            Assert.Greater(BGFlashAlpha(flash), 0.2f, "前提: 非ポーズ中に点灯している");

            // ポーズ中: BPMCount >= Beat の状態でも発火せず α は消える。
            PauseGate.Enter();
            InvokeUpdate(flash);
            Assert.AreEqual(0f, BGFlashAlpha(flash), Epsilon,
                "ポーズ中は拍フラッシュが発火せず、点灯中の α は 0 に戻る");

            // 再開: BPMCount はポーズ中に進んでいないが Beat=0 なので発火して復帰する。
            PauseGate.Exit();
            InvokeUpdate(flash);
            Assert.Greater(BGFlashAlpha(flash), 0.2f,
                "再開後は拍フラッシュが通常どおり発火する");
        }

        // --- V6PlayfieldView ---

        [Test]
        public void V6PlayfieldView_LaneFlash_RespectsPause_AndResumes()
        {
            V6PlayfieldView view = CreateView();
            Image img = LaneFlashImage(view, 0);
            KeyBindings.InjectLaneKeyDown(0);

            InvokeUpdate(view);
            Assert.AreEqual(0.3f, img.color.a, Epsilon,
                "非ポーズ中はレーンキー押下で発光する");

            PauseGate.Enter();
            InvokeUpdate(view);
            Assert.AreEqual(0f, img.color.a, Epsilon,
                "ポーズ中は押下があっても発光せず α は 0 に戻る");

            PauseGate.Exit();
            InvokeUpdate(view);
            Assert.Greater(img.color.a, 0.2f,
                "再開後はレーンキー押下で通常どおり発光する");
        }
    }
}
