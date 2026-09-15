using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class ball : MonoBehaviour
{
    public Rigidbody rb;
    public GameObject target0;
    public GameObject target1;

    public float Tthrust;
    public float yThrust;
    float th;
    bool trigger = false;

    public NavMeshAgent agent;
    public Vector3 locMin;
    public Vector3 locMax;

    public float stopMagnitude;
    public bool state;

    public GameObject particle;

    void Start()
    {
        th = Tthrust;

        SetRandomDes();
    }

    void Update()
    {
        if (state)
        {
            if (rb.velocity.magnitude < stopMagnitude)
            {
                SetRandomDes();
                print("yeee");
                state = false;
            }
            
            
        }
    }

    void SetRandomDes()
    {
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        agent.enabled = true;
        transform.Rotate(0, 180, 0, 0);
        //state = false;

        Vector3 location = new Vector3(Random.Range(locMin.x, locMax.x), 0, Random.Range(locMin.z, locMax.z));
        agent.SetDestination(location);
    }
    
    // Start is called before the first frame update
    public IEnumerator destroyTrail()
    {
        this.gameObject.GetComponent<TrailRenderer>().enabled = false;

        yield return new WaitForSeconds(0.5f);

        this.gameObject.GetComponent<TrailRenderer>().enabled = true;
    }

    public void Shoot(int target)
    {
        print("shot");

        Instantiate(particle, transform.position, Quaternion.identity);

        state = false;
        StartCoroutine(cooldown());

        rb.constraints = RigidbodyConstraints.None;
        agent.enabled = false;

        if (target == 0)
        {
            //Stop Moving/Translating
            rb.velocity = Vector3.zero;

            //Stop rotating
            rb.angularVelocity = Vector3.zero;

            Vector3 directionVector = (target0.transform.position - this.transform.position).normalized;
            rb.AddForce(new Vector3(directionVector.x, yThrust, directionVector.z) * Tthrust);
        }

        if (target == 1)
        {
            //Stop Moving/Translating
            rb.velocity = Vector3.zero;

            //Stop rotating
            rb.angularVelocity = Vector3.zero;

            Vector3 directionVector = (target1.transform.position - this.transform.position).normalized;
            rb.AddForce(new Vector3(directionVector.x, yThrust, directionVector.z) * Tthrust);
        }
    }

    IEnumerator cooldown()
    {        
        yield return new WaitForSeconds(1f);

        state = true;
    }
}
