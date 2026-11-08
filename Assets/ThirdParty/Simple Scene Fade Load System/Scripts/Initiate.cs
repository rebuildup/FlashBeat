using UnityEngine;
using System;
using System.Collections;
using UnityEngine.UI;
public static class Initiate
{
    static bool areWeFading = false;

    /// <summary>
    /// シーン読み込みの開始/進捗/終了を受け取るフック。FlashBeat 側の
    /// LoadingOverlay が RuntimeInitializeOnLoadMethod で差し込む。
    /// SimpleFadeSystem アセンブリは FlashBeat を参照できない (循環参照になる)
    /// ため、デリゲートで逆方向に通知する。
    /// </summary>
    public static class LoadingFeedback
    {
        public static Action<string> Begin;
        public static Action<float> Progress;
        public static Action End;
    }

    //Create Fader object and assing the fade scripts and assign all the variables
    public static void Fade(string scene, Color col, float multiplier)
    {
        if (areWeFading)
        {
            Debug.Log("Already Fading");
            return;
        }

        GameObject init = new GameObject();
        init.name = "Fader";
        Canvas myCanvas = init.AddComponent<Canvas>();
        myCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        init.AddComponent<Fader>();
        init.AddComponent<CanvasGroup>();
        init.AddComponent<Image>();

        Fader scr = init.GetComponent<Fader>();
        scr.fadeDamp = multiplier;
        scr.fadeScene = scene;
        scr.fadeColor = col;
        scr.start = true;
        areWeFading = true;
        scr.InitiateFader();
        
    }

    public static void DoneFading() {
        areWeFading = false;
    }
}