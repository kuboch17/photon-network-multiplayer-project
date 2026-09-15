using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class sittingLogic : MonoBehaviour
{
    public Animator anim;

    public float delay = 3f;
    
    // Start is called before the first frame update
    void Start()
    {
        InnerLoop();
    }

    // Update is called once per frame
    void InnerLoop()
    {
        StartCoroutine(OuterLoop());

        anim.SetInteger("int", Random.Range(0, 4));
    }

    IEnumerator OuterLoop()
    {
        yield return new WaitForSeconds(delay);

        delay = Random.Range(1f, 10f);
        InnerLoop();
    }
}
