using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class DeferredEditorActions
{
    private static readonly List<Action> _queue = new List<Action>();

    static DeferredEditorActions()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    public static void Enqueue(Action action)
    {
        if (action == null) return;
        if (!Application.isPlaying)
        {
            action();
            return;
        }
        _queue.Add(action);
        Debug.Log($"[DeferredEditorActions] Queued action for post-Play-Mode (total pending: {_queue.Count}).");
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredEditMode) return;
        if (_queue.Count == 0) return;

        var snapshot = new List<Action>(_queue);
        _queue.Clear();
        foreach (var action in snapshot)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DeferredEditorActions] Action failed: {ex.Message}");
            }
        }
    }
}
