using UnityEngine;

// Den galna fågeln. Den flyger bredvid gubben och gör ett antal anfall:
//   spana (flyger bredvid) -> ladda upp (skakar, lyser rött, skriker = varning!) -> dyk mot där ballongen VAR -> vänd tillbaka.
// Flyttar man sig åt sidan när den laddar upp missar den. Efter alla anfall ger den upp och flyger iväg.
public class Bird : MonoBehaviour
{
    enum Phase { Approach, Hover, Windup, Lunge, Recover, Flee }

    Player target;
    int attacksLeft;
    float windupTime, lungeSpeed, t, relZ, smart;   // smart: högre bana = smartare fågel
    Phase phase;
    int side;
    Vector2 pos, lungeTarget;          // fågelns och målets läge i sidled (x) och höjd (y)
    Transform wingL, wingR, body, warn;

    bool hawk;   // höken: större, brun med vitt huvud, dyker snabbare

    float hoverWait = 1.3f;   // hur länge den spanar innan den laddar upp (kompisen väntar lite längre)

    public static Bird Spawn(Transform parent, Player target, float difficulty, bool deadly, bool hawk = false, int forceSide = 0, float extraWait = 0f)
    {
        var go = new GameObject(hawk ? "Hok" : "GalenFagel");
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * (hawk ? 2.0f : 1.5f);
        var b = go.AddComponent<Bird>();
        b.target = target;
        // tidiga banor: ett enda anfall med lång varning; svåra banor: många anfall med kort varning
        b.attacksLeft = 1 + Mathf.RoundToInt(difficulty * 4f);
        b.windupTime = Mathf.LerpUnclamped(1.1f, 0.5f, difficulty);    // över 1 = finalen: ännu kortare varning
        b.lungeSpeed = Mathf.LerpUnclamped(11f, 17f, difficulty);
        b.smart = Mathf.Clamp01(difficulty);
        b.hawk = hawk;
        if (hawk) { b.lungeSpeed *= 1.3f; b.smart = 1f; }   // höken dyker snabbare och siktar smart
        b.side = forceSide != 0 ? forceSide : (Random.value < 0.5f ? -1 : 1);
        b.hoverWait = 1.3f + extraWait;
        Vector3 bc = target.BalloonCenter;
        b.pos = new Vector2(bc.x + b.side * 4f, bc.y + 5f);
        b.relZ = 24f;
        b.Build(deadly);
        b.Place();
        return b;
    }

    void Build(bool deadly)
    {
        Color black = hawk ? new Color(0.36f, 0.24f, 0.14f) : new Color(0.16f, 0.15f, 0.2f), white = new Color(0.95f, 0.95f, 0.92f);
        Color beak = new Color(1f, 0.7f, 0.15f), eye = hawk ? new Color(1f, 0.8f, 0.1f) : new Color(0.95f, 0.15f, 0.1f);
        Color headCol = hawk ? new Color(0.92f, 0.9f, 0.85f) : black;      // höken har vitt huvud
        Color belly = hawk ? new Color(0.6f, 0.45f, 0.3f) : new Color(0.35f, 0.34f, 0.38f);
        body = new GameObject("Kropp").transform;
        body.SetParent(transform, false);
        var mb = new MeshBuilder();
        mb.Sphere(new Vector3(0, 0, 0.05f), new Vector3(0.3f, 0.3f, 0.55f), black, 12, 16);
        mb.Sphere(new Vector3(0, -0.1f, -0.12f), new Vector3(0.22f, 0.2f, 0.32f), belly, 10, 14);
        mb.Sphere(new Vector3(0, 0.2f, -0.42f), new Vector3(0.2f, 0.21f, 0.22f), headCol, 12, 16);
        if (hawk) mb.Tube(new Vector3(0, 0.12f, -0.62f), new Vector3(0, 0.0f, -0.78f), 0.05f, 0.012f, beak, 8);   // krokig näbb
        mb.Tube(new Vector3(0, 0.17f, -0.58f), new Vector3(0, 0.12f, -0.9f), 0.06f, 0.012f, beak, 10);
        mb.Sphere(new Vector3(-0.12f, 0.3f, -0.6f), Vector3.one * 0.08f, white, 4, 6);
        mb.Sphere(new Vector3(0.12f, 0.3f, -0.6f), Vector3.one * 0.08f, white, 4, 6);
        mb.Sphere(new Vector3(-0.12f, 0.3f, -0.67f), Vector3.one * 0.045f, eye, 4, 6);
        mb.Sphere(new Vector3(0.12f, 0.3f, -0.67f), Vector3.one * 0.045f, eye, 4, 6);
        mb.Box(new Vector3(-0.12f, 0.42f, -0.6f), new Vector3(0.18f, 0.04f, 0.04f), black, Quaternion.Euler(0, 0, -25f));
        mb.Box(new Vector3(0.12f, 0.42f, -0.6f), new Vector3(0.18f, 0.04f, 0.04f), black, Quaternion.Euler(0, 0, 25f));
        mb.Sphere(new Vector3(0, 0.05f, 0.65f), new Vector3(0.2f, 0.04f, 0.3f), black, 6, 10);
        mb.Tube(new Vector3(-0.08f, -0.25f, 0.05f), new Vector3(-0.08f, -0.42f, 0.1f), 0.02f, 0.015f, beak, 6);
        mb.Tube(new Vector3(0.08f, -0.25f, 0.05f), new Vector3(0.08f, -0.42f, 0.1f), 0.02f, 0.015f, beak, 6);
        var bodyMesh = Toon.MakeSmooth("Mesh", mb.ToMesh("fagel"), body);
        bodyMesh.AddComponent<MeshCollider>().sharedMesh = bodyMesh.GetComponent<MeshFilter>().sharedMesh;

        wingL = Wing(-1, black);
        wingR = Wing(1, black);

        // röd varningsglöd när den laddar upp ett anfall
        warn = new GameObject("Varning").transform;
        warn.SetParent(body, false);
        mb = new MeshBuilder();
        mb.Sphere(Vector3.zero, new Vector3(0.9f, 0.8f, 1.1f), new Color(1f, 0.15f, 0.08f, 0.35f), 10, 14);
        Toon.MakeGlow("Sken", mb.ToMesh("varning"), warn);
        warn.gameObject.SetActive(false);

        var hz = gameObject.AddComponent<Hazard>();
        hz.deadly = deadly;
    }

    Transform Wing(int s, Color col)
    {
        var p = new GameObject(s < 0 ? "VingeV" : "VingeH").transform;
        p.SetParent(body, false);
        p.localPosition = new Vector3(s * 0.3f, 0.1f, 0);
        var mb = new MeshBuilder();
        mb.Sphere(new Vector3(s * 0.5f, 0, 0.05f), new Vector3(0.55f, 0.04f, 0.26f), col, 8, 12);
        for (int i = 0; i < 4; i++)
            mb.Sphere(new Vector3(s * (0.75f + i * 0.08f), 0, 0.15f + i * 0.06f), new Vector3(0.22f, 0.025f, 0.07f), col, 4, 8);
        var wm = Toon.MakeSmooth("Mesh", mb.ToMesh("vinge"), p);
        wm.AddComponent<MeshCollider>().sharedMesh = wm.GetComponent<MeshFilter>().sharedMesh;
        return p;
    }

    // Var fågeln vill vara när den spanar: snett ovanför och bredvid ballongen.
    Vector2 HoverPoint()
    {
        Vector3 bc = target.BalloonCenter;
        return new Vector2(bc.x + side * 2.8f, bc.y + 1.4f);
    }

    void Update()
    {
        if (target == null) { Destroy(gameObject); return; }
        float dt = Time.deltaTime;
        t += dt;
        if (target.dying && phase != Phase.Flee) { phase = Phase.Flee; t = 0; }
        Vector3 bc = target.BalloonCenter;
        float shake = 0f;

        switch (phase)
        {
            case Phase.Approach:
                relZ = Mathf.MoveTowards(relZ, 0.2f, 14f * dt);
                pos = Vector2.MoveTowards(pos, HoverPoint(), 6f * dt);
                if (relZ <= 0.25f) Go(Phase.Hover);
                break;
            case Phase.Hover:
                // följer efter, men lite långsammare än gubben kan styra
                pos = Vector2.MoveTowards(pos, HoverPoint(), (5f + 4f * smart) * dt);
                if (t > hoverWait) { Go(Phase.Windup); hoverWait = 1.3f; Game.I?.OnBirdWindup(); }
                break;
            case Phase.Windup:
                pos = Vector2.MoveTowards(pos, HoverPoint(), 3f * dt);
                shake = 0.12f;
                warn.gameObject.SetActive(Mathf.Repeat(t * 10f, 1f) > 0.35f);
                if (t > windupTime)
                {
                    // smart fågel: siktar dit gubben är på väg (byt håll för att lura den!)
                    float lead = Mathf.Lerp(0f, 0.45f, smart);
                    float tx = Mathf.Clamp(bc.x + target.vel.x * lead, -Game.StreetHalf + 0.6f, Game.StreetHalf - 0.6f);
                    lungeTarget = new Vector2(tx, bc.y);
                    warn.gameObject.SetActive(false);
                    Go(Phase.Lunge);
                }
                break;
            case Phase.Lunge:
                {
                    Vector2 dir = (lungeTarget - pos).normalized;
                    pos = Vector2.MoveTowards(pos, lungeTarget + dir * 1.5f, lungeSpeed * dt);
                    if (Vector2.Distance(pos, lungeTarget + dir * 1.5f) < 0.05f || t > 0.6f)
                    {
                        attacksLeft--;
                        side = -side;
                        Go(attacksLeft > 0 ? Phase.Recover : Phase.Flee);
                    }
                }
                break;
            case Phase.Recover:
                pos = Vector2.MoveTowards(pos, HoverPoint(), 7f * dt);
                if (t > 0.7f) Go(Phase.Hover);
                break;
            case Phase.Flee:
                pos += new Vector2(side * 4f, 7f) * dt;
                relZ += 10f * dt;
                if (t > 3f)
                {
                    if (!target.dying) Game.I?.OnBirdGone();
                    Destroy(gameObject);
                    return;
                }
                break;
        }
        Place();

        // titta mot ballongen (eller bort när den flyr), vingarna slår
        Vector3 look = phase == Phase.Flee ? Vector3.forward : (bc - transform.position);
        look.y *= 0.4f;
        if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(-look.normalized, Vector3.up);
        body.localPosition = shake > 0 ? Random.insideUnitSphere * shake : Vector3.zero;
        float flap = Mathf.Sin(Time.time * (phase == Phase.Lunge ? 30f : 18f)) * 40f;
        wingL.localRotation = Quaternion.Euler(0, 0, -flap);
        wingR.localRotation = Quaternion.Euler(0, 0, flap);
    }

    void Go(Phase p)
    {
        phase = p;
        t = 0f;
    }

    void Place()
    {
        transform.position = new Vector3(pos.x, pos.y, target.BalloonCenter.z + relZ);
    }
}
