using System.Collections.Generic;
using UnityEngine;

public class Level
{
    public GameObject root;
    public float height, length, slope, floorY;
    public bool cave;
    public int env;   // 0 staden, 1 öknen, 2 bergsbyn, 3 vattengrottan, 4 snöberget
    public Vector3 start;
    public readonly List<GameObject> coins = new List<GameObject>();
    public readonly List<float> birdZ = new List<float>();            // var den galna fågeln dyker upp
    public readonly List<bool> birdHawk = new List<bool>();           // true = höken
    public readonly List<Transform> docks = new List<Transform>();    // bryggor/flotte i grottan
    public readonly List<float> dockHalf = new List<float>();

    // Glidbanan: gubben svävar framåt (z) och sjunker samtidigt, från avsatsen ner till marken.
    public float PathY(float z) => Mathf.Max(floorY, height - Mathf.Max(0f, z) * slope);

    // Står gubben på en brygga/flotte vid x? (bara grottan)
    public bool OnDock(float x)
    {
        for (int i = 0; i < docks.Count; i++)
            if (Mathf.Abs(x - docks[i].position.x) <= dockHalf[i] + 0.35f) return true;
        return false;
    }
}

// Bygger en bana: en lång gata mellan höga hus. Man hoppar från klocktornet och glider
// framåt och neråt längs gatan, förbi hinder, tills man landar på marknaden längst bort.
// Gubben styr i sidled (x); höjden följer glidbanan.
public static class LevelBuilder
{
    const float W = Game.StreetHalf;
    const float ChunkVerts = 45000;

    static System.Random rnd;
    static float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
    static int RI(int a, int b) => rnd.Next(a, b);
    static bool Chance(float p) => rnd.NextDouble() < p;
    static Color Jit(Color c, float j) => new Color(c.r + R(-j, j), c.g + R(-j, j), c.b + R(-j, j));

    // dämpad industriell skala: jord, rost, umbra, ockra och sot
    static readonly Color[] Facades =
    {
        new Color(0.62f, 0.47f, 0.33f), new Color(0.58f, 0.31f, 0.2f), new Color(0.44f, 0.33f, 0.26f),
        new Color(0.68f, 0.6f, 0.47f), new Color(0.45f, 0.42f, 0.39f), new Color(0.66f, 0.4f, 0.27f),
        new Color(0.52f, 0.47f, 0.33f), new Color(0.72f, 0.56f, 0.36f),
    };
    static readonly Color[] Accents =
    {
        new Color(0.62f, 0.22f, 0.13f), new Color(0.27f, 0.42f, 0.39f), new Color(0.74f, 0.56f, 0.2f),
        new Color(0.3f, 0.36f, 0.45f), new Color(0.48f, 0.3f, 0.2f),
    };
    static readonly Color Wood = new Color(0.47f, 0.3f, 0.18f), DarkWood = new Color(0.27f, 0.18f, 0.12f);
    static readonly Color Iron = new Color(0.3f, 0.29f, 0.3f), Brass = new Color(0.72f, 0.52f, 0.24f);
    static readonly Color Rust = new Color(0.55f, 0.27f, 0.15f);
    static readonly Color Glass = new Color(0.16f, 0.15f, 0.16f, 0.08f), RoofCol = new Color(0.5f, 0.24f, 0.16f);
    static readonly Color Stone = new Color(0.7f, 0.6f, 0.46f), Danger = new Color(0.78f, 0.2f, 0.12f);
    static readonly Color Cream = new Color(0.9f, 0.84f, 0.7f);

    class Bld
    {
        public int side;
        public float z0, z1, inset, top, floorH;
        public Color col, trim, accent;
        public bool shutters, tower;
        public float mat;
    }

    static Mesh coinMesh;
    static bool caveMode;
    const int City = 0, Desert = 1, Village = 2, Cave = 3, Snow = 4;
    static int env;   // miljön som byggs just nu
    static float curD;   // svårighet för raden som byggs just nu

    public static Level Build(int idx, Transform parent)
    {
        rnd = new System.Random(4711 + idx * 7919);
        float H = Game.Height(idx);
        float L = H * Game.Ratio(idx);
        float D = Game.Diff(idx);
        int theme = Game.Theme(idx);
        env = theme;
        caveMode = env == Cave;
        float floor = caveMode ? 0.8f : 0f;
        var lv = new Level { height = H, length = L, floorY = floor, slope = (H - floor) / L, cave = caveMode, env = env };
        lv.root = new GameObject("Bana " + (idx + 1));
        lv.root.transform.SetParent(parent, false);
        lv.start = new Vector3(-5.2f, H, 0);

        if (env == City)
        {
            BuildCity(lv.root.transform, Plan(lv), lv);
            BuildTower(lv.root.transform, H);
            BuildGround(lv.root.transform, L);
        }
        else
        {
            // öknen, bergsbyn, snöberget och grottan: en kanjon mellan klippväggar
            BuildCave(lv.root.transform, lv);
            if (caveMode) BuildDocks(lv.root.transform, lv);
            else BuildCanyonGround(lv.root.transform, L);
        }
        BuildObstacles(lv, idx);
        PreciseColliders(lv.root.transform.Find("Hinder"));
        // fåglar: fler ju svårare banan är
        // fåglar: inga på de första banorna, sedan på alla stadsbanor (fler ju svårare), aldrig i grottan
        int nb = idx == Game.LevelCount - 1 ? 5 // finalen: flest och argast fåglar
               : idx < Game.BirdsFrom ? 0
               : 1 + Mathf.RoundToInt(D * 2.5f);
        if (idx >= 89 && idx < Game.LevelCount - 1) nb++;   // de allra svåraste banorna (90–99): en fågel till
        // höken: en extra fågel från bana 50, två på bana 90–100
        int hawks = idx >= 89 ? 2 : idx >= 49 ? 1 : 0;
        nb += hawks;
        for (int i = 0; i < nb; i++)
        {
            lv.birdZ.Add(L * Mathf.Lerp(0.2f, 0.85f, nb == 1 ? 0.5f : i / (float)(nb - 1)));
            bool h = hawks == 1 ? i == nb / 2 : hawks == 2 && (i == 1 || i == nb - 2);
            lv.birdHawk.Add(h);
        }
        return lv;
    }

    // ---------------------------------------------------------------- Staden

    static List<Bld> Plan(Level lv)
    {
        var list = new List<Bld>();
        float end = lv.length + 90f;
        for (int side = -1; side <= 1; side += 2)
        {
            float z = -30f;
            while (z < end)
            {
                if (side == -1 && Mathf.Abs(z + 4f) < 0.01f)
                {
                    list.Add(new Bld
                    {
                        side = -1, z0 = -4f, z1 = 6f, inset = 0f, top = lv.height + 14f, floorH = 4f,
                        col = Stone, trim = Color.Lerp(Stone, Color.white, 0.35f), accent = Accents[0], tower = true, mat = MeshBuilder.Stone,
                    });
                    z = 6f;
                    continue;
                }
                float w = R(6f, 11f);
                if (side == -1 && z < -4f && z + w > -4f) w = -4f - z;
                var b = new Bld
                {
                    side = side, z0 = z, z1 = z + w, inset = R(0f, 0.9f),
                    floorH = R(3.3f, 4f), shutters = Chance(0.5f),
                    col = Color.Lerp(Jit(Facades[RI(0, Facades.Length)], 0.03f), new Color(0.5f, 0.49f, 0.47f), 0.3f), // väderbitet
                    accent = Accents[RI(0, Accents.Length)],
                };
                b.top = Mathf.Max(12f, lv.PathY(z) + R(5f, 24f));
                b.trim = Color.Lerp(b.col, new Color(0.9f, 0.82f, 0.66f), 0.35f);
                b.mat = Chance(0.3f) ? MeshBuilder.Brick : Chance(0.55f) ? MeshBuilder.OldPlaster : MeshBuilder.Plaster;
                list.Add(b);
                z += w;
            }
        }
        return list;
    }

    static void BuildCity(Transform root, List<Bld> blds, Level lv)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            var mb = new MeshBuilder();
            int n = 0;
            foreach (var b in blds)
            {
                if (b.side != side) continue;
                AddBuilding(mb, b, lv);
                if (mb.VertexCount > ChunkVerts)
                {
                    Toon.Make("Kvarter " + side + "_" + n++, mb.ToMesh("kvarter"), root);
                    mb = new MeshBuilder();
                }
            }
            if (side == -1) AddStreetDecor(mb, lv);
            if (mb.VertexCount > 0) Toon.Make("Kvarter " + side + "_" + n, mb.ToMesh("kvarter"), root);
        }

        // solstrålar som faller in snett genom diset mellan husen
        var glow = new MeshBuilder();
        Vector3 sunDir = Quaternion.Euler(16f, 58f, 0f) * Vector3.forward;
        Quaternion beamRot = Quaternion.LookRotation(sunDir);
        for (float z = R(5f, 20f); z < lv.length + 60f; z += R(14f, 30f))
        {
            float y = lv.PathY(z) + R(4f, 22f);
            Vector3 c = new Vector3(R(-3f, 3f), y, z);
            glow.Box(c, new Vector3(R(1.5f, 4f), R(3f, 9f), 46f), new Color(1f, 0.86f, 0.66f, R(0.035f, 0.06f)), beamRot);
            glow.Box(c + Vector3.up * 1.5f, new Vector3(R(0.6f, 1.4f), R(1.5f, 3f), 46f), new Color(1f, 0.9f, 0.75f, 0.05f), beamRot);
        }
        Toon.MakeGlow("Solstralar", glow.ToMesh("solstralar"), root);
    }

    static void AddBuilding(MeshBuilder mb, Bld b, Level lv)
    {
        int s = b.side;
        float fx = s * (W + b.inset);
        float depth = 14f;
        float zc = (b.z0 + b.z1) * 0.5f, zw = b.z1 - b.z0;
        float pathLow = lv.PathY(b.z1);
        float detailLo = pathLow - 30f, stripLo = pathLow - 110f;

        mb.Mat = b.mat;
        mb.Box(new Vector3(fx + s * depth * 0.5f, b.top * 0.5f, zc), new Vector3(depth, b.top, zw - 0.02f), b.col);
        mb.Mat = MeshBuilder.Stone;
        if (zw > 3f)
        {
            mb.Box(new Vector3(fx - s * 0.1f, b.top * 0.5f, b.z0 + 0.25f), new Vector3(0.22f, b.top, 0.4f), b.trim);
            mb.Box(new Vector3(fx - s * 0.1f, b.top * 0.5f, b.z1 - 0.25f), new Vector3(0.22f, b.top, 0.4f), b.trim);
        }
        // industriella stuprör längs fasaden
        if (zw > 4f && Chance(0.5f))
        {
            float pz = R(b.z0 + 0.8f, b.z1 - 0.8f);
            mb.Mat = MeshBuilder.Metal;
            mb.Box(new Vector3(fx - s * 0.22f, b.top * 0.5f, pz), new Vector3(0.24f, b.top, 0.24f), Iron);
            mb.Box(new Vector3(fx - s * 0.22f, b.top - 0.3f, pz), new Vector3(0.4f, 0.5f, 0.4f), Rust);
        }

        for (int f = 0; f * b.floorH < b.top - 2f; f++)
        {
            float fy = f * b.floorH;
            if (fy < stripLo && f > 0) continue;
            mb.Mat = MeshBuilder.Stone;
            if (f == 0)
            {
                if (zw > 2.5f)
                {
                    mb.Mat = 0;
                    mb.Box(new Vector3(fx - s * 0.06f, 1.4f, zc), new Vector3(0.12f, 2.2f, zw - 1.6f), Glass);
                    mb.Box(new Vector3(fx - s * 0.8f, 2.9f, zc), new Vector3(1.6f, 0.12f, zw - 1.2f), b.accent, Quaternion.Euler(0, 0, s * 15f));
                }
                continue;
            }
            if (f % 3 == 0)
                mb.Box(new Vector3(fx - s * 0.12f, fy, zc), new Vector3(0.25f, 0.22f, zw - 0.1f), f % 6 == 0 ? Color.Lerp(b.trim, Rust, 0.55f) : b.trim);

            float wy = fy + b.floorH * 0.5f;
            if (fy >= detailLo)
            {
                int nw = Mathf.Max(1, Mathf.FloorToInt((zw - 0.8f) / 2.4f));
                float step = zw / nw;
                for (int i = 0; i < nw; i++)
                {
                    float wz = b.z0 + step * (i + 0.5f);
                    mb.Mat = MeshBuilder.Stone;
                    mb.Box(new Vector3(fx - s * 0.05f, wy, wz), new Vector3(0.1f, 1.55f, 1.15f), b.trim);
                    mb.Mat = 0;
                    mb.Box(new Vector3(fx - s * 0.09f, wy + 0.03f, wz), new Vector3(0.1f, 1.35f, 0.9f), Glass);
                    // gardiner och spröjs gör fönstren levande
                    if (Chance(0.5f)) mb.Box(new Vector3(fx - s * 0.1f, wy + 0.35f, wz), new Vector3(0.1f, 0.6f, 0.86f), new Color(0.82f, 0.78f, 0.68f));
                    mb.Box(new Vector3(fx - s * 0.14f, wy + 0.03f, wz), new Vector3(0.04f, 1.35f, 0.05f), b.trim);
                    mb.Box(new Vector3(fx - s * 0.14f, wy + 0.1f, wz), new Vector3(0.04f, 0.05f, 0.9f), b.trim);
                    mb.Mat = MeshBuilder.Wood;
                    if (b.shutters)
                    {
                        mb.Box(new Vector3(fx - s * 0.1f, wy, wz - 0.8f), new Vector3(0.08f, 1.4f, 0.42f), b.accent);
                        mb.Box(new Vector3(fx - s * 0.1f, wy, wz + 0.8f), new Vector3(0.08f, 1.4f, 0.42f), b.accent);
                    }
                    else
                    {
                        mb.Mat = MeshBuilder.Stone;
                        mb.Box(new Vector3(fx - s * 0.15f, wy - 0.8f, wz), new Vector3(0.3f, 0.1f, 1.3f), b.trim);
                    }
                    // små blomlådor – bara långt under eller över glidbanan så de inte ser ut som hinder
                    float py = lv.PathY(wz);
                    if ((wy < py - 4f || wy > py + 8f) && Chance(0.06f))
                    {
                        mb.Box(new Vector3(fx - s * 0.45f, wy - 0.85f, wz), new Vector3(0.9f, 0.15f, 1.6f), b.trim);
                        mb.Sphere(new Vector3(fx - s * 0.45f, wy - 0.6f, wz), Vector3.one * 0.25f, new Color(0.4f, 0.47f, 0.25f), 4, 6);
                    }
                }
            }
            else
            {
                mb.Mat = 0;
                mb.Box(new Vector3(fx - s * 0.06f, wy, zc), new Vector3(0.12f, 1.3f, zw - 1.2f), Glass);
            }
        }

        if (!b.tower)
        {
            float ty = b.top;
            mb.Mat = MeshBuilder.Stone;
            mb.Box(new Vector3(fx + s * depth * 0.5f, ty + 0.2f, zc), new Vector3(depth + 0.6f, 0.4f, zw + 0.3f), b.trim);
            mb.Mat = MeshBuilder.Roof;
            mb.Box(new Vector3(fx + s * 3.5f, ty + 2.2f, zc), new Vector3(7.9f, 0.3f, zw + 0.2f), Jit(RoofCol, 0.05f), Quaternion.Euler(0, 0, s * 27.2f));
            mb.Box(new Vector3(fx + s * 10.5f, ty + 2.2f, zc), new Vector3(7.9f, 0.3f, zw + 0.2f), Jit(RoofCol, 0.05f), Quaternion.Euler(0, 0, -s * 27.2f));
            int ch = RI(1, 3);
            mb.Mat = MeshBuilder.Brick;
            for (int i = 0; i < ch; i++)
                mb.Box(new Vector3(fx + s * R(2.5f, 6f), ty + 3.6f, zc + R(-zw * 0.3f, zw * 0.3f)), new Vector3(0.8f, 2.6f, 0.8f), new Color(0.6f, 0.35f, 0.28f));
        }
    }

    // Flaggspel tvärs över gatan, högt över glidbanan (bara dekor).
    static void AddStreetDecor(MeshBuilder mb, Level lv)
    {
        mb.Mat = 0;
        for (float z = R(10f, 25f); z < lv.length + 60f; z += R(14f, 30f))
        {
            float y = lv.PathY(z) + R(8f, 16f);
            mb.Box(new Vector3(0, y, z), new Vector3(2f * W + 1f, 0.05f, 0.05f), DarkWood);
            for (float x = -W + 0.6f; x < W - 0.5f; x += 0.9f)
                mb.Box(new Vector3(x, y - 0.3f, z), new Vector3(0.45f, 0.5f, 0.04f), Accents[RI(0, Accents.Length)], Quaternion.Euler(0, 0, R(-8f, 8f)));
        }
    }

    static void BuildTower(Transform root, float H)
    {
        var mb = new MeshBuilder();
        mb.Mat = MeshBuilder.Roof;
        float face = -W;
        for (int i = 0; i < 6; i++)
        {
            float sz = 11f - i * 1.8f;
            mb.Box(new Vector3(-W - 7f, H + 14.8f + i * 1.6f, 1f), new Vector3(sz, 1.6f, sz), i % 2 == 0 ? RoofCol : RoofCol * 0.85f);
        }
        mb.Box(new Vector3(-W - 7f, H + 25.5f, 1f), new Vector3(0.25f, 3f, 0.25f), Iron);
        mb.Box(new Vector3(-W - 7f, H + 14.4f, 1f), new Vector3(14.8f, 0.6f, 10.6f), Color.Lerp(Stone, Color.white, 0.3f));

        mb.Mat = 0;
        Clock(mb, new Vector3(face, H + 7f, 1f), 1.9f, 0.4f, 0.1f);
        Clock(mb, new Vector3(face, H - 14f, 1f), 2.3f, 0.7f, 0.55f);

        // dörr och avsats som man hoppar ifrån
        mb.Mat = MeshBuilder.Stone;
        mb.Box(new Vector3(face + 0.02f, H + 1.25f, 0), new Vector3(0.1f, 2.7f, 1.7f), Color.Lerp(Stone, Color.white, 0.3f));
        mb.Mat = MeshBuilder.Wood;
        mb.Box(new Vector3(face + 0.06f, H + 1.15f, 0), new Vector3(0.1f, 2.3f, 1.3f), DarkWood);
        mb.Mat = MeshBuilder.Stone;
        mb.Box(new Vector3(-6.0f, H - 0.25f, 0.3f), new Vector3(3.2f, 0.5f, 2.4f), Color.Lerp(Stone, Color.white, 0.2f));
        mb.Box(new Vector3(-6.6f, H - 0.9f, 0), new Vector3(0.3f, 1.2f, 1.2f), Stone * 0.9f, Quaternion.Euler(0, 0, -30f));
        Toon.Make("Klocktornet", mb.ToMesh("torn"), root);
    }

    static void Clock(MeshBuilder mb, Vector3 c, float r, float hour, float minute)
    {
        Quaternion axis = Quaternion.Euler(0, 0, 90f);
        mb.Cylinder(c + Vector3.right * 0.1f, r + 0.25f, 0.2f, 28, Brass * 0.75f, axis);
        mb.Cylinder(c + Vector3.right * 0.2f, r, 0.3f, 28, new Color(0.96f, 0.93f, 0.84f), axis);
        float fx = c.x + 0.37f;
        for (int i = 0; i < 12; i++)
        {
            float a = i * 30f;
            Vector3 d = new Vector3(0, Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
            mb.Box(new Vector3(fx, c.y, c.z) + d * r * 0.82f, new Vector3(0.05f, i % 3 == 0 ? 0.4f : 0.22f, 0.1f), DarkWood, Quaternion.Euler(a, 0, 0));
        }
        float ha = hour * 360f, ma = minute * 360f;
        Vector3 hd = new Vector3(0, Mathf.Cos(ha * Mathf.Deg2Rad), Mathf.Sin(ha * Mathf.Deg2Rad));
        Vector3 md = new Vector3(0, Mathf.Cos(ma * Mathf.Deg2Rad), Mathf.Sin(ma * Mathf.Deg2Rad));
        mb.Box(new Vector3(fx + 0.02f, c.y, c.z) + hd * r * 0.25f, new Vector3(0.05f, r * 0.5f, 0.16f), DarkWood, Quaternion.Euler(ha, 0, 0));
        mb.Box(new Vector3(fx + 0.04f, c.y, c.z) + md * r * 0.36f, new Vector3(0.05f, r * 0.72f, 0.1f), DarkWood, Quaternion.Euler(ma, 0, 0));
        mb.Cylinder(new Vector3(fx + 0.05f, c.y, c.z), 0.12f, 0.1f, 10, DarkWood, axis);
    }

    static void BuildGround(Transform root, float L)
    {
        var mb = new MeshBuilder();
        float len = L + 130f, mid = (L + 90f - 40f) * 0.5f;
        mb.Mat = MeshBuilder.Cobble;
        mb.Box(new Vector3(0, -0.5f, mid), new Vector3(40f, 1f, len), new Color(0.5f, 0.46f, 0.42f));
        mb.Mat = MeshBuilder.Stone;
        mb.Box(new Vector3(-W + 0.6f, 0.08f, mid), new Vector3(1.2f, 0.16f, len), new Color(0.56f, 0.5f, 0.42f));
        mb.Box(new Vector3(W - 0.6f, 0.08f, mid), new Vector3(1.2f, 0.16f, len), new Color(0.56f, 0.5f, 0.42f));
        mb.Mat = MeshBuilder.Cobble;
        for (int i = 0; i < 90; i++)
            mb.Box(new Vector3(R(-5.5f, 5.5f), 0.01f, L + R(-50f, 60f)), new Vector3(R(0.3f, 0.6f), 0.02f, R(0.3f, 0.6f)), new Color(0.34f, 0.3f, 0.26f));

        // landningsmål
        mb.Mat = 0;
        mb.Cylinder(new Vector3(0, 0.02f, L), 2.6f, 0.04f, 32, Danger, Quaternion.identity);
        mb.Cylinder(new Vector3(0, 0.04f, L), 1.8f, 0.06f, 32, Cream, Quaternion.identity);
        mb.Cylinder(new Vector3(0, 0.06f, L), 1.0f, 0.08f, 28, Danger, Quaternion.identity);
        mb.Cylinder(new Vector3(0, 0.08f, L), 0.35f, 0.1f, 16, Cream, Quaternion.identity);

        // marknadsstånd runt landningsplatsen
        for (int i = 0; i < 10; i++)
        {
            float x = (i % 2 == 0 ? -1f : 1f) * 5.1f, z = L - 30f + i * 7f;
            mb.Mat = MeshBuilder.Wood;
            mb.Box(new Vector3(x, 0.5f, z), new Vector3(1.6f, 1f, 2.6f), Wood);
            for (int px = -1; px <= 1; px += 2)
                for (int pz = -1; pz <= 1; pz += 2)
                    mb.Box(new Vector3(x + px * 0.7f, 1.3f, z + pz * 1.2f), new Vector3(0.08f, 2.6f, 0.08f), DarkWood);
            Color ac = Accents[RI(0, Accents.Length)];
            mb.Mat = 0;
            for (int st = 0; st < 5; st++)
                mb.Box(new Vector3(x, 2.65f, z - 1.04f + st * 0.52f), new Vector3(2.0f, 0.1f, 0.52f), st % 2 == 0 ? ac : Cream, Quaternion.Euler(0, 0, -Mathf.Sign(x) * 12f));
            for (int fr = 0; fr < 6; fr++)
                mb.Sphere(new Vector3(x + R(-0.6f, 0.6f), 1.1f, z + R(-1.1f, 1.1f)), Vector3.one * 0.14f, Accents[RI(0, Accents.Length)], 4, 6);
        }
        for (float z = L - 40f; z < L + 60f; z += 12f)
        {
            for (int sd = -1; sd <= 1; sd += 2)
            {
                mb.Mat = MeshBuilder.Metal;
                mb.Box(new Vector3(sd * 6.3f, 2f, z), new Vector3(0.14f, 4f, 0.14f), Iron);
                mb.Mat = 0;
                mb.Box(new Vector3(sd * 6.0f, 4.1f, z), new Vector3(0.5f, 0.45f, 0.45f), new Color(1f, 0.9f, 0.55f));
            }
        }
        var ground = Toon.Make("Marken", mb.ToMesh("mark"), root);
        // fysiska ytor som kroppen kan slå i: gatan och husväggarna längs gatan
        var gc = ground.AddComponent<BoxCollider>();
        gc.center = new Vector3(0, -5f, mid);     // tjock mark så att inget kan ramla igenom
        gc.size = new Vector3(40f, 10f, len);
        foreach (int sd in new[] { -1, 1 })
        {
            var wc = ground.AddComponent<BoxCollider>();
            wc.center = new Vector3(sd * (W + 0.6f), 20f, mid);
            wc.size = new Vector3(1f, 40f, len);
        }
    }

    // ---------------------------------------------------------------- Grottan

    static readonly Color[] Rocks =
    {
        new Color(0.3f, 0.33f, 0.37f), new Color(0.24f, 0.27f, 0.31f), new Color(0.36f, 0.38f, 0.41f), new Color(0.28f, 0.29f, 0.32f),
    };
    static readonly Color Water = new Color(0.12f, 0.3f, 0.36f, 0.04f);
    static readonly Color[] DesertRocks =
    {
        new Color(0.74f, 0.44f, 0.27f), new Color(0.66f, 0.37f, 0.22f), new Color(0.8f, 0.52f, 0.33f), new Color(0.6f, 0.34f, 0.2f),
    };
    static readonly Color[] SnowRocks =
    {
        new Color(0.56f, 0.59f, 0.64f), new Color(0.48f, 0.51f, 0.56f), new Color(0.63f, 0.65f, 0.69f), new Color(0.44f, 0.47f, 0.52f),
    };
    static readonly Color[] VillageRocks =
    {
        new Color(0.44f, 0.46f, 0.4f), new Color(0.37f, 0.41f, 0.34f), new Color(0.5f, 0.5f, 0.44f), new Color(0.35f, 0.38f, 0.33f),
    };
    static Color RockCol()
    {
        var pal = env == Desert ? DesertRocks : env == Snow ? SnowRocks : env == Village ? VillageRocks : Rocks;
        return Jit(pal[RI(0, pal.Length)], 0.03f);
    }
    static float RockMat() => env == Desert ? MeshBuilder.Sandstone : env == Village ? MeshBuilder.Moss : MeshBuilder.Rock;
    static readonly Color SnowWhite = new Color(0.94f, 0.96f, 0.99f), Ice = new Color(0.78f, 0.88f, 1f, 0.15f);

    static void BuildCave(Transform root, Level lv)
    {
        float end = lv.length + 70f;
        var glow = new MeshBuilder();
        for (int side = -1; side <= 1; side += 2)
        {
            var mb = new MeshBuilder { Mat = RockMat() };
            int n = 0;
            for (float z = -30f; z < end;)
            {
                float w = R(4f, 8f), inset = R(0f, 1.2f);
                float top = lv.PathY(z) + R(16f, 26f);
                float fx = side * (W + inset);
                mb.Box(new Vector3(fx + side * 5f, (top - 5f) * 0.5f, z + w * 0.5f), new Vector3(10f, top + 5f, w + 0.4f), RockCol());
                float py = lv.PathY(z);
                if (env == Snow)
                {
                    // snötäcke på klippkanten och snö på hyllor
                    mb.Mat = MeshBuilder.SnowMat;
                    mb.Box(new Vector3(fx + side * 5f, top + 0.3f, z + w * 0.5f), new Vector3(10.4f, 0.8f, w + 0.6f), SnowWhite);
                    for (int k = 0; k < 2; k++)
                        mb.Box(new Vector3(fx - side * 0.1f, R(py - 20f, top - 2f), z + R(0f, w)), new Vector3(1.2f, 0.35f, R(1.5f, 3f)), SnowWhite);
                    mb.Mat = RockMat();
                }
                if (env == Village && Chance(0.4f)) VillageHouse(mb, side, fx, z, w, py);
                if ((caveMode || env == Village) && Chance(caveMode ? 0.2f : 0.1f))
                {
                    // gamla trägångar med stolpar och räcke längs bergväggen
                    float gy = py + (Chance(0.5f) ? R(-14f, -5f) : R(7f, 14f));
                    float gx = side * (W - 0.4f);
                    mb.Mat = MeshBuilder.Wood;
                    mb.Box(new Vector3(gx, gy, z + w * 0.5f), new Vector3(1.6f, 0.12f, w + 0.4f), Wood);
                    mb.Box(new Vector3(gx - side * 0.75f, gy + 0.55f, z + w * 0.5f), new Vector3(0.06f, 0.06f, w + 0.4f), DarkWood);
                    for (float pz = 0f; pz <= w; pz += 2f)
                    {
                        mb.Box(new Vector3(gx - side * 0.75f, gy + 0.3f, z + pz), new Vector3(0.07f, 0.6f, 0.07f), DarkWood);
                        mb.Box(new Vector3(gx - side * 0.6f, gy - 2f, z + pz), new Vector3(0.12f, 4f, 0.12f), DarkWood, Quaternion.Euler(0, 0, side * 12f));
                    }
                    mb.Mat = RockMat();
                }
                for (int k = 0; k < 4; k++)
                    mb.Box(new Vector3(fx + side * 0.3f, R(py - 25f, top), z + R(0f, w)), new Vector3(1.4f, R(1f, 4f), R(1f, 3f)), RockCol() * 0.9f, Quaternion.Euler(R(-20f, 20f), R(-20f, 20f), R(-20f, 20f)));
                if (side == -1 && caveMode)
                {
                    // grottans tak med droppstenar
                    float cy = py + R(13f, 19f);
                    mb.Box(new Vector3(0, cy + 3f, z + w * 0.5f), new Vector3(2f * W + 14f, 6f, w + 0.4f), RockCol() * 0.8f);
                    int st = RI(1, 4);
                    for (int i = 0; i < st; i++)
                    {
                        float sx = R(-6f, 6f), sz = z + R(0f, w);
                        Color c = RockCol();
                        mb.Box(new Vector3(sx, cy - 0.6f, sz), new Vector3(1.1f, 1.2f, 1.1f), c);
                        mb.Box(new Vector3(sx, cy - 1.6f, sz), new Vector3(0.65f, 1.0f, 0.65f), c);
                        mb.Box(new Vector3(sx, cy - 2.4f, sz), new Vector3(0.3f, 0.8f, 0.3f), c);
                    }
                }
                z += w;
                if (mb.VertexCount > ChunkVerts)
                {
                    Toon.Make("Klippa " + side + "_" + n++, mb.ToMesh("klippa"), root);
                    mb = new MeshBuilder { Mat = RockMat() };
                }
            }
            Toon.Make("Klippa " + side + "_" + n, mb.ToMesh("klippa"), root);
        }

        // ljusstrålar genom sprickor i taket (bara grottan)
        for (float z = R(10f, 25f); caveMode && z < end; z += R(18f, 34f))
        {
            float top = lv.PathY(z) + 16f;
            float x = R(-5f, 5f);
            glow.Box(new Vector3(x, top * 0.5f, z), new Vector3(1.5f, top, 1.5f), new Color(0.75f, 0.88f, 1f, 0.08f), Quaternion.Euler(R(-6f, 6f), 0, R(-8f, 8f)));
            glow.Box(new Vector3(x, top * 0.5f, z), new Vector3(0.6f, top, 0.6f), new Color(0.85f, 0.94f, 1f, 0.1f), Quaternion.Euler(R(-6f, 6f), 0, R(-8f, 8f)));
        }
        // breda kalla ljuskäglor som faller in snett från sidan genom dimman (som i Tintin-grottan)
        Quaternion sideBeam = Quaternion.LookRotation(new Vector3(1f, -0.35f, 0.25f));
        for (float z = R(5f, 20f); caveMode && z < end; z += R(25f, 45f))
        {
            var c = new Vector3(-W + 4f, lv.PathY(z) + R(0f, 8f), z);
            glow.Box(c, new Vector3(R(3f, 5f), R(2.5f, 4f), 34f), new Color(0.7f, 0.85f, 1f, 0.07f), sideBeam);
            glow.Box(c, new Vector3(1.5f, 1.4f, 34f), new Color(0.85f, 0.93f, 1f, 0.08f), sideBeam);
        }
        Toon.MakeGlow("Ljusstralar", glow.ToMesh("ljus"), root);

        // startavsats av sten med en lykta
        var ledge = new MeshBuilder { Mat = RockMat() };
        ledge.Box(new Vector3(-6.0f, lv.height - 0.25f, 0.3f), new Vector3(3.2f, 0.5f, 2.4f), RockCol());
        ledge.Box(new Vector3(-6.6f, lv.height - 1.0f, 0.3f), new Vector3(1.6f, 1.2f, 2.0f), RockCol(), Quaternion.Euler(0, 0, 20f));
        if (env == Snow) { ledge.Mat = MeshBuilder.SnowMat; ledge.Box(new Vector3(-6.0f, lv.height + 0.02f, 0.3f), new Vector3(3.3f, 0.08f, 2.5f), SnowWhite); ledge.Mat = RockMat(); }
        ledge.Box(new Vector3(-6.7f, lv.height + 1.2f, 1.2f), new Vector3(0.1f, 2.4f, 0.1f), Iron);
        ledge.Box(new Vector3(-6.7f, lv.height + 2.5f, 1.2f), new Vector3(0.4f, 0.5f, 0.4f), new Color(1f, 0.85f, 0.4f));
        Toon.Make("Startavsats", ledge.ToMesh("avsats"), root);

        if (!caveMode) return;
        // vattnet längst ner
        var wmb = new MeshBuilder();
        float len = end + 40f, mid = (end - 40f) * 0.5f;
        wmb.Box(new Vector3(0, -0.5f, mid), new Vector3(40f, 1f, len), Water);
        for (int i = 0; i < 160; i++)
            wmb.Box(new Vector3(R(-6.5f, 6.5f), 0.02f, R(-20f, end)), new Vector3(R(0.8f, 2f), 0.02f, 0.12f), Color.Lerp(Water, Color.white, 0.35f));
        Toon.Make("Vatten", wmb.ToMesh("vatten"), root, false);
    }

    // Tre ställen att landa på: två bryggor och en flotte som guppar fram och tillbaka.
    static void BuildDocks(Transform root, Level lv)
    {
        float L = lv.length;
        float[] xs = { -4.6f + R(-0.5f, 0.5f), R(-0.6f, 0.6f), 4.6f + R(-0.5f, 0.5f) };
        int raft = RI(0, 3);
        for (int i = 0; i < 3; i++)
        {
            var mb = new MeshBuilder { Mat = MeshBuilder.Wood };
            float half;
            if (i == raft)
            {
                half = 1.2f;
                for (int k = 0; k < 6; k++)
                    mb.Cylinder(new Vector3(-1.0f + k * 0.4f, 0.4f, 0), 0.2f, 3.2f, 10, Jit(Wood, 0.04f), Quaternion.Euler(90f, 0, 0));
                mb.Box(new Vector3(0, 0.62f, -1.0f), new Vector3(2.4f, 0.1f, 0.3f), DarkWood);
                mb.Box(new Vector3(0, 0.62f, 1.0f), new Vector3(2.4f, 0.1f, 0.3f), DarkWood);
                mb.Box(new Vector3(0.6f, 1.5f, 0.6f), new Vector3(0.08f, 1.8f, 0.08f), DarkWood);
                mb.Box(new Vector3(0.6f, 2.0f, 0.3f), new Vector3(0.04f, 0.6f, 0.6f), Danger);
            }
            else
            {
                half = 1.0f;
                mb.Box(new Vector3(0, 0.65f, 3f), new Vector3(2.0f, 0.3f, 18f), Wood);
                for (float z = -5.5f; z < 12f; z += 0.6f)
                    mb.Box(new Vector3(0, 0.81f, z), new Vector3(2.0f, 0.02f, 0.05f), DarkWood);
                for (float z = -5f; z < 12f; z += 3f)
                {
                    mb.Box(new Vector3(-0.95f, 0f, z), new Vector3(0.2f, 1.8f, 0.2f), DarkWood);
                    mb.Box(new Vector3(0.95f, 0f, z), new Vector3(0.2f, 1.8f, 0.2f), DarkWood);
                }
                mb.Box(new Vector3(-0.9f, 1.3f, -5.2f), new Vector3(0.1f, 1.2f, 0.1f), Iron);
                mb.Box(new Vector3(-0.9f, 2.0f, -5.2f), new Vector3(0.35f, 0.35f, 0.35f), new Color(1f, 0.85f, 0.4f));
            }
            var go = Toon.Make(i == raft ? "Flotte" : "Brygga", mb.ToMesh("brygga"), root);
            var dc = go.AddComponent<BoxCollider>();   // så att kroppen kan landa på bryggan/flotten
            dc.center = i == raft ? new Vector3(0, 0.45f, 0) : new Vector3(0, 0.65f, 3f);
            dc.size = i == raft ? new Vector3(2.4f, 0.4f, 3.2f) : new Vector3(2f, 0.3f, 18f);
            go.transform.localPosition = new Vector3(xs[i], 0, L);
            if (i == raft)
            {
                var mv = go.AddComponent<Mover>();
                mv.minX = Mathf.Max(-W + 1.3f, xs[i] - 1.4f);
                mv.maxX = Mathf.Min(W - 1.3f, xs[i] + 1.4f);
                mv.speed = 0.7f;
            }
            lv.docks.Add(go.transform);
            lv.dockHalf.Add(half);
        }
    }

    // ---------------------------------------------------------------- Hinder
    // Hindren står i rader tvärs över gatan. Varje rad har en säker korridor [c - w/2, c + w/2]
    // som flyttar sig lagom mycket mellan raderna, så banan alltid går att klara.
    // Hindren täcker höjden 0–4,8 m över glidbanan, alltså både gubben och ballongerna.

    const float Mid = 2.4f;

    static void BuildObstacles(Level lv, int idx)
    {
        var p = new GameObject("Hinder").transform;
        p.SetParent(lv.root.transform, false);
        var cp = new GameObject("Mynt").transform;
        cp.SetParent(lv.root.transform, false);
        int value = 1 + idx / 20;
        float fast = Game.Speed(idx);
        float D = Game.Diff(idx);
        int theme = Game.Theme(idx);
        float L = lv.length;

        for (int i = 0; i < 3; i++)
        {
            float z = 8f + i * 4f;
            AddCoin(lv, cp, new Vector3(-2.5f + i * 0.3f, lv.PathY(z) + 1.2f, z), value);
        }

        float zr = 30f, c = -2f, prev = -2f;
        while (zr < L - 25f)
        {
            // Bana 1–2 är lugna för att lära sig. Sedan stiger svårigheten snabbt – hela spelet är på riktigt utmanande.
            float dc = idx < 2 ? idx * 0.06f : 1f - Mathf.Pow(1f - D, 2.2f);
            float d = Mathf.Clamp01(dc * 0.9f + zr / L * 0.1f);
            curD = d;
            float w = Mathf.Lerp(7f, 3.0f, d);            // luckan: bred i början, smal på slutet
            float spacing = Mathf.Lerp(30f, 10f, d) + R(1f, 1f + 5f * (1f - d)); // glest i början, tätt på slutet
            float maxShift = 7f * (spacing / fast) * 0.55f;
            float lim = W - w * 0.5f - 0.4f;
            float next = Mathf.Clamp(c + R(-maxShift, maxShift), -lim, lim);
            float py = lv.PathY(zr);

            float a = c - w * 0.5f, b = c + w * 0.5f;
            float ua = Mathf.Min(prev, Mathf.Min(c, next)) - w * 0.5f;
            float ub = Mathf.Max(prev, Mathf.Max(c, next)) + w * 0.5f;

            int type = PickType(theme, d);
            int usedSide = 0;
            if (type == 1) usedSide = TryGear(p, zr, py, ua, ub, false);
            else if (type == 2) usedSide = TryGear(p, zr, py, ua, ub, true);
            else if (type == 3) usedSide = TrySpinner(p, zr, py, ua, ub);
            else if (type == 4 && TryLaundry(p, zr, py, a, b)) usedSide = 2;
            else if (type == 5) { SpikeWheels(p, zr, py, a, b, w, d); usedSide = 2; }
            else if (type == 6) usedSide = TrySmoke(p, zr, py, ua, ub);
            else if (type == 7) { Drums(lv, p, cp, zr, value); usedSide = 2; }

            if (usedSide == 0) Walls(p, zr, py, a, b, 0);
            else if (usedSide != 2 && d > 0.15f && Chance(0.8f)) Walls(p, zr, py, a, b, usedSide);  // hinder på båda sidor oftare

            float nz = zr + spacing;

            // extra spikhjul mitt mellan raderna på svårare banor – inga lugna sträckor
            if (d > 0.25f && spacing > 11f && Chance(d * 0.8f) && nz < L - 25f)
            {
                float zm = zr + spacing * 0.5f, cm = (c + next) * 0.5f;
                SpikeWheels(p, zm, lv.PathY(zm), cm - w * 0.5f, cm + w * 0.5f, w, d);
            }
            if (Chance(0.65f) && nz < L - 25f)
            {
                for (int i = 1; i <= 4; i++)
                {
                    float t = i / 5f;
                    float z = Mathf.Lerp(zr + 2f, nz - 2f, t);
                    AddCoin(lv, cp, new Vector3(Mathf.Lerp(c, next, t), lv.PathY(z) + 1.2f, z), value);
                }
            }
            if (Chance(0.35f))
            {
                float lw = a + W, rw = W - b;
                float cx = lw > rw ? (-W + a) * 0.5f : (b + W) * 0.5f;
                float z = zr + spacing * 0.5f;
                for (int i = -1; i <= 1; i++)
                    AddCoin(lv, cp, new Vector3(cx + i * 0.7f, lv.PathY(z) + 1.2f, z + i * 1.5f), value);
            }

            prev = c;
            c = next;
            zr = nz;
        }
        for (int i = 0; i < 4; i++)
        {
            float z = Mathf.Lerp(zr, L - 4f, i / 3f);
            float goal = lv.cave ? lv.docks[1].localPosition.x : 0f;
            AddCoin(lv, cp, new Vector3(Mathf.Lerp(c, goal, i / 3f), lv.PathY(z) + 1.2f, z), value);
        }
    }

    // 0 vägghinder, 1 kugghjul, 2 rörligt kugghjul, 3 vindsnurra, 4 tvättlina, 5 spikhjul/droppsten, 6 giftig rök, 7 spikvalsar
    // Exakta träffzoner: varje hinder får en träffyta som är EXAKT samma form som det man ser
    // (varje spik, kuggtand, stolpe och lina). Minsta nudd räknas. Giftröken behåller sitt klot (det är gas).
    static void PreciseColliders(Transform hinder)
    {
        if (hinder == null) return;
        foreach (var hz in hinder.GetComponentsInChildren<Hazard>(true))
        {
            if (hz.name == "GiftigRok") continue;
            foreach (var c in hz.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var mf in hz.GetComponentsInChildren<MeshFilter>(true))
            {
                var r = mf.GetComponent<MeshRenderer>();
                if (r == null || r.sharedMaterial == Toon.Glow || mf.sharedMesh == null) continue;
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            }
        }
    }

    static int PickType(int theme, float d)
    {
        // theme = miljön: 0 staden, 1 öknen, 2 bergsbyn, 3 vattengrottan, 4 snöberget
        bool cave = theme == Cave;
        float[] wt = new float[8];
        wt[0] = 1f;                                                                        // vägg-/klipphinder
        wt[5] = theme == City ? 0.8f : theme == Village ? 0.7f : theme == Snow ? 1.3f : 1f; // spikhjul / droppstenar / istappar
        wt[4] = (theme == City || theme == Village) && d >= 0.05f ? 0.6f : 0f;              // tvättlinor
        wt[1] = theme == City && d >= 0.1f || theme == Desert && d >= 0.3f || theme == Village && d >= 0.3f ? 0.7f : 0f; // kugghjul
        wt[2] = (theme == City || theme == Desert) && d >= 0.3f ? 0.6f : 0f;                // rörliga kugghjul
        wt[3] = theme == Village && d >= 0.1f ? 0.9f : (theme == City || theme == Desert || theme == Snow) && d >= 0.4f ? 0.6f : 0f; // väderkvarnar
        wt[6] = cave && d >= 0.15f ? 0.4f + 0.8f * d : 0f;                                  // giftig rök
        wt[7] = cave && d >= 0.3f ? 1.1f : 0f;                                              // spikvalsar
        float sum = 0f;
        foreach (float v in wt) sum += v;
        float r = R(0f, sum);
        for (int i = 0; i < wt.Length; i++)
            if ((r -= wt[i]) < 0f) return i;
        return 0;
    }

    static GameObject NewHazard(Transform p, string name, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(p, false);
        go.transform.localPosition = pos;
        go.AddComponent<Hazard>();
        return go;
    }

    static void AddBox(GameObject go, Vector3 center, Vector3 size)
    {
        var bc = go.AddComponent<BoxCollider>();
        bc.center = center;
        bc.size = size;
    }

    // skip = -1/+1 hoppar över den sidan (där ett annat hinder redan sitter)
    static void Walls(Transform p, float z, float py, float a, float b, int skip)
    {
        float lw = a + W, rw = W - b;
        if (skip != -1 && lw >= 0.8f) Wall(p, -1, lw, z, py);
        if (skip != 1 && rw >= 0.8f) Wall(p, 1, rw, z, py);
    }

    // Ett hinder som sticker ut från en husvägg: banderoll, byggställning eller balkonger.
    static void Wall(Transform p, int side, float len, float z, float py)
    {
        var go = NewHazard(p, "Hinder", new Vector3(0, py, z));
        var mb = new MeshBuilder { Mat = MeshBuilder.Wood };
        float tip = side * (W - len), root = side * (W + 1.5f), wall = side * W;
        float cx = (tip + root) * 0.5f, sx = Mathf.Abs(root - tip);
        float bx = (tip + wall) * 0.5f, bw = len;
        int style = caveMode || env == Desert || env == Snow ? 3 : RI(0, 3);
        if (style == 3)
        {
            // klippblock som sticker ut från grottväggen
            mb.Mat = RockMat();
            for (float h = -1f; h < 5.4f; h += R(0.9f, 1.5f))
            {
                float l = len * R(0.75f, 1.05f);
                mb.Box(new Vector3(side * (W - l * 0.5f + 0.6f), h, R(-0.2f, 0.2f)), new Vector3(l + 1.2f, R(1.1f, 1.7f), R(1.0f, 1.6f)), RockCol(), Quaternion.Euler(R(-8f, 8f), R(-10f, 10f), R(-6f, 6f)));
            }
            AddBox(go, new Vector3(bx, 2.2f, 0), new Vector3(bw, 5.2f, 1f));
        }
        else if (style == 0)
        {
            // bjälke med en stor banderoll som hänger ner
            Color col = Accents[RI(0, Accents.Length)];
            mb.Box(new Vector3(cx, 5.6f, 0), new Vector3(sx, 0.4f, 0.5f), Wood);
            mb.Box(new Vector3(tip + side * 0.12f, 5.6f, 0), new Vector3(0.24f, 0.5f, 0.6f), Iron);
            mb.Mat = 0;
            mb.Box(new Vector3(bx, 3.15f, 0), new Vector3(bw - 0.1f, 4.7f, 0.06f), col);
            mb.Box(new Vector3(bx, 3.8f, -0.04f), new Vector3(bw - 0.1f, 0.35f, 0.04f), Cream);
            mb.Box(new Vector3(bx, 1.9f, -0.04f), new Vector3(bw - 0.1f, 0.35f, 0.04f), Cream);
            mb.Mat = MeshBuilder.Wood;
            mb.Box(new Vector3(bx, 0.75f, 0), new Vector3(bw - 0.1f, 0.15f, 0.12f), DarkWood);
            AddBox(go, new Vector3(bx, 3.15f, 0), new Vector3(bw, 4.8f, 0.8f));
        }
        else if (style == 1)
        {
            // byggställning från marken och uppåt
            float bottom = -1.5f;
            for (float x = 0.3f; x <= len; x += 1.6f)
            {
                float px = side * (W - Mathf.Min(x, len - 0.1f));
                mb.Box(new Vector3(px, (5.6f + bottom) * 0.5f, 0), new Vector3(0.15f, 5.6f - bottom, 0.15f), Wood);
            }
            foreach (float h in new[] { 0.3f, 2.0f, 3.7f, 5.4f })
                mb.Box(new Vector3(bx, h, 0), new Vector3(bw, 0.14f, 0.6f), Wood);
            mb.Box(new Vector3(bx, 2.85f, 0.05f), new Vector3(bw * 1.1f, 0.12f, 0.08f), DarkWood, Quaternion.Euler(0, 0, side * Mathf.Atan2(5f, bw) * Mathf.Rad2Deg));
            for (float x = 0.5f; x < len - 0.3f; x += R(0.8f, 1.3f))
            {
                float s = R(0.45f, 0.7f);
                float h = Chance(0.5f) ? 0.37f : 2.07f;
                mb.Box(new Vector3(side * (W - x), h + s * 0.5f, 0), new Vector3(s, s, s), Wood * 1.1f, Quaternion.Euler(0, R(-10f, 10f), 0));
            }
            AddBox(go, new Vector3(bx, 2.75f, 0), new Vector3(bw, 5.5f, 0.8f));
        }
        else
        {
            // två balkonger ovanpå varandra med en tät murgröna emellan
            Color slab = new Color(0.86f, 0.8f, 0.7f);
            foreach (float h in new[] { 0.2f, 3.2f })
            {
                mb.Mat = MeshBuilder.Stone;
                mb.Box(new Vector3(cx, h, 0), new Vector3(sx, 0.25f, 1.6f), slab);
                mb.Mat = MeshBuilder.Metal;
                for (float x = 0.2f; x < len; x += 0.35f)
                    mb.Box(new Vector3(side * (W - x), h + 0.5f, -0.72f), new Vector3(0.06f, 0.8f, 0.06f), Iron);
                mb.Box(new Vector3(cx, h + 0.9f, -0.72f), new Vector3(sx, 0.08f, 0.08f), Iron);
                mb.Mat = 0;
                for (float x = 0.5f; x < len - 0.3f; x += 0.9f)
                    mb.Sphere(new Vector3(side * (W - x), h + 0.45f, 0.3f), Vector3.one * 0.25f, Chance(0.5f) ? Danger : new Color(0.4f, 0.47f, 0.25f), 4, 6);
            }
            mb.Box(new Vector3(bx, 2.15f, 0.1f), new Vector3(bw - 0.2f, 2.0f, 0.3f), new Color(0.38f, 0.42f, 0.24f));
            AddBox(go, new Vector3(bx, 2.1f, 0), new Vector3(bw, 4.2f, 0.9f));
        }
        Toon.Make("Mesh", mb.ToMesh("hinder"), go.transform);
    }

    // Stora spikhjul (i grottan: droppstenar) som faller ner ovanifrån eller studsar upp underifrån.
    // De sitter på kanten av korridoren, så det finns alltid plats bredvid – eller så väntar man ut dem.
    static void SpikeWheels(Transform p, float z, float py, float a, float b, float w, float d)
    {
        int count = w >= 5f && Chance(0.5f) ? 2 : 1;
        int first = Chance(0.5f) ? -1 : 1;
        for (int i = 0; i < count; i++)
        {
            int s = i == 0 ? first : -first;
            float cx = s < 0 ? a : b;
            bool drip = caveMode || env == Snow;   // grottan: droppstenar, snöberget: istappar
            var go = NewHazard(p, drip ? (env == Snow ? "Istapp" : "Droppsten") : "Spikhjul", new Vector3(cx, py + Mid, z));
            var f = go.AddComponent<Faller>();
            f.mode = drip ? 0 : (Chance(0.5f) ? 0 : 1);
            f.period = R(2.0f, 2.8f) * Mathf.Lerp(1.0f, 0.6f, d);   // snabbare ju svårare
            f.phase = R(0f, 1f);
            var mb = new MeshBuilder { Mat = drip ? (env == Snow ? 0f : MeshBuilder.Rock) : MeshBuilder.Metal };
            float r = 1.1f;
            if (drip)
            {
                Color c = env == Snow ? Ice : RockCol();
                mb.Box(new Vector3(0, 0.9f, 0), new Vector3(1.6f, 1.2f, 1.6f), c);
                mb.Box(new Vector3(0, -0.1f, 0), new Vector3(1.0f, 1.0f, 1.0f), c);
                mb.Box(new Vector3(0, -0.9f, 0), new Vector3(0.5f, 0.8f, 0.5f), c);
                mb.Box(new Vector3(0, -1.45f, 0), new Vector3(0.2f, 0.4f, 0.2f), c);
                var sc = go.AddComponent<SphereCollider>();
                sc.radius = 0.9f;
                Toon.Make("Mesh", mb.ToMesh("droppsten"), go.transform);
            }
            else
            {
                Quaternion axis = Quaternion.Euler(90f, 0, 0);
                mb.Cylinder(Vector3.zero, r, 0.4f, 20, new Color(0.3f, 0.28f, 0.3f), axis);
                mb.Cylinder(Vector3.zero, r * 0.6f, 0.5f, 16, Danger, axis);
                mb.Cylinder(Vector3.zero, r * 0.2f, 0.6f, 10, Iron, axis);
                for (int k = 0; k < 12; k++)
                {
                    float ang = k * 30f;
                    Vector3 dir = new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad), 0);
                    mb.Box(dir * (r + 0.28f), new Vector3(0.6f, 0.16f, 0.16f), new Color(0.88f, 0.88f, 0.92f), Quaternion.Euler(0, 0, ang));
                }
                var sc = go.AddComponent<SphereCollider>();
                sc.radius = r + 0.35f;
                var wheel = Toon.Make("Hjul", mb.ToMesh("spikhjul"), go.transform);
                wheel.AddComponent<Spinner>().speed = R(120f, 220f) * (Chance(0.5f) ? 1 : -1);
            }
        }
        // vägghinder längre ut så att man inte kan smita runt på sidorna
        if (d > 0.15f || Chance(0.5f)) Walls(p, z, py, a - 1.8f, b + 1.8f, 0);
    }

    // Giftigt rökmoln som dödar direkt (grottan). Ligger på sidan bort från korridoren.
    static int TrySmoke(Transform p, float z, float py, float ua, float ub)
    {
        float lw = ua + W, rw = W - ub;
        int side = lw > rw ? -1 : 1;
        float reg = Mathf.Max(lw, rw);
        float edge = side < 0 ? ua : ub;
        float r = Mathf.Min(2.3f, reg * 0.5f + 0.4f);
        if (r < 1.2f) return 0;
        float cx = edge - side * (r + 0.3f);
        var go = NewHazard(p, "GiftigRok", new Vector3(cx, py + Mid, z));
        go.GetComponent<Hazard>().deadly = true;
        var sc = go.AddComponent<SphereCollider>();
        sc.radius = r;   // hela molnet räknas

        var vis = new GameObject("Moln");
        vis.transform.SetParent(go.transform, false);
        var pulse = vis.AddComponent<Pulse>();
        pulse.phase = R(0f, 6f);
        var mb = new MeshBuilder();
        for (int i = 0; i < 6; i++)
        {
            Vector3 o = new Vector3(R(-0.5f, 0.5f), R(-0.5f, 0.5f), R(-0.4f, 0.4f)) * r;
            float s = r * R(0.45f, 0.7f);
            mb.Sphere(o, Vector3.one * s, new Color(0.45f, 0.9f, 0.2f, 0.32f), 8, 12);
        }
        mb.Sphere(Vector3.zero, Vector3.one * r * 0.4f, new Color(0.7f, 1f, 0.3f, 0.45f), 8, 12);
        Toon.MakeGlow("Sken", mb.ToMesh("rok"), vis.transform);
        // dödskalle-varning
        var skull = new MeshBuilder();
        skull.Sphere(new Vector3(0, 0.1f, -r * 0.5f), new Vector3(0.35f, 0.32f, 0.3f), Cream, 6, 8);
        skull.Box(new Vector3(-0.12f, 0.12f, -r * 0.5f - 0.28f), new Vector3(0.12f, 0.12f, 0.04f), DarkWood);
        skull.Box(new Vector3(0.12f, 0.12f, -r * 0.5f - 0.28f), new Vector3(0.12f, 0.12f, 0.04f), DarkWood);
        skull.Box(new Vector3(0, -0.2f, -r * 0.5f - 0.2f), new Vector3(0.3f, 0.12f, 0.12f), Cream);
        Toon.Make("Varning", skull.ToMesh("skalle"), vis.transform);

        // ibland driver röken fram och tillbaka
        float lo = side < 0 ? -W + r * 0.6f : edge + r + 0.3f;
        float hi = side < 0 ? edge - r - 0.3f : W - r * 0.6f;
        if (hi - lo > 1.5f && Chance(0.5f))
        {
            var mv = go.AddComponent<Mover>();
            mv.minX = lo;
            mv.maxX = hi;
            mv.speed = R(0.5f, 1.1f);
            mv.phase = R(0f, 6f);
        }
        return side;
    }

    // Två jättelika spikvalsar tvärs över grottan, en ovanför och en nedanför.
    // Man måste flyga genom luckan mellan dem – luckan ligger lite olika högt, så man styr upp/ner med W/S.
    static void Drums(Level lv, Transform p, Transform cp, float z, int value)
    {
        float R0 = R(2.6f, 3.6f);
        float spike = 0.85f, Reff = R0 + spike;
        // gubben sjunker längs glidbanan medan han passerar valsen: ge extra marginal för lutningen
        float s = lv.slope;
        float dz = s * Reff / Mathf.Sqrt(1f + s * s);
        float extra = Mathf.Max(0f, s * dz - (Reff - Mathf.Sqrt(Reff * Reff - dz * dz)));
        float half = 2.3f + 0.55f + extra;
        float gapC = lv.PathY(z) + 2.3f + R(-1.2f, 1.2f);
        Drum(p, new Vector3(0, gapC + half + R0, z), R0, spike, 1);
        Drum(p, new Vector3(0, gapC - half - R0, z), R0, spike, -1);
        // mynt mitt i luckan visar vägen
        for (int i = -1; i <= 1; i++) AddCoin(lv, cp, new Vector3(i * 1.6f, gapC - 1.1f, z), value);
    }

    static void Drum(Transform p, Vector3 pos, float r, float spike, int dir)
    {
        var go = NewHazard(p, "Spikvals", pos);
        float len = 2f * W + 3f;
        var col = go.AddComponent<CapsuleCollider>();
        col.direction = 0;
        col.radius = r + spike * 0.75f;
        col.height = len + 2f * col.radius;
        var rot = new GameObject("Rotor");
        rot.transform.SetParent(go.transform, false);
        var sp = rot.AddComponent<Spinner>();
        sp.axis = Vector3.right;
        sp.speed = R(35f, 60f) * dir;
        var mb = new MeshBuilder { Mat = MeshBuilder.Wood };
        Quaternion axis = Quaternion.Euler(0, 0, 90f);
        mb.Cylinder(Vector3.zero, r, len, 32, new Color(0.28f, 0.22f, 0.17f), axis);
        mb.Mat = MeshBuilder.Metal;
        for (float x = -len * 0.5f + 1f; x < len * 0.5f; x += 3.2f)
            mb.Cylinder(new Vector3(x, 0, 0), r + 0.06f, 0.35f, 32, new Color(0.3f, 0.3f, 0.33f), axis); // järnband
        int around = Mathf.RoundToInt(2f * Mathf.PI * r / 1.3f);
        for (float x = -len * 0.5f + 0.8f; x < len * 0.5f; x += 1.6f)
        {
            float off = R(0f, 6.28f);
            for (int i = 0; i < around; i++)
            {
                float a = off + i * Mathf.PI * 2f / around;
                var d = new Vector3(0, Mathf.Cos(a), Mathf.Sin(a));
                mb.Tube(new Vector3(x, 0, 0) + d * (r - 0.05f), new Vector3(x, 0, 0) + d * (r + spike), 0.15f, 0.01f, new Color(0.62f, 0.62f, 0.66f, 0.3f), 6, false);
            }
        }
        Toon.Make("Mesh", mb.ToMesh("spikvals"), rot.transform);
    }

    // Bergsbyn: ett litet korsvirkeshus byggt mot klippan (bara dekor, långt från glidbanan)
    static void VillageHouse(MeshBuilder mb, int side, float fx, float z, float w, float py)
    {
        float hy = Chance(0.5f) ? py + R(8f, 16f) : py - R(6f, 16f);
        float hw = Mathf.Min(w * 0.85f, 4.5f), hz = z + w * 0.5f, hh = R(2.6f, 3.6f), dep = 1.4f;
        float hx = fx - side * (dep * 0.5f - 0.2f);
        Color plaster = Jit(new Color(0.86f, 0.82f, 0.72f), 0.04f);
        mb.Mat = MeshBuilder.Plaster;
        mb.Box(new Vector3(hx, hy, hz), new Vector3(dep, hh, hw), plaster);
        mb.Mat = MeshBuilder.Wood;
        float fxw = hx - side * (dep * 0.5f + 0.03f);
        mb.Box(new Vector3(fxw, hy - hh * 0.5f + 0.08f, hz), new Vector3(0.08f, 0.14f, hw), DarkWood);
        mb.Box(new Vector3(fxw, hy + hh * 0.5f - 0.08f, hz), new Vector3(0.08f, 0.14f, hw), DarkWood);
        for (int k = 0; k <= 3; k++)
            mb.Box(new Vector3(fxw, hy, hz - hw * 0.5f + hw * k / 3f), new Vector3(0.08f, hh, 0.14f), DarkWood);
        mb.Box(new Vector3(fxw, hy, hz), new Vector3(0.08f, 0.12f, hw * 1.1f), DarkWood, Quaternion.Euler(Mathf.Atan2(hh, hw) * Mathf.Rad2Deg, 0, 0));
        mb.Mat = 0;
        mb.Box(new Vector3(fxw - side * 0.02f, hy + 0.25f, hz - hw * 0.22f), new Vector3(0.06f, 0.7f, 0.55f), Glass);
        mb.Box(new Vector3(fxw - side * 0.02f, hy + 0.25f, hz + hw * 0.22f), new Vector3(0.06f, 0.7f, 0.55f), Glass);
        mb.Mat = MeshBuilder.Roof;
        mb.Box(new Vector3(hx - side * 0.25f, hy + hh * 0.5f + 0.45f, hz), new Vector3(dep + 0.8f, 0.18f, hw + 0.5f), Jit(RoofCol, 0.05f), Quaternion.Euler(0, 0, side * 22f));
        mb.Mat = RockMat();
    }

    // Marken längst ner i öknen, bergsbyn och snöberget: sand, gräs eller snö, landningsmål och miljödetaljer.
    static void BuildCanyonGround(Transform root, float L)
    {
        var mb = new MeshBuilder();
        float len = L + 130f, mid = (L + 90f - 40f) * 0.5f;
        float gm = env == Desert ? MeshBuilder.Sand : env == Snow ? MeshBuilder.SnowMat : MeshBuilder.Grass;
        Color gc = env == Desert ? new Color(0.84f, 0.62f, 0.42f) : env == Snow ? SnowWhite : new Color(0.55f, 0.6f, 0.42f);
        mb.Mat = gm;
        mb.Box(new Vector3(0, -0.5f, mid), new Vector3(40f, 1f, len), gc);
        // landningsmål
        mb.Mat = 0;
        mb.Cylinder(new Vector3(0, 0.02f, L), 2.6f, 0.04f, 32, Danger, Quaternion.identity);
        mb.Cylinder(new Vector3(0, 0.04f, L), 1.8f, 0.06f, 32, Cream, Quaternion.identity);
        mb.Cylinder(new Vector3(0, 0.06f, L), 1.0f, 0.08f, 28, Danger, Quaternion.identity);
        mb.Cylinder(new Vector3(0, 0.08f, L), 0.35f, 0.1f, 16, Cream, Quaternion.identity);
        for (int i = 0; i < 26; i++)
        {
            float x = (i % 2 == 0 ? -1f : 1f) * R(3.6f, 6.2f), z = L + R(-45f, 55f);
            if (Mathf.Abs(z - L) < 4f && Mathf.Abs(x) < 4f) continue;
            if (env == Desert)
            {
                if (Chance(0.5f))
                {
                    // kaktus med armar
                    Color cg = Jit(new Color(0.3f, 0.45f, 0.25f), 0.04f);
                    float h = R(1.6f, 3.2f);
                    mb.Mat = 0;
                    mb.Tube(new Vector3(x, 0, z), new Vector3(x, h, z), 0.22f, 0.18f, cg, 10);
                    mb.Tube(new Vector3(x, h * 0.45f, z), new Vector3(x + 0.55f, h * 0.55f, z), 0.13f, 0.12f, cg, 8);
                    mb.Tube(new Vector3(x + 0.55f, h * 0.55f, z), new Vector3(x + 0.55f, h * 0.85f, z), 0.12f, 0.1f, cg, 8);
                }
                else { mb.Mat = MeshBuilder.Sandstone; mb.Sphere(new Vector3(x, 0.2f, z), new Vector3(R(0.5f, 1.2f), R(0.4f, 0.8f), R(0.5f, 1.1f)), RockCol(), 6, 8); }
            }
            else if (env == Snow)
            {
                if (Chance(0.6f))
                {
                    // gran med snö
                    float h = R(2.5f, 4.5f);
                    mb.Mat = MeshBuilder.Wood;
                    mb.Tube(new Vector3(x, 0, z), new Vector3(x, h * 0.3f, z), 0.12f, 0.1f, DarkWood, 8);
                    mb.Mat = 0;
                    for (int k = 0; k < 3; k++)
                    {
                        float y0 = h * (0.2f + k * 0.25f), rr = h * (0.32f - k * 0.08f);
                        mb.Tube(new Vector3(x, y0, z), new Vector3(x, y0 + h * 0.4f, z), rr, 0.02f, new Color(0.16f, 0.3f, 0.22f), 10, false);
                        mb.Tube(new Vector3(x, y0 + h * 0.06f, z), new Vector3(x, y0 + h * 0.3f, z), rr * 0.75f, 0.02f, SnowWhite, 10, false);
                    }
                }
                else { mb.Mat = MeshBuilder.SnowMat; mb.Sphere(new Vector3(x, 0.05f, z), new Vector3(R(0.8f, 1.6f), R(0.3f, 0.6f), R(0.8f, 1.6f)), SnowWhite, 6, 10); }
            }
            else
            {
                if (Chance(0.4f))
                {
                    // höstack
                    mb.Mat = 0;
                    mb.Sphere(new Vector3(x, 0.5f, z), new Vector3(0.8f, 0.7f, 0.8f), new Color(0.78f, 0.66f, 0.36f), 8, 10);
                }
                else
                {
                    // staket
                    mb.Mat = MeshBuilder.Wood;
                    mb.Box(new Vector3(x, 0.5f, z), new Vector3(0.1f, 1f, 0.1f), Wood);
                    mb.Box(new Vector3(x, 0.75f, z + 0.9f), new Vector3(0.06f, 0.08f, 1.8f), Wood);
                    mb.Box(new Vector3(x, 0.4f, z + 0.9f), new Vector3(0.06f, 0.08f, 1.8f), Wood);
                }
            }
        }
        if (env == Village)
        {
            // brunn på torget
            mb.Mat = MeshBuilder.Stone;
            mb.Cylinder(new Vector3(4.6f, 0.45f, L + 8f), 0.9f, 0.9f, 20, new Color(0.62f, 0.6f, 0.55f), Quaternion.identity);
            mb.Mat = MeshBuilder.Wood;
            mb.Box(new Vector3(4.0f, 1.4f, L + 8f), new Vector3(0.1f, 1.9f, 0.1f), DarkWood);
            mb.Box(new Vector3(5.2f, 1.4f, L + 8f), new Vector3(0.1f, 1.9f, 0.1f), DarkWood);
            mb.Mat = MeshBuilder.Roof;
            mb.Box(new Vector3(4.6f, 2.45f, L + 8f), new Vector3(1.8f, 0.12f, 1.4f), RoofCol);
        }
        var ground = Toon.Make("Marken", mb.ToMesh("mark"), root);
        var col = ground.AddComponent<BoxCollider>();
        col.center = new Vector3(0, -5f, mid);
        col.size = new Vector3(40f, 10f, len);
        foreach (int sd in new[] { -1, 1 })
        {
            var wc = ground.AddComponent<BoxCollider>();
            wc.center = new Vector3(sd * (W + 0.6f), 20f, mid);
            wc.size = new Vector3(1f, 40f, len);
        }
    }

    static bool TryLaundry(Transform p, float z, float py, float a, float b)
    {
        float lw = a + W, rw = W - b;
        if (lw < 1.5f && rw < 1.5f) return false;
        if (lw >= 1f) Line(p, -1, lw, z, py);
        if (rw >= 1f) Line(p, 1, rw, z, py);
        return true;
    }

    static void Line(Transform p, int side, float len, float z, float py)
    {
        var go = NewHazard(p, "Tvattlina", new Vector3(0, py, z));
        var mb = new MeshBuilder();
        float tip = side * (W - len), root = side * (W + 0.5f);
        float cx = (tip + root) * 0.5f, sx = Mathf.Abs(root - tip);
        mb.Box(new Vector3(cx, 4.9f, 0), new Vector3(sx, 0.05f, 0.05f), DarkWood);
        mb.Mat = MeshBuilder.Wood;
        mb.Box(new Vector3(tip, 2.4f, 0), new Vector3(0.16f, 5.2f, 0.16f), Wood);
        mb.Mat = 0;
        for (float x = 0.3f; x < len - 0.3f; x += R(0.8f, 1.1f))
        {
            float px = side * (W - x);
            Color col = Chance(0.3f) ? Cream : Accents[RI(0, Accents.Length)];
            float h = R(2.8f, 3.9f);
            mb.Box(new Vector3(px, 4.9f - h * 0.5f, 0), new Vector3(0.85f, h, 0.04f), col, Quaternion.Euler(0, 0, R(-4f, 4f)));
        }
        AddBox(go, new Vector3(cx, 3.1f, 0), new Vector3(sx, 3.7f, 0.8f));
        Toon.Make("Mesh", mb.ToMesh("lina"), go.transform);
    }

    // Kugghjul på sidan bort från korridoren. Returnerar sidan (-1/+1) eller 0.
    static int TryGear(Transform p, float z, float py, float ua, float ub, bool moving)
    {
        float lw = ua + W, rw = W - ub;
        int side = lw > rw ? -1 : 1;
        float reg = Mathf.Max(lw, rw);
        float edge = side < 0 ? ua : ub;
        float r;
        GameObject go;
        var mb = new MeshBuilder() { Mat = MeshBuilder.Metal };
        if (!moving)
        {
            r = Mathf.Min(R(1.6f, 2.8f), (reg + 1f) * 0.5f - 0.1f);
            if (r < 1.4f) return 0;
            float cx = edge - side * (r + 0.15f);
            go = NewHazard(p, "Kugghjul", new Vector3(cx, py + Mid, z));
            float root = side * (W + 1.5f);
            mb.Box(new Vector3((root - cx) * 0.5f, 0, 0.4f), new Vector3(Mathf.Abs(root - cx), 0.35f, 0.3f), Iron);
            AddBox(go, new Vector3((root - cx) * 0.5f, 0, 0), new Vector3(Mathf.Abs(root - cx), 0.3f, 0.8f));
        }
        else
        {
            r = Mathf.Min(R(1.5f, 2.2f), (reg - 1.5f) * 0.5f);
            if (r < 1.3f) return 0;
            float lo = side < 0 ? -W + r : edge + r + 0.15f;
            float hi = side < 0 ? edge - r - 0.15f : W - r;
            if (hi - lo < 0.5f) return 0;
            go = NewHazard(p, "RorligtKugghjul", new Vector3(lo, py + Mid, z));
            var mv = go.AddComponent<Mover>();
            mv.minX = lo;
            mv.maxX = hi;
            mv.speed = R(0.9f, 1.5f) * Mathf.Lerp(1f, 1.7f, curD);
            mv.phase = R(0f, 6.28f);
            mb.Box(new Vector3(0, 4f, 0.3f), new Vector3(0.12f, 8f, 0.12f), Iron);
        }
        var sc = go.AddComponent<SphereCollider>();
        sc.radius = r + 0.15f;
        Toon.Make("Fäste", mb.ToMesh("faste"), go.transform);

        var gm = new MeshBuilder { Mat = MeshBuilder.Metal };
        gm.Gear(Vector3.zero, r, Chance(0.5f) ? Brass : new Color(0.55f, 0.55f, 0.6f), DarkWood);
        var wheel = Toon.Make("Hjul", gm.ToMesh("kugghjul"), go.transform);
        var sp = wheel.AddComponent<Spinner>();
        sp.speed = R(80f, 150f) * Mathf.Lerp(1f, 1.8f, curD) * (Chance(0.5f) ? 1 : -1);
        return side;
    }

    // Roterande vindsnurra med armar, vänd mot spelaren.
    static int TrySpinner(Transform p, float z, float py, float ua, float ub)
    {
        float lw = ua + W, rw = W - ub;
        int side = lw > rw ? -1 : 1;
        float reg = Mathf.Max(lw, rw);
        float edge = side < 0 ? ua : ub;
        float rad = Mathf.Min(4.2f, reg * 0.5f + 0.65f);
        if (rad < 2.2f) return 0;
        float cx = edge - side * (rad + 0.15f);
        var go = NewHazard(p, "Vindsnurra", new Vector3(cx, py + Mid, z));

        var mount = new MeshBuilder { Mat = MeshBuilder.Wood };
        float root = side * (W + 1.5f);
        mount.Box(new Vector3((root - cx) * 0.5f, 0, 0.5f), new Vector3(Mathf.Abs(root - cx), 0.4f, 0.3f), DarkWood);
        Toon.Make("Fäste", mount.ToMesh("faste"), go.transform);

        var rot = new GameObject("Rotor");
        rot.transform.SetParent(go.transform, false);
        var sp = rot.AddComponent<Spinner>();
        sp.speed = R(70f, 120f) * Mathf.Lerp(1f, 1.7f, curD) * (Chance(0.5f) ? 1 : -1);
        int arms = Chance(0.5f) ? 2 : 3;
        var mb = new MeshBuilder();
        mb.Mat = MeshBuilder.Metal;
        mb.Cylinder(Vector3.zero, 0.45f, 0.6f, 14, Iron, Quaternion.Euler(90f, 0, 0));
        mb.Mat = MeshBuilder.Wood;
        for (int i = 0; i < arms; i++)
        {
            float a = i * 360f / arms;
            Quaternion q = Quaternion.Euler(0, 0, a);
            mb.Box(q * new Vector3(rad * 0.5f, 0, 0), new Vector3(rad, 0.5f, 0.35f), Wood, q);
            mb.Box(q * new Vector3(rad - 0.3f, 0, 0), new Vector3(0.6f, 0.7f, 0.45f), Danger, q);
            var col = new GameObject("Arm" + i);
            col.transform.SetParent(rot.transform, false);
            col.transform.localRotation = q;
            var bc = col.AddComponent<BoxCollider>();
            bc.center = new Vector3(rad * 0.5f, 0, 0);
            bc.size = new Vector3(rad, 0.55f, 0.8f);
        }
        Toon.Make("Mesh", mb.ToMesh("snurra"), rot.transform);
        var hub = go.AddComponent<SphereCollider>();
        hub.radius = 0.45f;
        return side;
    }

    // Ett detaljerat guldmynt: räfflad kant, upphöjd ram, pärlring och ett "B" i mitten på båda sidor.
    // Myntet ligger i XY-planet med ytorna mot ±z.
    public static Mesh CoinMesh()
    {
        var mb = new MeshBuilder();
        Color gold = new Color(0.85f, 0.6f, 0.15f, 0.12f);      // blank guldyta
        Color dark = new Color(0.6f, 0.4f, 0.09f, 0.16f);    // räfflor och mönster
        Color bright = new Color(0.98f, 0.78f, 0.32f, 0.06f);     // extra blankt "B"
        const float R0 = 0.36f, T = 0.09f;
        mb.Cylinder(Vector3.zero, R0, T, 48, gold, Quaternion.Euler(90f, 0, 0));

        // räfflad kant: små streck tvärs över kanten runt hela myntet
        for (int i = 0; i < 60; i++)
        {
            float a = i * 6f;
            var d = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad), 0);
            mb.Box(d * (R0 + 0.004f), new Vector3(0.012f, 0.014f, T * 0.92f), dark, Quaternion.Euler(0, 0, a));
        }

        foreach (int side in new[] { 1, -1 })
        {
            float z = side * (T * 0.5f + 0.006f);
            // upphöjd ram längs kanten
            for (int i = 0; i < 48; i++)
            {
                float a = i * 7.5f;
                var d = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad), 0);
                mb.Box(d * 0.327f + Vector3.forward * z, new Vector3(0.03f, 0.046f, 0.014f), gold, Quaternion.Euler(0, 0, a));
            }
            // pärlring
            for (int i = 0; i < 28; i++)
            {
                float a = i * (360f / 28f);
                var d = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad), 0);
                mb.Box(d * 0.268f + Vector3.forward * z, new Vector3(0.016f, 0.016f, 0.01f), dark, Quaternion.Euler(0, 0, a + 45f));
            }
            // tunn inre cirkel
            for (int i = 0; i < 40; i++)
            {
                float a = i * 9f;
                var d = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad), 0);
                mb.Box(d * 0.228f + Vector3.forward * z, new Vector3(0.006f, 0.038f, 0.008f), dark, Quaternion.Euler(0, 0, a));
            }
            Letter_B(mb, side, side * (T * 0.5f + 0.01f), bright);
        }
        return mb.ToMesh("mynt");
    }

    // Ett upphöjt "B" mitt på myntet. side = -1 speglar bokstaven så den läses rätt från baksidan.
    static void Letter_B(MeshBuilder mb, int side, float z, Color col)
    {
        const float th = 0.034f, depth = 0.016f;       // linjens tjocklek och höjd
        float m = side;                                 // spegling i x på baksidan
        Vector3 P(float x, float y) => new Vector3(-m * x, y, z);   // x speglas så att B:et läses rätt framifrån
        float sx = -0.055f;                             // stammens x
        // stam
        mb.Box(P(sx, 0f), new Vector3(th, 0.23f, depth), col);
        // övre och undre bågar (högersidan av B:et)
        void Bowl(float cy, float r, float cx)
        {
            mb.Box(P((sx + cx) * 0.5f, cy + r), new Vector3(cx - sx + 0.002f, th, depth), col);   // överkant
            mb.Box(P((sx + cx) * 0.5f, cy - r), new Vector3(cx - sx + 0.002f, th, depth), col);   // underkant
            int n = 12;
            for (int i = 0; i < n; i++)
            {
                float a0 = 90f - 180f * i / n, a1 = 90f - 180f * (i + 1) / n, am = (a0 + a1) * 0.5f;
                float x = cx + Mathf.Cos(am * Mathf.Deg2Rad) * r, y = cy + Mathf.Sin(am * Mathf.Deg2Rad) * r;
                float seg = 2f * r * Mathf.Sin(Mathf.PI / n) + 0.004f;
                mb.Box(P(x, y), new Vector3(th, seg, depth), col, Quaternion.Euler(0, 0, -m * am));
            }
        }
        Bowl(0.0555f, 0.0545f - th * 0.5f + 0.017f, -0.005f);
        Bowl(-0.0555f, 0.0575f - th * 0.5f + 0.017f, 0.0f);
    }

    static void AddCoin(Level lv, Transform p, Vector3 pos, int value)
    {
        if (coinMesh == null) coinMesh = CoinMesh();
        var go = Toon.MakeSmooth("Mynt", coinMesh, p);
        go.transform.localPosition = new Vector3(Mathf.Clamp(pos.x, -W + 0.5f, W - 0.5f), pos.y, pos.z);
        var sc = go.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 0.6f;
        go.AddComponent<Coin>().value = value;
        var sp = go.AddComponent<Spinner>();
        sp.axis = Vector3.up;
        sp.speed = 160f;
        lv.coins.Add(go);
    }
}
