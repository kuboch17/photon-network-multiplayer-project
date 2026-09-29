using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class shadow : MonoBehaviour
{
    public GameObject ball;
    
    // Update is called once per frame
    void Update()
    {
        this.transform.position = new Vector3(ball.transform.position.x, this.transform.position.y, ball.transform.position.z);
    }
}
