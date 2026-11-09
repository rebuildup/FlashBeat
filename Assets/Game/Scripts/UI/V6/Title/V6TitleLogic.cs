using System.Globalization;
using FlashBeat.Update;
using UnityEngine;

namespace FlashBeat.UI.V6.Title
{
    /// <summary>
    /// V6TitleView の表示文 / 表示条件の純ロジック (Unity API に依存しない)。
    /// V6SelectViewLogic / V6ResultLogic と同じ分離。EditMode テストは
    /// Assets/Tests/Editor/V6TitleLogicTests.cs。
    /// </summary>
    public static class V6TitleLogic
    {
        /// <summary>design フレームの基準サイズ (QJtTv)。</summary>
        public const float FrameW = 1728f;
        public const float FrameH = 972f;

        /// <summary>
        /// 旧 (v5) タイトル UI のオブジェクト名か (V6UiMode.HideLegacyTitleUi の判定本体)。
        /// 名前は TitleScene.unity 実測 (シーン配置 "Canvas" 配下の Stage 16:9 ゾーンと
        /// その子: Logo / Menu / Menu GameStart|Option|Exit / Catalog Status / 枠飾り)。
        /// v6 側の名前 ("V6 Title Canvas" / "V6 Title View" / "Tabs" / "KeyCap" など) は
        /// 一致させない (v6 自身を隠さない)。null / 空 / 前後空白は非該当。
        /// </summary>
        public static bool IsLegacyTitleObjectName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return false;
            }

            switch (objectName)
            {
                case "Canvas":
                case "Stage 16:9":
                case "BG":
                case "Logo":
                case "Menu":
                case "Menu GameStart":
                case "Menu Option":
                case "Menu Exit":
                case "Catalog Status":
                case "Edge Deco Left":
                case "Edge Deco Right":
                case "Bar V":
                case "Bar H":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// design フレーム (1728×972) を実画面へ <b>1:1 (等倍)</b> で中央配置するときの
        /// 左上 origin = ((w−1728)/2, (h−972)/2) (design px、切り捨て)。
        /// 正本: design POJwG "Resolution Check · Title" の実配置
        /// (FHD 1920×1080 → (96,54) / 2K 2560×1440 → (416,234) / Mac 2560×1664 → (416,346))。
        /// 旧実装の min(w/1728, h/972) による引き伸ばし (等比スケール) は廃止
        /// (フレームは固定解像度であり、拡大縮小はしない)。
        /// </summary>
        public static Vector2 FrameOrigin(int screenW, int screenH)
        {
            if (screenW <= 0 || screenH <= 0)
            {
                return Vector2.zero;
            }

            return new Vector2(
                Mathf.Floor((screenW - FrameW) / 2f),
                Mathf.Floor((screenH - FrameH) / 2f));
        }

        // --- Title 固有ノードの design 配置 (QJtTv "01 Title · v6") ---
        // 座標は各ゾーン ローカルの design px (左上原点・y_down)。
        // Header/Main/Footer ゾーン自体の矩形は V6Frame (契約 §4) が持つ。

        /// <summary>戻るボタン (zUcsi/zCQBY): Header ローカル 56×56。</summary>
        public static readonly Vector2 BackButtonPos = new Vector2(0f, 20f);
        public static readonly Vector2 BackButtonSize = new Vector2(56f, 56f);

        /// <summary>ヘッダー タイトル文字 (aqq3q/ci867): Header ローカル。</summary>
        public static readonly Vector2 HeaderTitlePos = new Vector2(72f, 20f);
        public static readonly Vector2 HeaderTitleSize = new Vector2(185f, 56f);
        public const float HeaderTitleFontSize = 38f;

        /// <summary>右上の曲情報ゾーン (WUQeP) / 状態ゾーン (l26KMD): Header ローカル 300×96。</summary>
        public static readonly Vector2 SongInfoZonePos = new Vector2(1080f, 0f);
        public static readonly Vector2 StateZonePos = new Vector2(1380f, 0f);
        public static readonly Vector2 HeaderZoneSize = new Vector2(300f, 96f);

        /// <summary>ロゴ (CnRJj, assets/13.png contain): Main ローカル。</summary>
        public static readonly Vector2 LogoPos = new Vector2(0f, 180f);
        public static readonly Vector2 LogoSize = new Vector2(1680f, 332f);

        /// <summary>タブ列 (FHcxk, $overlay r8) とタブ 1 枚: Main ローカル。</summary>
        public static readonly Vector2 TabsPos = new Vector2(280f, 552f);
        public static readonly Vector2 TabsSize = new Vector2(1120f, 48f);
        public static readonly Vector2 TabSize = new Vector2(280f, 48f);
        public const float TabIconSize = 24f;
        public const float TabGap = 8f;
        public const float TabPaddingX = 10f;
        public const float TabLabelFontSize = 26f;

        // Issue #239: History / Ranking タブの KeyCap 表記と、TitleSceneManager が
        // 実際に poll するキー。KeyActions に割当が無い固定キーなので、cap 文字と
        // KeyCode を単一ソースに置いて凡例と実キーの食い違いを防ぐ。
        /// <summary>History タブの KeyCap 表記。</summary>
        public const string HistoryTabKey = "H";

        /// <summary>Ranking タブの KeyCap 表記。</summary>
        public const string RankingTabKey = "R";

        /// <summary>History タブを開くキー (KeyActions に割当が無いため固定)。</summary>
        public static KeyCode HistoryTabKeyCode { get { return KeyCode.H; } }

        /// <summary>Ranking タブを開くキー (KeyActions に割当は無い)。</summary>
        public static KeyCode RankingTabKeyCode { get { return KeyCode.R; } }


        /// <summary>フッター キー凡例 (QQ4jM, gap16): Footer ローカル。</summary>
        public static readonly Vector2 LegendPos = new Vector2(0f, 30f);
        public const float LegendGap = 16f;
        public const float LegendInnerGap = 8f;
        public const float LegendFontSize = 22f;

        /// <summary>右下の補助操作 (FChYu→G1dOc 設定) / 主操作 (F8SwvS→i9eZ1G START): Footer ローカル。</summary>
        public static readonly Vector2 FooterAuxPos = new Vector2(1198f, 20f);
        public static readonly Vector2 FooterAuxSize = new Vector2(197f, 56f);
        public static readonly Vector2 FooterPrimaryPos = new Vector2(1407f, 20f);
        public static readonly Vector2 FooterPrimarySize = new Vector2(273f, 56f);

        /// <summary>
        /// プレイ可能な曲数 (選曲画面 V6SelectDataSource.RebuildSongs と同じ基準):
        /// index 0 (noSong プレースホルダ) と unplayable (譜面のない曲) は数えない。
        /// </summary>
        public static int PlayableSongCount(SongData[] songs)
        {
            if (songs == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 1; i < songs.Length; i++)
            {
                SongData song = songs[i];
                if (song == null || song.unplayable)
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        /// <summary>ヘッダー右上に出すカタログ状態の文言 (実データからのみ作る)。</summary>
        public static string CatalogStatusText(bool hasRemoteCatalog, int songCount)
        {
            string state = hasRemoteCatalog ? "クラウドカタログ 統合済み" : "ローカルカタログ";
            return "楽曲 " + songCount + " 曲 · " + state;
        }

        /// <summary>バージョン表記 ("1.2.3" → "v1.2.3")。空なら空文字。</summary>
        public static string VersionLabel(string version)
        {
            return string.IsNullOrEmpty(version) ? "" : "v" + version;
        }

        /// <summary>
        /// 「更新して再起動」ボタンを出すか。更新フローが動いているときだけ
        /// (design lyK9E の更新ボタンは常在ノードではなく、更新があるときの状態)。
        /// </summary>
        public static bool ShowUpdateButton(UpdateFlowPhase phase)
        {
            return phase == UpdateFlowPhase.Available || phase == UpdateFlowPhase.Downloading
                || phase == UpdateFlowPhase.Ready;
        }

        /// <summary>更新ボタンのラベル (Downloading 中は進捗を出す)。</summary>
        public static string UpdateButtonLabel(UpdateFlowPhase phase, float progress)
        {
            if (phase == UpdateFlowPhase.Downloading)
            {
                return "更新を準備中... " + Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f) + "%";
            }

            return "更新して再起動";
        }

        /// <summary>新版のバージョン (→ の右側) を出すか。取得済みのときだけ。</summary>
        public static bool ShowNewVersion(UpdateFlowPhase phase, string latestVersion)
        {
            return ShowUpdateButton(phase) && !string.IsNullOrEmpty(latestVersion);
        }

        // --- Issue #118: v6 タイトルでの明示的な「アップデートあり」表示 ---
        //
        // 旧 (v5) タイトルは UpdateNoticeUI のピルで「アップデートあり」を出して
        // いたが、v6 モードでは V6UiMode.HideLegacyUpdateNotice() がそのピルを隠す
        // (v6 側が更新情報を自前で描くため)。v6 タイトルには「更新して再起動」ボタン
        // (操作) しかなく、「更新があること」自体は示していなかった。
        // ここでは **状態** の表示 (「アップデートあり」) を、**操作**
        // (「更新して再起動」ボタン) と別 element として出す。

        /// <summary>
        /// 更新が利用できるときの通知ラベル。
        /// 旧 UpdateNoticePresenter.BuildText と同じ文言 (日本語フォントのアトラスに
        /// 既存の字形がある文言だけを使う)。
        /// </summary>
        public const string UpdateAvailableLabel = "アップデートあり";

        /// <summary>
        /// 「アップデートあり」通知 (状態表示) を出すか。
        /// - 更新が実際に利用できる (Available / Downloading / Ready / Applying) ときだけ
        /// - このセッションで dismiss (F4 / ×) されていないときだけ
        /// 「更新して再起動」ボタンの **操作** とは別の 표시。
        /// </summary>
        public static bool ShowUpdateAvailableNotice(UpdateFlowModel model)
        {
            if (model == null) { return false; }
            if (model.DismissedForSession) { return false; }
            return model.HasAvailableUpdate;
        }

        /// <summary>
        /// 通知の見出し (版の番号がわかれば併記)。
        /// 通知を出さない状態 (Idle / Checking) では空文字。
        /// </summary>
        public static string UpdateNoticeTitle(UpdateFlowPhase phase, string latestVersion, float progress)
        {
            string version = VersionLabel(latestVersion);
            switch (phase)
            {
                case UpdateFlowPhase.Available:
                    return version.Length > 0
                        ? UpdateAvailableLabel + " " + version
                        : UpdateAvailableLabel;
                case UpdateFlowPhase.Downloading:
                    return "アップデート" + (version.Length > 0 ? " " + version : "") + " を準備中... " +
                        Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f) + "%";
                case UpdateFlowPhase.Ready:
                    return version.Length > 0
                        ? "更新の準備ができました " + version
                        : "更新の準備ができました";
                case UpdateFlowPhase.Applying:
                    return "適用中... 終了すると更新されます";
                default:
                    return "";
            }
        }

        /// <summary>
        /// 通知の補足 (操作の説明)。見出しを出さない状態では空文字。
        /// 「押さなければ更新しません」を明示する (issue #118 の受入条件)。
        /// </summary>
        public static string UpdateNoticeHint(UpdateFlowPhase phase)
        {
            switch (phase)
            {
                case UpdateFlowPhase.Available:
                    return "[F3: 今すぐ更新] (押さなければ更新しません)";
                case UpdateFlowPhase.Downloading:
                    return "[完了したら F3 で適用]";
                case UpdateFlowPhase.Ready:
                    return "[F3: 適用] (アプリを終了して再起動します)";
                default:
                    return "";
            }
        }

        /// <summary>
        /// 「アップデートあり」バナー (Main ゾーン ローカル)。
        /// タブ列 (TabsPos) と同じ幅・左右位置にして、タイトル画面の中央軸に揃える。
        /// </summary>
        public static readonly Vector2 UpdateNoticePos = new Vector2(280f, 96f);
        public static readonly Vector2 UpdateNoticeSize = new Vector2(1120f, 56f);

        /// <summary>バナー左端のアクセント帯 (通知だと分かる目印) の幅。</summary>
        public const float UpdateNoticeAccentWidth = 6f;

        /// <summary>バナー内の左右余白。</summary>
        public const float UpdateNoticePadX = 24f;

        /// <summary>見出しと補足の間の空き。</summary>
        public const float UpdateNoticeGap = 16f;

        /// <summary>見出しの fs (design 空間)。</summary>
        public const float UpdateNoticeTitleFontSize = 26f;

        /// <summary>補足の fs (design 空間)。</summary>
        public const float UpdateNoticeHintFontSize = 22f;

        /// <summary>記録が無いときの文言 (タイトル右上の曲情報)。</summary>
        public const string NoRecordText = "まだ記録がありません";

        /// <summary>
        /// タイトル右上の「曲情報」に出す自己ベスト (記録のある曲のうち最高スコアの曲)。
        /// 実データ (GManager のカタログ + 保存済みベストスコア) だけから作る。
        /// </summary>
        public struct V6TitleRecord
        {
            /// <summary>記録があるか (0 点は未プレイなので記録なし)。</summary>
            public bool HasRecord;

            /// <summary>ActiveSongs の index (記録なしなら -1)。</summary>
            public int SongIndex;

            /// <summary>曲名 (実データの値)。</summary>
            public string Title;

            /// <summary>アーティスト (実データの値)。</summary>
            public string Artist;

            /// <summary>その曲のベストスコア (実データの値)。</summary>
            public int Score;
        }

        /// <summary>
        /// 曲ごとのベストスコアから、タイトルに出す自己ベスト (最高スコアの曲) を作る。
        /// scores は songs と同じ index 対応。記録が 1 件も無いときは HasRecord=false。
        /// </summary>
        public static V6TitleRecord BestRecord(SongData[] songs, int[] bestScores)
        {
            V6TitleRecord record = new V6TitleRecord();
            record.SongIndex = -1;

            int index = IndexOfBestRecord(bestScores);
            if (index < 0 || songs == null || index >= songs.Length)
            {
                return record;
            }

            SongData song = songs[index];
            if (song == null)
            {
                return record;
            }

            record.HasRecord = true;
            record.SongIndex = index;
            record.Title = song.title ?? "";
            record.Artist = song.musician ?? "";
            record.Score = bestScores[index];
            return record;
        }

        /// <summary>
        /// スコアの配列から、記録のある曲 (0 点は未プレイとして無視) のうち
        /// 最高スコアの index を返す。記録が 1 件も無ければ -1。
        /// 同点はカタログ順 (先頭) を保つ。
        /// </summary>
        public static int IndexOfBestRecord(int[] bestScores)
        {
            if (bestScores == null)
            {
                return -1;
            }

            int bestIndex = -1;
            int bestScore = 0;
            for (int i = 0; i < bestScores.Length; i++)
            {
                int score = bestScores[i];
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        /// <summary>スコアの表示文字列 (3 桁区切り)。0 以下 (未プレイ) は空文字。</summary>
        public static string ScoreText(int score)
        {
            if (score <= 0)
            {
                return "";
            }

            return score.ToString("#,0", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// タブ選択の移動 (端で折り返す)。count が 0 以下なら -1。
        /// 現在の選択が範囲外なら先頭から数える。
        /// </summary>
        public static int MoveIndex(int current, int delta, int count)
        {
            if (count <= 0)
            {
                return -1;
            }

            int index = current;
            if (index < 0 || index >= count)
            {
                index = 0;
            }

            index = (index + delta) % count;
            if (index < 0)
            {
                index += count;
            }

            return index;
        }
    }
}
