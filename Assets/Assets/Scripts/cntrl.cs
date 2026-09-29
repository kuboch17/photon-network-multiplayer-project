using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class cntrl : MonoBehaviour
{
    public float speed;
    public float strafeSpeed;
    public float jF;

    public ConfigurableJoint hipsJ;
    public Animator anim;

    private bool w;
    private bool a;
    private bool s;
    private bool d;

    public Rigidbody hips;
    
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.S))
        {
            hips.AddForce(hips.transform.forward * speed);
            s = true;
            this.transform.rotation = new Quaternion(0, 0.5f, 0, 0);
        }
        else if (Input.GetKeyUp(KeyCode.S)) 
        {
            s = false;
        }

        if (Input.GetKey(KeyCode.W))
        {
            hips.AddForce(hips.transform.forward * speed);
            w = true;
            this.transform.rotation = new Quaternion(0, 0, 0, 0);
        }
        else if (Input.GetKeyUp(KeyCode.W))
        {
            w = false;
        }

        if (Input.GetKey(KeyCode.D))
        {
            this.transform.rotation = new Quaternion(0, 90, 0, 1);
            hips.AddForce(hips.transform.forward * speed);
            d = true;
            
        }
        else if (Input.GetKeyUp(KeyCode.D))
        {
            d = false;
        }

        if (Input.GetKey(KeyCode.A))
        {
            this.transform.rotation = new Quaternion(0, -90, 0, 1);
            hips.AddForce(hips.transform.forward * speed);
            a = true;
            
        }
        else if (Input.GetKeyUp(KeyCode.A))
        {
            a = false;
        }


        if (w == false & a == false & s == false & d == false)
        {
            anim.SetBool("Running", false);
        }
        else
        {
            anim.SetBool("Running", true);
        }
    }
}
