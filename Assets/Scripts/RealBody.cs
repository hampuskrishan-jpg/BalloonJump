using System.Collections.Generic;
using UnityEngine;

// Realistisk människa: Microsoft Rocketbox "Male_Adult_05" (äldre man med hår, skägg och arbetsväst) (MIT-licens, se Resources/Gubbe/LICENSE_Microsoft_Rocketbox.md).
// Modellen är gjord av proffs med riktigt skelett. Vi poserar skelettet i kod: lederna riktas
// mot önskade håll (armen upp mot ballongerna, benen som dinglar), så vi behöver inga färdiga animationer.
public class RealBody
{
    public Transform Model, HandR, Pelvis;
    Transform spine1, spine2, head;
    public Transform Head => head;
    // ögonblicksbild av alla ben i viloläge, så att ragdollen kan återställas till nästa försök
    Transform[] allBones;
    Vector3[] restPos;
    Quaternion[] restRot;
    readonly List<Object> ragdollParts = new List<Object>();
    public bool Ragdoll { get; private set; }
    Transform visual, uArmR, fArmR, uArmL, fArmL, handL, thighL, calfL, footL, thighR, calfR, footR;
    readonly List<Transform> joints = new List<Transform>();
    readonly List<Quaternion> rest = new List<Quaternion>();

    public static RealBody TryCreate(Transform visual)
    {
        var prefab = Resources.Load<GameObject>("Gubbe/Export/Male_Adult_05");
        if (prefab == null) return null;
        var m = Object.Instantiate(prefab, visual, false);
        m.name = "RiktigGubbe";
        m.transform.localPosition = Vector3.zero;
        m.transform.localRotation = Quaternion.Euler(0, 180f, 0); // modellen tittar mot +z, gubbens framsida är lokal -z

        // material: lite mattare hud och kläder, och genomskinliga hårstrån/fransar
        var hair = Resources.Load<Material>("Gubbe/HarCutout");
        foreach (var r in m.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                if (mats[i].name.Contains("opacity") && hair != null) mats[i] = hair;
                else
                {
                    var c = new Material(mats[i]);
                    c.SetFloat("_Glossiness", 0.22f);
                    mats[i] = c;
                }
            }
            r.sharedMaterials = mats;
            r.updateWhenOffscreen = true;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows = true;
        }

        var all = m.GetComponentsInChildren<Transform>();
        Transform F(string n)
        {
            foreach (var t in all) if (t.name == n) return t;
            return null;
        }
        var b = new RealBody
        {
            visual = visual, Model = m.transform,
            uArmR = F("Bip01 R UpperArm"), fArmR = F("Bip01 R Forearm"), HandR = F("Bip01 R Hand"),
            uArmL = F("Bip01 L UpperArm"), fArmL = F("Bip01 L Forearm"), handL = F("Bip01 L Hand"),
            thighL = F("Bip01 L Thigh"), calfL = F("Bip01 L Calf"), footL = F("Bip01 L Foot"),
            thighR = F("Bip01 R Thigh"), calfR = F("Bip01 R Calf"), footR = F("Bip01 R Foot"),
        };
        b.Pelvis = F("Bip01 Pelvis");
        b.spine1 = F("Bip01 Spine1");
        b.spine2 = F("Bip01 Spine2");
        b.head = F("Bip01 Head");
        b.allBones = all;
        b.restPos = new Vector3[all.Length];
        b.restRot = new Quaternion[all.Length];
        for (int i = 0; i < all.Length; i++) { b.restPos[i] = all[i].localPosition; b.restRot[i] = all[i].localRotation; }
        foreach (var j in new[] { b.uArmR, b.fArmR, b.HandR, b.uArmL, b.fArmL, b.handL, b.thighL, b.calfL, b.footL, b.thighR, b.calfR, b.footR })
        {
            if (j == null) { Object.Destroy(m); return null; }
            b.joints.Add(j);
            b.rest.Add(j.localRotation);
        }
        return b;
    }

    // ---- Lyckad landning: tar emot sig med knäna, lättnad, släpper ballongerna och jublar.
    // tw = tid sedan landningen. Returnerar hur mycket knäna är böjda (0–1) så att kroppen kan sänkas.
    public float PoseWin(float tw, float t)
    {
        if (Ragdoll) return 0f;
        for (int i = 0; i < joints.Count; i++) joints[i].localRotation = rest[i];
        if (spine1 != null) spine1.localRotation = RestOf(spine1);
        if (spine2 != null) spine2.localRotation = RestOf(spine2);
        if (head != null) head.localRotation = RestOf(head);

        // knäna böjs i nedslaget och sträcks sedan sakta
        float crouch = tw < 0.12f ? tw / 0.12f : Mathf.Clamp01(1f - (tw - 0.12f) / 0.55f);
        crouch = crouch * crouch * (3f - 2f * crouch);
        Aim(thighL, calfL, new Vector3(0.07f, -1f, -crouch * 0.9f));
        Aim(thighR, calfR, new Vector3(-0.07f, -1f, -crouch * 0.9f));
        Aim(calfL, footL, new Vector3(0f, -1f, crouch * 0.9f));
        Aim(calfR, footR, new Vector3(0f, -1f, crouch * 0.9f));

        float yaw = 0f, pitch = 0f;
        Vector3 holdUpper = new Vector3(-0.12f, 1f, 0.05f), holdFore = new Vector3(-0.04f, 1f, 0f);
        if (tw < 1.25f)
        {
            // lättnad: håller fortfarande ballongerna, andra handen mot bröstet, huvudet sänkt
            Aim(uArmR, fArmR, holdUpper);
            Aim(fArmR, HandR, holdFore);
            Aim(uArmL, fArmL, new Vector3(0.25f, -0.6f, -0.6f));
            Aim(fArmL, handL, new Vector3(-0.6f, 0.5f, -0.4f));
            pitch = 22f * Mathf.Clamp01(tw / 0.5f);
        }
        else if (tw < 3.8f)
        {
            // jubel: båda armarna upp, knutna nävar som pumpar
            float c = Mathf.Clamp01((tw - 1.25f) / 0.35f);
            float pump = tw > 1.6f ? Mathf.Sin((tw - 1.6f) * 8f) * 0.18f : 0f;
            Aim(uArmR, fArmR, Vector3.Lerp(holdUpper, new Vector3(-0.4f, 1f, -0.1f + pump), c));
            Aim(fArmR, HandR, new Vector3(-0.15f, 1f, -0.05f));
            Aim(uArmL, fArmL, Vector3.Lerp(new Vector3(0.25f, -0.6f, -0.6f), new Vector3(0.4f, 1f, -0.1f - pump), c));
            Aim(fArmL, handL, new Vector3(0.15f, 1f, -0.05f));
            pitch = Mathf.Lerp(22f, -18f, c);          // tittar upp mot ballongerna som flyger iväg
        }
        else
        {
            // sänker armarna, andas ut och tittar sig omkring – glad att vara på marken
            float d = Mathf.Clamp01((tw - 3.8f) / 0.8f);
            Aim(uArmR, fArmR, Vector3.Lerp(new Vector3(-0.4f, 1f, -0.1f), new Vector3(-0.15f, -1f, 0.02f), d));
            Aim(fArmR, HandR, Vector3.Lerp(new Vector3(-0.15f, 1f, -0.05f), new Vector3(-0.08f, -1f, -0.1f), d));
            Aim(uArmL, fArmL, Vector3.Lerp(new Vector3(0.4f, 1f, -0.1f), new Vector3(0.15f, -1f, 0.02f), d));
            Aim(fArmL, handL, Vector3.Lerp(new Vector3(0.15f, 1f, -0.05f), new Vector3(0.08f, -1f, -0.1f), d));
            yaw = Mathf.Sin((tw - 3.8f) * 0.8f) * 30f;
            pitch = Mathf.Lerp(-18f, 5f, d);
        }
        // andas häftigt efter landningen, lugnar sig sakta
        float calm = Mathf.Clamp01(tw / 6f);
        Alive(t, Mathf.Lerp(7.5f, 2.6f, calm), Mathf.Lerp(3.5f, 1.6f, calm), yaw, pitch);
        return crouch;
    }

    // ---- Ragdoll: i nedslaget tar riktig fysik över hela kroppen (vikt, leder, studs).
    public void MakeRagdoll(Vector3 velocity)
    {
        if (Ragdoll) return;
        Ragdoll = true;
        var all = allBones;
        Transform F(string n) { foreach (var t in all) if (t.name == n) return t; return null; }
        Rigidbody Body(Transform bone, Transform toward, float radius, float mass)
        {
            if (bone == null) return null;
            var rb = bone.gameObject.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.angularDamping = 0.6f;
            rb.linearVelocity = velocity;
            ragdollParts.Add(rb);
            var g = new GameObject("Ragdoll_kropp");
            Vector3 d = toward != null ? toward.position - bone.position : Vector3.up * radius * 2f;
            g.transform.position = bone.position + d * 0.5f;
            g.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            g.transform.SetParent(bone, true);
            var c = g.AddComponent<CapsuleCollider>();
            c.direction = 2;
            c.radius = radius;
            c.height = d.magnitude + radius * 2f;
            ragdollParts.Add(g);
            return rb;
        }
        void Joint(Rigidbody child, Rigidbody parent, float swing, float twistLo, float twistHi)
        {
            if (child == null || parent == null) return;
            var j = child.gameObject.AddComponent<CharacterJoint>();
            j.connectedBody = parent;
            j.enableProjection = true;
            j.lowTwistLimit = new SoftJointLimit { limit = twistLo };
            j.highTwistLimit = new SoftJointLimit { limit = twistHi };
            j.swing1Limit = new SoftJointLimit { limit = swing };
            j.swing2Limit = new SoftJointLimit { limit = swing };
            ragdollParts.Add(j);
        }
        var pelvis = Body(Pelvis, spine1, 0.14f, 12f);
        var chest = Body(spine2, F("Bip01 Neck"), 0.16f, 14f);
        var hd = Body(head, null, 0.11f, 5f);
        var ual = Body(uArmL, fArmL, 0.055f, 2.5f); var fal = Body(fArmL, handL, 0.045f, 1.8f);
        var uar = Body(uArmR, fArmR, 0.055f, 2.5f); var far = Body(fArmR, HandR, 0.045f, 1.8f);
        var thl = Body(thighL, calfL, 0.08f, 7f); var cal = Body(calfL, footL, 0.06f, 4f);
        var thr = Body(thighR, calfR, 0.08f, 7f); var car = Body(calfR, footR, 0.06f, 4f);
        Joint(chest, pelvis, 25f, -20f, 30f);
        Joint(hd, chest, 35f, -40f, 40f);
        Joint(ual, chest, 80f, -70f, 70f); Joint(uar, chest, 80f, -70f, 70f);
        Joint(fal, ual, 10f, -5f, 130f); Joint(far, uar, 10f, -5f, 130f);
        Joint(thl, pelvis, 55f, -20f, 80f); Joint(thr, pelvis, 55f, -20f, 80f);
        Joint(cal, thl, 8f, -130f, 5f); Joint(car, thr, 8f, -130f, 5f);
        foreach (var o in ragdollParts)
            if (o is Rigidbody r) r.angularVelocity = Random.insideUnitSphere * 3f;   // varje nedslag ser olika ut
    }

    // Tillbaka till vanlig gubbe inför nästa försök
    public void ClearRagdoll()
    {
        if (!Ragdoll) return;
        foreach (var o in ragdollParts) if (o is Joint) Object.DestroyImmediate(o);
        foreach (var o in ragdollParts) if (o is Rigidbody) Object.DestroyImmediate(o);
        foreach (var o in ragdollParts) if (o is GameObject) Object.DestroyImmediate(o);
        ragdollParts.Clear();
        for (int i = 0; i < allBones.Length; i++)
            if (allBones[i] != null) { allBones[i].localPosition = restPos[i]; allBones[i].localRotation = restRot[i]; }
        Ragdoll = false;
    }

    // Ligger utsträckt på rygg efter fallet: armar och ben utspridda, lite böjda.
    public void PoseLying(float t)
    {
        for (int i = 0; i < joints.Count; i++) joints[i].localRotation = rest[i];
        float breathe = Mathf.Sin(t * 1.7f) * 0.03f;
        Aim(uArmR, fArmR, new Vector3(-1f, 0.25f + breathe, 0.15f));
        Aim(fArmR, HandR, new Vector3(-0.8f, 0.6f, 0.1f));
        Aim(uArmL, fArmL, new Vector3(1f, 0.05f - breathe, 0.2f));
        Aim(fArmL, handL, new Vector3(0.9f, -0.3f, 0.25f));
        Aim(thighL, calfL, new Vector3(0.28f, -1f, -0.12f));
        Aim(thighR, calfR, new Vector3(-0.18f, -1f, -0.02f));
        Aim(calfL, footL, new Vector3(0.12f, -1f, 0.3f));
        Aim(calfR, footR, new Vector3(-0.1f, -1f, 0.05f));
    }

    // Exakta träffzoner som följer kroppen: kapslar längs skelettets ben + ett klot för huvudet.
    public void AddColliders()
    {
        var all = Model.GetComponentsInChildren<Transform>();
        Transform F(string n) { foreach (var t in all) if (t.name == n) return t; return null; }
        void Capsule(string a, string b, float r)
        {
            var ta = F(a); var tb = F(b);
            if (ta == null || tb == null) return;
            Vector3 d = tb.position - ta.position;
            var g = new GameObject("Traff_" + a);
            g.transform.position = ta.position + d * 0.5f;
            g.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            g.transform.SetParent(ta, true);
            var c = g.AddComponent<CapsuleCollider>();
            c.isTrigger = true;
            c.direction = 2;
            c.radius = r;
            c.height = d.magnitude + 2f * r;
        }
        Capsule("Bip01 Pelvis", "Bip01 Spine2", 0.15f);
        Capsule("Bip01 Spine2", "Bip01 Neck", 0.16f);
        foreach (var s in new[] { "L", "R" })
        {
            Capsule("Bip01 " + s + " UpperArm", "Bip01 " + s + " Forearm", 0.06f);
            Capsule("Bip01 " + s + " Forearm", "Bip01 " + s + " Hand", 0.05f);
            Capsule("Bip01 " + s + " Hand", "Bip01 " + s + " Finger12", 0.045f);
            Capsule("Bip01 " + s + " Thigh", "Bip01 " + s + " Calf", 0.085f);
            Capsule("Bip01 " + s + " Calf", "Bip01 " + s + " Foot", 0.06f);
            Capsule("Bip01 " + s + " Foot", "Bip01 " + s + " Toe0", 0.05f);
        }
        var head = F("Bip01 Head");
        if (head != null)
        {
            var g = new GameObject("Traff_Huvud");
            g.transform.position = head.position + Vector3.up * 0.09f;
            g.transform.SetParent(head, true);
            var c = g.AddComponent<SphereCollider>();
            c.isTrigger = true;
            c.radius = 0.12f;
        }
    }

    // Riktar en led (a) så att nästa led (b) hamnar i önskad riktning, angiven i gubbens eget rum
    // (framåt = -z, upp = +y, gubbens högra sida = -x).
    void Aim(Transform a, Transform b, Vector3 dir)
    {
        Vector3 cur = b.position - a.position;
        Vector3 want = visual.TransformDirection(dir.normalized);
        a.rotation = Quaternion.FromToRotation(cur, want) * a.rotation;
    }

    // ---- Levande rörelser ovanpå poserna: andning, huvudet tittar mot faror och dit han styr, små ryck.
    float twitchTime = 2f, twitchPhase;
    int twitchLimb;

    void Alive(float t, float breathRate, float breathAmp, float yaw, float pitch)
    {
        float br = Mathf.Sin(t * breathRate);
        Vector3 right = visual.TransformDirection(Vector3.left);   // gubbens högra sida är -x
        if (spine1 != null) spine1.rotation = Quaternion.AngleAxis(br * breathAmp * 0.6f, right) * spine1.rotation;
        if (spine2 != null) spine2.rotation = Quaternion.AngleAxis(br * breathAmp, right) * spine2.rotation;
        if (head != null) head.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(pitch, right) * head.rotation;
        twitchTime -= Time.deltaTime;
        if (twitchTime <= 0f)
        {
            twitchTime = Random.Range(1.5f, 4f);
            twitchPhase = 1f;
            twitchLimb = Random.Range(0, 4);
        }
        if (twitchPhase > 0f)
        {
            twitchPhase = Mathf.Max(0f, twitchPhase - Time.deltaTime * 5f);
            float a = Mathf.Sin(twitchPhase * Mathf.PI) * 9f;
            Transform limb = twitchLimb == 0 ? handL : twitchLimb == 1 ? fArmL : twitchLimb == 2 ? footL : footR;
            limb.rotation = Quaternion.AngleAxis(a, right) * limb.rotation;
        }
    }

    Quaternion RestOf(Transform t) => restRot[System.Array.IndexOf(allBones, t)];

    // panic: 0 = lugn, 0–1 = rycker till av skräck (en ballong sprack), 1+ = faller utan ballonger (viftar vilt)
    public void Pose(bool falling, float t, float panic = 0f, bool holdingBalloons = true, float lookYaw = 0f, float lookPitch = 0f)
    {
        if (Ragdoll) return;
        for (int i = 0; i < joints.Count; i++) joints[i].localRotation = rest[i];
        if (spine1 != null) spine1.localRotation = RestOf(spine1);
        if (spine2 != null) spine2.localRotation = RestOf(spine2);
        if (head != null) head.localRotation = RestOf(head);
        if (falling)
        {
            float p = Mathf.Clamp01(panic);
            float s = Mathf.Sin(t * (3f + 9f * p));
            float w = Mathf.Sin(t * (5f + 12f * p));
            // höger arm: håller ballongerna – eller viftar i panik när de är borta
            if (holdingBalloons)
            {
                Aim(uArmR, fArmR, new Vector3(-0.15f - 0.2f * p, 1f, 0.1f));
                Aim(fArmR, HandR, new Vector3(-0.05f, 1f, 0.05f));
            }
            else
            {
                Aim(uArmR, fArmR, new Vector3(-0.8f, 0.6f + w * 0.4f, -0.2f + s * 0.3f));
                Aim(fArmR, HandR, new Vector3(-0.6f, 0.9f - w * 0.3f, -0.3f));
            }
            // vänster arm balanserar – far upp i luften av skräck
            Vector3 calmArm = new Vector3(0.75f, -0.6f + Mathf.Sin(t * 5f) * 0.2f, -0.15f + Mathf.Sin(t * 2.3f) * 0.2f);
            Vector3 panicArm = new Vector3(0.8f, 0.7f + w * 0.4f, -0.25f - s * 0.3f);
            Aim(uArmL, fArmL, Vector3.Lerp(calmArm, panicArm, p));
            Aim(fArmL, handL, Vector3.Lerp(new Vector3(0.5f, -0.75f, -0.45f), new Vector3(0.5f, 0.9f, -0.4f), p));
            // benen dinglar – sparkar häftigt i panik
            float amp = 0.35f + 0.45f * p;
            Aim(thighL, calfL, new Vector3(0.04f + 0.15f * p, -1f, -0.2f - 0.25f * p - s * amp));
            Aim(thighR, calfR, new Vector3(-0.04f - 0.15f * p, -1f, -0.2f - 0.25f * p + s * amp));
            Aim(calfL, footL, new Vector3(0f, -1f, 0.35f + Mathf.Max(0f, s) * (0.5f + 0.4f * p)));
            Aim(calfR, footR, new Vector3(0f, -1f, 0.35f + Mathf.Max(0f, -s) * (0.5f + 0.4f * p)));
        }
        else
        {
            // står på avsatsen: ballongarmen upp, andra armen längs sidan
            Aim(uArmR, fArmR, new Vector3(-0.12f, 1f, 0.05f));
            Aim(fArmR, HandR, new Vector3(-0.04f, 1f, 0f));
            Aim(uArmL, fArmL, new Vector3(0.2f, -1f, 0.02f + Mathf.Sin(t * 2f) * 0.03f));
            Aim(fArmL, handL, new Vector3(0.1f, -1f, -0.12f));
            // står nervöst på kanten och tittar ner ibland
            lookPitch += 18f + Mathf.Sin(t * 0.7f) * 12f;
            lookYaw += Mathf.Sin(t * 0.45f) * 20f;
        }
        float p2 = Mathf.Clamp01(panic);
        Alive(t, Mathf.Lerp(falling ? 3.2f : 2.2f, 9f, p2), Mathf.Lerp(1.6f, 3.5f, p2), lookYaw, lookPitch);
    }
}
