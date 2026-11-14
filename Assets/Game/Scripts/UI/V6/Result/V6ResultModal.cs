using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.UI.V6.Result
{

    /// <summary>
    /// V6ResultView の登録モーダル (05b lqHaN) 部分
    /// (Issue #385 Phase 2.1 で分割)。partial class のため
    /// フィールド・メソッドは V6ResultView 本体と共有する。
    /// この領域はモーダル構築・名前入力・送信・結果表示・チップが
    /// 一体で凝集しているため 1 ファイルにまとめる (約 780 行、
    /// 600 行目標の例外として #385 に記録)。
    /// </summary>
    public sealed partial class V6ResultView
    {
        // 登録モーダル (Issue #240)。名前入力 + 送信 + 結果表示を実装する。
        private GameObject modal;
        private TextMeshProUGUI modalNameValue;
        private TextMeshProUGUI modalNamePlaceholder;
        private TMP_InputField modalNameInput;
        private string submittedNickname = string.Empty;
        private TextMeshProUGUI modalNamePrompt;
        private GameObject modalChip;
        private CanvasGroup modalChipGroup;
        private TextMeshProUGUI modalChipLabel;
        private Image modalChipBackground;
        private TextMeshProUGUI modalStatus;
        private TextMeshProUGUI modalHead;
        private TextMeshProUGUI modalNameHint;
        private TextMeshProUGUI modalRegisterLabel;
        private Button modalRegisterButton;
        private ResultRankingOverlay legacyOverlay;
        private bool legacyOverlayWasEnabled;
        private TextMeshProUGUI modalSongTitle;
        private TextMeshProUGUI modalSongArtist;
        private TextMeshProUGUI modalGrade;
        private TextMeshProUGUI modalRecordLabel;
        private TextMeshProUGUI modalRecordValue;
        private TextMeshProUGUI modalRecordNote;
        private TextMeshProUGUI modalScoreValue;
        private Image modalThumb;
        private bool modalOpen;

        // チップ (登録しました / 登録できませんでした) の表示時刻と後処理。
        private bool chipActive;
        private float chipShownAt;
        private bool chipClosesModal;

        // モーダルを閉じる・開き直すときに onEndEdit (フォーカス喪失 = 確定) を
        // 抑止する。閉じる操作が送信を誘発しないようにするため。
        private bool suppressNameCommit;

        // 登録送信の状態 (loading / success / duplicate / offline / server error)。
        private V6ResultLogic.SubmitOutcome submitOutcome = V6ResultLogic.SubmitOutcome.Pending;
        private int lastSubmitHttpCode;

        // 登録の方式 (Issue #240)。モーダルを開いた時点で確定する。
        // Online = オンライン順位へ送信 / Local = 端末内だけに保存。
        private V6RegistrationMode registrationMode = V6RegistrationMode.Local;

        // 送信状態 (Idle → Submitting → Succeeded / Failed) と二重送信の抑止
        // (Issue #384)。名前の確定は Enter / フォーカス喪失 / ボタンのどれでも
        // 同じ gate を通る。
        private readonly V6RegistrationGate gate = new V6RegistrationGate();

        // --- 登録モーダル (05b lqHaN) ---
        // 名前の入力 (文字入力 / Backspace / Enter)、送信 (オンライン曲)、
        // ローカル保存 (ローカル曲)、結果表示 (送信中 / 成功 / 429 / オフライン /
        // その他 HTTP) を実装済み。

        private void BuildModal()
        {
            modal = V6Layout.Place(V5Ui.Panel(frame.gameObject, "Modal 05b", new Vector2(FrameW, FrameH),
                V5Tokens.FromHex("#00000099"), 0f), 0f, 0f);
            var scrimButton = modal.AddComponent<Button>();
            scrimButton.transition = Selectable.Transition.None;
            var nav = scrimButton.navigation;
            nav.mode = Navigation.Mode.None;
            scrimButton.navigation = nav;
            scrimButton.onClick.AddListener(CloseModal);

            var dialog = V6Layout.Place(V5Ui.Panel(modal.gameObject, "Dialog",
                    new Vector2(V6ResultLogic.ModalDialogW, V6ResultLogic.ModalDialogH), V5Tokens.Key1, 16f),
                V6ResultLogic.ModalDialogX, V6ResultLogic.ModalDialogY);

            // design YwPw9 の全寸法を Dialog 左上基準で写す。V6Layout.Place(...) の既定 pivot (0,1)
            // で矩形の左上が design 座標に来る (pivot 付き版は使わない)。
            // Head (28,28) 180×43 fs30 / Name Prompt (404,38) 380×29 fs20 (右寄せ)。
            modalHead = OneLine(V5GameUi.Text(dialog.gameObject, "Head", "スコアを登録", 30f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold));
            V6Layout.Place(modalHead, 28f, 28f, 180f, 43f);

            var prompt = OneLine(V5GameUi.Text(dialog.gameObject, "Name Prompt", "プレイヤー名を入力↓", 20f,
                V5Tokens.TextMuted, TextAlignmentOptions.Right));
            V6Layout.Place(prompt, 404f, 38f, 380f, 29f);
            modalNamePrompt = prompt;

            // Name Field (28,112) 784×84 $k0 r8 / Value (24,20) 83×58 fs40 $text-dim。
            var field = V6Layout.Place(V5Ui.Panel(dialog.gameObject, "Name Field", new Vector2(784f, 84f), V5Tokens.Key0, 8f),
                28f, 112f);
            // design lqHaN の Name Field は空のとき "YOU" を $text-dim で出す
            // (v5 ResultView の Placeholder と同じ既定名)。入力すると $text になる。
            modalNameValue = OneLine(V5GameUi.Text(field.gameObject, "Value", string.Empty, 40f,
                V5Tokens.Text, TextAlignmentOptions.Left, FontStyles.Bold));
            // Issue #425: 文字が枠の左端に張り付いて見えるため、内側余白を広げる。
            V6Layout.Place(modalNameValue, 32f, 20f, 728f, 58f);
            modalNamePlaceholder = OneLine(V5GameUi.Text(field.gameObject, "Placeholder",
                V6ResultLogic.LocalNameDefault, 40f, V5Tokens.TextDim,
                TextAlignmentOptions.Left, FontStyles.Bold));
            V6Layout.Place(modalNamePlaceholder, 32f, 20f, 728f, 58f);

            // 名前入力は TMP_InputField に任せる (Issue #384)。自前の Input.inputString
            // 読み取りはレガシー Input に IME の確定文字が来ないため、日本語の名前を
            // 入力できず、カーソルも描けなかった。TMP_InputField は IME 変換・点滅
            // カーソル・フォーカス喪失 (確定) をまとめて持つ。
            // TMP の設定とフォーカス操作は V6 共通部品 (Issue #385 Phase 1.4)。
            // 保存名が入っている欄は「全選択」状態で開く (Issue #425)。
            // 何も打たずに Enter なら保存名がそのまま確定し、何か入力すれば
            // 選択範囲が置き換わって保存名の末尾へ追記される事故を防ぐ。
            // Esc で閉じたときに TMP が元の文字列へ戻すと、確定扱いの onEndEdit が
            // 古い名前を送ってしまうため、復元も切る。
            modalNameInput = V6CursorInput.Attach(field, new V6CursorInput.Spec
            {
                Value = modalNameValue,
                Placeholder = modalNamePlaceholder,
                CaretBlinkHalfCycleSeconds = V6ResultRevealLogic.CaretHalfCycleSeconds,
                OnValidateInput = ValidateNameChar,
                OnEndEdit = OnNameEditEnd,
            });

            // Input Hints (28,208) 784×32:
            // Hint Cap (0,0) 104×32 (pad [0,12], Key fs20) / Hint Text (128,4.5) fs16 /
            // Legend · 移動 (509,3) = KeyCap 57×26 + Label fs18 (65,0) /
            // Legend · 削除 (634,3) = KeyCap 106×26 + Label fs18 (114,0)。
            var hints = V6Layout.Place(V5Ui.Node(dialog.gameObject, "Input Hints"), 28f, 208f, 784f, 32f);
            var cap = V6Layout.Place(V5Ui.Panel(hints, "Hint Cap", new Vector2(104f, 32f), V5Tokens.Key4, 4f), 0f, 0f);
            var capText = OneLine(V5GameUi.Text(cap, "Key", "A-Z 0-9", 20f, V5Tokens.TextMuted,
                TextAlignmentOptions.Left));
            V6Layout.Place(capText, 12f, 1.5f, 80f, 29f);
            modalNameHint = OneLine(V5GameUi.Text(hints, "Hint Text",
                "最大" + RankingClient.NicknameMaxLength + "文字", 16f, V5Tokens.TextMuted));
            V6Layout.Place(modalNameHint, 128f, 4.5f, 200f, 23f);
            BuildHintLegend(hints, 509f, 57f, "← →", "移動");
            BuildHintLegend(hints, 634f, 106f, "Backspace", "削除");

            // Summary (28,264) 784×140 $k2 r8。
            var summary = V6Layout.Place(V5Ui.Panel(dialog.gameObject, "Summary", new Vector2(784f, 140f), V5Tokens.Key2, 8f),
                28f, 264f);
            // 曲バッジ (Thumb + Title/Artist) は共通部品 (Issue #385 Phase 1.2)。
            // Title (120,18) h38 fs26 / Artist (120,52) h26 fs18。design の hug 幅
            // (131 / 126) はサンプル曲名の幅なので、実データが長くても Grade (x704)
            // に届かない幅 568 まで広げ、1 行固定にする。
            var songBadge = V6SongBadge.Create(summary, new V6SongBadge.Spec
            {
                ThumbPos = new Vector2(16f, 20f),
                ThumbSize = new Vector2(88f, 50f),
                ThumbColor = V5Tokens.Key3,
                Title = new V6SongBadge.Line
                {
                    Pos = new Vector2(120f, 18f), Size = new Vector2(568f, 38f), FontSize = 26f,
                    Color = V5Tokens.Text,
                },
                Sub = new V6SongBadge.Line
                {
                    Name = "Artist", Pos = new Vector2(120f, 52f), Size = new Vector2(568f, 26f),
                    FontSize = 18f, Color = V5Tokens.TextMuted,
                    // fs18×h26 は行送り 26.06 を 0.06px 下回るので、Ellipsis が 0 行に
                    // ならないよう実効高を広げる (TMP Ellipsis の共通契約 #354)。
                    FitLineHeight = true, FitHeight = 26f,
                },
            });
            modalThumb = songBadge.Thumb.GetComponent<Image>();
            modalSongTitle = songBadge.Title;
            modalSongArtist = songBadge.Sub;
            // Grade (704,38) 64×40 $k4 r4 / GradeText fs24 centered。
            var gradeChip = V6Layout.Place(V5Ui.Panel(summary, "Grade", new Vector2(64f, 40f), V5Tokens.Key4, 4f),
                704f, 38f);
            modalGrade = V5GameUi.Text(gradeChip, "GradeText", "–", 24f, V5Tokens.Text,
                TextAlignmentOptions.Center, FontStyles.Normal);
            V6Layout.StretchCenter(modalGrade.GetComponent<RectTransform>(), 64f, 40f);
            V6Layout.Place(V5Ui.Panel(summary, "Divider", new Vector2(752f, 1f), V5Tokens.FromHex("#FFFFFF1F"), 0f), 16f, 94f);
            // Record Label (16,104) h23 fs16 $text-dim / Record Value (440,102)
            // 180×29 fs20 右寄せ / Record Note (640,104) 113×23 fs16 $text-dim。
            modalRecordLabel = OneLine(V5GameUi.Text(summary, "Record Label", "", 16f, V5Tokens.TextDim));
            V6Layout.Place(modalRecordLabel, 16f, 104f, 300f, 23f);
            modalRecordValue = OneLine(V5GameUi.Text(summary, "Record Value", "", 20f, V5Tokens.Text,
                TextAlignmentOptions.Right));
            V6Layout.Place(modalRecordValue, 440f, 102f, 180f, 29f);
            modalRecordNote = OneLine(V5GameUi.Text(summary, "Record Note", "", 16f, V5Tokens.TextDim));
            V6Layout.Place(modalRecordNote, 640f, 104f, 113f, 23f);
            // Score Group (400,26) 288×52:
            // Score Label (91,14.5) 51×23 fs16 / Score Value (156,1.5) 132×49 fs34。
            // 正本の text ノードは alignment 未指定 = 既定 (左)。design 画像の実測でも
            // 値のインクは 157..285 (箱 156..288) で左詰め。右寄せにすると 0 のとき
            // SCORE から離れて Grade の手前へ寄るため、design どおり左にする。
            // 横並びの相対配置ではなく design の実座標に置く (旧 Row 実装では
            // 子の矩形幅が 0 のまま詰められ、SCORE と数値が design と違う位置に出た)。
            var scoreGroup = V6Layout.Place(V5Ui.Node(summary, "Score Group"), 400f, 26f, 288f, 52f);
            OneLine(V5GameUi.Text(scoreGroup, "Score Label", "SCORE", 16f, V5Tokens.TextDim));
            V6Layout.Place(scoreGroup.transform.Find("Score Label"), 91f, 14.5f, 51f, 23f);
            modalScoreValue = OneLine(V5GameUi.Text(scoreGroup, "Score Value", "0", 34f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold));
            V6Layout.Place(modalScoreValue, 156f, 1.5f, 132f, 49f);

            // Buttons (28,432) 784×50:
            // Skip (318,0) 198×50 / Register (532,0) 252×50。旧 Row は左詰めに
            // していたため design より 318px 左に寄っていた。
            var buttons = V6Layout.Place(V5Ui.Node(dialog.gameObject, "Buttons"), 28f, 432f, 784f, 50f);
            var skip = V6Layout.Place(V6Button.Panel(buttons, "Button Skip", new Vector2(198f, 50f),
                V5Tokens.Key4, 8f, CloseModal), 318f, 0f);
            // Button · Skip: Icon (24,13) 24 / Label (60,6) 54×38 fs26 /
            // KeyCap (126,10.5) 48×29 pad [0,8] Key fs20。
            var skipIcon = V6Icons.Create(skip, "x", 24f, V5Tokens.TextMuted, "Icon");
            V6Layout.Place(skipIcon.gameObject, 24f, 13f, 24f, 24f);
            OneLine(V5GameUi.Text(skip, "Label", "Skip", 26f, V5Tokens.Text, TextAlignmentOptions.Left));
            V6Layout.Place(skip.transform.Find("Label"), 60f, 6f, 54f, 38f);
            BuildModalKeyCap(skip, 126f, 10.5f, 48f, "Esc", V5Tokens.FromHex("#00000040"), V5Tokens.TextMuted, 20f);
            var register = V6Layout.Place(V6Button.Panel(buttons, "Button Register", new Vector2(252f, 50f),
                V5Tokens.ButtonPrimaryBg, 8f, TrySubmitFromButton), 532f, 0f);
            // Button · Register: Icon (18.5,13) 24#1B1B1B / Label (54.5,6) 101×38 fs26 /
            // KeyCap (167.5,10.5) 66×29 #00000026 Key fs20 #1B1B1B。
            var registerIcon = V6Icons.Create(register, "check", 24f, V5Tokens.ButtonPrimaryLabel, "Icon");
            V6Layout.Place(registerIcon.gameObject, 18.5f, 13f, 24f, 24f);
            modalRegisterLabel = OneLine(V5GameUi.Text(register, "Label", "Register", 26f,
                V5Tokens.ButtonPrimaryLabel, TextAlignmentOptions.Left, FontStyles.Normal));
            V6Layout.Place(modalRegisterLabel, 54.5f, 6f, 101f, 38f);
            BuildModalKeyCap(register, 167.5f, 10.5f, 66f, "Enter", V5Tokens.FromHex("#00000026"),
                V5Tokens.ButtonPrimaryLabel, 20f);
            modalRegisterButton = register.GetComponent<Button>();

            // 状態メッセージ (loading / success / duplicate / offline / server error)。
            // design に無い実装都合の行なので Head/Prompt と重ならない位置 (右側 y74) に置く。
            modalStatus = OneLine(V5GameUi.Text(dialog.gameObject, "Status", "", 17f, V5Tokens.TextMuted,
                TextAlignmentOptions.Left));
            V6Layout.Place(modalStatus, 220f, 74f, 592f, 26f);

            // 登録チップ (design TYshI 272×32)。成功は $ok-tint / $ok-light、
            // 失敗は $danger-tint / $danger-light。Head の右に出し、表示中は
            // Name Prompt を隠す (同じ位置に重なるため)。
            modalChip = V6Layout.Place(V5Ui.Panel(dialog.gameObject, "Status Chip", new Vector2(272f, 32f),
                V5Tokens.OkTint, 4f), 540f, 28f);
            modalChipBackground = modalChip.GetComponent<Image>();
            modalChipGroup = modalChip.AddComponent<CanvasGroup>();
            modalChipLabel = V5GameUi.Text(modalChip, "Label", "", 16f, V5Tokens.OkLight,
                TextAlignmentOptions.Center, FontStyles.Bold);
            V6Layout.StretchCenter(modalChipLabel.GetComponent<RectTransform>(), 272f, 32f);
            modalChip.SetActive(false);

            modal.SetActive(false);
        }

        /// <summary>
        /// design YwPw9 の凡例 (Legend): KeyCap h26 / pad [0,8] / Key fs18 +
        /// Label fs18 (capW + 8 の位置)。x は Input Hints 左端からの design 座標。
        /// </summary>
        private void BuildHintLegend(GameObject parent, float x, float capW, string key, string label)
        {
            var item = V6Layout.Place(V5Ui.Node(parent, "Legend " + label), x, 3f, capW + 8f + 36f, 26f);
            var cap = V6Layout.Place(V5Ui.Panel(item, "KeyCap", new Vector2(capW, 26f), V5Tokens.Key4, 4f), 0f, 0f);
            var keyText = OneLine(V5GameUi.Text(cap, "Key", key, 18f, V5Tokens.TextMuted,
                TextAlignmentOptions.Left));
            V6Layout.Place(keyText, 8f, 0f, capW - 16f, 26f);
            var labelText = OneLine(V5GameUi.Text(item, "Label", label, 18f, V5Tokens.TextMuted,
                TextAlignmentOptions.Left));
            V6Layout.Place(labelText, capW + 8f, 0f, 36f, 26f);
        }

        /// <summary>
        /// design のモーダル ボタン内 KeyCap (h29 / pad [0,8] / Key fs20)。
        /// 位置を design 座標で受け取る (旧実装は Row の中央寄せに頼っていた)。
        /// </summary>
        private void BuildModalKeyCap(GameObject parent, float x, float y, float w, string key,
            Color fill, Color keyColor, float fontSize)
        {
            var cap = V6Layout.Place(V5Ui.Panel(parent, "KeyCap", new Vector2(w, 29f), fill, 4f), x, y);
            var capText = OneLine(V5GameUi.Text(cap, "Key", key, fontSize, keyColor,
                TextAlignmentOptions.Center));
            V6Layout.StretchCenter(capText.GetComponent<RectTransform>(), w, 29f);
        }

        private void OpenModal()
        {
            if (modal == null || modalOpen)
            {
                return;
            }

            modalOpen = true;
            submitOutcome = V6ResultLogic.SubmitOutcome.Pending;

            // 登録の方式 (送信対象か) を確定する。曲情報はプレイ開始時に固定した
            // (pinned) 版を使う (GManager.GetSong が ResultScene では snapshot を優先)。
            registrationMode = CurrentRegistrationMode();
            modalNameInput.characterLimit = NameMaxLength();

            // 初期値: オンラインはオンライン側の保存名、無ければ端末内の保存名。
            // ローカルは端末内の保存名 (v5 と同じ PlayerPrefs キー)。
            string saved = V6ResultLogic.InitialName(
                registrationMode, RankingClient.LoadNickname(), V6LocalNameStore.Load());
            modalNameInput.SetTextWithoutNotify(saved ?? string.Empty);

            ApplyRegistrationModeTexts();
            HideSubmitChip();

            // 旧オーバーレイ (ResultRankingOverlay) は Result シーンで自動で
            // モーダルを開き、キーも処理する。二重モーダルと二重入力を防ぐため
            // v6 モーダル表示中は停止し、閉じたら戻す (ソースは編集しない)。
            SuspendLegacyOverlay();

            RefreshModal();
            SetModalStatus(gate.Failed ? SubmitStatusText() : "",
                gate.Failed ? V5Tokens.FromHex("#FF9999") : V5Tokens.TextMuted);
            modal.SetActive(true);
            FocusNameField();
        }

        private void CloseModal()
        {
            if (modal == null)
            {
                return;
            }

            modalOpen = false;

            // 閉じる操作で onEndEdit (フォーカス喪失 = 確定) が走り、意図しない
            // 自動登録を誘発するのを防ぐ。
            suppressNameCommit = true;
            UnfocusNameField();
            suppressNameCommit = false;

            // 応答待ちの送信は破棄しない (閉じてもコルーチンは完了させる)。
            // gate の送信中フラグも落とさない (落とすと再送できてしまう)。
            SetRegisterInteractable(true);
            HideSubmitChip();
            modal.SetActive(false);
            ResumeLegacyOverlay();
            FlashBeat.UI.SceneInputGate.BlockThisFrame();
        }

        // --- 名前入力 (TMP_InputField) ---

        private void FocusNameField()
        {
            // Issue #425: 文末カーソルではなく全選択で開く (V6CursorInput 側の契約)。
            V6CursorInput.FocusSelectAll(modalNameInput);
        }

        private void UnfocusNameField()
        {
            V6CursorInput.Unfocus(modalNameInput);
        }

        /// <summary>
        /// 入力 1 文字の検証。端末内保存は v5 と同じ A-Z 0-9 だけを受ける
        /// (オンラインは日本語も含めて自由)。
        /// </summary>
        private char ValidateNameChar(string text, int charIndex, char addedChar)
        {
            if (registrationMode == V6RegistrationMode.Local && !IsLocalNameChar(addedChar))
            {
                return '\0';
            }

            return addedChar;
        }

        /// <summary>
        /// Enter / フォーカス喪失で名前が確定した (Issue #384)。空でなければ
        /// 自動で登録する。送信中・登録済みは gate が弾く。
        /// </summary>
        /// <remarks>
        /// Esc で取り消した編集では送信しない (PR #387 レビュー指摘)。
        /// TMP_InputField は Esc で <c>m_WasCanceled</c> を立ててから
        /// <c>ReleaseSelection</c> 経由で <c>onEndEdit</c> を通知する。ここで
        /// <c>wasCanceled</c> を見ないと、保存済みの名前が入力欄に戻った状態で
        /// 自動登録が走ってしまう (「取り消したのに登録された」)。
        /// </remarks>
        private void OnNameEditEnd(string value)
        {
            if (suppressNameCommit || !modalOpen)
            {
                return;
            }

            if (modalNameInput != null && modalNameInput.wasCanceled)
            {
                return; // Esc で取り消した編集は確定しない
            }

            string name = (value ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                return; // 空は確定しない (プレースホルダのまま)
            }

            TrySubmit();
        }

        // --- 登録送信 (Issue #240) ---

        /// <summary>ボタン経由の送信。送信中と登録済みは無視する (二重送信防止)。</summary>
        private void TrySubmitFromButton()
        {
            TrySubmit();
        }

        private void TrySubmit()
        {
            if (!gate.CanStart)
            {
                return;
            }

            // 送信対象でない (ローカル曲 / 譜面未取得) はオンラインへ送らない。
            // v5 と同じ PlayerPrefs キーへ端末内保存だけする (Issue #240)。
            if (registrationMode == V6RegistrationMode.Local)
            {
                SaveLocalName();
                return;
            }

            V6ResultLogic.NicknameError error = V6ResultLogic.ValidateNicknameInput(
                modalNameInput.text, RankingClient.NicknameMaxLength);
            if (error != V6ResultLogic.NicknameError.None)
            {
                SetModalStatus(NicknameErrorMessage(error), V5Tokens.FromHex("#FFD64D"));
                return;
            }

            if (!gate.Begin())
            {
                return;
            }

            submitOutcome = V6ResultLogic.SubmitOutcome.Pending;
            SetModalStatus(V6ResultLogic.SubmitStatusMessage(V6ResultLogic.SubmitOutcome.Pending, 0),
                V5Tokens.FromHex("#99E5FF"));
            SetRegisterInteractable(false);
            // 送った名前を控える。応答が返るまでに入力欄が編集されても、
            // 保存するのは実際に送った名前 (送った後に書いた文字ではない)。
            submittedNickname = modalNameInput.text.Trim();
            RunCoroutine(SubmitCoroutine(submittedNickname));
        }

        private IEnumerator SubmitCoroutine(string nickname)
        {
            // 曲情報はプレイ開始時に固定した (pinned) 版。song.chartVersion が
            // 実際にプレイした譜面の版になる (GManager.GetSong は ResultScene で
            // PlaySessionSnapshot の固定を優先する)。
            SongData song = SongOf();
            int chartVersion = V6ResultLogic.ResolveChartVersion(
                PinnedChartVersionOf(), song != null ? song.chartVersion : 0);
            if (!V6ResultLogic.IsSubmittable(
                    song != null && song.isRemote,
                    song != null ? song.remoteId : null,
                    chartVersion))
            {
                // 送信対象が無い (ローカル曲 / 譜面未取得)。オンラインの失敗表示
                // (「オフライン」等) は出さず、端末内保存だけ行う。
                SaveLocalName();
                yield break;
            }

            RankingClient.Result result = null;
            yield return RankingClient.PostScore(song.remoteId, chartVersion, nickname,
                runScore, runPerfect, runGreat, runBad, runMiss, runMaxCombo, r => { result = r; });
            OnSubmitResult(result);
        }

        /// <summary>
        /// 端末内保存 (Issue #240)。v5 と同じ PlayerPrefs キー "V5PlayerName" に
        /// v5 と同じ規則 (大文字化 + A-Z0-9 + 最大8文字) で保存し、
        /// オンライン語彙を含まない結果メッセージを表示する。
        /// </summary>
        private void SaveLocalName()
        {
            string saved = V6ResultLogic.NormalizeLocalName(modalNameInput.text);
            V6LocalNameStore.Save(saved);
            gate.Succeed();

            // 入力欄を正規化済みの保存名にそろえる (onEndEdit は発火させない)。
            suppressNameCommit = true;
            modalNameInput.SetTextWithoutNotify(saved);
            suppressNameCommit = false;

            // 保存済み。Register ボタンで二重保存しない (Esc / Skip で閉じる)。
            SetRegisterInteractable(false);
            SetModalStatus(V6ResultLogic.LocalSaveMessage(saved), V5Tokens.FromHex("#99E5FF"));
            ShowSubmitChip("保存しました", true);
        }

        /// <summary>
        /// 現在の登録の方式を求める。曲情報はプレイ開始時に固定した版を使う。
        /// </summary>
        private V6RegistrationMode CurrentRegistrationMode()
        {
            SongData song = SongOf();
            int chartVersion = V6ResultLogic.ResolveChartVersion(
                PinnedChartVersionOf(), song != null ? song.chartVersion : 0);
            return V6ResultLogic.RegistrationMode(
                song != null && song.isRemote,
                song != null ? song.remoteId : null,
                chartVersion);
        }

        /// <summary>
        /// プレイ開始時に固定した譜面版 (pin)。固定が無い / 結果画面でないときは 0。
        /// </summary>
        private static int PinnedChartVersionOf()
        {
            SongData pinned = PlaySessionSnapshot.PinnedSongFor(GManager.songID);
            return pinned != null ? pinned.chartVersion : 0;
        }

        /// <summary>
        /// 方式に応じてモーダルの見出し・ボタン・入力ヒントを切り替える。
        /// ローカル曲にはオンラインの語彙 (スコア登録 / Register) を出さない。
        /// ローカル曲の上限は v5 と同じ (8 文字 / A-Z 0-9)。
        /// </summary>
        private void ApplyRegistrationModeTexts()
        {
            bool local = registrationMode == V6RegistrationMode.Local;
            if (modalHead != null)
            {
                // ローカル曲ではオンラインの語彙 (スコア登録) を出さない。
                // v5 のローカル登録モーダルと同じ見出し ("この端末の名前") を使う。
                modalHead.text = local ? "この端末の名前" : "スコアを登録";
            }

            if (modalRegisterLabel != null)
            {
                // v5 と同じ (ローカル保存は Save / オンライン送信は Register)。
                modalRegisterLabel.text = local ? "Save" : "Register";
            }

            if (modalNameHint != null)
            {
                modalNameHint.text = local
                    ? "最大" + V6ResultLogic.LocalNameMaxLength + "文字 (A-Z 0-9)"
                    : "最大" + RankingClient.NicknameMaxLength + "文字";
            }
        }

        private void OnSubmitResult(RankingClient.Result result)
        {
            if (!gate.InFlight)
            {
                return; // 閉じたあとの応答は捨てる
            }

            lastSubmitHttpCode = result != null ? result.httpCode : 0;
            submitOutcome = V6ResultLogic.ClassifySubmitResult(
                result != null && result.ok, lastSubmitHttpCode,
                result != null ? result.error : null);

            if (submitOutcome == V6ResultLogic.SubmitOutcome.Success
                && result.score != null && result.score.rank > 0)
            {
                // 偽の成功を出さない: 順位はサーバ応答からのみ。
                RankingClient.SaveNickname(submittedNickname);
                gate.Succeed();
                myRank = result.score.rank;
                SetRegisterInteractable(false);
                SetModalStatus("", V5Tokens.TextMuted);
                ShowSubmitChip("登録しました", true);
                // 登録後の順位 (自分の行のハイライト) は再取得したランキングから。
                StartRankingFetch();
                return;
            }

            if (submitOutcome == V6ResultLogic.SubmitOutcome.Duplicate)
            {
                // 同じ内容が既に届いている (429 の 10 秒窓)。登録済みとして扱い、
                // 再送は許さない。
                gate.Succeed();
                SetRegisterInteractable(false);
                SetModalStatus("", V5Tokens.TextMuted);
                ShowSubmitChip("登録ずみです", true);
                return;
            }

            // 失敗: その事実を出して再送を許す (Esc = スキップ)。
            gate.Fail();
            SetRegisterInteractable(true);
            if (modalRegisterLabel != null)
            {
                modalRegisterLabel.text = "再試行";
            }

            SetModalStatus(V6ResultLogic.SubmitStatusMessage(submitOutcome, lastSubmitHttpCode),
                V5Tokens.FromHex("#FF9999"));
            ShowSubmitChip("登録できませんでした", false);
        }

        private string SubmitStatusText()
        {
            return V6ResultLogic.SubmitStatusMessage(submitOutcome, lastSubmitHttpCode);
        }

        // --- 登録チップ (Issue #384) ---

        private void ShowSubmitChip(string message, bool success)
        {
            if (modalChip == null)
            {
                return;
            }

            modalChipLabel.text = message;
            modalChipLabel.color = success ? V5Tokens.OkLight : V5Tokens.DangerLight;
            modalChipBackground.color = success ? V5Tokens.OkTint : V5Tokens.DangerTint;
            modalChipGroup.alpha = 0f;
            modalChip.SetActive(true);
            if (modalNamePrompt != null)
            {
                modalNamePrompt.gameObject.SetActive(false);
            }

            chipActive = true;
            chipShownAt = Time.unscaledTime;
            // 成功はチップが消えたあとモーダルを閉じる (結果画面へ戻る)。
            // 失敗は再試行できるようモーダルを残す。
            chipClosesModal = success;
        }

        private void HideSubmitChip()
        {
            chipActive = false;
            if (modalChip != null)
            {
                modalChip.SetActive(false);
            }

            if (modalNamePrompt != null)
            {
                modalNamePrompt.gameObject.SetActive(true);
            }
        }

        private void UpdateSubmitChip()
        {
            if (!chipActive || modalChip == null)
            {
                return;
            }

            float elapsed = Time.unscaledTime - chipShownAt;
            float fadeIn = V6ResultRevealLogic.ChipFadeInSeconds;
            float hold = V6ResultRevealLogic.ChipHoldSeconds;
            float fadeOut = V6ResultRevealLogic.ChipFadeOutSeconds;

            if (elapsed < fadeIn)
            {
                modalChipGroup.alpha = Mathf.Clamp01(elapsed / fadeIn);
            }
            else if (elapsed < fadeIn + hold)
            {
                modalChipGroup.alpha = 1f;
            }
            else if (elapsed < fadeIn + hold + fadeOut)
            {
                modalChipGroup.alpha = Mathf.Clamp01(1f - (elapsed - fadeIn - hold) / fadeOut);
            }
            else
            {
                bool close = chipClosesModal;
                HideSubmitChip();
                if (close)
                {
                    CloseModal();
                }
            }
        }

        private static string NicknameErrorMessage(V6ResultLogic.NicknameError error)
        {
            switch (error)
            {
                case V6ResultLogic.NicknameError.Empty:
                    return "なまえを いれてください";
                case V6ResultLogic.NicknameError.TooLong:
                    return "なまえが ながすぎます (最大 " + RankingClient.NicknameMaxLength + "文字)";
                case V6ResultLogic.NicknameError.InvalidChar:
                    return "なまえに つかえない文字が あります";
                default:
                    return "";
            }
        }

        private void HandleModalInput()
        {
            if (gate.InFlight)
            {
                return; // 応答待ち。Esc で中断はしない (二重送信を防ぐ)。
            }

            // Esc だけを画面の操作として扱い、それ以外のキーは入力欄へ渡す
            // (design 決定 §7.3: 入力欄がフォーカス中は Esc 以外を奪わない)。
            // 文字入力・Backspace・Enter・矢印移動は TMP_InputField が処理する。
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseModal();
            }
        }

        /// <summary>ローカル名に使える文字 (v5 と同じ A-Z0-9)。</summary>
        private static bool IsLocalNameChar(char c)
        {
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
        }

        /// <summary>入力可能な名前の上限文字数 (ローカル 8 / オンライン 16)。</summary>
        private int NameMaxLength()
        {
            return registrationMode == V6RegistrationMode.Local
                ? V6ResultLogic.LocalNameMaxLength
                : RankingClient.NicknameMaxLength;
        }

        private void SetModalStatus(string message, Color color)
        {
            if (modalStatus != null)
            {
                modalStatus.text = message;
                modalStatus.color = color;
            }
        }

        private void SetRegisterInteractable(bool interactable)
        {
            if (modalRegisterButton != null)
            {
                modalRegisterButton.interactable = interactable;
            }
        }

        private void SuspendLegacyOverlay()
        {
            if (legacyOverlay == null)
            {
                legacyOverlay = UnityEngine.Object.FindFirstObjectByType<ResultRankingOverlay>();
            }

            if (legacyOverlay != null)
            {
                // 停止前の状態を覚えておく。統合側が v6 有効時に先に enabled=false して
                // いる場合、モーダルを閉じたときに再有効化して自動モーダルを
                // 復活させてしまわないようにする。
                legacyOverlayWasEnabled = legacyOverlay.enabled;
                legacyOverlay.enabled = false;
            }
        }

        private void ResumeLegacyOverlay()
        {
            if (legacyOverlay != null)
            {
                if (legacyOverlayWasEnabled)
                {
                    legacyOverlay.enabled = true;
                }
                legacyOverlay = null;
            }
        }

        private void RefreshModal()
        {
            int score = runScore;
            modalScoreValue.text = V6ResultLogic.FormatScore(score);
            modalGrade.text = V6ResultLogic.GradeOf(score);

            SongData song = SongOf();
            if (song != null)
            {
                modalSongTitle.text = song.title ?? "";
                modalSongArtist.text = song.musician ?? "";
                SetThumb(modalThumb, song);
            }
            else
            {
                modalSongTitle.text = "";
                modalSongArtist.text = "";
            }

            int best = BestOf();
            if (prevHiscore >= 0)
            {
                string delta = V6ResultLogic.DeltaLabel(prevHiscore, score);
                bool beaten = delta.Length > 0;
                modalRecordLabel.text = "前回最高 " + V6ResultLogic.FormatScore(beaten ? prevHiscore : best);
                modalRecordValue.text = delta;
                modalRecordNote.text = beaten ? "自己ベスト更新" : "";
            }
            else
            {
                modalRecordLabel.text = "前回最高 " + V6ResultLogic.FormatScore(best);
                modalRecordValue.text = "";
                modalRecordNote.text = "";
            }
        }
    }
}
