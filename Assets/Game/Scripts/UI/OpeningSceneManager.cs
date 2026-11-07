using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class OpeningSceneManager : MonoBehaviour
{
    float Tcount = 0f;

    void Start()
    {
        // v5 UI (Issue #120): 背景 + エッジ装飾 + ロゴをコードで構築。
        FlashBeat.UI.V5.OpeningView.Build();
    }

    void Update()
    {
        Tcount += Time.deltaTime;

        if (Tcount > 6.031f)
        {
            //SceneManager.LoadScene("TitleScene");
            Initiate.Fade("TitleScene", Color.black, 1.0f);
        }
    }

    
}
