using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class spearIdle : MonoBehaviour
{
    public float _InitialVelocity;
    public float _Angle;

    void Awake()
    {
        float angle = _Angle * Mathf.Deg2Rad;
        StartCoroutine(Move(_InitialVelocity, angle));
    }
    
    // Start is called before the first frame update
    //void Awake()
    //{
    //    rb.AddForce(new Vector3(-transform.forward.x * amountOfForce, 10, -transform.forward.z * amountOfForce) , ForceMode.Impulse);
    //}

    IEnumerator Move(float v0,float angle)
    {
        float t = 0;
        while (t < 100)
        {
            float x = v0 * t * Mathf.Cos(angle);
            float y = v0 * t * Mathf.Sin(angle) - (1f / 2f) * -Physics.gravity.y * Mathf.Pow(t, 2);
            transform.position = new Vector3(x, y, 0);
            t += Time.deltaTime;
            yield return null;
        }
    }
}
