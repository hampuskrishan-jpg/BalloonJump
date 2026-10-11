using UnityEngine;

// Spikhjul/droppsten som faller ner ovanifrån (mode 0) eller studsar upp underifrån (mode 1), om och om igen.
public class Faller : MonoBehaviour
{
    public int mode;
    public float top = 16f, bottom = -12f, period = 2.6f, phase;
    float baseY;

    void Start() => baseY = transform.localPosition.y;

    void Update()
    {
        float t = Mathf.Repeat(Time.time / period + phase, 1f);
        float y = mode == 0
            ? top - (top - bottom) * t * t
            : bottom + (top - bottom) * Mathf.Sin(t * Mathf.PI);
        var p = transform.localPosition;
        p.y = baseY + y;
        transform.localPosition = p;
    }
}
