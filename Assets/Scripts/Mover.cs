using UnityEngine;

// Pendlar fram och tillbaka i sidled mellan minX och maxX.
public class Mover : MonoBehaviour
{
    public float minX, maxX, speed = 1f, phase;

    void Update()
    {
        var p = transform.localPosition;
        p.x = Mathf.Lerp(minX, maxX, 0.5f + 0.5f * Mathf.Sin(Time.time * speed + phase));
        transform.localPosition = p;
    }
}
