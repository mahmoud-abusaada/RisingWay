// Puts the new sky into the scene: the galaxy glow and the star field (SpaceSky.cs) in place of
// the galaxy cylinder, the nebula cube and the star particle systems. The old objects are switched
// off, not deleted, so going back is switching them on again and removing the SpaceSky component.
//
//   Unity.exe -batchmode -quit -projectPath <project> -executeMethod SpaceSkySetup.Run -logFile <log>
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SpaceSkySetup
{
    private const string TAG = "[SpaceSkySetup] ";
    private const string GALAXY_TEXTURE = "Assets/Textures/Ambient/Sky/MilkyWayGlow.png";
    private const string MATERIALS = "Assets/Materials/Sky";

    public static void Run()
    {
        // The galaxy texture: smooth glow, so 2048 wide and compressed hard is enough. Repeats
        // left to right (longitude), clamps at the poles.
        AssetDatabase.ImportAsset(GALAXY_TEXTURE);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(GALAXY_TEXTURE);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.wrapModeU = TextureWrapMode.Repeat;
        importer.wrapModeV = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 2048;
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
        {
            name = "Android", overridden = true, maxTextureSize = 2048, format = TextureImporterFormat.ASTC_4x4
        });
        importer.SaveAndReimport();

        if (!AssetDatabase.IsValidFolder(MATERIALS))
            AssetDatabase.CreateFolder("Assets/Materials", "Sky");
        Material galaxy = material(MATERIALS + "/Galaxy.mat", "RisingWay/Space Sky");
        galaxy.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(GALAXY_TEXTURE));
        Material stars = material(MATERIALS + "/Stars.mat", "RisingWay/Star Field");
        Material nearStars = material(MATERIALS + "/Near Stars.mat", "RisingWay/Near Star");
        EditorUtility.SetDirty(nearStars);
        EditorUtility.SetDirty(galaxy);
        EditorUtility.SetDirty(stars);
        AssetDatabase.SaveAssets();

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        AmbientEffectsController ambient = Object.FindAnyObjectByType<AmbientEffectsController>(FindObjectsInactive.Include);
        SpaceSky sky = Object.FindAnyObjectByType<SpaceSky>(FindObjectsInactive.Include);
        if (sky == null)
        {
            GameObject go = new GameObject("SpaceSky");
            go.transform.SetParent(ambient.transform, false);
            sky = go.AddComponent<SpaceSky>();
        }
        SerializedObject so = new SerializedObject(sky);
        so.FindProperty("galaxyMaterial").objectReferenceValue = galaxy;
        so.FindProperty("starsMaterial").objectReferenceValue = stars;
        so.FindProperty("nearStarsMaterial").objectReferenceValue = nearStars;
        so.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject controller = new SerializedObject(ambient);
        controller.FindProperty("spaceSky").objectReferenceValue = sky;
        controller.ApplyModifiedPropertiesWithoutUndo();

        foreach (string old in new[] { "GalaxyCylinder", "CubeSkyBox", "StarsContainer" })
        {
            bool found = false;
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == old)
                {
                    t.gameObject.SetActive(false);
                    found = true;
                    Debug.Log(TAG + "switched off " + old);
                }
            if (!found)
                Debug.LogWarning(TAG + old + " not found");
        }

        setUpSolarSystem();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(TAG + "done");
    }

    // The solar system: its own materials (the planets used to share the balls' materials, in the
    // Lit shader), the Sun's surface and glow, trails and Saturn's rings.
    private const string SOLAR = "Assets/Materials/SolarSystem";
    private const string TEXTURES = "Assets/Textures/Balls/Solar/";

    private static void setUpSolarSystem()
    {
        if (!AssetDatabase.IsValidFolder(SOLAR))
            AssetDatabase.CreateFolder("Assets/Materials", "SolarSystem");

        // There are two SolarSystem components in the scene (one on AmbientEffects, which is the
        // one that runs, and one on the solar system's own Parent object): both get the same.
        SolarSystem[] all = Object.FindObjectsByType<SolarSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Debug.Log(TAG + all.Length + " SolarSystem component(s)");

        // name, texture, atmosphere colour (HDR), atmosphere thinness
        object[][] planets =
        {
            new object[] { "mercury", "2k_mercury.jpg", Color.black, 3f },
            new object[] { "venus", "2k_venus_atmosphere.jpg", new Color(1.1f, 0.85f, 0.45f), 2.5f },
            new object[] { "earth", "2k_earth_daymap.jpg", new Color(0.35f, 0.65f, 1.6f), 2.6f },
            new object[] { "mars", "2k_mars.jpg", new Color(0.7f, 0.32f, 0.2f), 4f },
            new object[] { "jupiter", "2k_jupiter.jpg", new Color(0.45f, 0.38f, 0.3f), 4f },
            new object[] { "saturn", "2k_saturn.jpg", new Color(0.5f, 0.45f, 0.32f), 4f },
            new object[] { "uranus", "2k_uranus.jpg", new Color(0.35f, 0.75f, 0.85f), 3f },
            new object[] { "neptune", "2k_neptune.jpg", new Color(0.25f, 0.4f, 1.3f), 3f },
            new object[] { "pluto", "2k_ceres_fictional.jpg", Color.black, 3f },
        };
        foreach (object[] p in planets)
        {
            string name = (string)p[0];
            Material m = material(SOLAR + "/" + char.ToUpper(name[0]) + name.Substring(1) + ".mat", "RisingWay/Planet");
            m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TEXTURES + (string)p[1]));
            m.SetColor("_Atmosphere", (Color)p[2]);
            m.SetFloat("_AtmospherePower", (float)p[3]);
            if (name == "earth")
            {
                m.SetTexture("_NightTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TEXTURES + "2k_earth_nightmap.jpg"));
                m.SetColor("_NightColor", new Color(1.6f, 1.2f, 0.7f));
            }
            EditorUtility.SetDirty(m);
            foreach (SolarSystem solar in all)
                set(solar, name, m);
        }

        Material sun = material(SOLAR + "/Sun.mat", "RisingWay/Sun");
        sun.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TEXTURES + "2k_sun.jpg"));
        Material corona = material(SOLAR + "/Sun Corona.mat", "RisingWay/Sun Corona");
        Material trail = material(SOLAR + "/Trail.mat", "RisingWay/Space Line");
        Material rings = material(SOLAR + "/Saturn Rings.mat", "RisingWay/Space Line");
        rings.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TEXTURES + "2k_saturn_ring_alpha.png"));
        rings.SetFloat("_RingFromCentre", 1f);
        rings.SetColor("_Color", new Color(0.9f, 0.85f, 0.75f, 1f));
        foreach (Material m in new[] { sun, corona, trail, rings })
            EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();

        foreach (SolarSystem solar in all)
        {
            set(solar, "coronaMaterial", corona);
            set(solar, "trailMaterial", trail);
            set(solar, "ringMaterial", rings);
            Transform sunTrail = new SerializedObject(solar).FindProperty("sunTrail").objectReferenceValue as Transform;
            if (sunTrail == null)
                continue;
            MeshRenderer sunRenderer = sunTrail.parent.GetComponent<MeshRenderer>();
            sunRenderer.sharedMaterial = sun;
            sunRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            EditorUtility.SetDirty(sunRenderer);
            Debug.Log(TAG + "sun material set on " + sunTrail.parent.name + " (SolarSystem on " + solar.name + ")");
        }
    }

    private static void set(Object target, string property, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        so.FindProperty(property).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Made once; afterwards its values are whatever was tuned in the Inspector.
    private static Material material(string path, string shader)
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find(shader));
            AssetDatabase.CreateAsset(m, path);
            Debug.Log(TAG + "created " + path);
        }
        return m;
    }
}
