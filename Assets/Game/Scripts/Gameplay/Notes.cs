using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Notes : MonoBehaviour
{
    
    void Update()
    {
        if (KeyActions.IsDown(KeyActions.KeyAction.Confirm))
        {
            //start = true;
        }

        if (GManager.Start)
        {
            transform.position -= transform.forward * Time.deltaTime * GManager.noteSpeed;
        }
        
    }

}
