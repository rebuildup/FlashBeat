using System;
using UnityEngine;

namespace NoteEditor.Presenter.FlashBeatSong
{
    // 曲切替時の確認ダイアログ。
    //
    // NoteEditor のシーン / プレハブを編集せずに導入できるよう、
    // 実行時に生成される自律的な IMGUI ダイアログとして実装する
    // (uGUI のダイアログはシーンに GameObject を足す必要があるため)。
    // ロジック (何をするか) は SongSwitchDecision / SongSwitchCoordinator が
    // 担い、このクラスは「ボタン押された → 選択を通知」だけを行う。
    public class SongSwitchPromptDialog : MonoBehaviour, ISongSwitchPrompt
    {
        Action<SongSwitchChoice> onChoice_;
        string message_ = "";
        bool visible_;

        public bool IsVisible { get { return visible_; } }

        public void Show(string message, Action<SongSwitchChoice> onChoice)
        {
            message_ = message ?? "";
            onChoice_ = onChoice;
            visible_ = true;
        }

        // 選択を通知する (ボタン押下 / テストからの注入)。
        public void Choose(SongSwitchChoice choice)
        {
            if (!visible_)
                return;

            visible_ = false;
            var callback = onChoice_;
            onChoice_ = null;
            if (callback != null)
                callback(choice);
        }

        void OnGUI()
        {
            if (!visible_)
                return;

            var area = new Rect(
                Screen.width * 0.25f,
                Screen.height * 0.35f,
                Screen.width * 0.5f,
                Screen.height * 0.3f);

            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label(message_);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("保存して切り替え"))
                Choose(SongSwitchChoice.SaveAndSwitch);

            if (GUILayout.Button("保存せずに切り替え"))
                Choose(SongSwitchChoice.DiscardAndSwitch);

            if (GUILayout.Button("キャンセル"))
                Choose(SongSwitchChoice.Cancel);

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
