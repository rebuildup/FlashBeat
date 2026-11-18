using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FlashBeat.Gameplay;

public class BGFlash : MonoBehaviour
{
    private float BPMCount;
    [SerializeField] private float lightSpeed = 3;

    [SerializeField] public GameObject Flash;
    

    private Renderer rend;
    private float Beat;

    private float alfa = 0;

    void Start()
    {

        rend = GetComponent<Renderer>();
        // Issue #183: 選択中の行ではなく、プレイセッションで固定した曲の BPM を使う。
        SongData sessionSong = PlaySessionSnapshot.ResolveSong(GManager.songID);
        Beat = sessionSong != null ? (60f / sessionSong.bpm) : float.MaxValue;
        Beat *= BeatIntervalMultiplier(GManager.FlashBG);
        BPMCount = (float)(GManager.FlashT/100);

    }

    /// <summary>
    /// FlashBG 設定値から拍間隔の倍率を返す (60/BPM に掛ける係数:
    /// 1→×8, 2→×4, 4→×2, 8・その他→×1)。
    /// Option 画面のゲームプレビュー (V6OptionPreviewRig) も同じ写像を
    /// 使うため static にしている (Issue #695)。
    /// </summary>
    public static float BeatIntervalMultiplier(int flashBG)
    {
        switch (flashBG)
        {
            case 1: return 8f;
            case 2: return 4f;
            case 4: return 2f;
            default: return 1f;
        }
    }


    void Update()
    {
        if (GManager.played)
        {
            // Issue #580: ポーズ中は拍フラッシュを発火させず、点灯中の α を消す。
            // BPMCount (壁時計) も進めないので、再開後はポーズ前の拍位相から続く。
            if (PauseGate.IsPaused)
            {
                alfa = 0f;
                if (!(rend.material.color.a <= 0))
                {
                    rend.material.color = new Color(rend.material.color.r, rend.material.color.g, rend.material.color.b, 0f);
                }
                return;
            }

            if (!(rend.material.color.a <= 0))
            {
                rend.material.color = new Color(rend.material.color.r, rend.material.color.g, rend.material.color.b, alfa);
            }
            if (BPMCount >= Beat)
            {
                colorChange();
                BPMCount = 0;
            }
            else
            {
                BPMCount += Time.deltaTime;
            }


            alfa -= lightSpeed * Time.deltaTime;
        }
        
    }

    void colorChange()
    {
        alfa = 0.3f;
        rend.material.color = new Color(rend.material.color.r, rend.material.color.g, rend.material.color.b, alfa);
    }
}
