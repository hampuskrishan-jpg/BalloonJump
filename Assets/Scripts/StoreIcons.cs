using UnityEngine;

// Bilder till affären: små 3D-modeller av sakerna som renderas en gång till texturer,
// med samma material och ljus som i spelet.
// 0 ballong, 1 styrning (propeller), 2 uppflygning, 3 sidoturbo, 4 raket, 5 sköld, 6 broms (fallskärm), 7 diamant
public static class StoreIcons
{
    const int Layer = 29, Size = 256;
    static RenderTexture[] icons;

    public static Texture Get(int i) => icons != null && i >= 0 && i < icons.Length ? icons[i] : null;

    public static void Ensure()
    {
        if (icons != null) return;
        icons = new RenderTexture[8];
        var root = new GameObject("Affarsbilder");
        root.transform.position = new Vector3(0, -800f, 0);

        var camGo = new GameObject("IkonKamera");
        camGo.transform.SetParent(root.transform, false);
        var cam = camGo.AddComponent<Camera>();
        cam.enabled = false;
        cam.cullingMask = 1 << Layer;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.17f, 0.21f, 0.27f, 1f);
        cam.fieldOfView = 30f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 20f;
        cam.transform.localPosition = new Vector3(0, 0.15f, -4.2f);
        cam.transform.LookAt(root.transform.position + Vector3.up * 0.1f);

        // eget ljus för bilderna
        var key = new GameObject("Ljus").AddComponent<Light>();
        key.transform.SetParent(root.transform, false);
        key.transform.localPosition = new Vector3(-2f, 2.5f, -3f);
        key.type = LightType.Point;
        key.range = 12f;
        key.intensity = 2.6f;
        key.cullingMask = 1 << Layer;
        var fill = new GameObject("Fyllljus").AddComponent<Light>();
        fill.transform.SetParent(root.transform, false);
        fill.transform.localPosition = new Vector3(2.5f, -0.5f, -2f);
        fill.type = LightType.Point;
        fill.range = 10f;
        fill.intensity = 1.2f;
        fill.color = new Color(0.7f, 0.85f, 1f);
        fill.cullingMask = 1 << Layer;

        bool fog = RenderSettings.fog;
        RenderSettings.fog = false;
        for (int i = 0; i < icons.Length; i++)
        {
            var item = new GameObject("Sak" + i);
            item.transform.SetParent(root.transform, false);
            Build(i, item.transform);
            item.transform.localRotation = Quaternion.Euler(-12f, 28f, 0f);
            foreach (var t in item.GetComponentsInChildren<Transform>()) t.gameObject.layer = Layer;
            icons[i] = new RenderTexture(Size, Size, 24) { name = "Ikon" + i, antiAliasing = 4 };
            cam.targetTexture = icons[i];
            cam.Render();
            item.SetActive(false);
            Object.Destroy(item);
        }
        cam.targetTexture = null;
        RenderSettings.fog = fog;
        Object.Destroy(root);
    }

    static void Build(int i, Transform p)
    {
        var mb = new MeshBuilder();
        var glow = new MeshBuilder();
        Color brass = new Color(0.85f, 0.62f, 0.25f, 0.2f), wood = new Color(0.45f, 0.28f, 0.16f);
        switch (i)
        {
            case 0: // ballong
            {
                Color c = new Color(0.85f, 0.12f, 0.1f, 0.12f);
                mb.Tube(new Vector3(0, -1.2f, 0), new Vector3(0.05f, -0.45f, 0), 0.012f, 0.012f, new Color(0.9f, 0.88f, 0.8f), 5, false);
                mb.Loft(
                    new[] { new Vector3(0, -0.5f, 0), new Vector3(0, -0.4f, 0), new Vector3(0, -0.2f, 0), new Vector3(0, 0.05f, 0), new Vector3(0, 0.35f, 0), new Vector3(0, 0.6f, 0), new Vector3(0, 0.8f, 0), new Vector3(0, 0.9f, 0) },
                    new[] { new Vector2(0.06f, 0.06f), new Vector2(0.3f, 0.3f), new Vector2(0.52f, 0.52f), new Vector2(0.64f, 0.64f), new Vector2(0.64f, 0.64f), new Vector2(0.54f, 0.54f), new Vector2(0.34f, 0.34f), new Vector2(0.1f, 0.1f) },
                    c, 24);
                mb.Loft(new[] { new Vector3(0, -0.56f, 0), new Vector3(0, -0.48f, 0) }, new[] { new Vector2(0.05f, 0.05f), new Vector2(0.1f, 0.1f) }, c, 12);
                break;
            }
            case 1: // propeller (bättre styrning)
                mb.Cylinder(Vector3.zero, 0.2f, 0.3f, 20, brass, Quaternion.Euler(90f, 0, 0));
                for (int k = 0; k < 3; k++)
                {
                    Quaternion q = Quaternion.Euler(0, 0, k * 120f + 15f);
                    mb.Box(q * new Vector3(0, 0.62f, 0), new Vector3(0.32f, 1.05f, 0.06f), wood, q * Quaternion.Euler(0, 18f, 0));
                }
                mb.Sphere(new Vector3(0, 0, -0.18f), Vector3.one * 0.14f, brass, 8, 10);
                break;
            case 2: // uppflygning: pil uppåt
            {
                Color g = new Color(0.25f, 0.75f, 0.35f, 0.25f);
                mb.Box(new Vector3(0, -0.45f, 0), new Vector3(0.38f, 0.9f, 0.38f), g);
                mb.Tube(new Vector3(0, 0f, 0), new Vector3(0, 0.95f, 0), 0.7f, 0.02f, g, 4, false);
                mb.Cylinder(new Vector3(0, 0f, 0), 0.7f, 0.02f, 4, g, Quaternion.Euler(0, 45f, 0));
                break;
            }
            case 3: // sidoturbo: dubbelpil
            {
                Color b = new Color(0.25f, 0.55f, 0.95f, 0.25f);
                mb.Box(Vector3.zero, new Vector3(1.0f, 0.3f, 0.3f), b);
                foreach (int s in new[] { -1, 1 })
                {
                    mb.Tube(new Vector3(s * 0.45f, 0, 0), new Vector3(s * 1.15f, 0, 0), 0.5f, 0.02f, b, 4, false);
                    mb.Cylinder(new Vector3(s * 0.45f, 0, 0), 0.5f, 0.02f, 4, b, Quaternion.Euler(0, 0, 90f) * Quaternion.Euler(0, 45f, 0));
                }
                break;
            }
            case 4: // raket
            {
                Color red = new Color(0.8f, 0.15f, 0.12f, 0.25f), silver = new Color(0.85f, 0.85f, 0.88f, 0.15f), gold = new Color(0.9f, 0.72f, 0.2f, 0.3f);
                Quaternion tilt = Quaternion.Euler(0, 0, -35f);
                mb.Cylinder(tilt * new Vector3(0, -0.1f, 0), 0.3f, 1.2f, 20, red, tilt);
                mb.Tube(tilt * new Vector3(0, 0.5f, 0), tilt * new Vector3(0, 1.05f, 0), 0.3f, 0.02f, silver, 20, false);
                for (int k = 0; k < 3; k++)
                {
                    Quaternion q = tilt * Quaternion.Euler(0, k * 120f, 0);
                    mb.Box(q * new Vector3(0.36f, -0.6f, 0), new Vector3(0.3f, 0.45f, 0.05f), gold, q);
                }
                mb.Cylinder(tilt * new Vector3(0, -0.72f, 0), 0.24f, 0.08f, 16, silver, tilt);
                glow.Sphere(tilt * new Vector3(0, -1.05f, 0), new Vector3(0.22f, 0.45f, 0.22f), new Color(1f, 0.55f, 0.1f, 0.85f), 8, 10, tilt);
                glow.Sphere(tilt * new Vector3(0, -0.92f, 0), new Vector3(0.12f, 0.28f, 0.12f), new Color(1f, 0.95f, 0.6f, 0.95f), 8, 10, tilt);
                break;
            }
            case 5: // sköld i en bubbla
            {
                Color steel = new Color(0.7f, 0.74f, 0.8f, 0.12f), blue = new Color(0.2f, 0.4f, 0.75f, 0.2f);
                mb.Cylinder(Vector3.zero, 0.62f, 0.12f, 28, steel, Quaternion.Euler(90f, 0, 0));
                mb.Cylinder(new Vector3(0, 0, -0.05f), 0.48f, 0.12f, 28, blue, Quaternion.Euler(90f, 0, 0));
                mb.Box(new Vector3(0, 0, -0.12f), new Vector3(0.14f, 0.8f, 0.06f), steel);
                mb.Box(new Vector3(0, 0.08f, -0.12f), new Vector3(0.62f, 0.14f, 0.06f), steel);
                glow.Sphere(Vector3.zero, Vector3.one * 1.02f, new Color(0.5f, 0.85f, 1f, 0.16f), 16, 24);
                break;
            }
            case 6: // fallskärm (broms)
            {
                Color[] cols = { new Color(0.9f, 0.3f, 0.2f), new Color(0.95f, 0.92f, 0.85f) };
                for (int k = 0; k < 8; k++)
                {
                    Quaternion q = Quaternion.Euler(0, k * 45f, 0);
                    mb.Sphere(q * new Vector3(0.38f, 0.55f, 0), new Vector3(0.42f, 0.32f, 0.26f), cols[k % 2], 8, 10, q);
                }
                for (int k = 0; k < 6; k++)
                {
                    float a = k * 60f * Mathf.Deg2Rad;
                    mb.Tube(new Vector3(Mathf.Cos(a) * 0.72f, 0.4f, Mathf.Sin(a) * 0.72f), new Vector3(0, -0.7f, 0), 0.01f, 0.01f, new Color(0.3f, 0.25f, 0.2f), 4, false);
                }
                mb.Box(new Vector3(0, -0.8f, 0), new Vector3(0.25f, 0.22f, 0.2f), wood);
                break;
            }
            default: // diamant
            {
                Color gem = new Color(0.45f, 0.85f, 1f, 0.04f);
                mb.Tube(new Vector3(0, 0.15f, 0), new Vector3(0, -0.85f, 0), 0.75f, 0.01f, gem, 8, false);
                mb.Tube(new Vector3(0, 0.15f, 0), new Vector3(0, 0.45f, 0), 0.75f, 0.45f, gem, 8, false);
                mb.Cylinder(new Vector3(0, 0.45f, 0), 0.45f, 0.01f, 8, gem, Quaternion.identity);
                glow.Sphere(new Vector3(-0.25f, 0.32f, -0.4f), Vector3.one * 0.12f, new Color(1f, 1f, 1f, 0.8f), 6, 8);
                break;
            }
        }
        Toon.MakeSmooth("Modell", mb.ToMesh("ikon" + i), p);
        if (glow.VertexCount > 0) Toon.MakeGlow("Sken", glow.ToMesh("ikonsken" + i), p);
    }
}
