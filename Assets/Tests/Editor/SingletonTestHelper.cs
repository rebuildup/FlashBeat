using System.Reflection;
using System.Runtime.CompilerServices;
using NoteEditor.Model;
using NoteEditor.Presenter;
using NoteEditor.Utility;
using UniRx;
using UnityEngine;

namespace FlashBeat.Tests.Editor
{
    // Test-only helper that forces SingletonMonoBehaviour<T> instances to exist
    // for EditMode tests. The Instance getter's `new GameObject().AddComponent<T>()`
    // fallback fails in EditMode tests for editor-only T types ("Can't add script
    // behaviour 'X' because it is an editor script"), so we create instances via
    // RuntimeHelpers.GetUninitializedObject, initialize the ReactiveProperty fields
    // via reflection, then register them through SetInstanceForTesting.
    //
    // SetInstanceForTesting short-circuits the getter so it returns our injected
    // reference even though Unity's overloaded == treats uninitialized MonoBehaviours
    // as null — the getter's `if (externallySet_) return instance_` path executes
    // before any Unity == comparison.
    internal static class SingletonTestHelper
    {
        const BindingFlags InstanceNonPublic = BindingFlags.NonPublic | BindingFlags.Instance;

        public static void EnsureSingletons()
        {
            EnsureEditData();
            EnsureEditNotesPresenter();
            // Force Audio to exist so EditNotesPresenter.Awake's subscription doesn't
            // create an unmanaged instance we cannot later tear down.
            EnsureAudio();
        }

        public static void TeardownSingletons()
        {
            SetInstance<Audio>(null);
            SetInstance<EditNotesPresenter>(null);
            SetInstance<EditData>(null);
        }

        static void EnsureEditData()
        {
            if (IsInstanceSet<EditData>())
            {
                return;
            }

            var instance = (EditData)RuntimeHelpers.GetUninitializedObject(typeof(EditData));
            typeof(EditData).GetField("name_", InstanceNonPublic)
                .SetValue(instance, new ReactiveProperty<string>(""));
            typeof(EditData).GetField("maxBlock_", InstanceNonPublic)
                .SetValue(instance, new ReactiveProperty<int>(5));
            typeof(EditData).GetField("LPB_", InstanceNonPublic)
                .SetValue(instance, new ReactiveProperty<int>(4));
            typeof(EditData).GetField("BPM_", InstanceNonPublic)
                .SetValue(instance, new ReactiveProperty<int>(120));
            typeof(EditData).GetField("offsetSamples_", InstanceNonPublic)
                .SetValue(instance, new ReactiveProperty<int>(0));
            typeof(EditData).GetField("videoId_", InstanceNonPublic)
                .SetValue(instance, new ReactiveProperty<string>(""));
            typeof(EditData).GetField("isDirty_", InstanceNonPublic)
                .SetValue(instance, new ReactiveProperty<bool>(false));
            typeof(EditData).GetField("notes_", InstanceNonPublic)
                .SetValue(instance, new System.Collections.Generic.Dictionary<NoteEditor.Notes.NotePosition, NoteEditor.Notes.NoteObject>());
            SetInstance<EditData>(instance);
        }

        static void EnsureEditNotesPresenter()
        {
            if (IsInstanceSet<EditNotesPresenter>())
            {
                return;
            }

            // canvasEvents stays null — Awake() does not run for uninitialized
            // MonoBehaviours, and the tests never exercise canvas event observables.
            var instance = (EditNotesPresenter)RuntimeHelpers.GetUninitializedObject(typeof(EditNotesPresenter));
            SetInstance<EditNotesPresenter>(instance);
        }

        static void EnsureAudio()
        {
            if (IsInstanceSet<Audio>())
            {
                return;
            }

            var instance = (Audio)RuntimeHelpers.GetUninitializedObject(typeof(Audio));
            typeof(Audio).GetField("source_", InstanceNonPublic).SetValue(instance, null);
            typeof(Audio).GetField("onLoad", InstanceNonPublic).SetValue(instance, new Subject<Unit>());
            typeof(Audio).GetField("volume_", InstanceNonPublic).SetValue(instance, new ReactiveProperty<float>(1));
            typeof(Audio).GetField("isPlaying_", InstanceNonPublic).SetValue(instance, new ReactiveProperty<bool>(false));
            typeof(Audio).GetField("timeSamples_", InstanceNonPublic).SetValue(instance, new ReactiveProperty<int>(0));
            typeof(Audio).GetField("smoothedTimeSamples_", InstanceNonPublic).SetValue(instance, new ReactiveProperty<float>(0));
            SetInstance<Audio>(instance);
        }

        static bool IsInstanceSet<T>() where T : MonoBehaviour
        {
            // Inspect the `externallySet_` flag directly to avoid invoking the
            // Instance getter (whose fallback would call AddComponent and log
            // editor-script warnings before our SetInstanceForTesting call).
            var closedType = typeof(SingletonMonoBehaviour<T>);
            var flag = closedType.GetField("externallySet_", BindingFlags.NonPublic | BindingFlags.Static);
            return (bool)flag.GetValue(null);
        }

        static void SetInstance<T>(T value) where T : MonoBehaviour
        {
            SingletonMonoBehaviour<T>.SetInstanceForTesting(value);
        }
    }
}