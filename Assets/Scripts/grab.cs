using UnityEngine;

/// <summary>
/// Legacy collision probe used by RPGCharacterController while the character is airborne.
/// Kept with the original lowercase name so existing scenes and scripts remain compatible.
/// </summary>
public class grab : MonoBehaviour
{
    public bool collided { get; private set; }

    private void OnTriggerEnter(Collider other)
    {
        collided = true;
    }

    private void OnTriggerStay(Collider other)
    {
        collided = true;
    }

    private void OnTriggerExit(Collider other)
    {
        collided = false;
    }

    private void OnDisable()
    {
        collided = false;
    }
}
