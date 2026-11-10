using UnityEngine;

namespace FlashBeat.UI.V6.Game.Pause
{
    /// <summary>
    /// v6 ポーズ画面 (Issue #224, design `uDNC0`) の純ロジック (EditMode テスト対象)。
    /// 寸法は design px (1728x972 共通フレーム基準)。
    /// 表示値はすべて実データ (GManager / KeyBindings) から派生させる。
    /// </summary>
    public static class V6PauseViewLogic
    {
        /// <summary>design 上の共通フレーム寸法 (契約 §4 / V6Frame と同じ)。</summary>
        public const float FrameW = 1728f;
        public const float FrameH = 972f;

        /// <summary>
        /// 共通フレーム 1728×972 を 1:1 (等倍) で画面に置くときの左上原点 (design px)。
        /// 等比拡大 (min(W/1728, H/972)) は行わない — design の
        /// "Resolution Check · Game (PAUSE)" は各解像度ごとにフレーム原点を固定している
        /// (FHD 1920×1080 → (96,54) / 2K 2560×1440 → (416,234) / MacBook 2560×1664 → (416,346)、
        /// いずれも ((w-1728)/2, (h-972)/2) の切捨て)。
        /// 画面がフレームより小さいときは (0,0) (左上固定、はみ出しは右下方向)。
        /// </summary>
        public static Vector2 FrameOrigin(int width, int height)
        {
            float x = Mathf.Max(0f, Mathf.Floor((width - FrameW) / 2f));
            float y = Mathf.Max(0f, Mathf.Floor((height - FrameH) / 2f));
            return new Vector2(x, y);
        }

        /// <summary>タブの数 (design: Play / Display / Sound / Data)。</summary>
        public const int TabCount = 4;

        /// <summary>index を 0..count-1 に収める (範囲外は端に止める)。</summary>
        public static int ClampIndex(int index, int count)
        {
            if (count <= 0) return 0;
            if (index < 0) return 0;
            if (index > count - 1) return count - 1;
            return index;
        }

        /// <summary>
        /// Tab キーでのタブ切替のように、端で折り返す。
        /// 端をまたぐ (index + delta が count を超える / 0 を下回る) ときだけ折り返し、
        /// 上方向には 0 へ戻す。負値は count を足してから mod を取る。
        /// </summary>
        public static int WrapIndex(int index, int delta, int count)
        {
            if (count <= 0) return 0;

            int next = index + delta;
            if (next < 0)
            {
                // 負値は count を足してから mod を取る (0 の 1 つ前 = count - 1)。
                next = ((next % count) + count) % count;
                return next;
            }

            if (next > count - 1)
            {
                // 端をまたいだときは 0 へ折り返す (範囲外 index も 0 に戻す)。
                return 0;
            }

            return next;
        }

        /// <summary>NoteSpeed の表示 (design 例: "10.0")。小数 1 桁に丸める。</summary>
        public static string FormatNoteSpeed(float value)
        {
            float rounded = Mathf.RoundToInt(value * 10f) / 10f;
            return rounded.ToString("F1");
        }

        /// <summary>NoteTiming の表示 (design 例: "5")。</summary>
        public static string FormatNoteTiming(int value)
        {
            return value.ToString();
        }

        /// <summary>音量 (0..1) の表示 (design 例: "50")。</summary>
        public static string FormatVolume(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f).ToString();
        }

        /// <summary>曲長の表示 (design 例: "1:43")。負値は 0:00。</summary>
        public static string FormatDuration(float seconds)
        {
            if (seconds <= 0f)
            {
                return "0:00";
            }

            int total = (int)seconds;
            return (total / 60) + ":" + (total % 60).ToString("D2");
        }

        /// <summary>
        /// 判定数の表示 (Data タブの「判定」行)。例: "P12 G3 B1 M2"。
        /// 判定 4 種を 1 行にまとめる (Data タブのページは 8 行までしか入らない)。
        /// </summary>
        public static string FormatJudgements(int perfect, int great, int bad, int miss)
        {
            return "P" + perfect + " G" + great + " B" + bad + " M" + miss;
        }

        /// <summary>
        /// 演奏進捗の表示 (Data タブの「進捗」行)。例: "12 / 345"。
        /// cleared は判定済みノーツ数 (perfect+great+bad+miss)、total は譜面の総ノーツ数。
        /// </summary>
        public static string FormatProgress(int cleared, int total)
        {
            return cleared + " / " + total;
        }

        /// <summary>NOTES の表示。クラウド譜面の実ノーツ数 (noteCount) を優先し、
        /// 不明のときだけローカルの totalHits に頼る (#95: Shit は実譜面とずれる)。</summary>
        public static string FormatNotes(SongData song)
        {
            if (song == null)
            {
                return "0";
            }

            return (song.noteCount > 0 ? song.noteCount : song.totalHits).ToString();
        }

        /// <summary>右上の副題 (design 例: "柊マグネタイト・BPM 147")。</summary>
        public static string SongSub(SongData song)
        {
            if (song == null)
            {
                return "";
            }

            string musician = string.IsNullOrEmpty(song.musician) ? "" : song.musician + "・";
            return musician + "BPM " + song.bpm;
        }

        /// <summary>プレビュー パネルの「キー割当」見出しに添える現在のレイアウト状態。
        /// KeyBindings の現在値が既定と同じかで出し分ける (旧 Key Layout 行と共通)。</summary>
        public static string LayoutLabel(bool customLayout)
        {
            return customLayout ? "カスタム" : "標準";
        }

        /// <summary>
        /// JIS キーボード刻印 1 文字 → 候補 KeyCode。
        /// KeyBindings の既定は '-' を Equals と Minus の両方で受ける (KeyConfigUI.DefaultLayout)。
        /// </summary>
        public static KeyCode[] CharToKeyCodes(char c)
        {
            if (c >= '0' && c <= '9')
            {
                return new[] { KeyCode.Alpha0 + (c - '0') };
            }

            if (c >= 'A' && c <= 'Z')
            {
                // KeyCode の英字は小文字の ASCII 値 (KeyCode.A = 97 = 'a')。
                return new[] { (KeyCode)char.ToLower(c) };
            }

            switch (c)
            {
                case '-': return new[] { KeyCode.Minus, KeyCode.Equals };
                case '^': return new[] { KeyCode.Caret };
                case '\\': return new[] { KeyCode.Backslash };
                case '@': return new[] { KeyCode.At };
                case '[': return new[] { KeyCode.LeftBracket };
                case ';': return new[] { KeyCode.Semicolon };
                case ':': return new[] { KeyCode.Colon };
                case ']': return new[] { KeyCode.RightBracket };
                case ',': return new[] { KeyCode.Comma };
                case '.': return new[] { KeyCode.Period };
                case '/': return new[] { KeyCode.Slash };
                case '_': return new[] { KeyCode.Underscore };
                case '*': return new[] { KeyCode.Asterisk };
                case '`': return new[] { KeyCode.BackQuote };
                default: return System.Array.Empty<KeyCode>();
            }
        }

        /// <summary>
        /// JIS 刻印が属するレーン (0 基底)。未割当なら -1。
        /// 正本は KeyBindings.Current (呼び出し側から渡す)。
        /// </summary>
        public static int LaneOfKey(KeyCode[][] bindings, char c)
        {
            if (bindings == null)
            {
                return -1;
            }

            KeyCode[] candidates = CharToKeyCodes(c);
            for (int lane = 0; lane < bindings.Length; lane++)
            {
                KeyCode[] keys = bindings[lane];
                if (keys == null)
                {
                    continue;
                }

                for (int i = 0; i < keys.Length; i++)
                {
                    for (int j = 0; j < candidates.Length; j++)
                    {
                        if (keys[i] == candidates[j])
                        {
                            return lane;
                        }
                    }
                }
            }

            return -1;
        }

        /// <summary>プレビューのキー一覧 (JIS 4 段)。design uDNC0 の Key 行と同じ刻印順。</summary>
        public static readonly char[][] JisKeyRows =
        {
            new[] { '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '-', '^', '\\' },
            new[] { 'Q', 'W', 'E', 'R', 'T', 'Y', 'U', 'I', 'O', 'P', '@', '[' },
            new[] { 'A', 'S', 'D', 'F', 'G', 'H', 'J', 'K', 'L', ';', ':', ']' },
            new[] { 'Z', 'X', 'C', 'V', 'B', 'N', 'M', ',', '.', '/', '_' },
        };

        /// <summary>JIS 各段の x 原点 (design px, プレビューパネル基準)。</summary>
        public static readonly float[] JisKeyRowX = { 8f, 25f, 42f, 59f };

        /// <summary>JIS 各段の y (design px, プレビューパネル基準、上端から)。</summary>
        public static readonly float[] JisKeyRowY = { 356f, 400f, 444f, 488f };
    }
}
