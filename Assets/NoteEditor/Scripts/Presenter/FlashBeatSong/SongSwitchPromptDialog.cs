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
        string errorMessage_ = "";
        bool visible_;

        public bool IsVisible { get { return visible_; } }

        // 最後に表示したエラーメッセージ (テストで検証する)。
        public string ErrorMessage { get { return errorMessage_; } }

        // 選択待ち (ボタンのある状態) かどうか。
        public bool HasPendingChoice { get { return onChoice_ != null; } }

        public bool Show(string message, Action<SongSwitchChoice> onChoice)
        {
            // ダイアログ表示中に新しい要求が来た場合、保留中の切替を
            // 上書きしない。拒んで呼び出し側へ返す (独立レビュー S-3)。
            if (visible_)
            {
                Debug.LogWarning("[SongSwitchPromptDialog] ダイアログ表示中のため新しい切替要求を受け付けませんでした。");
                return false;
            }

            message_ = message ?? "";
            errorMessage_ = "";
            onChoice_ = onChoice;
            visible_ = true;
            return true;
        }

        // エラー表示。ダイアログを (選択待ちでなければ) 表示したまま理由を出す。
        public void ShowError(string message)
        {
            errorMessage_ = message ?? "";
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
            errorMessage_ = "";
            if (callback != null)
                callback(choice);
        }

        // エラー表示だけを出しているダイアログを閉じる。
        public void Close()
        {
            visible_ = false;
            onChoice_ = null;
            errorMessage_ = "";
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

            if (!string.IsNullOrEmpty(errorMessage_))
                GUILayout.Label(errorMessage_);

            if (onChoice_ != null)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("保存して切り替え"))
                    Choose(SongSwitchChoice.SaveAndSwitch);

                if (GUILayout.Button("保存せずに切り替え"))
                    Choose(SongSwitchChoice.DiscardAndSwitch);

                if (GUILayout.Button("キャンセル"))
                    Choose(SongSwitchChoice.Cancel);

                GUILayout.EndHorizontal();
            }
            else
            {
                if (GUILayout.Button("閉じる"))
                    Close();
            }

            GUILayout.EndArea();
        }
    }
}
