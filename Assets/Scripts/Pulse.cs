using UnityEngine;

// Får giftig rök att bölja.
public class Pulse : MonoBehaviour
{
    public float amount = 0.12f, speed = 2f, phase;

    void Update() => transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * speed + phase) * amount);
}
