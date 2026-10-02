using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Store art from the game's own pictures and font. Batch mode:
///
///   Unity.exe -batchmode -projectPath . -executeMethod StoreArtComposer.ContactSheet -in <folder> -out <file.png>
///   Unity.exe -batchmode -projectPath . -executeMethod StoreArtComposer.Compose -spec <spec.json>
///
/// Compose reads a list of pictures to make (screenshots with captions, the feature graphic, the
/// icon) from a JSON spec - see Tools/StoreArt/spec.json. Each picture is a background image
/// (cropped and zoomed), an optional darkening gradient, and up to two lines of text in Rexlia,
/// the font the game uses everywhere, with its own outlined title material. It is rendered by a
/// camera in a scratch scene, so the text looks exactly as it does in the game.
/// </summary>
public static class StoreArtComposer
{
    [Serializable] public class Spec { public Item[] items; }

    [Serializable]
    public class Item
    {
        public string src;               // background picture
        public string @out;              // output file (.png or .jpg)
        public int width = 1440, height = 2560;
        public float cx = 0.5f, cy = 0.5f; // centre of the crop, 0..1 of the source
        public float zoom = 1f;          // 1 = the source's full width
        public float shade = 0.45f;      // top gradient reaches this fraction of the height (0 = none)
        public float shadeAlpha = 0.85f;
        public float bottomShade = 0f;   // same from the bottom (for the feature graphic)
        public string title;
        public string sub;
        public float titleSize = 150f, subSize = 62f;
        public float titleY = 0.09f;     // top of the title block, fraction of height from the top
        public string align = "center";  // center | left
        public float marginX = 0.07f;
        public string subColor = "#8FE3FF";
        public float sy0 = -1f, sy1 = -1f; // band layout: these source rows (0..1 from the top), full width,
        public string band = "#03040C";    // at the bottom of the picture, under a plain band for the caption
    }

    private const string FONT = "Assets/Fonts/Rexlia SDF.asset";

    // ---- Contact sheet -------------------------------------------------------------------------

    public static void ContactSheet()
    {
        string folder = Arg("-in"), outFile = Arg("-out");
        int tw = 216, th = 384, cols = 6;
        string[] files = Directory.GetFiles(folder, "*.png").Where(f => !f.EndsWith("sheet.png")).OrderBy(f => f).ToArray();
        int rows = (files.Length + cols - 1) / cols;
        Texture2D sheet = new Texture2D(cols * tw, rows * (th + 24), TextureFormat.RGB24, false);
        Color32[] fill = Enumerable.Repeat(new Color32(40, 40, 40, 255), sheet.width * sheet.height).ToArray();
        sheet.SetPixels32(fill);
        for (int i = 0; i < files.Length; i++)
        {
            Texture2D src = Load(files[i]);
            int ox = (i % cols) * tw, oy = sheet.height - (i / cols + 1) * (th + 24) + 24;
            for (int y = 0; y < th; y++)
                for (int x = 0; x < tw; x++)
                    sheet.SetPixel(ox + x, oy + y, src.GetPixelBilinear((x + 0.5f) / tw, (y + 0.5f) / th));
            UnityEngine.Object.DestroyImmediate(src);
            Debug.Log("[SA] sheet " + (i + 1) + ": " + Path.GetFileName(files[i]));
        }
        sheet.Apply();
        File.WriteAllBytes(outFile, sheet.EncodeToPNG());
        Debug.Log("[SA] wrote " + outFile + " (" + files.Length + " pictures, numbered left to right, top to bottom)");
        EditorApplication.Exit(0);
    }

    // ---- Composition ---------------------------------------------------------------------------

    public static void Compose()
    {
        Spec spec = JsonUtility.FromJson<Spec>(File.ReadAllText(Arg("-spec")));
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT);
        foreach (Item item in spec.items)
        {
            try { Render(item, font); }
            catch (Exception e) { Debug.LogError("[SA] " + item.@out + ": " + e); }
        }
        EditorApplication.Exit(0);
    }

    private static void Render(Item it, TMP_FontAsset font)
    {
        int W = it.width, H = it.height;
        GameObject root = new GameObject("~compose");
        Camera cam = new GameObject("cam").AddComponent<Camera>();
        cam.transform.SetParent(root.transform);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.orthographic = true;
        cam.cullingMask = 1 << 5;
        RenderTexture rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 8;
        cam.targetTexture = rt;

        Canvas canvas = new GameObject("canvas", typeof(Canvas)).GetComponent<Canvas>();
        canvas.transform.SetParent(root.transform);
        canvas.gameObject.layer = 5;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 1f;
        RectTransform cr = (RectTransform)canvas.transform;

        // Background, cropped: the crop keeps the output's aspect, `zoom` times narrower than the source.
        Texture2D src = Load(it.src);
        float outAspect = (float)W / H, srcAspect = (float)src.width / src.height;
        float uw = 1f / it.zoom, uh = uw * srcAspect / outAspect;
        if (uh > 1f) { uw /= uh; uh = 1f; }
        float ux = Mathf.Clamp(it.cx - uw / 2f, 0f, 1f - uw), uy = Mathf.Clamp((1f - it.cy) - uh / 2f, 0f, 1f - uh);
        RawImage bg = Child<RawImage>(cr, "bg");
        bg.texture = src;
        bg.uvRect = new Rect(ux, uy, uw, uh);
        Stretch(bg.rectTransform);
        if (it.sy1 > it.sy0 && it.sy0 >= 0f)
        {
            ColorUtility.TryParseHtmlString(it.band, out Color bandColor);
            cam.backgroundColor = bandColor;
            bg.uvRect = new Rect(0f, 1f - it.sy1, 1f, it.sy1 - it.sy0);
            RectTransform r = bg.rectTransform;
            r.anchorMin = Vector2.zero; r.anchorMax = new Vector2(1f, 0f);
            r.pivot = new Vector2(0.5f, 0f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = new Vector2(0f, W * (it.sy1 - it.sy0) * src.height / src.width);
        }

        if (it.shade > 0f) Shade(cr, "shadeTop", it.shade, it.shadeAlpha, true);
        if (it.bottomShade > 0f) Shade(cr, "shadeBottom", it.bottomShade, it.shadeAlpha, false);

        float margin = W * it.marginX;
        TextAlignmentOptions align = it.align == "left" ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Top;
        float y = H * it.titleY;
        if (!string.IsNullOrEmpty(it.title))
        {
            TextMeshProUGUI t = Text(cr, "title", font, it.title, it.titleSize, Color.white, align, margin, y);
            t.fontStyle = FontStyles.UpperCase;
            t.ForceMeshUpdate();
            y += t.preferredHeight + it.titleSize * 0.15f;
        }
        if (!string.IsNullOrEmpty(it.sub))
        {
            Color c;
            ColorUtility.TryParseHtmlString(it.subColor, out c);
            Text(cr, "sub", font, it.sub, it.subSize, c, align, margin, y);
        }

        Canvas.ForceUpdateCanvases();
        cam.Render();
        RenderTexture.active = rt;
        Texture2D outTex = new Texture2D(W, H, TextureFormat.RGB24, false);
        outTex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        outTex.Apply();
        RenderTexture.active = null;
        Directory.CreateDirectory(Path.GetDirectoryName(it.@out));
        byte[] bytes = it.@out.EndsWith(".jpg") ? outTex.EncodeToJPG(95) : outTex.EncodeToPNG();
        File.WriteAllBytes(it.@out, bytes);
        Debug.Log("[SA] wrote " + it.@out + " " + W + "x" + H + " (" + bytes.Length / 1024 + " KB)");

        cam.targetTexture = null;
        rt.Release();
        UnityEngine.Object.DestroyImmediate(outTex);
        UnityEngine.Object.DestroyImmediate(src);
        UnityEngine.Object.DestroyImmediate(root);
    }

    private static TextMeshProUGUI Text(RectTransform parent, string name, TMP_FontAsset font, string text, float size,
                                        Color color, TextAlignmentOptions align, float margin, float top)
    {
        TextMeshProUGUI t = Child<TextMeshProUGUI>(parent, name);
        t.font = font;
        t.fontSharedMaterial = font.material; // the game's outlined title look
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.enableWordWrapping = true;
        RectTransform r = t.rectTransform;
        r.anchorMin = new Vector2(0, 1);
        r.anchorMax = new Vector2(1, 1);
        r.pivot = new Vector2(0.5f, 1f);
        r.offsetMin = new Vector2(margin, 0);
        r.offsetMax = new Vector2(-margin, 0);
        r.sizeDelta = new Vector2(r.sizeDelta.x, size * 4f);
        r.anchoredPosition = new Vector2(r.anchoredPosition.x, -top);
        return t;
    }

    // A vertical fade: opaque black at the edge, clear at `reach` of the height.
    private static void Shade(RectTransform parent, string name, float reach, float alpha, bool top)
    {
        Texture2D g = new Texture2D(1, 256, TextureFormat.RGBA32, false);
        g.wrapMode = TextureWrapMode.Clamp;
        for (int i = 0; i < 256; i++)
        {
            float t = i / 255f;               // 0 bottom .. 1 top of the texture
            float fromEdge = top ? 1f - t : t; // 0 at the shaded edge
            float a = Mathf.Clamp01(1f - fromEdge / reach);
            g.SetPixel(0, i, new Color(0.01f, 0.015f, 0.05f, alpha * a * a * (3f - 2f * a)));
        }
        g.Apply();
        RawImage r = Child<RawImage>(parent, name);
        r.texture = g;
        Stretch(r.rectTransform);
    }

    private static T Child<T>(RectTransform parent, string name) where T : Component
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return go.AddComponent<T>();
    }

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }

    private static Texture2D Load(string file)
    {
        Texture2D t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        t.LoadImage(File.ReadAllBytes(file));
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Trilinear;
        return t;
    }

    private static string Arg(string name)
    {
        string[] a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++)
            if (a[i] == name) return a[i + 1];
        return null;
    }
}
