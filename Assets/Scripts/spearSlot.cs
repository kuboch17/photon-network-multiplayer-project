using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class spearSlot : MonoBehaviour
{
    public GameObject spear;
    public GameObject releaseSpear;
    public bool isEquipped;
    public float offset;

    // Update is called once per frame
    void Update()
    {
        if (isEquipped == true)
        {
            spear.SetActive(true);
        }
        else if (isEquipped == false)
        {
            spear.SetActive(false);
        }
    }

    public void Release()
    {
        if (isEquipped)
        {
            isEquipped = false;
            Instantiate(releaseSpear, new Vector3(this.transform.position.x, 1.98f, this.transform.position.z), Quaternion.Euler(new Vector3(0, this.transform.eulerAngles.y + offset, 0)));
            //print(this.transform.eulerAngles);
        }
    }
}
