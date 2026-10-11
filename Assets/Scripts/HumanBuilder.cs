using System.Collections.Generic;
using UnityEngine;

// Bygger gubben som EN sammanhängande kropp med ett skelett inuti (skinned mesh),
// precis som riktiga spelfigurer. När skelettet rör sig böjer sig ytan mjukt som hud och tyg,
// i stället för att separata delar vrids mot varandra som på en docka.
// Kroppen byggs av tvärsnitt (ringar) på olika höjder – som en skulptör som bygger upp formen.
// Framsidan är lokal -z, fötterna står på y = 0, längd ca 1,8 m.
public class HumanBuilder
{
    public enum B { Hips, Spine, Chest, Neck, Head, ShoulderL, ElbowL, HandL, ShoulderR, ElbowR, HandR, ThighL, KneeL, FootL, ThighR, KneeR, FootR, Count }

    static readonly Vector3[] Rest =
    {
        new Vector3(0, 0.95f, 0), new Vector3(0, 1.08f, 0), new Vector3(0, 1.27f, 0), new Vector3(0, 1.45f, 0.01f), new Vector3(0, 1.54f, 0.01f),
        new Vector3(-0.17f, 1.41f, 0.01f), new Vector3(-0.2f, 1.13f, 0.01f), new Vector3(-0.21f, 0.92f, 0.01f),
        new Vector3(0.17f, 1.41f, 0.01f), new Vector3(0.2f, 1.13f, 0.01f), new Vector3(0.21f, 0.92f, 0.01f),
        new Vector3(-0.097f, 0.93f, 0), new Vector3(-0.1f, 0.5f, 0), new Vector3(-0.1f, 0.09f, 0),
        new Vector3(0.097f, 0.93f, 0), new Vector3(0.1f, 0.5f, 0), new Vector3(0.1f, 0.09f, 0),
    };
    static readonly int[] Parent = { -1, 0, 1, 2, 3, 2, 5, 6, 2, 8, 9, 0, 11, 12, 0, 14, 15 };

    // material (negativt = tyg som följer kroppen, se BallongLit.shader)
    const float Tweed = -10, Cord = -11, Jersey = -12, Linen = -13, Leather = -14, HairTex = -15, SkinMat = 0;

    static readonly Color Jacket = new Color(0.4f, 0.33f, 0.26f), Knickers = new Color(0.56f, 0.45f, 0.32f);
    static readonly Color Sock = new Color(0.27f, 0.32f, 0.29f), Vest = new Color(0.62f, 0.5f, 0.26f);
    static readonly Color Shirt = new Color(0.86f, 0.84f, 0.79f), ShoeCol = new Color(0.42f, 0.27f, 0.16f);
    static readonly Color Skin = new Color(0.93f, 0.75f, 0.63f), Stubble = new Color(0.8f, 0.66f, 0.57f);
    static readonly Color Hair = new Color(0.55f, 0.31f, 0.16f), Lip = new Color(0.74f, 0.47f, 0.42f);

    public readonly Transform[] bones = new Transform[(int)B.Count];
    readonly List<Vector3> verts = new List<Vector3>();
    readonly List<Vector3> norms = new List<Vector3>();
    readonly List<Color> cols = new List<Color>();
    readonly List<Vector2> mats = new List<Vector2>();
    readonly List<Vector2> cloth = new List<Vector2>();
    readonly List<BoneWeight> weights = new List<BoneWeight>();
    readonly List<int> tris = new List<int>();

    public Transform this[B b] => bones[(int)b];

    struct Ring
    {
        public float y, rx, rz, x, z;
        public Color col;
        public float mat;
        public Ring(float y, float rx, float rz, float x, float z, Color col, float mat)
        { this.y = y; this.rx = rx; this.rz = rz; this.x = x; this.z = z; this.col = col; this.mat = mat; }
    }

    delegate void Weigh(float y, out int b0, out int b1, out float w);

    public SkinnedMeshRenderer Build(Transform root, Material material)
    {
        for (int i = 0; i < (int)B.Count; i++)
        {
            var t = new GameObject(((B)i).ToString()).transform;
            t.SetParent(Parent[i] < 0 ? root : bones[Parent[i]], false);
            t.localPosition = Rest[i] - (Parent[i] < 0 ? Vector3.zero : Rest[Parent[i]]);
            bones[i] = t;
        }

        BuildTorso();
        BuildLeg(-1);
        BuildLeg(1);
        BuildArm(-1);
        BuildArm(1);
        BuildHead();

        var mesh = new Mesh { name = "gubbe" };
        if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetColors(cols);
        mesh.SetUVs(0, mats);
        mesh.SetUVs(2, cloth);
        mesh.SetTriangles(tris, 0);
        mesh.boneWeights = weights.ToArray();

        var go = new GameObject("Kropp");
        go.transform.SetParent(root, false);
        var bind = new Matrix4x4[bones.Length];
        for (int i = 0; i < bones.Length; i++) bind[i] = bones[i].worldToLocalMatrix * go.transform.localToWorldMatrix;
        mesh.bindposes = bind;
        mesh.RecalculateBounds();

        var smr = go.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = mesh;
        smr.bones = bones;
        smr.rootBone = bones[0];
        smr.sharedMaterial = material;
        smr.updateWhenOffscreen = true;
        smr.quality = SkinQuality.Bone2;
        return smr;
    }

    // ---------------------------------------------------------------- kroppsdelar

    void BuildTorso()
    {
        // kavaj från höften upp till axlarna – smal midja, bredare bröst och axlar, skört som vidgar sig
        float[,] spec =
        {
            // y      rx      rz      z
            { 0.70f, 0.205f, 0.152f, 0.012f },
            { 0.78f, 0.198f, 0.146f, 0.01f },
            { 0.88f, 0.186f, 0.136f, 0.008f },
            { 0.98f, 0.17f, 0.124f, 0.004f },
            { 1.08f, 0.16f, 0.117f, 0f },
            { 1.18f, 0.167f, 0.121f, -0.002f },
            { 1.27f, 0.183f, 0.126f, -0.003f },
            { 1.34f, 0.196f, 0.124f, 0.002f },
            { 1.39f, 0.2f, 0.117f, 0.008f },
            { 1.425f, 0.17f, 0.1f, 0.012f },
            { 1.45f, 0.1f, 0.075f, 0.012f },
            { 1.465f, 0.06f, 0.055f, 0.012f },
        };
        var rings = new List<Ring>();
        for (int i = 0; i < spec.GetLength(0); i++)
            rings.Add(new Ring(spec[i, 0], spec[i, 1], spec[i, 2], 0, spec[i, 3], Jacket, Tweed));
        Sweep(rings, 28, TorsoW, true, false);

        // väst och skjortkrage i den öppna kavajen, knappar, fickor
        Blob(new Vector3(0, 1.33f, -0.108f), new Vector3(0.062f, 0.085f, 0.016f), Vest, Jersey, B.Chest);
        Blob(new Vector3(-0.03f, 1.435f, -0.07f), new Vector3(0.034f, 0.014f, 0.03f), Shirt, Linen, B.Chest, Quaternion.Euler(0, 20f, -25f));
        Blob(new Vector3(0.03f, 1.435f, -0.07f), new Vector3(0.034f, 0.014f, 0.03f), Shirt, Linen, B.Chest, Quaternion.Euler(0, -20f, 25f));
        Blob(new Vector3(-0.065f, 1.3f, -0.116f), new Vector3(0.022f, 0.11f, 0.008f), Jacket * 0.8f, Tweed, B.Chest, Quaternion.Euler(0, 0, -14f)); // slag
        Blob(new Vector3(0.065f, 1.3f, -0.116f), new Vector3(0.022f, 0.11f, 0.008f), Jacket * 0.8f, Tweed, B.Chest, Quaternion.Euler(0, 0, 14f));
        for (int i = 0; i < 3; i++)
            Blob(new Vector3(0, 1.17f - i * 0.085f, -0.12f + i * 0.002f), Vector3.one * 0.009f, new Color(0.2f, 0.15f, 0.1f), SkinMat, i == 0 ? B.Spine : B.Hips);
        Blob(new Vector3(-0.11f, 0.9f, -0.13f), new Vector3(0.05f, 0.012f, 0.012f), Jacket * 0.75f, Tweed, B.Hips);
        Blob(new Vector3(0.11f, 0.9f, -0.13f), new Vector3(0.05f, 0.012f, 0.012f), Jacket * 0.75f, Tweed, B.Hips);
        Blob(new Vector3(0.1f, 1.0f, -0.124f), new Vector3(0.035f, 0.035f, 0.006f), Knickers * 0.9f, Cord, B.Hips); // lapp
    }

    void BuildLeg(int side)
    {
        float x = side * 0.1f;
        float[,] spec =
        {
            // y      rx      rz      z
            { 0.06f, 0.036f, 0.04f, 0f },
            { 0.13f, 0.036f, 0.042f, 0.003f },
            { 0.24f, 0.046f, 0.05f, 0.008f },
            { 0.33f, 0.054f, 0.058f, 0.012f },  // vaden
            { 0.41f, 0.05f, 0.054f, 0.006f },
            { 0.455f, 0.053f, 0.057f, 0.004f },
            { 0.462f, 0.068f, 0.072f, 0.004f }, // knickersens kant
            { 0.5f, 0.082f, 0.086f, 0.004f },   // pösen vid knät
            { 0.56f, 0.085f, 0.089f, 0.002f },
            { 0.64f, 0.08f, 0.085f, 0f },
            { 0.75f, 0.083f, 0.088f, -0.004f },
            { 0.86f, 0.088f, 0.093f, -0.004f },
            { 0.96f, 0.09f, 0.095f, 0f },
            { 1.0f, 0.06f, 0.06f, 0f },
        };
        var rings = new List<Ring>();
        for (int i = 0; i < spec.GetLength(0); i++)
        {
            bool sock = spec[i, 0] < 0.46f;
            Color c = sock ? (spec[i, 0] > 0.44f ? Sock * 1.35f : Sock) : Knickers;
            float xx = Mathf.Lerp(0.1f, 0.097f, Mathf.InverseLerp(0.5f, 0.95f, spec[i, 0])) * side;
            rings.Add(new Ring(spec[i, 0], spec[i, 1], spec[i, 2], xx, spec[i, 3], c, sock ? Jersey : Cord));
        }
        B thigh = side < 0 ? B.ThighL : B.ThighR, knee = side < 0 ? B.KneeL : B.KneeR, foot = side < 0 ? B.FootL : B.FootR;
        Sweep(rings, 18, (float y, out int b0, out int b1, out float w) =>
        {
            if (y < 0.1f) { b0 = b1 = (int)foot; w = 0; }
            else if (y < 0.16f) { b0 = (int)foot; b1 = (int)knee; w = (y - 0.1f) / 0.06f; }
            else if (y < 0.47f) { b0 = b1 = (int)knee; w = 0; }
            else if (y < 0.55f) { b0 = (int)knee; b1 = (int)thigh; w = (y - 0.47f) / 0.08f; }
            else if (y < 0.9f) { b0 = b1 = (int)thigh; w = 0; }
            else { b0 = (int)thigh; b1 = (int)B.Hips; w = Mathf.Clamp01((y - 0.9f) / 0.1f); }
        }, false, false);

        // sko av läder: tå, häl och sula
        Blob(new Vector3(x, 0.058f, -0.045f), new Vector3(0.052f, 0.055f, 0.125f), ShoeCol, Leather, foot);
        Blob(new Vector3(x, 0.055f, 0.03f), new Vector3(0.045f, 0.055f, 0.05f), ShoeCol, Leather, foot);
        Blob(new Vector3(x, 0.012f, -0.035f), new Vector3(0.056f, 0.013f, 0.135f), ShoeCol * 0.45f, Leather, foot);
        Blob(new Vector3(x, 0.105f, -0.06f), new Vector3(0.03f, 0.008f, 0.04f), ShoeCol * 0.7f, Leather, foot); // snörning
    }

    void BuildArm(int side)
    {
        float[,] spec =
        {
            // y      rx      rz
            { 0.945f, 0.037f, 0.033f },
            { 0.955f, 0.045f, 0.043f },
            { 1.03f, 0.043f, 0.041f },
            { 1.12f, 0.047f, 0.045f },
            { 1.2f, 0.051f, 0.049f },
            { 1.3f, 0.055f, 0.053f },
            { 1.38f, 0.061f, 0.059f },
            { 1.43f, 0.05f, 0.05f },
            { 1.455f, 0.022f, 0.022f },
        };
        var rings = new List<Ring>();
        for (int i = 0; i < spec.GetLength(0); i++)
        {
            float y = spec[i, 0];
            float x = y < 1.13f ? Mathf.Lerp(0.21f, 0.2f, Mathf.InverseLerp(0.93f, 1.13f, y)) : Mathf.Lerp(0.2f, 0.175f, Mathf.InverseLerp(1.13f, 1.43f, y));
            bool cuff = i == 0;
            rings.Add(new Ring(y, spec[i, 1], spec[i, 2], x * side, 0.01f, cuff ? Shirt : Jacket, cuff ? Linen : Tweed));
        }
        B sh = side < 0 ? B.ShoulderL : B.ShoulderR, el = side < 0 ? B.ElbowL : B.ElbowR, hand = side < 0 ? B.HandL : B.HandR;
        Sweep(rings, 16, (float y, out int b0, out int b1, out float w) =>
        {
            if (y < 0.96f) { b0 = (int)hand; b1 = (int)el; w = Mathf.Clamp01((y - 0.93f) / 0.04f); }
            else if (y < 1.1f) { b0 = b1 = (int)el; w = 0; }
            else if (y < 1.18f) { b0 = (int)el; b1 = (int)sh; w = (y - 1.1f) / 0.08f; }
            else if (y < 1.4f) { b0 = b1 = (int)sh; w = 0; }
            else { b0 = (int)sh; b1 = (int)B.Chest; w = Mathf.Clamp01((y - 1.4f) / 0.05f); }
        }, true, false);

        // armbågslapp och hand med tumme och böjda fingrar
        Blob(new Vector3(side * 0.2f, 1.11f, 0.05f), new Vector3(0.03f, 0.04f, 0.01f), Knickers * 0.85f, Cord, el);
        float hx = side * 0.21f;
        Blob(new Vector3(hx, 0.89f, 0.01f), new Vector3(0.03f, 0.05f, 0.022f), Skin, SkinMat, hand);
        Blob(new Vector3(hx, 0.85f, 0.0f), new Vector3(0.029f, 0.03f, 0.024f), Skin, SkinMat, hand);
        for (int f = 0; f < 4; f++)
            Blob(new Vector3(hx, 0.832f, -0.02f + f * 0.012f), new Vector3(0.009f, 0.022f, 0.006f), Skin, SkinMat, hand);
        Blob(new Vector3(hx - side * 0.02f, 0.88f, -0.022f), new Vector3(0.011f, 0.03f, 0.011f), Skin, SkinMat, hand, Quaternion.Euler(20f, 0, side * 25f));
    }

    void BuildHead()
    {
        // hals
        var neck = new List<Ring>
        {
            new Ring(1.44f, 0.05f, 0.048f, 0, 0.012f, Skin, SkinMat),
            new Ring(1.5f, 0.046f, 0.045f, 0, 0.012f, Skin, SkinMat),
            new Ring(1.56f, 0.045f, 0.044f, 0, 0.012f, Skin, SkinMat),
        };
        Sweep(neck, 14, (float y, out int b0, out int b1, out float w) => { b0 = (int)B.Neck; b1 = (int)B.Head; w = Mathf.Clamp01((y - 1.5f) / 0.06f); }, false, false);

        // huvudet som EN form: haka, käke, kinder, panna och bakhuvud
        float[,] spec =
        {
            // y       rx      rz      z
            { 1.505f, 0.025f, 0.02f, -0.055f },
            { 1.52f, 0.05f, 0.045f, -0.045f },
            { 1.545f, 0.07f, 0.07f, -0.025f },
            { 1.575f, 0.083f, 0.09f, -0.01f },
            { 1.61f, 0.09f, 0.1f, 0f },
            { 1.645f, 0.094f, 0.105f, 0.005f },
            { 1.68f, 0.093f, 0.104f, 0.01f },
            { 1.715f, 0.086f, 0.098f, 0.014f },
            { 1.745f, 0.07f, 0.085f, 0.016f },
            { 1.77f, 0.045f, 0.058f, 0.018f },
            { 1.783f, 0.015f, 0.02f, 0.018f },
        };
        var head = new List<Ring>();
        for (int i = 0; i < spec.GetLength(0); i++)
            head.Add(new Ring(spec[i, 0], spec[i, 1], spec[i, 2], 0, spec[i, 3], spec[i, 0] < 1.58f ? Color.Lerp(Stubble, Skin, 0.3f) : Skin, SkinMat));
        Sweep(head, 28, (float y, out int b0, out int b1, out float w) => { b0 = b1 = (int)B.Head; w = 0; }, true, true);

        // ansikte
        B h = B.Head;
        Blob(new Vector3(0, 1.533f, -0.068f), new Vector3(0.034f, 0.024f, 0.028f), Color.Lerp(Stubble, Skin, 0.3f), SkinMat, h); // haka
        Blob(new Vector3(0, 1.6f, -0.108f), new Vector3(0.016f, 0.032f, 0.026f), Skin * 0.98f, SkinMat, h, Quaternion.Euler(-15f, 0, 0)); // näsrygg
        Blob(new Vector3(0, 1.582f, -0.118f), new Vector3(0.02f, 0.013f, 0.017f), Skin * 0.97f, SkinMat, h);                             // nästipp
        Blob(new Vector3(-0.013f, 1.578f, -0.11f), new Vector3(0.01f, 0.008f, 0.01f), Skin * 0.95f, SkinMat, h);                         // näsvingar
        Blob(new Vector3(0.013f, 1.578f, -0.11f), new Vector3(0.01f, 0.008f, 0.01f), Skin * 0.95f, SkinMat, h);
        Blob(new Vector3(-0.092f, 1.62f, 0.008f), new Vector3(0.013f, 0.03f, 0.022f), Skin, SkinMat, h, Quaternion.Euler(0, -15f, 0));   // öron
        Blob(new Vector3(0.092f, 1.62f, 0.008f), new Vector3(0.013f, 0.03f, 0.022f), Skin, SkinMat, h, Quaternion.Euler(0, 15f, 0));
        foreach (int s in new[] { -1, 1 })
        {
            float ex = s * 0.036f;
            Blob(new Vector3(ex, 1.643f, -0.093f), new Vector3(0.019f, 0.012f, 0.009f), new Color(0.95f, 0.94f, 0.91f, 0.3f), SkinMat, h); // ögonvita
            Blob(new Vector3(ex, 1.643f, -0.1f), Vector3.one * 0.0085f, new Color(0.32f, 0.42f, 0.5f, 0.2f), SkinMat, h);                    // iris
            Blob(new Vector3(ex, 1.643f, -0.1045f), Vector3.one * 0.004f, new Color(0.04f, 0.03f, 0.03f, 0.1f), SkinMat, h);                  // pupill
            Blob(new Vector3(ex, 1.652f, -0.094f), new Vector3(0.022f, 0.006f, 0.012f), Skin * 0.93f, SkinMat, h);                         // ögonlock
            Blob(new Vector3(ex, 1.668f, -0.097f), new Vector3(0.024f, 0.0055f, 0.008f), Hair * 0.75f, SkinMat, h, Quaternion.Euler(0, 0, s * -8f)); // ögonbryn
            Blob(new Vector3(s * 0.055f, 1.6f, -0.08f), new Vector3(0.025f, 0.02f, 0.02f), Color.Lerp(Skin, new Color(0.9f, 0.6f, 0.55f), 0.25f), SkinMat, h); // kinder
        }
        Blob(new Vector3(0, 1.557f, -0.097f), new Vector3(0.022f, 0.0055f, 0.008f), Lip, SkinMat, h);   // överläpp
        Blob(new Vector3(0, 1.55f, -0.094f), new Vector3(0.019f, 0.006f, 0.009f), Lip * 1.05f, SkinMat, h); // underläpp

        // hår: rufsig kalott, nacke, lugg åt sidan och polisonger
        var hair = new List<Ring>
        {
            new Ring(1.665f, 0.098f, 0.106f, 0, 0.022f, Hair, HairTex),
            new Ring(1.7f, 0.0995f, 0.11f, 0, 0.015f, Hair, HairTex),
            new Ring(1.735f, 0.095f, 0.107f, 0, 0.014f, Hair, HairTex),
            new Ring(1.765f, 0.079f, 0.093f, 0, 0.016f, Hair, HairTex),
            new Ring(1.79f, 0.051f, 0.065f, 0, 0.018f, Hair, HairTex),
            new Ring(1.804f, 0.015f, 0.02f, 0, 0.018f, Hair, HairTex),
        };
        Sweep(hair, 24, (float y, out int b0, out int b1, out float w) => { b0 = b1 = (int)B.Head; w = 0; }, false, true);
        Blob(new Vector3(0, 1.62f, 0.05f), new Vector3(0.088f, 0.075f, 0.06f), Hair, HairTex, h);
        Blob(new Vector3(-0.03f, 1.705f, -0.09f), new Vector3(0.052f, 0.02f, 0.025f), Hair, HairTex, h, Quaternion.Euler(0, 0, 16f));
        Blob(new Vector3(0.035f, 1.712f, -0.086f), new Vector3(0.046f, 0.019f, 0.024f), Hair, HairTex, h, Quaternion.Euler(0, 0, -10f));
        Blob(new Vector3(0.0f, 1.79f, -0.02f), new Vector3(0.04f, 0.02f, 0.05f), Hair, HairTex, h, Quaternion.Euler(-20f, 0, 10f));
        Blob(new Vector3(-0.092f, 1.615f, -0.012f), new Vector3(0.009f, 0.03f, 0.017f), Hair, HairTex, h);
        Blob(new Vector3(0.092f, 1.615f, -0.012f), new Vector3(0.009f, 0.03f, 0.017f), Hair, HairTex, h);
    }

    static void TorsoW(float y, out int b0, out int b1, out float w)
    {
        if (y < 0.98f) { b0 = b1 = (int)B.Hips; w = 0; }
        else if (y < 1.12f) { b0 = (int)B.Hips; b1 = (int)B.Spine; w = (y - 0.98f) / 0.14f; }
        else if (y < 1.26f) { b0 = (int)B.Spine; b1 = (int)B.Chest; w = (y - 1.12f) / 0.14f; }
        else if (y < 1.43f) { b0 = b1 = (int)B.Chest; w = 0; }
        else { b0 = (int)B.Chest; b1 = (int)B.Neck; w = Mathf.Clamp01((y - 1.43f) / 0.05f); }
    }

    // ---------------------------------------------------------------- byggstenar

    void Add(Vector3 p, Vector3 n, Color c, float mat, Vector2 uv, int b0, int b1, float w)
    {
        verts.Add(p);
        norms.Add(n.normalized);
        cols.Add(c);
        mats.Add(new Vector2(mat, 0));
        cloth.Add(uv);
        var bw = new BoneWeight { boneIndex0 = b0, weight0 = 1f - w, boneIndex1 = b1, weight1 = w };
        if (b0 == b1) { bw.weight0 = 1f; bw.weight1 = 0f; }
        weights.Add(bw);
    }

    // Tvärsnitt nerifrån och upp, sammanfogade till en mjuk yta.
    void Sweep(List<Ring> rings, int seg, Weigh weigh, bool capBottom, bool capTop)
    {
        int start = verts.Count;
        int n = rings.Count;
        for (int k = 0; k < n; k++)
        {
            var r = rings[k];
            var a = rings[Mathf.Max(0, k - 1)];
            var b = rings[Mathf.Min(n - 1, k + 1)];
            float dy = Mathf.Max(0.001f, b.y - a.y);
            float drx = (a.rx - b.rx) / dy, drz = (a.rz - b.rz) / dy;
            weigh(r.y, out int b0, out int b1, out float w);
            float per = Mathf.PI * (r.rx + r.rz);
            for (int i = 0; i <= seg; i++)
            {
                float t = i * Mathf.PI * 2f / seg;
                float dx = Mathf.Cos(t), dz = Mathf.Sin(t);
                var p = new Vector3(r.x + dx * r.rx, r.y, r.z + dz * r.rz);
                var nn = new Vector3(dx * r.rz, 0, dz * r.rx).normalized;
                nn.y = dx * dx * drx + dz * dz * drz;
                Add(p, nn, r.col, r.mat, new Vector2(per * i / seg, r.y), b0, b1, w);
            }
        }
        for (int k = 0; k < n - 1; k++)
        {
            for (int i = 0; i < seg; i++)
            {
                int A = start + k * (seg + 1) + i, Bv = A + seg + 1;
                tris.Add(A); tris.Add(Bv); tris.Add(A + 1);
                tris.Add(A + 1); tris.Add(Bv); tris.Add(Bv + 1);
            }
        }
        if (capBottom) Cap(rings[0], seg, weigh, false);
        if (capTop) Cap(rings[n - 1], seg, weigh, true);
    }

    void Cap(Ring r, int seg, Weigh weigh, bool top)
    {
        weigh(r.y, out int b0, out int b1, out float w);
        Vector3 n = top ? Vector3.up : Vector3.down;
        int c = verts.Count;
        Add(new Vector3(r.x, r.y, r.z), n, r.col, r.mat, Vector2.zero, b0, b1, w);
        for (int i = 0; i <= seg; i++)
        {
            float t = i * Mathf.PI * 2f / seg;
            Add(new Vector3(r.x + Mathf.Cos(t) * r.rx, r.y, r.z + Mathf.Sin(t) * r.rz), n, r.col, r.mat, new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * r.rx, b0, b1, w);
        }
        for (int i = 0; i < seg; i++)
        {
            if (top) { tris.Add(c); tris.Add(c + i + 2); tris.Add(c + i + 1); }
            else { tris.Add(c); tris.Add(c + i + 1); tris.Add(c + i + 2); }
        }
    }

    // Liten mjuk form (ellipsoid) som sitter på ett ben i skelettet.
    void Blob(Vector3 c, Vector3 radii, Color col, float mat, B bone, Quaternion? rotation = null, int rings = 8, int segs = 12)
    {
        Quaternion rot = rotation ?? Quaternion.identity;
        int start = verts.Count;
        Vector3 inv = new Vector3(1f / radii.x, 1f / radii.y, 1f / radii.z);
        float ar = (radii.x + radii.y + radii.z) / 3f;
        for (int i = 0; i <= rings; i++)
        {
            float th = Mathf.PI * i / rings;
            for (int j = 0; j <= segs; j++)
            {
                float ph = Mathf.PI * 2f * j / segs;
                var n = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                Add(c + rot * Vector3.Scale(n, radii), rot * Vector3.Scale(n, inv), col, mat, new Vector2(ph * ar, th * ar), (int)bone, (int)bone, 0);
            }
        }
        for (int i = 0; i < rings; i++)
        {
            for (int j = 0; j < segs; j++)
            {
                int a = start + i * (segs + 1) + j, b = a + segs + 1;
                tris.Add(a); tris.Add(a + 1); tris.Add(b);
                tris.Add(a + 1); tris.Add(b + 1); tris.Add(b);
            }
        }
    }
}
