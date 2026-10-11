using UnityEngine;

// Dammoln som yr upp när gubben slår i marken (eller vattenstänk i grottan).
public class Dust : MonoBehaviour
{
    Mesh mesh;
    Color[] baseCols, cols;
    Vector3 vel;
    float life, max;

    public static void Puff(Vector3 pos, Color col, int count = 12)
    {
        for (int i = 0; i < count; i++)
        {
            var mb = new MeshBuilder();
            mb.Sphere(Vector3.zero, Vector3.one * Random.Range(0.4f, 0.8f), col, 6, 8);
            var go = Toon.MakeGlow("Damm", mb.ToMesh("damm"), null);
            go.transform.position = pos + new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(0f, 0.4f), Random.Range(-0.6f, 0.6f));
            var d = go.AddComponent<Dust>();
            d.mesh = go.GetComponent<MeshFilter>().sharedMesh;
            d.baseCols = d.mesh.colors;
            d.cols = new Color[d.baseCols.Length];
            d.vel = new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(0.6f, 2.2f), Random.Range(-2.5f, 2.5f));
            d.max = d.life = Random.Range(1.4f, 2.4f);
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        life -= dt;
        if (life <= 0f) { Destroy(mesh); Destroy(gameObject); return; }
        vel *= 1f - dt * 1.8f;
        transform.position += vel * dt;
        float k = 1f - life / max;
        transform.localScale = Vector3.one * (0.6f + k * 2.4f);
        float a = Mathf.Clamp01(life / max);
        for (int i = 0; i < cols.Length; i++) { var c = baseCols[i]; c.a *= a; cols[i] = c; }
        mesh.colors = cols;
    }
}
