using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class LaneFlash : MonoBehaviour
{

    [SerializeField] private float lightSpeed = 3;
    [FormerlySerializedAs("Lanesnum")] [SerializeField] private int laneNumber = 0;

    private Renderer rend;

    [FormerlySerializedAs("alfa")] private float alpha = 0;

    void Start()
    {

        rend = GetComponent<Renderer>();

    }


    void Update()
    {

        if(!(rend.material.color.a <= 0))
        {
            rend.material.color = new Color(rend.material.color.r,rend.material.color.g,rend.material.color.b,alpha);
        }

        if(laneNumber == 1)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)||Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.Z))
            {
                colorChange();
            }
        }
        if (laneNumber == 2)
        {
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.X))
            {
                colorChange();
            }
        }
        if (laneNumber == 3)
        {
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.C))
            {
                colorChange();
            }
        }
        if (laneNumber == 4)
        {
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.T) || Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.G) || Input.GetKeyDown(KeyCode.V) || Input.GetKeyDown(KeyCode.B))
            {
                colorChange();
            }
        }
        if (laneNumber == 5)
        {
            if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Y) || Input.GetKeyDown(KeyCode.U) || Input.GetKeyDown(KeyCode.H) || Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.M))
            {
                colorChange();
            }
        }
        if (laneNumber == 6)
        {
            if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.Less)||Input.GetKeyDown(KeyCode.Comma))
            {
                colorChange();
            }
        }
        if (laneNumber == 7)
        {
            if (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.O) || Input.GetKeyDown(KeyCode.L) || Input.GetKeyDown(KeyCode.Greater)||Input.GetKeyDown(KeyCode.Period))
            {
                colorChange();
            }
        }
        if (laneNumber == 8)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.Caret) || Input.GetKeyDown(KeyCode.Backslash) || Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.At) || Input.GetKeyDown(KeyCode.LeftBracket) || Input.GetKeyDown(KeyCode.Semicolon) || Input.GetKeyDown(KeyCode.Colon) || Input.GetKeyDown(KeyCode.RightBracket) || Input.GetKeyDown(KeyCode.Slash) || Input.GetKeyDown(KeyCode.Underscore)||Input.GetKeyDown(KeyCode.Minus)||Input.GetKeyDown(KeyCode.Asterisk)||Input.GetKeyDown(KeyCode.BackQuote))
            {
                colorChange();
            }
        }

        alpha -= lightSpeed * Time.deltaTime;

    }

    void colorChange()
    {
        alpha = 0.3f;
        rend.material.color = new Color(rend.material.color.r, rend.material.color.g, rend.material.color.b, alpha);
    }
}
