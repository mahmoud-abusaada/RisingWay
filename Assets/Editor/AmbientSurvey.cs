// Lists what draws the space around the track - the galaxy, the stars, the solar system, the
// lights, the cameras and the post-processing - so that work on them starts from what is there.
//
//   Unity.exe -batchmode -quit -projectPath <project> -executeMethod AmbientSurvey.Run -logFile <log>
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class AmbientSurvey
{
    private const string TAG = "[AS] ";

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        AmbientEffectsController ambient = Object.FindAnyObjectByType<AmbientEffectsController>(FindObjectsInactive.Include);
        SerializedObject so = new SerializedObject(ambient);
        foreach (string field in new[] { "starsContainer", "cloudsContainer", "galaxyContainer", "galaxySky", "cubeSkybox", "sunDirectionalLight", "solarSystem" })
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
                continue;
            Transform t = property.objectReferenceValue as Transform;
            Debug.Log(TAG + "==== " + field + " ====");
            if (t != null)
                Dump(t, 0);
        }
        Debug.Log(TAG + "daySky " + so.FindProperty("daySkyColor").colorValue + " midSky " + so.FindProperty("midSkyColor").colorValue + " nightSky " + so.FindProperty("nightSkyColor").colorValue);

        SolarSystem solar = Object.FindAnyObjectByType<SolarSystem>(FindObjectsInactive.Include);
        SerializedObject ss = new SerializedObject(solar);
        Debug.Log(TAG + "==== SolarSystem planet prefab ====");
        Transform planet = ss.FindProperty("planet").objectReferenceValue as Transform;
        if (planet != null)
            Dump(planet, 0);
        Transform trail = ss.FindProperty("sunTrail").objectReferenceValue as Transform;
        if (trail != null)
        {
            Debug.Log(TAG + "==== sunTrail ====");
            Dump(trail, 0);
        }
        foreach (string p in new[] { "mercury", "venus", "earth", "mars", "jupiter", "saturn", "uranus", "neptune", "pluto" })
            Debug.Log(TAG + p + ": " + Mat(ss.FindProperty(p).objectReferenceValue as Material));

        Debug.Log(TAG + "==== lights ====");
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Debug.Log(TAG + Path(l.transform) + (l.gameObject.activeInHierarchy ? "" : " [OFF]") + " " + l.type + " intensity " + l.intensity + " colour " + l.color +
                      " shadows " + l.shadows + " mask " + l.cullingMask + " range " + l.range + " rot " + l.transform.eulerAngles);

        Debug.Log(TAG + "==== cameras ====");
        foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Debug.Log(TAG + Path(c.transform) + (c.gameObject.activeInHierarchy ? "" : " [OFF]") + " clear " + c.clearFlags + " bg " + c.backgroundColor + " fov " + c.fieldOfView +
                      " near " + c.nearClipPlane + " far " + c.farClipPlane + " hdr " + c.allowHDR + " mask " + c.cullingMask + " depth " + c.depth);

        Debug.Log(TAG + "==== volumes ====");
        foreach (Volume v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            StringBuilder sb = new StringBuilder(Path(v.transform) + " global " + v.isGlobal + " weight " + v.weight + ":");
            if (v.sharedProfile != null)
                foreach (VolumeComponent vc in v.sharedProfile.components)
                {
                    sb.Append("\n" + TAG + "   " + vc.GetType().Name + " active " + vc.active + ":");
                    foreach (System.Reflection.FieldInfo f in vc.GetType().GetFields())
                        if (f.GetValue(vc) is VolumeParameter p && p.overrideState)
                            sb.Append(" " + f.Name + "=" + p);
                }
            Debug.Log(TAG + sb);
        }

        Debug.Log(TAG + "==== render settings ====");
        Debug.Log(TAG + "skybox " + Mat(RenderSettings.skybox) + " ambient mode " + RenderSettings.ambientMode + " ambient " + RenderSettings.ambientLight +
                  " sky/equator/ground " + RenderSettings.ambientSkyColor + RenderSettings.ambientEquatorColor + RenderSettings.ambientGroundColor +
                  " intensity " + RenderSettings.ambientIntensity + " fog " + RenderSettings.fog + " sun " + (RenderSettings.sun != null ? RenderSettings.sun.name : "none") +
                  " reflection " + RenderSettings.defaultReflectionMode + " x" + RenderSettings.reflectionIntensity);
        Debug.Log(TAG + "pipeline " + (GraphicsSettings.defaultRenderPipeline != null ? AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline) : "none"));
        Debug.Log(TAG + "done");
    }

    private static string Path(Transform t)
    {
        string s = t.name;
        for (Transform p = t.parent; p != null; p = p.parent)
            s = p.name + "/" + s;
        return s;
    }

    private static string Mat(Material m)
    {
        if (m == null)
            return "no material";
        StringBuilder sb = new StringBuilder("'" + m.name + "' (" + AssetDatabase.GetAssetPath(m) + ") shader '" + m.shader.name + "' queue " + m.renderQueue);
        foreach (string tex in m.GetTexturePropertyNames())
        {
            Texture t = m.GetTexture(tex);
            if (t != null)
                sb.Append(" " + tex + "=" + AssetDatabase.GetAssetPath(t) + " " + t.width + "x" + t.height + " tile " + m.GetTextureScale(tex));
        }
        foreach (string c in new[] { "_BaseColor", "_Color", "_EmissionColor", "_TintColor" })
            if (m.HasProperty(c))
                sb.Append(" " + c + "=" + m.GetColor(c));
        foreach (string f in new[] { "_Surface", "_Blend", "_Cull", "_ZWrite", "_Smoothness", "_Metallic" })
            if (m.HasProperty(f))
                sb.Append(" " + f + "=" + m.GetFloat(f));
        sb.Append(" keywords [" + string.Join(" ", m.shaderKeywords) + "]");
        return sb.ToString();
    }

    private static void Dump(Transform t, int depth)
    {
        string pad = new string(' ', depth * 2);
        StringBuilder sb = new StringBuilder(pad + t.name + (t.gameObject.activeSelf ? "" : " [OFF]") + " pos " + t.localPosition + " rot " + t.localEulerAngles + " scale " + t.localScale + " layer " + LayerMask.LayerToName(t.gameObject.layer));
        foreach (Component c in t.GetComponents<Component>())
        {
            if (c == null || c is Transform)
                continue;
            sb.Append("\n" + TAG + pad + "  - " + c.GetType().Name);
            if (c is MeshFilter mf && mf.sharedMesh != null)
                sb.Append(" mesh '" + mf.sharedMesh.name + "' (" + AssetDatabase.GetAssetPath(mf.sharedMesh) + ") verts " + mf.sharedMesh.vertexCount + " bounds " + mf.sharedMesh.bounds.size);
            if (c is Renderer r)
            {
                sb.Append(" enabled " + r.enabled + " shadows " + r.shadowCastingMode);
                foreach (Material m in r.sharedMaterials)
                    sb.Append("\n" + TAG + pad + "      " + Mat(m));
            }
            if (c is ParticleSystem ps)
            {
                ParticleSystem.MainModule main = ps.main;
                ParticleSystem.EmissionModule em = ps.emission;
                ParticleSystem.ShapeModule sh = ps.shape;
                sb.Append(" max " + main.maxParticles + " lifetime " + main.startLifetime.constantMin + "-" + main.startLifetime.constantMax + " size " + main.startSize.constantMin + "-" + main.startSize.constantMax +
                          " speed " + main.startSpeed.constantMin + "-" + main.startSpeed.constantMax + " colour " + main.startColor.colorMin + "/" + main.startColor.colorMax + " simSpeed " + main.simulationSpeed +
                          " space " + main.simulationSpace + " prewarm " + main.prewarm + " rate " + em.rateOverTime.constant + " shape " + sh.shapeType + " radius " + sh.radius + " scale " + sh.scale +
                          " sizeOverLife " + ps.sizeOverLifetime.enabled + " colourOverLife " + ps.colorOverLifetime.enabled + " noise " + ps.noise.enabled);
            }
            if (c is ParticleSystemRenderer pr)
                sb.Append(" mode " + pr.renderMode + " minSize " + pr.minParticleSize + " maxSize " + pr.maxParticleSize + " sort " + pr.sortMode);
            if (c is TrailRenderer tr)
                sb.Append(" time " + tr.time + " width " + tr.widthMultiplier);
            if (c is Light l)
                sb.Append(" " + l.type + " intensity " + l.intensity + " colour " + l.color + " range " + l.range);
            if (c is MonoBehaviour mb)
            {
                SerializedObject o = new SerializedObject(mb);
                SerializedProperty p = o.GetIterator();
                p.NextVisible(true);
                while (p.NextVisible(false))
                    if (p.propertyType == SerializedPropertyType.Float) sb.Append(" " + p.name + "=" + p.floatValue);
                    else if (p.propertyType == SerializedPropertyType.Integer) sb.Append(" " + p.name + "=" + p.intValue);
                    else if (p.propertyType == SerializedPropertyType.Boolean) sb.Append(" " + p.name + "=" + p.boolValue);
                    else if (p.propertyType == SerializedPropertyType.Color) sb.Append(" " + p.name + "=" + p.colorValue);
            }
        }
        Debug.Log(TAG + sb);
        if (depth < 5)
            foreach (Transform child in t)
                Dump(child, depth + 1);
    }
}
