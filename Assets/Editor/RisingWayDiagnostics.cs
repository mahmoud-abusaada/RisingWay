using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Dumps render-pipeline, scene, camera, material and texture state to a text file.
///
/// Built for the P1-01 Unity 6 spike so render state can be inspected without screenshots and
/// back-and-forth. Kept afterwards - the same information is what you want whenever something
/// renders differently than expected across an engine or URP upgrade.
///
///   Tools > Rising Way > Dump Diagnostics
///
/// Writes to  &lt;projectRoot&gt;/Diagnostics.txt  (overwritten each run).
///
/// Run it TWICE and send both:
///   1. In Edit Mode  - asset-level truth: import settings, shared materials, project config.
///   2. In Play Mode  - runtime truth: instanced materials after scripts have mutated them,
///                      live camera state, actual ambient probe.
///
/// Play Mode matters here because AmbientEffectsController animates material colour and alpha
/// from Update/FixedUpdate, so the asset on disk is not what is on screen.
///
/// Editor-only: lives under Assets/Editor/ and never ships.
/// </summary>
public static class RisingWayDiagnostics
{
    private const string OUTPUT_FILE = "Diagnostics.txt";

    /// <summary>
    /// Objects worth dumping in detail regardless of selection. Names are matched against the
    /// whole scene, so renaming one just means it stops being reported.
    /// </summary>
    private static readonly string[] InterestingObjectNames =
    {
        "GalaxyCylinder",
        "GalaxySky",
        "CubeSkybox",
        "StarsContainer",
        "CloudsContainer",
        "SolarSystem",
        "Player",
    };

    [MenuItem("Tools/Rising Way/Dump Diagnostics")]
    public static void Dump()
    {
        StringBuilder sb = new StringBuilder();

        Section(sb, "RUN INFO", () =>
        {
            Line(sb, "Timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Line(sb, "Unity version", Application.unityVersion);
            Line(sb, "Mode", Application.isPlaying ? "PLAY MODE (runtime state)" : "EDIT MODE (asset state)");
            Line(sb, "Active build target", EditorUserBuildSettings.activeBuildTarget.ToString());
            Line(sb, "Platform", Application.platform.ToString());
        });

        Section(sb, "PROJECT SETTINGS", () =>
        {
            Line(sb, "Color space", PlayerSettings.colorSpace.ToString());
            Line(sb, "Quality level", QualitySettings.names[QualitySettings.GetQualityLevel()]);
            Line(sb, "Target frame rate", Application.targetFrameRate.ToString());
            Line(sb, "vSyncCount", QualitySettings.vSyncCount.ToString());
        });

        Section(sb, "RENDER PIPELINE", () =>
        {
            RenderPipelineAsset current = GraphicsSettings.currentRenderPipeline;
            RenderPipelineAsset quality = QualitySettings.renderPipeline;

            Line(sb, "GraphicsSettings.currentRenderPipeline", current == null ? "<NULL - BUILT-IN PIPELINE>" : current.name);
            Line(sb, "QualitySettings.renderPipeline", quality == null ? "<null - falls back to Graphics setting>" : quality.name);

            UniversalRenderPipelineAsset urp = current as UniversalRenderPipelineAsset;
            if (urp == null)
            {
                Line(sb, "URP asset", "<not a UniversalRenderPipelineAsset>");
                return;
            }

            Line(sb, "supportsHDR", urp.supportsHDR.ToString());
            Line(sb, "msaaSampleCount", urp.msaaSampleCount.ToString());
            Line(sb, "renderScale", urp.renderScale.ToString("0.###"));
            Line(sb, "supportsCameraDepthTexture", urp.supportsCameraDepthTexture.ToString());
            Line(sb, "supportsCameraOpaqueTexture", urp.supportsCameraOpaqueTexture.ToString());
            Line(sb, "useSRPBatcher", urp.useSRPBatcher.ToString());
            Line(sb, "supportsDynamicBatching", urp.supportsDynamicBatching.ToString());
        });

        Section(sb, "SCENE RENDER SETTINGS (ambient / fog / skybox)", () =>
        {
            Line(sb, "ambientMode", RenderSettings.ambientMode.ToString());
            Line(sb, "ambientIntensity", RenderSettings.ambientIntensity.ToString("0.###"));
            Line(sb, "ambientLight (flat)", Col(RenderSettings.ambientLight));
            Line(sb, "ambientSkyColor", Col(RenderSettings.ambientSkyColor));
            Line(sb, "ambientEquatorColor", Col(RenderSettings.ambientEquatorColor));
            Line(sb, "ambientGroundColor", Col(RenderSettings.ambientGroundColor));
            Line(sb, "skybox material", RenderSettings.skybox == null ? "<none>" : RenderSettings.skybox.name);
            Line(sb, "fog", RenderSettings.fog.ToString());
            if (RenderSettings.fog)
            {
                Line(sb, "  fogMode", RenderSettings.fogMode.ToString());
                Line(sb, "  fogColor", Col(RenderSettings.fogColor));
            }
            Line(sb, "reflectionIntensity", RenderSettings.reflectionIntensity.ToString("0.###"));
        });

        Section(sb, "CAMERAS", () =>
        {
            Camera[] cams = FindAll<Camera>();
            Line(sb, "camera count", cams.Length.ToString());
            foreach (Camera cam in cams)
                DumpCamera(sb, cam);
        });

        Section(sb, "INTERESTING OBJECTS", () =>
        {
            foreach (string name in InterestingObjectNames)
            {
                GameObject go = FindByName(name);
                if (go == null)
                {
                    sb.AppendLine("  " + name + ": <not found in scene>");
                    continue;
                }
                DumpGameObject(sb, go);
            }
        });

        Section(sb, "SELECTED OBJECTS", () =>
        {
            if (Selection.gameObjects == null || Selection.gameObjects.Length == 0)
            {
                sb.AppendLine("  <nothing selected - select objects in the Hierarchy to dump them here>");
                return;
            }
            foreach (GameObject go in Selection.gameObjects)
                DumpGameObject(sb, go);
        });

        string path = Path.Combine(Application.dataPath, "..", OUTPUT_FILE);
        path = Path.GetFullPath(path);

        try
        {
            File.WriteAllText(path, sb.ToString());
            Debug.Log("Rising Way diagnostics written to:\n" + path);
            EditorUtility.RevealInFinder(path);
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to write diagnostics: " + e.Message);
        }
    }

    // ------------------------------------------------------------------ dumpers

    private static void DumpCamera(StringBuilder sb, Camera cam)
    {
        sb.AppendLine();
        sb.AppendLine("  [Camera] " + HierarchyPath(cam.transform) + (cam.isActiveAndEnabled ? "" : "   (INACTIVE)"));
        Line(sb, "    clearFlags", cam.clearFlags.ToString());
        Line(sb, "    backgroundColor", Col(cam.backgroundColor));
        Line(sb, "    allowHDR", cam.allowHDR.ToString());
        Line(sb, "    allowMSAA", cam.allowMSAA.ToString());
        Line(sb, "    depth", cam.depth.ToString("0.##"));
        Line(sb, "    fieldOfView", cam.fieldOfView.ToString("0.##"));
        Line(sb, "    cullingMask", "0x" + cam.cullingMask.ToString("X8"));

        try
        {
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                Line(sb, "    URP renderType", data.renderType.ToString());
                Line(sb, "    URP renderPostProcessing", data.renderPostProcessing.ToString());
                Line(sb, "    URP requiresDepthTexture", data.requiresDepthTexture.ToString());
                if (data.renderType == CameraRenderType.Base && data.cameraStack != null)
                {
                    Line(sb, "    URP cameraStack count", data.cameraStack.Count.ToString());
                    for (int i = 0; i < data.cameraStack.Count; i++)
                        Line(sb, "      stack[" + i + "]", data.cameraStack[i] == null ? "<null>" : data.cameraStack[i].name);
                }
            }
        }
        catch (Exception e)
        {
            Line(sb, "    URP camera data", "<unavailable: " + e.Message + ">");
        }
    }

    private static void DumpGameObject(StringBuilder sb, GameObject go)
    {
        sb.AppendLine();
        sb.AppendLine("  [GameObject] " + HierarchyPath(go.transform));
        Line(sb, "    activeInHierarchy", go.activeInHierarchy.ToString());
        Line(sb, "    layer", LayerMask.LayerToName(go.layer) + " (" + go.layer + ")");
        Line(sb, "    world position", go.transform.position.ToString("0.##"));
        Line(sb, "    lossyScale", go.transform.lossyScale.ToString("0.##"));

        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            sb.AppendLine("    <no renderers>");
            return;
        }

        foreach (Renderer r in renderers)
        {
            sb.AppendLine("    [Renderer] " + HierarchyPath(r.transform) + "  (" + r.GetType().Name + ")");
            Line(sb, "      enabled", r.enabled.ToString());
            Line(sb, "      isVisible", r.isVisible.ToString());
            Line(sb, "      shadowCastingMode", r.shadowCastingMode.ToString());
            Line(sb, "      sortingOrder", r.sortingOrder.ToString());

            // sharedMaterial in Edit Mode: reading .material there leaks an instance into the scene.
            // In Play Mode .material is what is actually on screen after scripts mutate it.
            Material[] mats = Application.isPlaying ? r.materials : r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                DumpMaterial(sb, mats[i], "      mat[" + i + "] ");
        }
    }

    private static void DumpMaterial(StringBuilder sb, Material m, string prefix)
    {
        if (m == null)
        {
            sb.AppendLine(prefix + "<NULL MATERIAL>");
            return;
        }

        sb.AppendLine(prefix + m.name + "   shader=" + (m.shader == null ? "<NULL SHADER>" : m.shader.name));

        if (m.shader != null && !m.shader.isSupported)
            sb.AppendLine(prefix + "  *** SHADER NOT SUPPORTED ON THIS PLATFORM ***");

        Line(sb, prefix + "  renderQueue", m.renderQueue.ToString());

        string[] kw = m.shaderKeywords;
        Line(sb, prefix + "  keywords", (kw == null || kw.Length == 0) ? "<none>" : string.Join(", ", kw));

        DumpFloatIfPresent(sb, m, "_Surface", prefix);
        DumpFloatIfPresent(sb, m, "_Blend", prefix);
        DumpFloatIfPresent(sb, m, "_SrcBlend", prefix);
        DumpFloatIfPresent(sb, m, "_DstBlend", prefix);
        DumpFloatIfPresent(sb, m, "_ZWrite", prefix);
        DumpFloatIfPresent(sb, m, "_Cull", prefix);
        DumpFloatIfPresent(sb, m, "_AlphaClip", prefix);

        DumpColorIfPresent(sb, m, "_BaseColor", prefix);
        DumpColorIfPresent(sb, m, "_Color", prefix);
        DumpColorIfPresent(sb, m, "_EmissionColor", prefix);

        DumpTextureIfPresent(sb, m, "_BaseMap", prefix);
        DumpTextureIfPresent(sb, m, "_MainTex", prefix);
        DumpTextureIfPresent(sb, m, "_EmissionMap", prefix);
    }

    private static void DumpFloatIfPresent(StringBuilder sb, Material m, string prop, string prefix)
    {
        if (m.HasProperty(prop))
            Line(sb, prefix + "  " + prop, m.GetFloat(prop).ToString("0.###"));
    }

    private static void DumpColorIfPresent(StringBuilder sb, Material m, string prop, string prefix)
    {
        if (m.HasProperty(prop))
            Line(sb, prefix + "  " + prop, Col(m.GetColor(prop)));
    }

    private static void DumpTextureIfPresent(StringBuilder sb, Material m, string prop, string prefix)
    {
        if (!m.HasProperty(prop))
            return;

        Texture t = m.GetTexture(prop);
        if (t == null)
        {
            Line(sb, prefix + "  " + prop, "<none>");
            return;
        }

        Line(sb, prefix + "  " + prop, t.name + "  " + t.width + "x" + t.height);

        Texture2D t2d = t as Texture2D;
        if (t2d != null)
        {
            // The RESOLVED runtime format. This is the number that matters: if it has no A,
            // the alpha channel was discarded at import and any alpha blending is dead.
            Line(sb, prefix + "    runtime format", t2d.format.ToString());
            Line(sb, prefix + "    mipmapCount", t2d.mipmapCount.ToString());
        }

        string assetPath = AssetDatabase.GetAssetPath(t);
        if (string.IsNullOrEmpty(assetPath))
            return;

        Line(sb, prefix + "    assetPath", assetPath);

        TextureImporter ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (ti == null)
            return;

        Line(sb, prefix + "    sourceHasAlpha", ti.DoesSourceTextureHaveAlpha().ToString());
        Line(sb, prefix + "    alphaSource", ti.alphaSource.ToString());
        Line(sb, prefix + "    alphaIsTransparency", ti.alphaIsTransparency.ToString());
        Line(sb, prefix + "    textureType", ti.textureType.ToString());
        Line(sb, prefix + "    sRGBTexture", ti.sRGBTexture.ToString());
        Line(sb, prefix + "    maxTextureSize", ti.maxTextureSize.ToString());
        Line(sb, prefix + "    textureCompression", ti.textureCompression.ToString());

        try
        {
            string platform = EditorUserBuildSettings.activeBuildTarget.ToString();
            TextureImporterPlatformSettings ps = ti.GetPlatformTextureSettings(platform);
            if (ps != null)
            {
                Line(sb, prefix + "    [" + platform + "] overridden", ps.overridden.ToString());
                Line(sb, prefix + "    [" + platform + "] format", ps.format.ToString());
                Line(sb, prefix + "    [" + platform + "] maxTextureSize", ps.maxTextureSize.ToString());
                Line(sb, prefix + "    [" + platform + "] compression", ps.textureCompression.ToString());
            }
        }
        catch (Exception e)
        {
            Line(sb, prefix + "    platform settings", "<unavailable: " + e.Message + ">");
        }
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// FindObjectsByType only exists from Unity 2022.2. This script has to compile in BOTH the
    /// Unity 6 spike and the 2021.3 project it came from, so the old API is kept as a fallback.
    /// </summary>
    private static T[] FindAll<T>() where T : UnityEngine.Object
    {
#if UNITY_2022_2_OR_NEWER
        return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        return UnityEngine.Object.FindObjectsOfType<T>(true);
#endif
    }

    private static GameObject FindByName(string name)
    {
        Transform[] all = FindAll<Transform>();
        foreach (Transform t in all)
            if (t.name == name)
                return t.gameObject;
        return null;
    }

    private static string HierarchyPath(Transform t)
    {
        string path = t.name;
        Transform p = t.parent;
        while (p != null)
        {
            path = p.name + "/" + path;
            p = p.parent;
        }
        return path;
    }

    private static string Col(Color c)
    {
        return "RGBA(" + c.r.ToString("0.###") + ", " + c.g.ToString("0.###") + ", " +
               c.b.ToString("0.###") + ", " + c.a.ToString("0.###") + ")";
    }

    private static void Line(StringBuilder sb, string key, string value)
    {
        sb.AppendLine("  " + key.PadRight(44) + " : " + value);
    }

    private static void Section(StringBuilder sb, string title, Action body)
    {
        sb.AppendLine();
        sb.AppendLine("================================================================================");
        sb.AppendLine(" " + title);
        sb.AppendLine("================================================================================");
        try
        {
            body();
        }
        catch (Exception e)
        {
            sb.AppendLine("  *** SECTION FAILED: " + e.GetType().Name + ": " + e.Message + " ***");
        }
    }
}
