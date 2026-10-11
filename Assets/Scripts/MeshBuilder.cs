using System.Collections.Generic;
using UnityEngine;

// Bygger ihop lådor, cylindrar och klot till ett enda mesh med vertexfärger.
// UV2 (kanal 1) får en "hörnriktning" som konturshadern använder.
public class MeshBuilder
{
    readonly List<Vector3> verts = new List<Vector3>();
    readonly List<Vector3> norms = new List<Vector3>();
    readonly List<Vector3> ext = new List<Vector3>();
    readonly List<Color> cols = new List<Color>();
    readonly List<int> tris = new List<int>();
    readonly List<Vector2> mats = new List<Vector2>();

    // Vilken fototextur ytorna som läggs till härnäst ska ha (0 = ingen, bara färg).
    // 1 puts, 2 väderbiten puts, 3 tegel, 4 gatsten, 5 takpannor, 6 trä, 7 berg, 8 metall, 9 betong/sten
    public float Mat;
    public const float Plaster = 1, OldPlaster = 2, Brick = 3, Cobble = 4, Roof = 5, Wood = 6, Rock = 7, Metal = 8, Stone = 9;
    public const float Sand = 16, SnowMat = 17, Grass = 18, Sandstone = 19, Moss = 20;   // miljöerna öknen, snöberget och bergsbyn

    public int VertexCount => verts.Count;

    static readonly Vector3[] FN = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
    static readonly Vector3[] FU = { Vector3.up, Vector3.forward, Vector3.forward, Vector3.right, Vector3.right, Vector3.up };
    static readonly Vector3[] FV = { Vector3.forward, Vector3.up, Vector3.right, Vector3.forward, Vector3.up, Vector3.right };

    void Push(Vector3 p, Vector3 n, Vector3 e, Color c)
    {
        verts.Add(p);
        norms.Add(n.normalized);
        ext.Add(e.normalized);
        cols.Add(c);
        mats.Add(new Vector2(Mat, 0));
    }

    public void Box(Vector3 c, Vector3 size, Color col) => Box(c, size, col, Quaternion.identity);

    public void Box(Vector3 c, Vector3 size, Color col, Quaternion rot)
    {
        Vector3 h = size * 0.5f;
        for (int f = 0; f < 6; f++)
        {
            Vector3 n = FN[f], u = FU[f], v = FV[f];
            int i = verts.Count;
            for (int k = 0; k < 4; k++)
            {
                float su = (k == 1 || k == 2) ? 1f : -1f;
                float sv = k >= 2 ? 1f : -1f;
                Vector3 local = n + u * su + v * sv;
                Push(c + rot * Vector3.Scale(local, h), rot * n, rot * local, col);
            }
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }
    }

    // Cylinder längs lokal Y-axel.
    public void Cylinder(Vector3 c, float r, float height, int seg, Color col, Quaternion rot)
    {
        float hh = height * 0.5f;
        Vector3 up = rot * Vector3.up;
        for (int s = 0; s < seg; s++)
        {
            float a0 = s * Mathf.PI * 2f / seg, a1 = (s + 1) * Mathf.PI * 2f / seg;
            Vector3 d0 = rot * new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
            Vector3 d1 = rot * new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
            int i = verts.Count;
            Push(c + d0 * r - up * hh, d0, d0 - up, col);
            Push(c + d0 * r + up * hh, d0, d0 + up, col);
            Push(c + d1 * r + up * hh, d1, d1 + up, col);
            Push(c + d1 * r - up * hh, d1, d1 - up, col);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            // lock
            int t = verts.Count;
            Push(c + up * hh, up, up, col);
            Push(c + d1 * r + up * hh, up, d1 + up, col);
            Push(c + d0 * r + up * hh, up, d0 + up, col);
            tris.Add(t); tris.Add(t + 1); tris.Add(t + 2);
            int b = verts.Count;
            Push(c - up * hh, -up, -up, col);
            Push(c + d0 * r - up * hh, -up, d0 - up, col);
            Push(c + d1 * r - up * hh, -up, d1 - up, col);
            tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
        }
    }

    public void Sphere(Vector3 c, Vector3 radii, Color col, int rings = 8, int segs = 12) => Sphere(c, radii, col, rings, segs, Quaternion.identity);

    public void Sphere(Vector3 c, Vector3 radii, Color col, int rings, int segs, Quaternion rot)
    {
        int start = verts.Count;
        Vector3 inv = new Vector3(1f / radii.x, 1f / radii.y, 1f / radii.z);
        for (int i = 0; i <= rings; i++)
        {
            float th = Mathf.PI * i / rings;
            for (int j = 0; j <= segs; j++)
            {
                float ph = Mathf.PI * 2f * j / segs;
                Vector3 n = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                Push(c + rot * Vector3.Scale(n, radii), rot * Vector3.Scale(n, inv), rot * n, col);
            }
        }
        for (int i = 0; i < rings; i++)
        {
            for (int j = 0; j < segs; j++)
            {
                int a = start + i * (segs + 1) + j;
                int b = a + segs + 1;
                tris.Add(a); tris.Add(a + 1); tris.Add(b);
                tris.Add(a + 1); tris.Add(b + 1); tris.Add(b);
            }
        }
    }

    // Avsmalnande rör mellan två punkter med rundade ändar – armar, ben, snören.
    public void Tube(Vector3 a, Vector3 b, float ra, float rb, Color col, int seg = 12, bool caps = true)
    {
        Vector3 axis = b - a;
        float len = axis.magnitude;
        if (len < 1e-5f) return;
        Vector3 dir = axis / len;
        Vector3 u = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        Vector3 v = Vector3.Cross(dir, u);
        float slope = (ra - rb) / len;
        int start = verts.Count;
        for (int i = 0; i <= seg; i++)
        {
            float t = i * Mathf.PI * 2f / seg;
            Vector3 rd = u * Mathf.Cos(t) + v * Mathf.Sin(t);
            Vector3 n = (rd + dir * slope).normalized;
            Push(a + rd * ra, n, n, col);
            Push(b + rd * rb, n, n, col);
        }
        for (int i = 0; i < seg; i++)
        {
            int k = start + i * 2;
            tris.Add(k); tris.Add(k + 2); tris.Add(k + 1);
            tris.Add(k + 2); tris.Add(k + 3); tris.Add(k + 1);
        }
        if (caps)
        {
            Sphere(a, Vector3.one * ra, col, 6, seg);
            Sphere(b, Vector3.one * rb, col, 6, seg);
        }
    }

    // Mjuk kroppsform: elliptiska ringar på olika höjder (bål, rock).
    public void Loft(Vector3[] centers, Vector2[] radii, Color col, int seg = 18)
    {
        int rings = centers.Length;
        int start = verts.Count;
        for (int k = 0; k < rings; k++)
        {
            float dr = 0f;
            if (k > 0 && k < rings - 1)
                dr = (Avg(radii[k - 1]) - Avg(radii[k + 1])) / Mathf.Max(0.01f, centers[k + 1].y - centers[k - 1].y);
            for (int i = 0; i <= seg; i++)
            {
                float t = i * Mathf.PI * 2f / seg;
                float dx = Mathf.Cos(t), dz = Mathf.Sin(t);
                Vector3 p = centers[k] + new Vector3(dx * radii[k].x, 0, dz * radii[k].y);
                Vector3 n = new Vector3(dx * radii[k].y, 0, dz * radii[k].x).normalized;
                n = (n + Vector3.up * dr).normalized;
                Push(p, n, n, col);
            }
        }
        for (int k = 0; k < rings - 1; k++)
        {
            for (int i = 0; i < seg; i++)
            {
                int A = start + k * (seg + 1) + i, B = A + seg + 1;
                tris.Add(A); tris.Add(B); tris.Add(A + 1);
                tris.Add(A + 1); tris.Add(B); tris.Add(B + 1);
            }
        }
        Cap(centers[0], radii[0], col, seg, false);
        Cap(centers[rings - 1], radii[rings - 1], col, seg, true);
    }

    static float Avg(Vector2 r) => (r.x + r.y) * 0.5f;

    void Cap(Vector3 c, Vector2 r, Color col, int seg, bool top)
    {
        Vector3 n = top ? Vector3.up : Vector3.down;
        int ci = verts.Count;
        Push(c, n, n, col);
        for (int i = 0; i <= seg; i++)
        {
            float t = i * Mathf.PI * 2f / seg;
            Push(c + new Vector3(Mathf.Cos(t) * r.x, 0, Mathf.Sin(t) * r.y), n, n, col);
        }
        for (int i = 0; i < seg; i++)
        {
            if (top) { tris.Add(ci); tris.Add(ci + i + 2); tris.Add(ci + i + 1); }
            else { tris.Add(ci); tris.Add(ci + i + 1); tris.Add(ci + i + 2); }
        }
    }

    // Kugghjul i XY-planet (axel längs Z).
    public void Gear(Vector3 c, float r, Color col, Color dark)
    {
        Quaternion axis = Quaternion.Euler(90f, 0f, 0f);
        Cylinder(c, r, 0.45f, 24, col, axis);
        int teeth = Mathf.Max(8, Mathf.RoundToInt(r * 7f));
        for (int i = 0; i < teeth; i++)
        {
            float a = i * 360f / teeth;
            Vector3 d = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad), 0);
            Box(c + d * (r + 0.17f), new Vector3(0.4f, 0.36f, 0.45f), col, Quaternion.Euler(0, 0, a));
        }
        for (int i = 0; i < 3; i++)
            Box(c + new Vector3(0, 0, -0.25f), new Vector3(r * 1.5f, 0.22f, 0.08f), dark, Quaternion.Euler(0, 0, i * 60f));
        Cylinder(c, r * 0.22f, 0.7f, 12, dark, axis);
    }

    public Mesh ToMesh(string name)
    {
        var m = new Mesh { name = name };
        if (verts.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(verts);
        m.SetNormals(norms);
        m.SetColors(cols);
        m.SetUVs(0, mats);
        m.SetUVs(1, ext);
        m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        return m;
    }
}
