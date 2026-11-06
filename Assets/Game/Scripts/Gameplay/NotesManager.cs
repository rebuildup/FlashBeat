using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Data
{
    public string name;
    public int maxBlock;
    public int BPM;
    public int offset;
    public Note[] notes;
}

[Serializable]
public class Note
{
    public int type;
    public int num;
    public int block;
    public int LPB;
}

public class NotesManager : MonoBehaviour
{
    public int noteNum;//総ノーツ数
    private string songName;

    public List<int> LaneNum = new List<int>();
    public List<int> NoteType = new List<int>();
    public List<float> NotesTime = new List<float>();
    public List<GameObject> NotesObj = new List<GameObject>();

    //[SerializeField] public static float NotesSpeed = 1.0f;
    //[SerializeField] private float NotesSpeed = 1.0f;
    [SerializeField] GameObject noteObj;

    void OnEnable()
    {
        //NotesSpeed = GManager.noteSpeed;
        noteNum = 0;
        SongData song = GManager.ActiveSongs[GManager.songID];
        songName = song.title;
        Load(song);
    }

    private void Load(SongData song)
    {
        // Every run starts with an empty list: a retry must not keep the note
        // times of the previous attempt.
        GameSceneManager.SaveNotesTime.Clear();

        Data inputJson = null;
        if (song.isRemote && CloudChartHolder.TryGet(song.remoteId, song.chartVersion, out string held))
        {
            // Cloud Catalog (#17): the chart snapshot pinned at playlist start.
            inputJson = CloudCatalog.ParseChartResponse(held);
        }
        if (inputJson == null)
        {
            TextAsset asset = Resources.Load<TextAsset>(song.title);
            if (asset == null)
            {
                Debug.LogWarning($"[NotesManager] no chart for '{song.title}' - playing without notes");
                return;
            }
            inputJson = JsonUtility.FromJson<Data>(asset.text);
        }

        noteNum = inputJson.notes.Length;
        GManager.maxScore = noteNum * 5;
        
        for (int i = 0; i < inputJson.notes.Length; i++)
        {

            float kankaku = 60 / (inputJson.BPM * (float)inputJson.notes[i].LPB);
            float beatSec = kankaku * (float)inputJson.notes[i].LPB;
            float time = (beatSec * inputJson.notes[i].num / (float)inputJson.notes[i].LPB) + GManager.noteTiming * 0.01f;
            NotesTime.Add(time);
            LaneNum.Add(inputJson.notes[i].block);
            NoteType.Add(inputJson.notes[i].type);
            GameSceneManager.SaveNotesTime.Add(time);

            float z = NotesTime[i] * GManager.noteSpeed;
            NotesObj.Add(Instantiate(noteObj, new Vector3(inputJson.notes[i].block - 3.5f, 0.55f, z), Quaternion.identity));

        }
        for (int i = 0; i < 10; i++)
        {
            NotesTime.Add(10000.0f);
            LaneNum.Add(1);
            NoteType.Add(1);
            GameSceneManager.SaveNotesTime.Add(1000000f);
        }
        
    }

}
