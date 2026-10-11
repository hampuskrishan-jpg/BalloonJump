using System.Collections.Generic;
using UnityEngine;

// Den fattiga gubben som hänger i sina ballonger. Roten står vid fötterna.
// Kroppen är byggd av mjuka former med riktiga proportioner (ca 1,8 m lång),
// med leder i axlar, armbågar, höfter och knän så att han kan röra sig.
public class Player : MonoBehaviour
{
    public const int MaxBalloons = 10; // man kan köpa upp till 10 ballonger – ett tak som är svårt att nå
    public static readonly Color[] BalloonColors =
    {
        new Color(0.82f, 0.12f, 0.1f), new Color(0.95f, 0.72f, 0.12f),
        new Color(0.16f, 0.42f, 0.82f), new Color(0.24f, 0.62f, 0.3f),
        new Color(0.6f, 0.2f, 0.7f), new Color(0.95f, 0.45f, 0.1f),
        new Color(0.1f, 0.65f, 0.7f), new Color(0.9f, 0.4f, 0.6f),
        new Color(0.92f, 0.9f, 0.85f), new Color(0.35f, 0.25f, 0.6f),
    };

    // Ballongerna sitter i ett knippe: utspridda i en solfjäder med olika långa snören.
    static float SpreadAngle(int i, int n) => n <= 1 ? 0f : Mathf.Lerp(-1f, 1f, i / (float)(n - 1)) * Mathf.Min(13f * (n - 1), 46f);
    static float StringScale(int i, int n) => n <= 2 ? 1f : 1f + ((i % 3) - 1) * 0.17f;

    public Vector3 vel;
    public float invuln, shieldTime, brakeTime;
    public bool falling, dying, rocket, lying;
    public float flinch;   // rycker till av skräck när en ballong spricker
    public Vector3? lookTarget;   // något han tittar på (t.ex. fågeln)
    Camera portraitCam;
    float celebrate = -1f;     // tid sedan lyckad landning (-1 = inte landat)
    bool releasing;            // ballongerna har släppts och flyger iväg
    Vector3 releaseVel;
    public bool Ragdolled => real != null && real.Ragdoll;
    public Vector3 BodyCenter => real != null && real.Pelvis != null ? real.Pelvis.position : transform.position + Vector3.up * 0.3f;
    const float BigBalloon = 1.2f; // en stor ballong redan från början

    RealBody real;
    Transform visual, hipL, hipR, kneeL, kneeR, armUp, elbowUp, armFree, elbowFree, balloonRoot, shieldRoot, rocketRoot, flame;
    readonly List<Transform> balloons = new List<Transform>();
    SphereCollider balloonCol;
    Renderer[] renderers;
    float t;

    // Gubben tittar framåt längs gatan (+z), så kameran bakom ser hans rygg.
    static readonly Quaternion Facing = Quaternion.Euler(0, 180f, 0);

    public int Balloons => balloons.Count;
    public float HalfWidth => balloons.Count >= 7 ? 1.3f : balloons.Count >= 3 ? 1.0f : 0.6f; // stort knippe = bredare
    public Vector3 BalloonCenter => balloonRoot.position + Vector3.up * 1.8f * balloonRoot.localScale.x;

    static readonly Color Shoe = new Color(0.3f, 0.18f, 0.1f); // remmar till raketen

    public const int PortraitLayer = 30;
    public RenderTexture Portrait { get; private set; }

    public static Player Create(Transform parent)
    {
        var go = new GameObject("Gubben");
        go.transform.SetParent(parent, false);
        var p = go.AddComponent<Player>();
        p.Build();
        return p;
    }

    void Build()
    {
        var rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        var body = gameObject.AddComponent<CapsuleCollider>();
        body.isTrigger = true;
        body.center = new Vector3(0, 0.95f, 0);
        body.height = 1.8f;
        body.radius = 0.25f;
        balloonCol = gameObject.AddComponent<SphereCollider>();
        balloonCol.isTrigger = true;

        visual = new GameObject("Visual").transform;
        visual.SetParent(transform, false);
        visual.localRotation = Facing;

        // ---- en riktig, proffsgjord människa om den finns (Rocketbox), annars vår egen kodbyggda kropp
        real = RealBody.TryCreate(visual);
        if (real != null)
        {
            // exakta träffzoner som sitter fast på skelettet (huvud, bål, armar, ben) i stället för en stor kapsel
            body.enabled = false;
            real.AddColliders();
        }
        HumanBuilder human = null;
        if (real == null)
        {
            human = new HumanBuilder();
            human.Build(visual, Toon.Smooth);
            hipL = human[HumanBuilder.B.ThighL];
            hipR = human[HumanBuilder.B.ThighR];
            kneeL = human[HumanBuilder.B.KneeL];
            kneeR = human[HumanBuilder.B.KneeR];
            armFree = human[HumanBuilder.B.ShoulderL];
            elbowFree = human[HumanBuilder.B.ElbowL];
            armUp = human[HumanBuilder.B.ShoulderR];
            elbowUp = human[HumanBuilder.B.ElbowR];
            armUp.localRotation = Quaternion.Euler(0, 0, 168f);
        }


        // kamera som filmar ansiktet till porträttet uppe till vänster
        Portrait = new RenderTexture(512, 512, 24) { name = "Portratt", antiAliasing = 4 };
        var pc = new GameObject("PortrattKamera").AddComponent<Camera>();
        portraitCam = pc;
        pc.transform.SetParent(visual, false);
        pc.transform.localPosition = V(0.05f, 1.63f, -0.62f);
        pc.transform.localRotation = Quaternion.Euler(4f, -4f, 0);
        pc.fieldOfView = 24f;
        pc.nearClipPlane = 0.05f;
        pc.farClipPlane = 3f;
        pc.clearFlags = CameraClearFlags.SolidColor;
        pc.backgroundColor = new Color(0.5f, 0.6f, 0.68f);   // ljus himmelsfärg så att ansiktet syns tydligt
        pc.cullingMask = 1 << PortraitLayer;
        pc.targetTexture = Portrait;
        // eget ljus för porträttet (lyser bara på gubben i porträttet, inte på banan)
        var key = new GameObject("PortrattLjus").AddComponent<Light>();
        key.transform.SetParent(pc.transform, false);
        key.transform.localPosition = new Vector3(0.25f, 0.2f, 0f);
        key.type = LightType.Point;
        key.range = 2.5f;
        key.intensity = 2.2f;
        key.color = new Color(1f, 0.94f, 0.86f);
        key.cullingMask = 1 << PortraitLayer;

        balloonRoot = new GameObject("Ballonger").transform;
        if (real != null) balloonRoot.SetParent(transform, false);
        else
        {
            balloonRoot.SetParent(human[HumanBuilder.B.HandR], false);
            balloonRoot.localPosition = new Vector3(0, -0.07f, 0);
        }

        // ---- sköld: en genomskinlig bubbla
        shieldRoot = new GameObject("Skold").transform;
        shieldRoot.SetParent(transform, false);
        shieldRoot.localPosition = new Vector3(0, 2.0f, 0);
        var mb = new MeshBuilder();
        mb.Sphere(Vector3.zero, new Vector3(1.5f, 2.2f, 1.2f), new Color(0.5f, 0.85f, 1f, 0.16f), 16, 24);
        Toon.MakeGlow("Bubbla", mb.ToMesh("skold"), shieldRoot);

        // ---- raket på ryggen (syns bara när raketen används)
        rocketRoot = new GameObject("Raket").transform;
        rocketRoot.SetParent(visual, false);
        rocketRoot.localPosition = new Vector3(0, 1.1f, 0.25f);
        mb = new MeshBuilder();
        mb.Cylinder(Vector3.zero, 0.13f, 0.8f, 16, new Color(0.7f, 0.16f, 0.12f, 0.3f), Quaternion.identity);
        mb.Sphere(new Vector3(0, 0.4f, 0), new Vector3(0.13f, 0.22f, 0.13f), new Color(0.8f, 0.8f, 0.82f, 0.2f), 8, 12);
        mb.Box(new Vector3(0.17f, -0.32f, 0), new Vector3(0.18f, 0.26f, 0.03f), new Color(0.85f, 0.7f, 0.2f));
        mb.Box(new Vector3(-0.17f, -0.32f, 0), new Vector3(0.18f, 0.26f, 0.03f), new Color(0.85f, 0.7f, 0.2f));
        mb.Tube(new Vector3(-0.16f, 0.25f, -0.05f), new Vector3(0.16f, 0.25f, -0.05f), 0.02f, 0.02f, Shoe, 6);
        Toon.MakeSmooth("Kropp", mb.ToMesh("raket"), rocketRoot);
        flame = new GameObject("Laga").transform;
        flame.SetParent(rocketRoot, false);
        flame.localPosition = new Vector3(0, -0.45f, 0);
        mb = new MeshBuilder();
        mb.Sphere(new Vector3(0, -0.3f, 0), new Vector3(0.18f, 0.5f, 0.18f), new Color(1f, 0.5f, 0.1f, 0.8f), 8, 10);
        mb.Sphere(new Vector3(0, -0.18f, 0), new Vector3(0.1f, 0.3f, 0.1f), new Color(1f, 0.95f, 0.65f, 0.95f), 8, 10);
        Toon.MakeGlow("Sken", mb.ToMesh("laga"), flame);
        rocketRoot.gameObject.SetActive(false);
    }

    static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

    static void SetLayer(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform c in t) SetLayer(c, layer);
    }

    public void SetBalloons(int n)
    {
        foreach (var b in balloons) Destroy(b.gameObject);
        balloons.Clear();
        n = Mathf.Clamp(n, 0, MaxBalloons);
        for (int i = 0; i < n; i++)
        {
            var piv = new GameObject("Ballong" + (i + 1)).transform;
            piv.SetParent(balloonRoot, false);
            piv.localRotation = Quaternion.Euler(0, 0, SpreadAngle(i, n));
            float o = (StringScale(i, n) - 1f) * 1.36f;   // olika långa snören
            Vector3 Y(float y) => new Vector3(0, y + o, 0);
            Color col = BalloonColors[i % BalloonColors.Length];
            col.a = 0.12f; // blank gummiyta
            var mb = new MeshBuilder();
            mb.Tube(Vector3.zero, Y(1.36f), 0.006f, 0.006f, new Color(0.85f, 0.82f, 0.75f), 5, false);
            mb.Loft(new[] { Y(1.33f), Y(1.38f) }, new[] { new Vector2(0.03f, 0.03f), new Vector2(0.06f, 0.06f) }, col, 10);
            mb.Loft(
                new[] { Y(1.38f), Y(1.48f), Y(1.62f), Y(1.8f), Y(1.98f), Y(2.12f), Y(2.24f), Y(2.3f) },
                new[] { new Vector2(0.05f, 0.05f), new Vector2(0.24f, 0.24f), new Vector2(0.37f, 0.37f), new Vector2(0.44f, 0.44f), new Vector2(0.44f, 0.44f), new Vector2(0.38f, 0.38f), new Vector2(0.24f, 0.24f), new Vector2(0.08f, 0.08f) },
                col, 20);
            Toon.MakeSmooth("Mesh", mb.ToMesh("ballong"), piv);
            // egen träffzon för just den här ballongen
            var bcol = piv.gameObject.AddComponent<SphereCollider>();
            bcol.isTrigger = true;
            bcol.center = Y(1.84f);
            bcol.radius = 0.45f;
            balloons.Add(piv);
        }
        balloonCol.enabled = false; // ersatt av en träffzon per ballong
        SetLayer(transform, PortraitLayer);
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    public void ResetFor(Vector3 pos, int n)
    {
        transform.position = pos;
        transform.rotation = Quaternion.identity;
        vel = Vector3.zero;
        invuln = shieldTime = brakeTime = 0f;
        if (real != null) real.ClearRagdoll();
        celebrate = -1f;
        releasing = false;
        balloonRoot.gameObject.SetActive(true);
        falling = dying = rocket = lying = false;
        flinch = 0f;
        visual.localPosition = Vector3.zero;
        balloonRoot.localScale = Vector3.one * BigBalloon;
        visual.localRotation = Facing;
        SetBalloons(n);
        foreach (var r in renderers) r.enabled = true;
    }

    public void Flinch() => flinch = 0.7f;

    // Landade säkert i mål
    public void Celebrate()
    {
        celebrate = 0f;
        falling = false;
        vel = Vector3.zero;
    }

    // Slår i marken: slutar snurra och blir liggande
    public void LieDown(Vector3 impactVelocity)
    {
        dying = false;
        falling = false;
        if (real != null) real.MakeRagdoll(impactVelocity);   // riktig fysik tar över kroppen
        else lying = true;
        vel = Vector3.zero;
    }

    public void PopAll()
    {
        while (balloons.Count > 0) Pop();
    }

    // Spräcker sista ballongen.
    public void Pop()
    {
        if (balloons.Count == 0) return;
        int i = balloons.Count - 1;
        var piv = balloons[i];
        Debris.Burst(piv.TransformPoint(0, 1.85f, 0), BalloonColors[i % BalloonColors.Length]);
        balloons.RemoveAt(i);
        Destroy(piv.gameObject);
        balloonCol.enabled = false;
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        t += dt;
        invuln = Mathf.Max(0, invuln - dt);
        shieldTime = Mathf.Max(0, shieldTime - dt);
        brakeTime = Mathf.Max(0, brakeTime - dt);

        if (real != null) { }
        else if (falling)
        {
            // benen dinglar och sparkar lite, fria armen balanserar
            float s = Mathf.Sin(t * 3f);
            hipL.localRotation = Quaternion.Euler(s * 22f + 12f, 0, 5f);
            hipR.localRotation = Quaternion.Euler(-s * 22f + 12f, 0, -5f);
            kneeL.localRotation = Quaternion.Euler(-25f - Mathf.Max(0, s) * 25f, 0, 0);
            kneeR.localRotation = Quaternion.Euler(-25f - Mathf.Max(0, -s) * 25f, 0, 0);
            armFree.localRotation = Quaternion.Euler(Mathf.Sin(t * 2.3f) * 15f, 0, -60f + Mathf.Sin(t * 5f) * 18f - vel.x * 3f);
            elbowFree.localRotation = Quaternion.Euler(0, 0, -25f + Mathf.Sin(t * 4f) * 10f);
            elbowUp.localRotation = Quaternion.Euler(0, 0, -8f);
        }
        else
        {
            hipL.localRotation = hipR.localRotation = Quaternion.identity;
            kneeL.localRotation = kneeR.localRotation = Quaternion.identity;
            armFree.localRotation = Quaternion.Euler(0, 0, -6f + Mathf.Sin(t * 2f) * 3f);
            elbowFree.localRotation = Quaternion.Euler(0, 0, -10f);
            elbowUp.localRotation = Quaternion.Euler(0, 0, -15f);
        }

        flinch = Mathf.Max(0f, flinch - dt);
        if (portraitCam != null) portraitCam.enabled = !Ragdolled;   // porträttet behåller sista bilden av ansiktet
        if (Ragdolled) return;   // fysiken styr kroppen nu
        if (lying)
        {
            // ligger på rygg på marken efter fallet
            visual.localRotation = Facing * Quaternion.Euler(90f, 0f, 0f);
            visual.localPosition = new Vector3(0f, 0.14f, 0f);
        }
        else if (dying) visual.Rotate(0, 0, 420f * dt);
        else visual.localRotation = Facing * Quaternion.Euler(falling ? 8f : 0f, 0, Mathf.Clamp(vel.x * 2.2f, -20f, 20f));

        if (celebrate >= 0f)
        {
            celebrate += dt;
            if (celebrate > 1.25f && !releasing)
            {
                releasing = true;   // släpper ballongerna
                releaseVel = new Vector3(Random.Range(-0.6f, 0.6f), 2.2f, Random.Range(0.2f, 0.8f));
            }
        }
        if (real != null && celebrate >= 0f)
        {
            float crouch = real.PoseWin(celebrate, t);
            visual.localPosition = new Vector3(0f, -0.21f * crouch, 0f);   // knäna böjs – kroppen sjunker
            if (!releasing) balloonRoot.position = real.HandR.position + Vector3.up * 0.04f;
        }
        else if (real != null)
        {
            if (lying) real.PoseLying(t);
            else
            {
                // huvudet: tittar dit han styr, eller mot fågeln om den är nära
                float yaw = Mathf.Clamp(vel.x * 3f, -25f, 25f), pitch = 0f;
                if (lookTarget.HasValue)
                {
                    Vector3 d = lookTarget.Value - (transform.position + Vector3.up * 1.65f);
                    yaw = Mathf.Clamp(Mathf.Atan2(d.x, Mathf.Max(0.3f, Mathf.Abs(d.z))) * Mathf.Rad2Deg, -60f, 60f);
                    pitch = Mathf.Clamp(-Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg, -40f, 40f);
                }
                real.Pose(falling || dying, t, dying ? 1f : flinch / 0.7f, balloons.Count > 0, yaw, pitch);
            }
            balloonRoot.position = real.HandR.position + Vector3.up * 0.04f;
        }

        if (releasing)
        {
            // ballongerna stiger mot himlen och försvinner
            releaseVel += Vector3.up * 1.2f * dt;
            balloonRoot.position += releaseVel * dt;
            if (celebrate > 12f) balloonRoot.gameObject.SetActive(false);
        }

        float sway = Mathf.Sin(t * 1.3f) * 5f + Mathf.Clamp(vel.x * 2.5f, -25f, 25f);
        balloonRoot.rotation = Quaternion.Euler(falling ? -Mathf.Clamp(vel.z * 1.2f, 0f, 22f) : 0f, 0, sway);
        float sc = Mathf.MoveTowards(balloonRoot.localScale.x, BigBalloon * (brakeTime > 0 ? 1.25f : 1f), dt * 2f);
        balloonRoot.localScale = Vector3.one * sc;
        balloonCol.radius = (0.5f + 0.12f * Mathf.Min(6, Mathf.Max(0, balloons.Count - 1))) * sc;
        if (balloonCol.enabled)
            balloonCol.center = transform.InverseTransformPoint(balloonRoot.position + Vector3.up * 1.8f * sc);

        shieldRoot.gameObject.SetActive(shieldTime > 0);
        shieldRoot.localScale = Vector3.one * (1f + Mathf.Sin(t * 6f) * 0.03f);
        rocketRoot.gameObject.SetActive(rocket);
        if (rocket) flame.localScale = new Vector3(1f, 0.8f + Mathf.PerlinNoise(t * 20f, 0f) * 0.6f, 1f);

        bool show = invuln <= 0 || shieldTime > 0 || Mathf.Repeat(t * 12f, 1f) > 0.4f;
        if (renderers != null)
            foreach (var r in renderers) if (r != null && r.transform.parent != shieldRoot) r.enabled = show;
    }

    // Porträttkameran följer ansiktet (även när han vrider på huvudet)
    void LateUpdate()
    {
        if (portraitCam == null || !portraitCam.enabled || real == null || real.Head == null) return;
        Vector3 h = real.Head.position + Vector3.up * 0.07f;
        Vector3 front = visual.TransformDirection(Vector3.back);   // gubbens framsida
        Vector3 side = visual.TransformDirection(Vector3.left);
        portraitCam.transform.position = h + front * 0.62f + side * 0.08f + Vector3.up * 0.02f;
        portraitCam.transform.rotation = Quaternion.LookRotation(h - portraitCam.transform.position, Vector3.up);
    }

    void OnTriggerEnter(Collider other) => Touch(other);
    void OnTriggerStay(Collider other) => Touch(other);

    void Touch(Collider other)
    {
        if (Game.I == null) return;
        var coin = other.GetComponent<Coin>();
        if (coin != null) { Game.I.OnCoin(coin); return; }
        var h = other.GetComponentInParent<Hazard>();
        if (h != null) Game.I.OnHazard(h.deadly);
    }
}
