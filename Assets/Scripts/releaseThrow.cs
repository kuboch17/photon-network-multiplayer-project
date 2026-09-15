using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class releaseThrow : MonoBehaviour
{
    public spearSlot sP;
    
    // Start is called before the first frame update
    void release()
    {
        sP.Release();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
