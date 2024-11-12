using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
        Beat = (60/(float)GManager.SBPM[GManager.songID]);
        switch (GManager.FlashBG)
        {
            case 1:
                Beat = Beat * 8;
                break;
            case 2:
                Beat = Beat * 4;
                break;
            case 4:
                Beat = Beat * 2;
                break;
            case 8:
                //‰½‚à‚µ‚È‚¢
                break;
            default:
                //‰½‚à‚µ‚È‚¢
                break;
        }
        BPMCount = (float)(GManager.FlashT/100);
        
    }


    void Update()
    {
        if (GManager.played)
        {
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
