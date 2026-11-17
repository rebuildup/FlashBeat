using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using FlashBeat.Gameplay;

public class LaneFlash : MonoBehaviour
{

    [SerializeField] private float lightSpeed = 3;
    [FormerlySerializedAs("Lanesnum")] [SerializeField] private int laneNumber = 0;

    private Renderer rend;

    /// <summary>1-based lane number of this flash (test access).</summary>
    public int LaneNumber { get { return laneNumber; } }

    [FormerlySerializedAs("alfa")] private float alpha = 0;

    void Start()
    {

        rend = GetComponent<Renderer>();

    }


    void Update()
    {

        // Issue #580: ポーズ中は発光を消灯し、キー入力と減衰を止める。
        // 点灯中にポーズへ入った場合もここで α を 0 に戻す。
        // 再開 (PauseGate.Exit) 後は通常どおり発光・減衰する。
        if (PauseGate.IsPaused)
        {
            alpha = 0f;
            if (!(rend.material.color.a <= 0))
            {
                rend.material.color = new Color(rend.material.color.r, rend.material.color.g, rend.material.color.b, 0f);
            }
            return;
        }

        if(!(rend.material.color.a <= 0))
        {
            rend.material.color = new Color(rend.material.color.r,rend.material.color.g,rend.material.color.b,alpha);
        }

        // The flash follows the CUSTOM key bindings (KeyBindings), not a hard-coded layout.
        if (laneNumber >= 1 && laneNumber <= KeyBindings.LaneCount && KeyBindings.IsLaneKeyDown(laneNumber - 1))
        {
            colorChange();
        }

        alpha -= lightSpeed * Time.deltaTime;

    }

    void colorChange()
    {
        alpha = 0.3f;
        rend.material.color = new Color(rend.material.color.r, rend.material.color.g, rend.material.color.b, alpha);
    }
}
