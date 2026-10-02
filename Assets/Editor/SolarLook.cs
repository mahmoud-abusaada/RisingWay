using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The look of the solar-system balls (planets, moons, the Sun), set through
/// Unity's own material API:
///
///   Unity.exe -batchmode -projectPath . -executeMethod SolarLook.Apply -quit
///
/// - Rocky bodies get relief: a normal map made from their own texture (Unity's "from greyscale"
///   import), so craters and ridges catch the light, and a matte surface.
/// - Gas giants get a soft, low sheen. Real planets are not glossy; they were all at 0.3.
/// - The Sun glows from its own texture (its emission map was set, emission itself was off).
/// The bright stars are left as they are: on a phone, with HDR and bloom, they already glow with
/// their texture showing. (This Editor capture has neither, so it cannot judge them.)
/// Safe to run again: it sets values, it does not add to them.
/// </summary>
public static class SolarLook
{
    private const string DIR = "Assets/Materials/Balls/Solar/";

    private static readonly string[] Rocky = { "Moon", "Mercury", "Mars", "Ceres", "Haumea", "Make Make", "Venus" };
    private static readonly string[] Gas = { "Jupiter", "Saturn", "Uranus", "Neptune", "Venus Atmo" };

    private const float ROCKY_SMOOTHNESS = 0.08f;
    private const float GAS_SMOOTHNESS = 0.18f;
    private const float RELIEF = 0.06f;           // heightmap scale for the generated normal maps
    private static readonly Color SunGlow = new Color(2.2f, 1.7f, 1.2f);

    public static void Apply()
    {
        foreach (string name in Rocky)
        {
            Material m = Load(name);
            m.SetFloat("_Smoothness", ROCKY_SMOOTHNESS);
            m.SetFloat("_Glossiness", ROCKY_SMOOTHNESS);
            Texture2D normal = NormalMapFor(m.GetTexture("_BaseMap") as Texture2D);
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.SetFloat("_BumpScale", 1f);
                m.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(m);
            Debug.Log("[Solar] " + name + ": matte, relief " + (normal != null ? normal.name : "none"));
        }

        foreach (string name in Gas)
        {
            Material m = Load(name);
            m.SetFloat("_Smoothness", GAS_SMOOTHNESS);
            m.SetFloat("_Glossiness", GAS_SMOOTHNESS);
            EditorUtility.SetDirty(m);
            Debug.Log("[Solar] " + name + ": soft sheen");
        }

        Material sun = Load("Sun");
        sun.SetTexture("_EmissionMap", sun.GetTexture("_BaseMap"));
        sun.SetColor("_EmissionColor", SunGlow);
        sun.EnableKeyword("_EMISSION");
        sun.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        sun.SetFloat("_Smoothness", 0f);
        EditorUtility.SetDirty(sun);
        Debug.Log("[Solar] Sun: glows from its texture");

        AssetDatabase.SaveAssets();
    }

    private static Material Load(string name)
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(DIR + name + ".mat");
        if (m == null)
            throw new FileNotFoundException(DIR + name + ".mat");
        return m;
    }

    // A copy of the texture imported as a normal map made from its greyscale.
    private static Texture2D NormalMapFor(Texture2D source)
    {
        if (source == null)
            return null;
        string src = AssetDatabase.GetAssetPath(source);
        string dst = Path.Combine(Path.GetDirectoryName(src), Path.GetFileNameWithoutExtension(src) + "_normal" + Path.GetExtension(src)).Replace('\\', '/');
        if (!File.Exists(dst))
            AssetDatabase.CopyAsset(src, dst);
        TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(dst);
        imp.textureType = TextureImporterType.NormalMap;
        imp.convertToNormalmap = true;
        imp.heightmapScale = RELIEF;
        imp.normalmapFilter = TextureImporterNormalFilter.Standard;
        imp.maxTextureSize = 1024;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
    }
}
