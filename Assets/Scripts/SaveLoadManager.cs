using UnityEngine;
using System.Linq;

public class SaveLoadManager : MonoBehaviour
{
    // Hiscore配列

    void Start()
    {
        // シーン名によってロードとセーブを切り替える
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "SelectScene")
        {
            LoadHiscore();
            Debug.Log("ro-dokanryou");
        }
        else if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "ResultScene")
        {
            SaveHiscore();
            Debug.Log("se-bukanryou");
        }
    }

    void SaveHiscore()
    {
        // 配列をカンマ区切りの文字列に変換
        string hiscoreString = string.Join(",", GManager.Hiscore.Select(p => p.ToString()).ToArray());

        // PlayerPrefsに保存
        PlayerPrefs.SetString("Hiscore", hiscoreString);
        PlayerPrefs.Save();
    }

    void LoadHiscore()
    {
        // PlayerPrefsから読み込み
        string hiscoreString = PlayerPrefs.GetString("Hiscore", "");

        // 文字列が空でない場合のみ配列に戻す
        if (!string.IsNullOrEmpty(hiscoreString))
        {
            GManager.Hiscore = hiscoreString.Split(',').Select(p => int.Parse(p)).ToArray();
        }
        else
        {
            // 文字列が空の場合は、Hiscoreを全て0の配列に設定
            GManager.Hiscore = new int[40]; // ここでは配列の長さを10としていますが、適切な長さに変更してください
        }
    }
    public void ResetHiscore()
    {
        // Hiscore配列の全ての要素を0に設定
        for (int i = 0; i < GManager.Hiscore.Length; i++)
        {
            GManager.Hiscore[i] = 0;
        }

        // 更新したHiscore配列を保存
        SaveHiscore();
        Debug.Log("kuriakanryou");
    }
}