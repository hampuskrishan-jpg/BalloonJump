using UnityEngine;

// Delade material och ljussättning för den realistiska stilen.
// (Klassen heter Toon av historiska skäl – den började som serietidningsstil.)
public static class Toon
{
    static Material world, smooth, glow, sky, openSky;
    static Light sun, rim;

    public static Material World { get { if (world == null) Init(); return world; } }
    public static Material Smooth { get { if (world == null) Init(); return smooth; } }
    public static Material Glow { get { if (world == null) Init(); return glow; } }

    static void Init()
    {
        Shader s = Shader.Find("Ballong/Lit");
        if (s == null) s = Shader.Find("Standard");
        world = new Material(s) { name = "Varld" };
        world.SetFloat("_Detail", 1f);
        smooth = new Material(s) { name = "Slat" };
        smooth.SetFloat("_Detail", 0f);
        Shader g = Shader.Find("Ballong/Glow");
        glow = new Material(g != null ? g : s) { name = "Sken" };
        sky = Resources.Load<Material>("Himmel");
        openSky = Resources.Load<Material>("HimmelOppen");   // öppen himmel för öknen, byn och snöberget

        // riktiga fototexturer + hur stor en bild är i meter (puts, väderbiten puts, tegel, gatsten, tak, trä, berg, metall, betong)
        var photos = Resources.Load<Texture2DArray>("Fototexturer");
        Shader.SetGlobalFloat("_HasPhoto", photos != null ? 1f : 0f);
        if (photos != null) Shader.SetGlobalTexture("_Photo", photos);
        Shader.SetGlobalFloatArray("_PhotoScale", new[] { 1f, 3f, 3f, 2.2f, 2f, 2.5f, 1.6f, 4f, 1.5f, 2.5f, 0.3f, 0.22f, 0.25f, 0.3f, 0.35f, 0.12f, 3f, 2.5f, 2.5f, 4f, 3f });
    }

    // Sol, himmelsljus, dimma och skuggor. Grottan är mörk med varmt lyktljus.
    // Miljöer: 0 staden, 1 öknen, 2 bergsbyn, 3 vattengrottan, 4 snöberget
    public static void SetupLighting(Camera cam, int env)
    {
        if (world == null) Init();
        bool cave = env == 3;
        if (sun == null)
        {
            var go = new GameObject("Solen");
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.3f;
        }
        if (rim == null)
        {
            // motljus: kallt ljus framifrån (mot kameran) som tänder kanterna på gubben, som i film
            var r = new GameObject("Motljus");
            rim = r.AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.shadows = LightShadows.None;
            rim.transform.rotation = Quaternion.Euler(20f, 180f, 0f);
        }
        rim.color = cave ? new Color(0.6f, 0.8f, 1f) : new Color(0.75f, 0.85f, 1f);
        rim.intensity = cave ? 0.75f : 0.6f;
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.High;
        QualitySettings.shadowDistance = 110f;
        QualitySettings.shadowCascades = 4;
        QualitySettings.antiAliasing = 4;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;

        Color fog;
        float depthDark;
        switch (env)
        {
            case 3: // vattengrottan: kallt ljus från sidan, petrolfärgad dimma
                sun.transform.rotation = Quaternion.Euler(30f, 90f, 0f);
                sun.color = new Color(0.72f, 0.84f, 1f);
                sun.intensity = 1.5f;
                Ambient(new Color(0.55f, 0.66f, 0.8f), new Color(0.46f, 0.45f, 0.47f), new Color(0.08f, 0.1f, 0.1f));
                fog = new Color(0.09f, 0.13f, 0.16f);
                Fog(10f, 120f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = fog;
                RenderSettings.skybox = null;
                depthDark = 0.4f;
                break;
            case 1: // öknen: het sol högt upp, varm dis
                sun.transform.rotation = Quaternion.Euler(52f, 35f, 0f);
                sun.color = new Color(1f, 0.88f, 0.7f);
                sun.intensity = 1.8f;
                Ambient(new Color(0.75f, 0.68f, 0.6f), new Color(0.62f, 0.48f, 0.36f), new Color(0.35f, 0.24f, 0.16f));
                fog = new Color(0.88f, 0.72f, 0.54f);
                Fog(25f, 260f);
                Open(cam, new Color(0.75f, 0.6f, 0.45f), 1.3f, 1.6f, fog);
                depthDark = 0.62f;
                break;
            case 2: // bergsbyn: gyllene morgonljus, grönaktig dis
                sun.transform.rotation = Quaternion.Euler(24f, 70f, 0f);
                sun.color = new Color(1f, 0.85f, 0.62f);
                sun.intensity = 1.4f;
                Ambient(new Color(0.55f, 0.65f, 0.75f), new Color(0.42f, 0.46f, 0.38f), new Color(0.16f, 0.18f, 0.12f));
                fog = new Color(0.66f, 0.72f, 0.7f);
                Fog(15f, 200f);
                Open(cam, new Color(0.5f, 0.6f, 0.7f), 1.1f, 1.2f, fog);
                depthDark = 0.5f;
                break;
            case 4: // snöberget: kall, klar och ljus
                sun.transform.rotation = Quaternion.Euler(32f, 120f, 0f);
                sun.color = new Color(0.95f, 0.97f, 1f);
                sun.intensity = 1.5f;
                Ambient(new Color(0.7f, 0.78f, 0.9f), new Color(0.6f, 0.65f, 0.72f), new Color(0.45f, 0.48f, 0.52f));
                fog = new Color(0.82f, 0.87f, 0.93f);
                Fog(15f, 190f);
                Open(cam, new Color(0.55f, 0.65f, 0.8f), 0.8f, 1.3f, fog);
                depthDark = 0.72f;
                break;
            default: // staden: låg kvällssol, bara övre våningarna får ljus
                sun.transform.rotation = Quaternion.Euler(16f, 58f, 0f);
                sun.color = new Color(1f, 0.76f, 0.52f);
                sun.intensity = 1.5f;
                Ambient(new Color(0.38f, 0.46f, 0.57f), new Color(0.32f, 0.33f, 0.36f), new Color(0.1f, 0.1f, 0.11f));
                fog = new Color(0.38f, 0.43f, 0.48f);
                Fog(6f, 170f);
                if (sky != null) { RenderSettings.skybox = sky; cam.clearFlags = CameraClearFlags.Skybox; }
                else { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = fog; }
                depthDark = 0.35f;
                break;
        }
        RenderSettings.fogColor = fog;
        // reflektioner i fönster, vatten och ballonger från den riktiga himlen
        if (sky != null && sky.HasProperty("_Tex") && sky.GetTexture("_Tex") is Cubemap cube)
        {
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = cube;
            RenderSettings.reflectionIntensity = cave ? 0.3f : 0.8f;
        }
        RenderSettings.sun = sun;
        Shader.SetGlobalColor("_BallongFogCol", fog);
        Shader.SetGlobalVector("_BallongFogParams", new Vector4(RenderSettings.fogStartDistance, RenderSettings.fogEndDistance, 0, 0));
        Shader.SetGlobalFloat("_BallongDepthDark", depthDark);

        // filmiskt efterfilter och dammkorn/snöflingor i luften
        var grade = cam.GetComponent<Grade>();
        if (grade == null) grade = cam.gameObject.AddComponent<Grade>();
        grade.cave = cave;
        SetupDust(cam, env);
    }

    static void Ambient(Color sky, Color eq, Color ground)
    {
        RenderSettings.ambientSkyColor = sky;
        RenderSettings.ambientEquatorColor = eq;
        RenderSettings.ambientGroundColor = ground;
    }

    static void Fog(float start, float end)
    {
        RenderSettings.fogStartDistance = start;
        RenderSettings.fogEndDistance = end;
    }

    // Öppen (procedurell) himmel med egen färgton – för miljöer utan stadens fotohimmel
    static void Open(Camera cam, Color tint, float exposure, float atmosphere, Color fog)
    {
        if (openSky == null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = fog; RenderSettings.skybox = null; return; }
        openSky.SetColor("_SkyTint", tint);
        openSky.SetFloat("_Exposure", exposure);
        openSky.SetFloat("_AtmosphereThickness", atmosphere);
        openSky.SetColor("_GroundColor", fog);
        RenderSettings.skybox = openSky;
        cam.clearFlags = CameraClearFlags.Skybox;
    }

    static ParticleSystem dust;

    static void SetupDust(Camera cam, int env)
    {
        if (dust == null)
        {
            var go = new GameObject("Damm");
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0, 0, 12f);
            dust = go.AddComponent<ParticleSystem>();
            dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = dust.main;
            main.loop = true;
            main.startLifetime = 7f;
            main.startSpeed = 0.15f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.1f);
            main.maxParticles = 500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.002f;
            var em = dust.emission;
            em.rateOverTime = 70f;
            var sh = dust.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(18f, 12f, 24f);
            var noise = dust.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.3f;
            var col = dust.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            var s = Shader.Find("Ballong/Soft");
            r.sharedMaterial = new Material(s != null ? s : Shader.Find("Ballong/Glow")) { name = "Damm" };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            dust.Play();
        }
        var m = dust.main;
        var em2 = dust.emission;
        bool snow = env == 4;
        // snöberget: snöflingor som faller; övriga: damm som svävar i ljuset
        m.startColor = snow ? new Color(1f, 1f, 1f, 0.75f) : env == 3 ? new Color(0.7f, 0.85f, 1f, 0.35f)
                     : env == 1 ? new Color(1f, 0.85f, 0.6f, 0.3f) : new Color(1f, 0.9f, 0.75f, 0.28f);
        m.startSize = snow ? new ParticleSystem.MinMaxCurve(0.06f, 0.16f) : new ParticleSystem.MinMaxCurve(0.03f, 0.1f);
        m.gravityModifier = snow ? 0.04f : -0.002f;
        em2.rateOverTime = snow ? 160f : 70f;
    }

    // Byggnader och hinder: matt yta med lite smuts.
    public static GameObject Make(string name, Mesh mesh, Transform parent, bool castShadows = true)
        => Create(name, mesh, parent, World, castShadows);

    // Gubben, fågeln, ballonger, mynt: släta ytor utan smuts.
    public static GameObject MakeSmooth(string name, Mesh mesh, Transform parent)
        => Create(name, mesh, parent, Smooth, true);

    static GameObject Create(string name, Mesh mesh, Transform parent, Material m, bool castShadows)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = true;
        return go;
    }

    public static GameObject MakeGlow(string name, Mesh mesh, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = Glow;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go;
    }
}
