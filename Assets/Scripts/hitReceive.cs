using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class hitReceive : MonoBehaviour
{
    public bool isHit;
    
    public Vector3 go;
    
    // Start is called before the first frame update
    public void hitt(float x,float y,float z)
    {
        //print("ye");
        go = new Vector3(x, y, z);
        isHit = true;
    }
}
