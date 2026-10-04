// The Black Hole balls: for each one a ball material (Assets/Materials/Balls/Black Holes, the
// event horizon, black) and its disk (Resources/BlackHoles/"<name> Lens", BlackHole.shader, found
// by BlackHoleBall from the ball's name). Puts them in MaterialsManager.ballMaterials after the
// stars, with new ids (73-76, see docs/cosmetic-id-contract.md) - box exclusives like the planets
// and stars - and hands the default lens to every PlayerLinkedObjectsController. Safe to run again:
// it updates what is there.
//
//   Unity.exe -batchmode -quit -projectPath <project> -executeMethod BlackHoleSetup.Run -logFile <log>
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BlackHoleSetup
{
    private const string TAG = "[BlackHoleSetup] ";
    private const string BALLS = "Assets/Materials/Balls/Black Holes/";
    private const string LENSES = "Assets/Resources/BlackHoles/";
    private const int PRICE = -1; // box exclusive, as the planets and stars

    private class Variant
    {
        public int id;
        public string name;
        public Color hot, cool, ring;
        public float tilt, roll, spin, doppler, brightness, outer, turbulence;
    }

    // Gargantua's orange; a blue-white hot one, fast; a red one seen almost edge on; a violet one
    // tilted towards us, its disk a wide ring.
    private static readonly Variant[] Variants =
    {
        new Variant { id = 73, name = "Black Hole", hot = new Color(4.2f, 3.4f, 2.4f), cool = new Color(1.7f, 0.55f, 0.12f), ring = new Color(3f, 2.4f, 1.7f),
                      tilt = 15, roll = -12, spin = 1f, doppler = 0.75f, brightness = 0.55f, outer = 3.7f, turbulence = 0.6f },
        new Variant { id = 74, name = "Black Hole Blue", hot = new Color(2.6f, 3.3f, 4.8f), cool = new Color(0.3f, 0.75f, 2.4f), ring = new Color(2.2f, 2.8f, 4f),
                      tilt = 20, roll = 10, spin = 1.7f, doppler = 0.9f, brightness = 0.6f, outer = 3.8f, turbulence = 0.7f },
        new Variant { id = 75, name = "Black Hole Crimson", hot = new Color(4.4f, 2.1f, 2.2f), cool = new Color(1.9f, 0.12f, 0.3f), ring = new Color(3.4f, 1.6f, 1.6f),
                      tilt = 8, roll = -20, spin = 0.8f, doppler = 0.6f, brightness = 0.6f, outer = 3.9f, turbulence = 0.5f },
        new Variant { id = 76, name = "Black Hole Violet", hot = new Color(3.6f, 2.8f, 4.8f), cool = new Color(1.1f, 0.3f, 2.2f), ring = new Color(3f, 2.4f, 4f),
                      tilt = 34, roll = 4, spin = 1.2f, doppler = 0.7f, brightness = 0.55f, outer = 3.6f, turbulence = 0.65f },
    };

    public static void Run()
    {
        System.IO.Directory.CreateDirectory(BALLS);
        System.IO.Directory.CreateDirectory(LENSES);
        AssetDatabase.Refresh();
        // The first black hole's ball material lived one folder up while it was a trial.
        if (AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Balls/Black Hole.mat") != null)
            Debug.Log(TAG + "moved the trial ball: " + AssetDatabase.MoveAsset("Assets/Materials/Balls/Black Hole.mat", BALLS + "Black Hole.mat"));

        Shader lensShader = Shader.Find("RisingWay/Black Hole");
        var made = new List<KeyValuePair<Variant, Material>>();
        Material defaultLens = null;
        foreach (Variant v in Variants)
        {
            string ballPath = BALLS + v.name + ".mat";
            Material ball = AssetDatabase.LoadAssetAtPath<Material>(ballPath);
            if (ball == null)
            {
                ball = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                ball.SetColor("_BaseColor", Color.black);
                AssetDatabase.CreateAsset(ball, ballPath);
            }
            string lensPath = LENSES + v.name + " Lens.mat";
            Material lens = AssetDatabase.LoadAssetAtPath<Material>(lensPath);
            if (lens == null)
            {
                lens = new Material(lensShader);
                AssetDatabase.CreateAsset(lens, lensPath);
            }
            lens.shader = lensShader;
            lens.SetFloat("_Reach", 4.3f); // the disk (outer edge up to 3.9) and the bending round it
            lens.SetFloat("_Bend", 0.8f);
            lens.SetFloat("_InnerGlow", 0f); // the user did not want it glowing
            lens.SetFloat("_MaxShift", 0.75f); // the warp shows more; still held close, so the sky stays off the track
            lens.SetFloat("_GlowWidth", 0.42f);
            lens.SetFloat("_Lift", 4.3f);
            lens.SetFloat("_DiskInner", 0.68f); // inside the shadow: its far side, bent up over the hole, then meets the shadow - a gap read as an empty band
            lens.SetFloat("_DiskOuter", v.outer);
            lens.SetFloat("_DiskTilt", v.tilt);
            lens.SetFloat("_DiskRoll", v.roll);
            lens.SetFloat("_Spin", v.spin);
            lens.SetFloat("_Doppler", v.doppler);
            lens.SetFloat("_DiskBrightness", v.brightness);
            lens.SetFloat("_Turbulence", v.turbulence);
            lens.SetFloat("_Ring", 1.2f);
            lens.SetColor("_HotColor", v.hot);
            lens.SetColor("_CoolColor", v.cool);
            lens.SetColor("_RingColor", v.ring);
            EditorUtility.SetDirty(lens);
            if (defaultLens == null)
                defaultLens = lens;
            made.Add(new KeyValuePair<Variant, Material>(v, ball));
            Debug.Log(TAG + v.name + ": " + ballPath + " + " + lensPath);
        }
        AssetDatabase.SaveAssets();

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        foreach (PlayerLinkedObjectsController c in Object.FindObjectsByType<PlayerLinkedObjectsController>(FindObjectsInactive.Include))
        {
            SerializedObject so = new SerializedObject(c);
            so.FindProperty("blackHoleLensMaterial").objectReferenceValue = defaultLens;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach (MaterialsManager m in Object.FindObjectsByType<MaterialsManager>(FindObjectsInactive.Include))
        {
            SerializedObject so = new SerializedObject(m);
            SerializedProperty list = so.FindProperty("ballMaterials");
            foreach (var pair in made)
            {
                SerializedProperty entry = null;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).FindPropertyRelative("id").intValue == pair.Key.id)
                        entry = list.GetArrayElementAtIndex(i);
                if (entry == null)
                {
                    list.InsertArrayElementAtIndex(list.arraySize);
                    entry = list.GetArrayElementAtIndex(list.arraySize - 1);
                    Debug.Log(TAG + "added ball " + pair.Key.id + " " + pair.Key.name + " to " + m.name);
                }
                entry.FindPropertyRelative("id").intValue = pair.Key.id;
                entry.FindPropertyRelative("isBright").boolValue = false;
                entry.FindPropertyRelative("price").intValue = PRICE;
                entry.FindPropertyRelative("material").objectReferenceValue = pair.Value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(TAG + "done");
    }
}
