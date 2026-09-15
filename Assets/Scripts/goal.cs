using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class goal : MonoBehaviour
{
    public GameObject exp;
    public float respawnDelay;
    public score sc;

    public int goalNr;

    private GameObject ballSpawned;

    public Vector3 spawnLoc;

    GameObject chick;

    void OnTriggerEnter(Collider col)
    {
        if (col.gameObject.tag == "ball")
        {
            Instantiate(exp, col.gameObject.transform.position, Quaternion.identity);
            ballSpawned = col.gameObject;
            sc.AddScore(goalNr);

            chick = col.gameObject;
            chick.GetComponent<Rigidbody>().velocity = Vector3.zero;
            chick.GetComponent<Rigidbody>().angularVelocity = Vector3.zero;

            chick.SetActive(false);

            StartCoroutine(delay());
        }
    }

    IEnumerator delay()
    {
        yield return new WaitForSeconds(respawnDelay);

        chick.SetActive(true);

        StartCoroutine(ballSpawned.GetComponent<ball>().destroyTrail());
        ballSpawned.transform.position = spawnLoc;
        print("ballReset");
    }
}
