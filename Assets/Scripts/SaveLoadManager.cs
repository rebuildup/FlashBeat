using UnityEngine;
using System.Linq;

public class SaveLoadManager : MonoBehaviour
{
    // Hiscore�z��

    void Start()
    {
        // �V�[�����ɂ���ă��[�h�ƃZ�[�u��؂�ւ���
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
        // �z����J���}��؂�̕�����ɕϊ�
        string hiscoreString = string.Join(",", GManager.Hiscore.Select(p => p.ToString()).ToArray());

        // PlayerPrefs�ɕۑ�
        PlayerPrefs.SetString("Hiscore", hiscoreString);
        PlayerPrefs.Save();
    }

    void LoadHiscore()
    {
        // PlayerPrefs����ǂݍ���
        string hiscoreString = PlayerPrefs.GetString("Hiscore", "");

        // �����񂪋�łȂ��ꍇ�̂ݔz��ɖ߂�
        if (!string.IsNullOrEmpty(hiscoreString))
        {
            GManager.Hiscore = hiscoreString.Split(',').Select(p => int.Parse(p)).ToArray();
        }
        else
        {
            // �����񂪋�̏ꍇ�́AHiscore��S��0�̔z��ɐݒ�
            GManager.Hiscore = new int[40]; // �����ł͔z��̒�����10�Ƃ��Ă��܂����A�K�؂Ȓ����ɕύX���Ă�������
        }
    }
    public void ResetHiscore()
    {
        // Hiscore�z��̑S�Ă̗v�f��0�ɐݒ�
        for (int i = 0; i < GManager.Hiscore.Length; i++)
        {
            GManager.Hiscore[i] = 0;
        }

        // �X�V����Hiscore�z���ۑ�
        SaveHiscore();
        Debug.Log("kuriakanryou");
    }
}