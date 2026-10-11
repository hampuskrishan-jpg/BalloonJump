using UnityEngine;

// Snurrar ett objekt (kugghjul, vindsnurror, mynt).
public class Spinner : MonoBehaviour
{
    public Vector3 axis = Vector3.forward;
    public float speed = 90f;

    void Update() => transform.Rotate(axis, speed * Time.deltaTime, Space.Self);
}
