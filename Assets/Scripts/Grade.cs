using UnityEngine;

// Filmiskt efterfilter på huvudkameran: glöd, färgkorrigering och filmbrus.
[RequireComponent(typeof(Camera))]
public class Grade : MonoBehaviour
{
    public bool cave;
    Material mat;

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (mat == null)
        {
            var s = Shader.Find("Ballong/Grade");
            if (s == null) { Graphics.Blit(src, dst); return; }
            mat = new Material(s);
        }
        int w = src.width / 4, h = src.height / 4;
        var a = RenderTexture.GetTemporary(w, h, 0, src.format);
        var b = RenderTexture.GetTemporary(w, h, 0, src.format);
        Graphics.Blit(src, a, mat, 0);
        for (int i = 0; i < 2; i++)
        {
            mat.SetVector("_Dir", new Vector2(1.5f, 0));
            Graphics.Blit(a, b, mat, 1);
            mat.SetVector("_Dir", new Vector2(0, 1.5f));
            Graphics.Blit(b, a, mat, 1);
        }
        mat.SetTexture("_Bloom", a);
        mat.SetFloat("_BloomStrength", cave ? 0.9f : 0.6f);
        mat.SetFloat("_Desat", cave ? 0.3f : 0.38f);
        mat.SetFloat("_Grain", 0.045f);
        mat.SetFloat("_Cave", cave ? 1f : 0f);
        Graphics.Blit(src, dst, mat, 2);
        RenderTexture.ReleaseTemporary(a);
        RenderTexture.ReleaseTemporary(b);
    }
}
