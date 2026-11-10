using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using NoteEditor.DTO;
using NoteEditor.Model;
using NoteEditor.Presenter;
using NoteEditor.Presenter.FlashBeatSong;
using UnityEditor;
using UnityEngine;

// Issue #225 条件 7「Unity Editor 実機での曲切替 → 保存」の実測プローブ。
//
// Mac の Unity Editor (batchmode) で NoteEditor 本体の曲切替経路
// (FlashBeatSongLoader / SongSwitchCoordinator / SongSwitchPromptDialog /
//  ChartFileWriter / EditDataSerializer) を実行し、
// 「別曲の JSON を上書きしない」ことをファイルの実測値 (size / SHA-256) で記録する。
//
// 実行:
//   Unity -batchmode -quit -projectPath <project> \
//         -executeMethod NoteEditorSongSwitchProbe.Run -logFile <log>
//
// 出力:
//   docs/evidence/225-editor-song-switch/results.json
//
// 注意:
//   - テスト用の譜面 JSON 2 個 (Assets/Game/Resources/__probe_a.json /
//     __probe_b.json) を一時的に作るが、成否にかかわらず finally で必ず削除する。
//   - 既存の実曲 JSON には一切触れない (読み取るだけ)。
//
// 補足 (UniRx を参照せずに書いている理由):
//   FlashBeat.Editor.asmdef は UniRx を参照していないため、
//   EditData.Name / EditData.IsDirty (ReactiveProperty) と
//   EditNotesPresenter.RequestForAddNote (Subject) を直接触るとコンパイルできない。
//   共有の asmdef を変更せずに済ませるため、これらの値だけは反射で読み書きする。
//   ロジック (曲切替の orchestration) は本番のクラスをそのまま使っている。
public static class NoteEditorSongSwitchProbe
{
    const string ProbeAName = "__probe_a";
    const string ProbeBName = "__probe_b";

    // プローブ用の譜面 JSON。実在の曲文件名とは衝突しないプレフィックスを使う。
    const string ProbeAJson =
        "{\"name\":\"__probe_a\",\"maxBlock\":8,\"BPM\":120,\"offset\":0,\"videoId\":\"probe-a-video\"," +
        "\"notes\":[{\"LPB\":4,\"num\":4,\"block\":0,\"type\":1,\"notes\":[]}," +
        "{\"LPB\":4,\"num\":5,\"block\":0,\"type\":1,\"notes\":[]}]," +
        "\"lyrics\":{\"startTime\":[],\"furigana\":[],\"mondai\":[],\"romaji\":[],\"endTime\":[]}}";

    const string ProbeBJson =
        "{\"name\":\"__probe_b\",\"maxBlock\":8,\"BPM\":150,\"offset\":480,\"videoId\":\"probe-b-video\"," +
        "\"notes\":[{\"LPB\":4,\"num\":8,\"block\":0,\"type\":1,\"notes\":[]}]," +
        "\"lyrics\":{\"startTime\":[],\"furigana\":[],\"mondai\":[],\"romaji\":[],\"endTime\":[]}}";

    // プローブ内で追加するノーツ (曲 A の既存ノーツ num=4,5 とは別)。
    const int AddedNoteNum = 6;
    const int AddedNoteLpb = 4;
    const int AddedNoteBlock = 0;

    // 曲 B 側のノーツ (num=8)。曲 A の保存結果に混ざっていないことの確認に使う。
    const int SongBNoteNum = 8;

    [Serializable]
    public class FileState
    {
        public string path;
        public bool exists;
        public long size;
        public string sha256;
    }

    [Serializable]
    public class CaseReport
    {
        public string name;
        public bool pass;
        public string switchResult;
        public bool promptShown;
        public string promptMessage;
        public string editDataNameAfter;
        public int editDataNoteCountAfter;
        public bool isDirtyAfter;
        public bool aChanged;
        public bool bChanged;
        public FileState aBefore;
        public FileState aAfter;
        public FileState bBefore;
        public FileState bAfter;
        public string savedAName;
        public int savedANoteCount;
        public bool savedAContainsSongBNote;
        public string savedAContent;
        public string aAfterContent;
        public List<string> failures = new List<string>();
    }

    [Serializable]
    public class ProbeReport
    {
        public string unityVersion;
        public string projectRoot;
        public string resourcesDir;
        public string currentDirectoryBefore;
        public string currentDirectoryAfter;
        public string timestampUtc;
        public bool pass;
        public CaseReport[] cases;
        public string[] cleanup;
        public string summary;
        public string originalAContent;
        public string originalBContent;
    }

    static string projectRoot_;
    static string resourcesDirAbs_;
    static string pathA_;
    static string pathB_;
    static string originalA_;
    static string originalB_;

    public static void Run()
    {
        var report = new ProbeReport
        {
            unityVersion = Application.unityVersion,
            timestampUtc = DateTime.UtcNow.ToString("o"),
        };

        var cleanupNotes = new List<string>();
        var cases = new List<CaseReport>();

        projectRoot_ = Path.GetDirectoryName(Application.dataPath);
        report.projectRoot = projectRoot_;
        report.currentDirectoryBefore = Directory.GetCurrentDirectory();

        // 相対パス (ChartFileWriter.ResourcesDir) を確実にプロジェクトルート基準で
        // 解釈させるため、作業ディレクトリをプロジェクトルートに寄せる。
        try
        {
            Directory.SetCurrentDirectory(projectRoot_);
        }
        catch (Exception ex)
        {
            cleanupNotes.Add("SetCurrentDirectory failed: " + ex.Message);
        }
        report.currentDirectoryAfter = Directory.GetCurrentDirectory();

        // 本番の Resources 置き場 (ChartFileWriter が 1 箇所で持つ値)。
        resourcesDirAbs_ = Path.Combine(projectRoot_, ChartFileWriter.ResourcesDir);
        report.resourcesDir = resourcesDirAbs_;

        pathA_ = Path.Combine(resourcesDirAbs_, ProbeAName + ".json");
        pathB_ = Path.Combine(resourcesDirAbs_, ProbeBName + ".json");
        originalA_ = ProbeAJson;
        originalB_ = ProbeBJson;

        GameObject host = null;

        try
        {
            if (!Directory.Exists(resourcesDirAbs_))
                throw new Exception("Resources ディレクトリが見つかりません: " + resourcesDirAbs_);

            // プローブ用の譜面を作る (既存の実曲 JSON には触らない)。
            File.WriteAllText(pathA_, originalA_, Encoding.UTF8);
            File.WriteAllText(pathB_, originalB_, Encoding.UTF8);

            // 本番と同じ構成を再現する:
            //   SongSwitchPromptDialog (本番の ISongSwitchPrompt 実装) を先に付け、
            //   FlashBeatSongLoader (本番の入口) を付ける。
            //   ローダーは GetComponent<SongSwitchPromptDialog>() で同じ実体を使う。
            host = new GameObject("__FlashBeatSongSwitchProbe");
            host.AddComponent<SongSwitchPromptDialog>();
            var loader = host.AddComponent<FlashBeatSongLoader>();

            var coordinator = GetProductionCoordinator(loader);
            if (coordinator == null)
                throw new Exception("SongSwitchCoordinator を取得できませんでした");

            // ケース 0: 未保存変更が無い状態での曲切替 (ダイアログを出さない経路)。
            // FlashBeatSongLoader.OnFileSelected (公開入口) を実際に呼ぶ。
            cases.Add(RunNoDirtySwitchCase(loader, host));

            // ケース 1〜3: 未保存変更がある状態での曲切替 (保存 / 破棄 / キャンセル)。
            cases.Add(RunSwitchCase("cancel", SongSwitchChoice.Cancel, coordinator, host));
            cases.Add(RunSwitchCase("save", SongSwitchChoice.SaveAndSwitch, coordinator, host));
            cases.Add(RunSwitchCase("discard", SongSwitchChoice.DiscardAndSwitch, coordinator, host));

            foreach (var c in cases)
            {
                if (!c.pass)
                {
                    Debug.LogError("[NoteEditorSongSwitchProbe] FAILED case: " + c.name + " -> " + string.Join(" / ", c.failures.ToArray()));
                }
            }
        }
        catch (Exception ex)
        {
            report.summary = "probe aborted: " + ex;
            Debug.LogError("[NoteEditorSongSwitchProbe] " + ex);
        }
        finally
        {
            if (host != null)
                UnityEngine.Object.DestroyImmediate(host);

            // プローブ用ファイルを必ず削除する (成否にかかわらず)。
            cleanupNotes.AddRange(RemoveProbeFiles());
            try
            {
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                cleanupNotes.Add("AssetDatabase.Refresh failed: " + ex.Message);
            }

            report.cleanup = cleanupNotes.ToArray();
            report.cases = cases.ToArray();
            report.originalAContent = originalA_;
            report.originalBContent = originalB_;
            bool allPass = cases.Count == 4 && cases.TrueForAll(c => c.pass);
            report.pass = allPass && report.summary == null;
            if (report.summary == null)
            {
                int passCount = cases.FindAll(c => c.pass).Count;
                report.summary = string.Format("{0}/{1} cases passed", passCount, cases.Count);
            }

            WriteReport(report);
            Debug.Log("[NoteEditorSongSwitchProbe] " + report.summary);
        }

        if (!report.pass)
            throw new Exception("NoteEditorSongSwitchProbe failed: " + report.summary);
    }

    // 未保存変更が無い状態での切替 (公開入口 FlashBeatSongLoader.OnFileSelected を使う)。
    static CaseReport RunNoDirtySwitchCase(FlashBeatSongLoader loader, GameObject host)
    {
        var o = new CaseReport { name = "no-dirty-switch" };

        ResetProbeFiles();

        string error;
        if (!loader.LoadSongFile(pathA_, out error))
        {
            o.failures.Add("曲 A の読み込みに失敗: " + error);
            return o;
        }

        o.aBefore = State(pathA_);
        o.bBefore = State(pathB_);

        if (GetIsDirty())
            o.failures.Add("読み込み直後に dirty が立っている");

        // 本番の公開入口。dirty が無いのでダイアログは出ず、そのまま切り替わる。
        loader.OnFileSelected(pathB_);

        o.promptShown = GetDialog(host).HasPendingChoice;
        o.editDataNameAfter = GetEditDataName();
        o.editDataNoteCountAfter = EditData.Notes.Count;
        o.isDirtyAfter = GetIsDirty();
        o.aAfter = State(pathA_);
        o.bAfter = State(pathB_);

        o.aChanged = !Same(o.aBefore, o.aAfter);
        o.bChanged = !Same(o.bBefore, o.bAfter);

        if (o.promptShown)
            o.failures.Add("未保存変更が無いのにダイアログが表示された");
        if (o.editDataNameAfter != ProbeBName)
            o.failures.Add("曲 B に切り替わっていない (Name=" + o.editDataNameAfter + ")");
        if (o.bChanged)
            o.failures.Add("曲 B のファイルが書き換わっている");
        if (o.aChanged)
            o.failures.Add("曲 A のファイルが書き換わっている");

        o.pass = o.failures.Count == 0;
        return o;
    }

    // 未保存変更がある状態での切替。choice で「保存 / 破棄 / キャンセル」を選ぶ。
    static CaseReport RunSwitchCase(
        string caseName,
        SongSwitchChoice choice,
        SongSwitchCoordinator coordinator,
        GameObject host)
    {
        var o = new CaseReport { name = caseName };

        // 各ケースは同じ初期状態 (曲 A / 曲 B のオリジナル内容) から始める。
        ResetProbeFiles();

        string error;
        if (!LoadSongA(out error))
        {
            o.failures.Add("曲 A の読み込みに失敗: " + error);
            return o;
        }

        o.aBefore = State(pathA_);
        o.bBefore = State(pathB_);

        // 本番の編集経路でノーツを追加して dirty を立てる。
        MakeDirtyByAddingNote(o);

        // 本番の coordinator で切替を要求する。
        var result = coordinator.RequestSwitch(pathB_);
        o.switchResult = result.ToString();
        var dialog = GetDialog(host);
        o.promptShown = dialog.HasPendingChoice;
        o.promptMessage = ReadDialogMessage(dialog);

        if (result != SongSwitchResult.AwaitingChoice)
            o.failures.Add("期待した SongSwitchResult.AwaitingChoice ではない: " + result);
        if (!o.promptShown)
            o.failures.Add("未保存変更の警告 (ダイアログ) が表示されていない");
        if (string.IsNullOrEmpty(o.promptMessage))
            o.failures.Add("ダイアログのメッセージが空");

        // 本番ダイアログの選択注入点 (IMGUI のボタン押下と同じ Choose)。
        dialog.Choose(choice);

        o.editDataNameAfter = GetEditDataName();
        o.editDataNoteCountAfter = EditData.Notes.Count;
        o.isDirtyAfter = GetIsDirty();
        o.aAfter = State(pathA_);
        o.bAfter = State(pathB_);

        o.aChanged = !Same(o.aBefore, o.aAfter);
        o.bChanged = !Same(o.bBefore, o.bAfter);

        // 保存された曲 A の内容を検証する (保存ケースのみ意味がある)。
        var savedA = ReadChart(pathA_);
        if (savedA != null)
        {
            o.savedAName = savedA.name;
            o.savedANoteCount = savedA.notes != null ? savedA.notes.Count : 0;
            o.savedAContainsSongBNote = savedA.notes != null
                && savedA.notes.Any(n => n.num == SongBNoteNum);
        }

        o.aAfterContent = File.Exists(pathA_) ? File.ReadAllText(pathA_, Encoding.UTF8) : "";

        switch (choice)
        {
            case SongSwitchChoice.Cancel:
                if (o.editDataNameAfter != ProbeAName)
                    o.failures.Add("キャンセルなのに曲 A から切り替わっている (Name=" + o.editDataNameAfter + ")");
                if (o.editDataNoteCountAfter != 3)
                    o.failures.Add("キャンセルなのに曲 A のノーツが失われている (count=" + o.editDataNoteCountAfter + ")");
                if (!o.isDirtyAfter)
                    o.failures.Add("キャンセルなのに未保存変更のフラグが消えている");
                if (o.aChanged)
                    o.failures.Add("キャンセルなのに曲 A のファイルが書き換わっている");
                if (o.bChanged)
                    o.failures.Add("キャンセルなのに曲 B のファイルが書き換わっている");
                break;

            case SongSwitchChoice.SaveAndSwitch:
                if (o.editDataNameAfter != ProbeBName)
                    o.failures.Add("保存して切り替えなのに曲 B に切り替わっていない (Name=" + o.editDataNameAfter + ")");
                if (o.editDataNoteCountAfter != 1)
                    o.failures.Add("曲 B のノーツ数がおかしい (count=" + o.editDataNoteCountAfter + ")");
                if (o.isDirtyAfter)
                    o.failures.Add("保存したのに dirty が立っている");
                if (!o.aChanged)
                    o.failures.Add("保存したのに曲 A のファイルが変化していない");
                if (o.bChanged)
                    o.failures.Add("★ 曲 B のファイルが書き換わっている (別曲の JSON を上書きしている)");
                if (savedA == null)
                    o.failures.Add("保存された曲 A の JSON を読めなかった");
                else
                {
                    if (o.savedAName != ProbeAName)
                        o.failures.Add("保存された曲 A の name が違う: " + o.savedAName);
                    if (o.savedANoteCount != 3)
                        o.failures.Add("保存された曲 A のノーツ数が 3 ではない: " + o.savedANoteCount);
                    if (o.savedAContainsSongBNote)
                        o.failures.Add("★ 曲 A の保存結果に曲 B のノーツ (num=" + SongBNoteNum + ") が混ざっている");
                }
                break;

            case SongSwitchChoice.DiscardAndSwitch:
                if (o.editDataNameAfter != ProbeBName)
                    o.failures.Add("破棄して切り替えなのに曲 B に切り替わっていない (Name=" + o.editDataNameAfter + ")");
                if (o.editDataNoteCountAfter != 1)
                    o.failures.Add("曲 B のノーツ数がおかしい (count=" + o.editDataNoteCountAfter + ")");
                if (o.isDirtyAfter)
                    o.failures.Add("破棄したのに dirty が立っている");
                if (o.aChanged)
                    o.failures.Add("破棄なのに曲 A のファイルが書き換わっている");
                if (o.bChanged)
                    o.failures.Add("★ 曲 B のファイルが書き換わっている (別曲の JSON を上書きしている)");
                break;
        }

        o.pass = o.failures.Count == 0;
        return o;
    }

    // 本番の公開入口と同じ経路で曲 A を読み込む。
    static bool LoadSongA(out string error)
    {
        var host = new GameObject("__FlashBeatSongSwitchProbeLoader");
        try
        {
            var loader = host.AddComponent<FlashBeatSongLoader>();
            return loader.LoadSongFile(pathA_, out error);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    // 本番の編集経路 (EditNotesPresenter) でノーツを追加し、dirty を立てる。
    static void MakeDirtyByAddingNote(CaseReport o)
    {
        var presenter = EditNotesPresenter.Instance;
        SubscribeDirtyTracking(presenter);

        var note = new NoteEditor.Notes.Note(
            new NoteEditor.Notes.NotePosition(AddedNoteLpb, AddedNoteNum, AddedNoteBlock),
            NoteEditor.Notes.NoteTypes.Single);

        // 本番の dirty 追跡 (NoteEditDirtyTracker) を通す。
        RequestAddNote(presenter, note);

        // ノーツ自体を譜面に入れる (本番の EditNotesPresenter.AddNote)。
        presenter.AddNote(note);

        if (!GetIsDirty())
            o.failures.Add("ノーツ追加で dirty が立っていない");
        if (!EditData.Notes.Any(n => n.Value.note.position.num == AddedNoteNum))
            o.failures.Add("追加したノーツが譜面に反映されていない");
    }

    // ローダーが本番で作る SongSwitchCoordinator を取得する
    // (private BuildCoordinator / private coordinator_ を反射で読む)。
    static SongSwitchCoordinator GetProductionCoordinator(FlashBeatSongLoader loader)
    {
        var field = typeof(FlashBeatSongLoader).GetField("coordinator_", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
        {
            var existing = field.GetValue(loader) as SongSwitchCoordinator;
            if (existing != null)
                return existing;
        }

        var method = typeof(FlashBeatSongLoader).GetMethod("BuildCoordinator", BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null)
            return null;

        return method.Invoke(loader, null) as SongSwitchCoordinator;
    }

    static SongSwitchPromptDialog GetDialog(GameObject host)
    {
        return host.GetComponent<SongSwitchPromptDialog>();
    }

    static string ReadDialogMessage(SongSwitchPromptDialog dialog)
    {
        var field = typeof(SongSwitchPromptDialog).GetField("message_", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
            return "";
        return field.GetValue(dialog) as string ?? "";
    }

    // ---- UniRx を参照せずに EditData / EditNotesPresenter を触るための反射ヘルパ ----

    static object GetReactiveValue(Type owner, string propertyName)
    {
        var prop = owner.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
        if (prop == null)
            throw new Exception("プロパティが見つかりません: " + owner.Name + "." + propertyName);

        var reactive = prop.GetValue(null);
        if (reactive == null)
            throw new Exception("プロパティが null です: " + owner.Name + "." + propertyName);

        var valueProp = reactive.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
        if (valueProp == null)
            throw new Exception("Value プロパティが見つかりません: " + reactive.GetType().FullName);

        return valueProp.GetValue(reactive);
    }

    static string GetEditDataName()
    {
        return (string)GetReactiveValue(typeof(EditData), "Name");
    }

    static bool GetIsDirty()
    {
        return (bool)GetReactiveValue(typeof(EditData), "IsDirty");
    }

    static void SubscribeDirtyTracking(EditNotesPresenter presenter)
    {
        // NoteEditDirtyTracker の戻り値型をコンパイル時に参照しないよう反射で呼ぶ。
        var method = typeof(EditNotesPresenter).GetMethod("SubscribeDirtyTracking", BindingFlags.Public | BindingFlags.Instance);
        if (method == null)
            throw new Exception("SubscribeDirtyTracking が見つかりません");

        method.Invoke(presenter, null);
    }

    static void RequestAddNote(EditNotesPresenter presenter, NoteEditor.Notes.Note note)
    {
        var prop = typeof(EditNotesPresenter).GetProperty("RequestForAddNote", BindingFlags.Public | BindingFlags.Instance);
        if (prop == null)
            throw new Exception("RequestForAddNote が見つかりません");

        var subject = prop.GetValue(presenter);
        if (subject == null)
            throw new Exception("RequestForAddNote が null です");

        var onNext = subject.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "OnNext" && m.GetParameters().Length == 1);
        if (onNext == null)
            throw new Exception("OnNext が見つかりません: " + subject.GetType().FullName);

        onNext.Invoke(subject, new object[] { note });
    }

    static void ResetProbeFiles()
    {
        File.WriteAllText(pathA_, originalA_, Encoding.UTF8);
        File.WriteAllText(pathB_, originalB_, Encoding.UTF8);
    }

    static MusicDTO.EditData ReadChart(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            return JsonUtility.FromJson<MusicDTO.EditData>(File.ReadAllText(path, Encoding.UTF8));
        }
        catch (Exception)
        {
            return null;
        }
    }

    static FileState State(string path)
    {
        var s = new FileState { path = path };
        if (!File.Exists(path))
        {
            s.exists = false;
            return s;
        }

        s.exists = true;
        s.size = new FileInfo(path).Length;
        s.sha256 = Sha256(path);
        return s;
    }

    static bool Same(FileState a, FileState b)
    {
        if (a == null || b == null)
            return false;
        return a.exists == b.exists && a.size == b.size && a.sha256 == b.sha256;
    }

    static string Sha256(string path)
    {
        using (var sha = SHA256.Create())
        using (var stream = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    // プローブ用ファイルを削除する (.meta ごと)。戻り値はクリーンアップ記録。
    static IEnumerable<string> RemoveProbeFiles()
    {
        var notes = new List<string>();

        foreach (var path in new[] { pathA_, pathB_ })
        {
            if (string.IsNullOrEmpty(path))
                continue;

            foreach (var target in new[] { path, path + ".meta" })
            {
                try
                {
                    if (File.Exists(target))
                    {
                        File.Delete(target);
                        notes.Add("deleted " + target);
                    }
                }
                catch (Exception ex)
                {
                    notes.Add("failed to delete " + target + ": " + ex.Message);
                }
            }
        }

        // 残存チェック (消えていない項目は失敗として記録する)。
        foreach (var path in new[] { pathA_, pathB_ })
        {
            if (!string.IsNullOrEmpty(path) && (File.Exists(path) || File.Exists(path + ".meta")))
                notes.Add("RESIDUAL FILE REMAINS: " + path);
        }

        return notes;
    }

    static void WriteReport(ProbeReport report)
    {
        try
        {
            var dir = Path.Combine(projectRoot_, "docs", "evidence", "225-editor-song-switch");
            Directory.CreateDirectory(dir);
            var outPath = Path.Combine(dir, "results.json");
            File.WriteAllText(outPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            Debug.Log("[NoteEditorSongSwitchProbe] report written: " + outPath);
        }
        catch (Exception ex)
        {
            Debug.LogError("[NoteEditorSongSwitchProbe] failed to write report: " + ex);
        }
    }
}
