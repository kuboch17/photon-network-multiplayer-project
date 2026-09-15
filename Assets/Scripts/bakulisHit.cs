using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class bakulisHit : MonoBehaviour
{
    public bool isP1;
    public bool isP2;

    // Start is called before the first frame update
    void OnTriggerEnter(Collider col)
    {
        if (col.gameObject.tag == "Player2" && !isP2)
        {
            //print("yes");
            col.gameObject.GetComponent<hitReceive>().hitt(this.gameObject.transform.position.x, this.gameObject.transform.position.y, this.gameObject.transform.position.z);
        }

        if (col.gameObject.tag == "Player" && !isP1)
        {
            print("yes");
            col.gameObject.GetComponent<hitReceive>().hitt(this.gameObject.transform.position.x, this.gameObject.transform.position.y, this.gameObject.transform.position.z);
        }
    }
}
