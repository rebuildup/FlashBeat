using NoteEditor.Model;
using UnityEngine;
using UnityEngine.UI;

namespace NoteEditor.Presenter.Lyrics
{
    public class LyricsListItem : MonoBehaviour
    {
        [SerializeField] Text timeText = default;
        [SerializeField] InputField furiganaInput = default;
        [SerializeField] InputField mondaiInput = default;
        [SerializeField] InputField romajiInput = default;
        [SerializeField] Button removeButton = default;

        public int Index { get; private set; }

        public void SetData(int index, float startTime, float endTime,
            string mondai, string furigana, string romaji)
        {
            Index = index;
            if (timeText != null) timeText.text = $"{startTime:0.00}s - {endTime:0.00}s";
            if (furiganaInput != null) furiganaInput.text = furigana;
            if (mondaiInput != null) mondaiInput.text = mondai;
            if (romajiInput != null) romajiInput.text = romaji;
            if (removeButton != null) removeButton.onClick.AddListener(OnRemoveClicked);

            // 各 InputField にフォーカス解除時のコミットを追加
            if (furiganaInput != null) furiganaInput.onEndEdit.AddListener(v => OnFieldChanged());
            if (mondaiInput != null) mondaiInput.onEndEdit.AddListener(v => OnFieldChanged());
            if (romajiInput != null) romajiInput.onEndEdit.AddListener(v => OnFieldChanged());
        }

        void OnRemoveClicked()
        {
            LyricsTabPresenter.RemoveLyricAt(Index);
        }

        void OnFieldChanged()
        {
            // endTime は固定 (2 秒後) とする。startTime は timeText から再パース。
            // Index が範囲外なら何もしない (RemoveLyricAt 後の stale callback)
            if (Index < 0 || Index >= EditData.Lyrics.StartTime.Value.Length) return;
            float startTime = EditData.Lyrics.StartTime.Value[Index];
            float endTime = EditData.Lyrics.EndTime.Value[Index];
            LyricsTabPresenter.UpdateLyricAt(Index, startTime, endTime,
                mondaiInput.text, furiganaInput.text, romajiInput.text);
        }
    }
}