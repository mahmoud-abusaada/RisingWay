using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Close-up renders of the Earth ball, to check its layers (water, clouds, night lights) by eye:
///
///   Unity.exe -batchmode -projectPath . -executeMethod EarthCloseup.Render -out &lt;folder&gt;
///
/// Writes earth_fixed.png with the materials as they are, and earth_preserveSpecular.png with the
/// layers switched (in memory only) to URP's "preserve specular" blending - what the Unity 6
/// upgrade had set, which lets the layers' glow and reflections cover the whole sphere.
/// </summary>
public static class EarthCloseup
{
    private const string SCENE = "Assets/Scenes/SampleScene.unity";

    public static void Render()
    {
        string outDir = Arg("-out") ?? "Builds/EarthCloseup";
        Directory.CreateDirectory(outDir);
        EditorSceneManager.OpenScene(SCENE);

        PlayerLinkedObjectsController linked = Object.FindAnyObjectByType<PlayerLinkedObjectsController>(FindObjectsInactive.Include);
        Transform ball = linked.transform;
        SerializedObject so = new SerializedObject(linked);
        ((Transform)so.FindProperty("earthStuff").objectReferenceValue).gameObject.SetActive(true);
        ((Transform)so.FindProperty("defaultStuff").objectReferenceValue).gameObject.SetActive(false);
        ball.gameObject.SetActive(true);
        ball.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Balls/Solar/Earth.mat");
        Transform atmosphere = (Transform)so.FindProperty("earthAtmosphere").objectReferenceValue;
        foreach (string f in new[] { "earthWater", "earthClouds", "earthNight", "earthAtmosphere" })
            for (Transform t = (Transform)so.FindProperty(f).objectReferenceValue; t != null && t != ball; t = t.parent)
                t.gameObject.SetActive(true);
        SphereCollider sc = ball.GetComponent<SphereCollider>();
        Vector3 ls = ball.lossyScale;
        Debug.Log("[Earth] '" + ball.name + "' collider radius " + (sc.radius * Mathf.Max(ls.x, ls.y, ls.z)).ToString("F4") +
                  " world, mesh " + ball.GetComponent<MeshFilter>().sharedMesh.name + " bounds " + ball.GetComponent<MeshFilter>().sharedMesh.bounds.extents.x.ToString("F4") +
                  " local, lossyScale " + ls.ToString("F4"));

        Camera cam = new GameObject("~earthCam").AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.02f, 0.06f);
        Bounds b = ball.GetComponent<Renderer>().bounds;
        float r = b.extents.x;
        cam.transform.position = b.center + new Vector3(0.3f, 0.6f, -1f).normalized * r * 5f;
        cam.transform.LookAt(b.center);
        cam.nearClipPlane = r * 0.5f;
        Debug.Log("[Earth] ball at " + b.center + " radius " + r + ", layers: " + DescribeLayers(so));
        cam.fieldOfView = 30f;
        atmosphere.LookAt(cam.transform.position);
        cam.cullingMask = ~0;
        RenderTexture rt = new RenderTexture(768, 768, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt;

        Shoot(cam, rt, Path.Combine(outDir, "earth_fixed.png"));

        // Copies, never the assets themselves: an asset changed here is written back to disk
        // when the Editor exits.
        foreach (string f in new[] { "earthWater", "earthClouds", "earthNight" })
        {
            Renderer layer = ((Transform)so.FindProperty(f).objectReferenceValue).GetComponent<Renderer>();
            Material m = new Material(layer.sharedMaterial);
            m.SetFloat("_BlendModePreserveSpecular", 1f);
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            layer.sharedMaterial = m;
        }
        Shoot(cam, rt, Path.Combine(outDir, "earth_preserveSpecular.png"));

        // Nothing is saved: the scene and the materials are left as they are on disk.
        EditorApplication.Exit(0);
    }

    private static string DescribeLayers(SerializedObject so)
    {
        string s = "";
        foreach (string f in new[] { "earthWater", "earthClouds", "earthNight", "earthAtmosphere" })
        {
            Transform t = (Transform)so.FindProperty(f).objectReferenceValue;
            Renderer rr = t.GetComponent<Renderer>();
            s += f + "(" + (t.gameObject.activeInHierarchy ? "on" : "off") + ", " + (rr != null && rr.sharedMaterial != null ? rr.sharedMaterial.name : "-") +
                 ", r=" + (rr != null ? rr.bounds.extents.x.ToString("F3") : "-") + ") ";
        }
        return s;
    }

    private static void Shoot(Camera cam, RenderTexture rt, string file)
    {
        cam.Render();
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(file, tex.EncodeToPNG());
        Debug.Log("[Earth] wrote " + file);
    }

    private static string Arg(string name)
    {
        string[] a = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++)
            if (a[i] == name) return a[i + 1];
        return null;
    }
}
