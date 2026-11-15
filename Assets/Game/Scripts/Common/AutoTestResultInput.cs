using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlashBeat.UI.V6.Result;

/// <summary>
/// Result 登録モーダルの名前入力を実機で確認するための検証ハーネス
/// (Issue #384)。ResultScene を開き、段階表示の終わりに自動で開く
/// 登録モーダルへ実キー入力 (OS のキーイベント) を打ち込み、
/// TMP_InputField に文字が入るかを記録する。
///
/// 使い方:
///   FlashBeat -autotest resultinput -autotest-out <dir> [-autotest-seconds 30]
///
/// モーダルが開いたら [AutoTestResultInput] name="..." を一定間隔で
/// Player.log へ出す。オペレータはその間に実キー (osascript の
/// keystroke 等) を打ち、結果ファイルに観測した文字列を書く。
/// </summary>
public class AutoTestResultInput : MonoBehaviour
{
    private const string Tag = "[AutoTestResultInput]";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (V6ScreenAudit.GetArg("-autotest") != "resultinput") { return; }

        var go = new GameObject("AutoTestResultInput");
        DontDestroyOnLoad(go);
        go.AddComponent<AutoTestResultInput>();
    }

    private IEnumerator Start()
    {
        string outDir = V6ScreenAudit.GetArg("-autotest-out");
        if (string.IsNullOrEmpty(outDir)) { outDir = Application.persistentDataPath; }
        try { Directory.CreateDirectory(outDir); } catch (Exception) { }

        // -autotest-remote <songId>: リモート曲のプレイ済みセッションを偽装し、
        // 登録モーダルをオンライン方式で開く (テスト用 API 向けの検証)。
        // 判定数・スコアは -autotest-perfect/-great/-bad/-miss/-combo/-score で渡す
        // (既定は全て 0 = サーバが 422 で拒否する失敗系の入力)。
        string remoteId = V6ScreenAudit.GetArg("-autotest-remote");
        if (!string.IsNullOrEmpty(remoteId))
        {
            SetupRemoteSession(remoteId);
        }

        SceneManager.LoadScene("ResultScene");

        V6ResultView view = null;
        // シーン遷移直後のフレームは unscaledDeltaTime がロード時間を含んで
        // 巨大になるため、壁時計ではなくフレーム数で待つ。
        int waitedFrames = 0;
        while (waitedFrames++ < 1200)
        {
            var driver = FindFirstObjectByType<V6ResultViewDriver>();
            view = driver != null ? driver.View : null;
            if (view != null && view.ModalOpenForAudit)
            {
                break;
            }

            yield return null;
        }

        if (view == null || !view.ModalOpenForAudit)
        {
            Debug.Log(Tag + " FAIL the register modal did not open");
            Finish(outDir, false, string.Empty);
            yield break;
        }

        Debug.Log(Tag + " modal open - type real keys now");

        float seconds = 30f;
        string raw = V6ScreenAudit.GetArg("-autotest-seconds");
        if (!string.IsNullOrEmpty(raw) && float.TryParse(raw, out float parsed))
        {
            seconds = parsed;
        }

        string last = null;
        float elapsed = 0f;
        float nextLog = 0f;
        while (elapsed < seconds)
        {
            // フレーム飛びで計測窓が飛ばないよう 1 フレーム分を丸める。
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            string text = view.NameInputTextForAudit ?? string.Empty;
            if (text != last)
            {
                last = text;
                Debug.Log(Tag + " name=\"" + text + "\"");
            }
            else if (elapsed >= nextLog)
            {
                nextLog = elapsed + 2f;
                Debug.Log(Tag + " name=\"" + text + "\" (unchanged)");
            }

            yield return null;
        }

        Debug.Log(Tag + " final name=\"" + last + "\"");
        Finish(outDir, true, last ?? string.Empty);
    }

    /// <summary>
    /// リモート曲をプレイしたことにするセッション固定 (Issue #384 の
    /// テスト環境検証用)。実際のプレイを通さずに ResultScene を開き、
    /// 登録モーダルをオンライン方式にする。曲メタデータはカタログの
    /// 実値 (who) を使うが、送信先は CloudCatalogConfig.json の baseUrl
    /// 次第なので、テストではローカル API を指すビルドで使うこと。
    /// 本番 API への誤送信を防ぐため、解決先が localhost 系でない場合は
    /// セッション固定を行わずローカル方式のままにする。
    /// </summary>
    /// <returns>リモートセッションを固定したら true。</returns>
    private static bool SetupRemoteSession(string remoteId)
    {
        string origin = RankingClient.Origin();
        if (origin.IndexOf("localhost", StringComparison.OrdinalIgnoreCase) < 0
            && origin.IndexOf("127.0.0.1", StringComparison.OrdinalIgnoreCase) < 0)
        {
            Debug.LogError(Tag + " -autotest-remote requires a test API origin " +
                "(localhost); resolved origin: " + origin);
            return false;
        }

        int chartVersion = IntArg("-autotest-chart-version", 1);
        int songId = GManager.songID;
        if (songId < 0 || songId >= GManager.ActiveSongs.Length)
        {
            songId = 1;
            GManager.songID = songId;
        }

        var song = new SongData(songId, "Who", "Azari", "8JXiXt0D6tw", 128, 5, 139, 68f)
        {
            isRemote = true,
            remoteId = remoteId,
            chartVersion = chartVersion,
            noteCount = 139,
        };
        PlaySessionSnapshot.Begin(song, chartVersion, null);

        GManager.perfect = IntArg("-autotest-perfect", 0);
        GManager.great = IntArg("-autotest-great", 0);
        GManager.bad = IntArg("-autotest-bad", 0);
        GManager.miss = IntArg("-autotest-miss", 0);
        GManager.maxCombo = IntArg("-autotest-combo", 0);
        GManager.score = IntArg("-autotest-score", 0);
        return true;
    }

    private static int IntArg(string name, int fallback)
    {
        string raw = V6ScreenAudit.GetArg(name);
        return !string.IsNullOrEmpty(raw) && int.TryParse(raw, out int value) ? value : fallback;
    }

    private void Finish(string outDir, bool modalOpened, string finalText)
    {
        try
        {
            File.WriteAllText(Path.Combine(outDir, "autotest-resultinput-result.txt"),
                "modalOpened=" + modalOpened + "\nname=\"" + finalText + "\"\n");
        }
        catch (Exception) { }

        Application.Quit();
    }
}
