using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class OpeningSceneManager : MonoBehaviour
{
    float Tcount = 0f;
    

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
