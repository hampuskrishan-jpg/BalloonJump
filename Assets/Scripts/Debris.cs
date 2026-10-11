using UnityEngine;

// Ballongbitar som flyger iväg när en ballong spricker.
public class Debris : MonoBehaviour
{
    public Vector3 velocity, spin;
    float life = 1.2f;

    void Update()
    {
        velocity += Vector3.down * 9f * Time.deltaTime;
        transform.position += velocity * Time.deltaTime;
        transform.Rotate(spin * Time.deltaTime);
        life -= Time.deltaTime;
        transform.localScale = Vector3.one * Mathf.Clamp01(life * 2f);
        if (life <= 0f) Destroy(gameObject);
    }

    public static void Burst(Vector3 pos, Color col)
    {
        var mb = new MeshBuilder();
        col.a = 0.15f;
        mb.Sphere(Vector3.zero, new Vector3(0.14f, 0.1f, 0.02f), col, 4, 6);
        var mesh = mb.ToMesh("bit");
        for (int i = 0; i < 10; i++)
        {
            var go = Toon.MakeSmooth("Ballongbit", mesh, null);
            go.transform.position = pos + Random.insideUnitSphere * 0.3f;
            var d = go.AddComponent<Debris>();
            d.velocity = Random.insideUnitSphere * 7f + Vector3.up * 2f;
            d.spin = Random.insideUnitSphere * 720f;
        }
    }
}
