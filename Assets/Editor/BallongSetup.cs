using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menyn "Ballongfallet" i Unity: skapa scenen och bygg en Mac-app.
public static class BallongSetup
{
    const string ScenePath = "Assets/Scenes/Ballongfallet.unity";

    [MenuItem("Ballongfallet/Skapa scen")]
    public static void CreateScene()
    {
        System.IO.Directory.CreateDirectory("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>();
        camGo.transform.position = new Vector3(1.5f, 155f, -17f);
        new GameObject("Ballongfallet").AddComponent<Game>();
        EditorSceneManager.SaveScene(scene, ScenePath);

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        PlayerSettings.productName = "Ballongfallet";
        PlayerSettings.companyName = "Hampus";
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        AssetDatabase.SaveAssets();
        Debug.Log("Ballongfallet: scenen skapad");
    }

    // Riktiga fototexturer (Poly Haven, CC0). Ordningen = materialnummer i MeshBuilder.Mat.
    public static readonly string[] Photos =
    {
        "plastered_wall_04", "red_plaster_weathered", "brick_wall_006", "cobblestone_floor_08", "clay_roof_tiles_02",
        "old_planks_02", "rock_wall_02", "metal_plate", "concrete_wall_003",
        // tyger till gubben (10–15): kavaj, knickers, strumpor/väst, skjorta, skor, hår
        "poly_wool_herringbone", "ribbed_corduroy", "jersey_melange", "rough_linen", "brown_leather", "curly_teddy_natural",
        // miljöer (16–20): sand, snö, gräs, sandsten, mossig sten
        "red_sand", "snow_02", "grass_path_2", "sandstone_cracks", "rock_pitted_mossy",
    };

    // Lägger ihop alla fototexturer (med skuggor från AO-kartan) i en texturarray som spelet använder.
    public static void BuildPhotoArray()
    {
        const int size = 1024;
        var arr = new Texture2DArray(size, size, Photos.Length + 1, TextureFormat.RGBA32, true);
        var white = new Color32[size * size];
        for (int i = 0; i < white.Length; i++) white[i] = new Color32(255, 255, 255, 255);
        arr.SetPixels32(white, 0, 0);
        for (int k = 0; k < Photos.Length; k++)
        {
            var diff = Load("Assets/Textures/" + Photos[k] + "_diff.jpg", size);
            var ao = Load("Assets/Textures/" + Photos[k] + "_ao.jpg", size);
            var px = diff.GetPixels32();
            var a = ao != null ? ao.GetPixels32() : null;
            for (int i = 0; i < px.Length; i++)
            {
                float m = a != null ? a[i].r / 255f : 1f;
                m = 0.35f + 0.65f * m;
                px[i] = new Color32((byte)(px[i].r * m), (byte)(px[i].g * m), (byte)(px[i].b * m), 255);
            }
            arr.SetPixels32(px, k + 1, 0);
        }
        arr.wrapMode = TextureWrapMode.Repeat;
        arr.filterMode = FilterMode.Trilinear;
        arr.anisoLevel = 8;
        arr.Apply(true, true);
        AssetDatabase.DeleteAsset("Assets/Resources/Fototexturer.asset");
        AssetDatabase.CreateAsset(arr, "Assets/Resources/Fototexturer.asset");
        AssetDatabase.SaveAssets();
    }

    static Texture2D Load(string path, int size)
    {
        if (!System.IO.File.Exists(path)) return null;
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        t.LoadImage(System.IO.File.ReadAllBytes(path));
        if (t.width == size && t.height == size) return t;
        var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(t, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var r = new Texture2D(size, size, TextureFormat.RGBA32, false);
        r.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        r.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return r;
    }

    // Riktig fotograferad himmel (HDRI från Piazza San Marco) som skybox och reflektioner.
    public static void EnsureSky()
    {
        const string hdr = "Assets/Textures/himmel_piazza_san_marco_2k.hdr";
        var imp = AssetImporter.GetAtPath(hdr) as TextureImporter;
        if (imp != null && imp.textureShape != TextureImporterShape.TextureCube)
        {
            imp.textureShape = TextureImporterShape.TextureCube;
            imp.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
        }
        var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(hdr);
        const string path = "Assets/Resources/Himmel.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null && mat.shader.name != "Skybox/Cubemap") { AssetDatabase.DeleteAsset(path); mat = null; }
        if (mat == null)
        {
            mat = new Material(Shader.Find("Skybox/Cubemap"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetTexture("_Tex", cube);
        mat.SetFloat("_Exposure", 0.9f);
        mat.SetFloat("_Rotation", 200f);
        mat.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f));
        EditorUtility.SetDirty(mat);

        // öppen procedurell himmel för öknen, bergsbyn och snöberget (färgen sätts i spelet)
        const string open = "Assets/Resources/HimmelOppen.mat";
        var om = AssetDatabase.LoadAssetAtPath<Material>(open);
        if (om == null)
        {
            om = new Material(Shader.Find("Skybox/Procedural"));
            AssetDatabase.CreateAsset(om, open);
        }
        om.SetFloat("_SunSize", 0.04f);
        om.SetFloat("_SunSizeConvergence", 5f);
        EditorUtility.SetDirty(om);
        AssetDatabase.SaveAssets();
    }

    // Realistisk människa (Microsoft Rocketbox, MIT-licens). Hår, fransar och bryn behöver
    // ett "cutout"-material så att de genomskinliga delarna av texturen inte syns.
    public static void EnsureGubbe()
    {
        const string fbx = "Assets/Resources/Gubbe/Export/Male_Adult_05.fbx";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        if (model == null) return;
        Material src = null;
        foreach (var r in model.GetComponentsInChildren<Renderer>())
            foreach (var m in r.sharedMaterials)
                if (m != null && m.name.Contains("opacity")) src = m;
        if (src == null) return;
        const string path = "Assets/Resources/Gubbe/HarCutout.mat";
        AssetDatabase.DeleteAsset(path);
        var mat = new Material(src) { name = "HarCutout" };
        mat.SetFloat("_Mode", 1f);
        mat.SetFloat("_Cutoff", 0.4f);
        mat.SetFloat("_Glossiness", 0.15f);
        mat.SetOverrideTag("RenderType", "TransparentCutout");
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.renderQueue = 2450;
        mat.SetFloat("_Cull", 0f);
        AssetDatabase.CreateAsset(mat, path);
        AssetDatabase.SaveAssets();
    }

    // Riktiga inspelade ljud: avkodas vid start så att spelet kan dela upp dem i flera smällar.
    public static void EnsureAudio()
    {
        var imp = AssetImporter.GetAtPath("Assets/Resources/Ljud/ballong_pang.ogg") as AudioImporter;
        if (imp == null) return;
        var st = imp.defaultSampleSettings;
        st.loadType = AudioClipLoadType.DecompressOnLoad;
        st.compressionFormat = AudioCompressionFormat.Vorbis;
        st.quality = 1f;
        imp.defaultSampleSettings = st;
        imp.forceToMono = true;
        imp.SaveAndReimport();
    }

    [MenuItem("Ballongfallet/Bygg Mac-app")]
    public static void BuildMac()
    {
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;   // mobil: liggande skärm
        EnsureSky();
        BuildPhotoArray();
        EnsureGubbe();
        EnsureAudio();
        if (!System.IO.File.Exists(ScenePath)) CreateScene();
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Build/Ballongfallet.app",
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.None,
        };
        var report = BuildPipeline.BuildPlayer(opts);
        Debug.Log("Ballongfallet BUILD: " + report.summary.result);
        if (Application.isBatchMode && report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }
}
