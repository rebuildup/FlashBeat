using UnityEngine;
using FlashBeat.Persistence;

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

    /// <summary>
    /// Write the best scores to the save file (issue #189).
    ///
    /// The scores are keyed by a stable song id and a chart version, so
    /// adding or reordering a song never moves the record of another one.
    ///
    /// The result screen writes into the legacy array (<c>GManager.Hiscore</c>,
    /// owned by another issue), so the legacy view is submitted into the
    /// stable store **before** it is written. The legacy `"Hiscore"` key is
    /// not written any more: it is only read, once, by the migration.
    /// </summary>
    void SaveHiscore()
    {
        ScorePersistence.SubmitLegacyView();
        ScorePersistence.Save();
    }

    /// <summary>
    /// Read the best scores from the save file (issue #189).
    ///
    /// The legacy index based data is migrated the first time, and only for
    /// the songs whose built-in slot is known. Afterwards the legacy array is
    /// filled from the stable store, so the screens show the record of the
    /// song they display.
    /// </summary>
    void LoadHiscore()
    {
        ScorePersistence.Load();
        ScorePersistence.SyncLegacyView();
    }

    public void ResetHiscore()
    {
        // ベストスコアの全記録を消す (新形式と旧形式の両方)
        ScorePersistence.ResetAll();
        for (int i = 0; i < GManager.Hiscore.Length; i++)
        {
            GManager.Hiscore[i] = 0;
        }
        Debug.Log("kuriakanryou");
    }
}
