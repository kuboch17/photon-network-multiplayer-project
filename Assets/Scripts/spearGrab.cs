using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class spearGrab : MonoBehaviour
{
    public GameObject des;
    public spearSlot slot;
    bool canBe;
    
    void Awake()
    {
        canBe = false;
        
    }
    
    // Start is called before the first frame update
    public void animend()
    {
        canBe = true;
    }

    // Update is called once per frame
    public void OnTriggerStay(Collider col)
    {
        if (col.gameObject.tag == "Player")
        {
            print("yoo");
            slot = col.gameObject.GetComponentInChildren<spearSlot>();

            if (Input.GetKeyDown(KeyCode.E) && canBe)
            {
                slot.isEquipped = true;
                Destroy(des);
            }           
        }
    }
}
