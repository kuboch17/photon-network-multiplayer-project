using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class tht : MonoBehaviour
{
    public CopyLimb cl;
    public ConfigurableJoint joint;
    public GameObject part;
    public float x;
    public float y;
    public float z;

    public float delBet;

    public float x1;
    public float y1;
    public float z1;

    public bool for1;

    public void Start()
    {
        if (for1)
        {
            part.SetActive(false);
        }
    }

    // Start is called before the first frame update
    public void Init()
    {
        cl.enabled = false;
        joint.targetRotation = new Quaternion(x, y, z, 1);
        
    }

    public void disInit()
    {
        StartCoroutine(pipik());

    }

    // Update is called once per frame
    IEnumerator pipik()
    {
        joint.targetRotation = new Quaternion(x1, y1, z1, 1);
        
        if (for1)
        {
            part.SetActive(true);
        }

        yield return new WaitForSeconds(delBet);

        if (for1)
        {
            part.SetActive(false);
        }
        cl.enabled = true;
    }
}
