using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The Black Hole ball. Its own material ("Black Hole") is just the event horizon, black; this
/// adds the rest - accretion disk, photon ring, the bending of what is behind it - as a square
/// around the ball drawn by BlackHole.shader. PlayerLinkedObjectsController calls
/// <see cref="Apply"/> whenever the ball's material changes.
///
/// There are several black holes (balls named "Black Hole ..."): each one's disk is its own lens
/// material, Resources/BlackHoles/"&lt;ball name&gt; Lens" (made by BlackHoleSetup).
///
/// The bending of what is behind needs the camera's copy of the scene (URP's opaque texture),
/// which costs a copy of the screen every frame, so it is switched on only while the player's ball
/// is a black hole, and back to what it was otherwise.
/// </summary>
public class BlackHoleBall : MonoBehaviour
{
    public const string MATERIAL_NAME = "Black Hole";
    public const string LENS_NAME = "Black Hole Lens";
    // Just before the lens (BlackHole.shader, Transparent-1): in the transparent queue, after the
    // camera's copy of the opaque scene. The trails go just before it.
    private const int AFTER_SCENE_COPY = 2998;
    private static readonly int LensingId = Shader.PropertyToID("_Lensing");
    private static readonly int StencilCompId = Shader.PropertyToID("_StencilComp");
    private static readonly int ZTestId = Shader.PropertyToID("_ZTest");
    private static readonly int LiftId = Shader.PropertyToID("_Lift");
    private const string LENSES = "BlackHoles/";
    private static Mesh quad;

    private MeshRenderer lens;
    private Material lensMaterial;
    private bool askedForOpaqueTexture;
    private CameraOverrideOption cameraWasSetTo;
    private readonly Dictionary<TrailRenderer, int> trailQueues = new Dictionary<TrailRenderer, int>();

    public static bool IsBlackHole(Material m)
    {
        return m != null && m.name.Contains(MATERIAL_NAME);
    }

    /// <summary>Shows or hides the effect on <paramref name="ball"/> for its current material.</summary>
    public static void Apply(GameObject ball, Material lensAsset)
    {
        bool on = lensAsset != null && IsBlackHole(ball.GetComponent<Renderer>().sharedMaterial);
        BlackHoleBall effect = ball.GetComponent<BlackHoleBall>();
        if (!on)
        {
            if (effect != null)
                effect.enabled = false;
            return;
        }
        if (effect == null)
            effect = ball.AddComponent<BlackHoleBall>();
        Material ballMaterial = ball.GetComponent<Renderer>().sharedMaterial;
        Material variant = Resources.Load<Material>(LENSES + ballMaterial.name.Replace(" (Instance)", "") + " Lens");
        effect.build(variant != null ? variant : lensAsset);

        // A ball in the shop's list is drawn only inside the list (StencilledLit); its disk too.
        bool stencilled = ballMaterial.shader != null && ballMaterial.shader.name.Contains("Stencil");
        effect.lensMaterial.SetFloat(StencilCompId, (float)(stencilled ? UnityEngine.Rendering.CompareFunction.Equal
                                                                        : UnityEngine.Rendering.CompareFunction.Always));
        // The ball in play is seen through the track (SeeThrough), and so is all of the hole: no
        // depth test, the square well in front of the ball for the whole disk. Anywhere else (the
        // shop, the mystery box) the square sits just in front of the ball and things in front of
        // the ball - its lock - cover it; it is drawn straight after its ball.
        bool player = ball.CompareTag("Player");
        effect.lensMaterial.SetFloat(ZTestId, (float)(player ? UnityEngine.Rendering.CompareFunction.Always
                                                             : UnityEngine.Rendering.CompareFunction.LessEqual));
        effect.lensMaterial.SetFloat(LiftId, player ? effect.lensMaterial.GetFloat("_Reach") : 1.05f);
        effect.lensMaterial.renderQueue = player ? -1 : ballMaterial.renderQueue + 1;
        effect.enabled = true;
        effect.OnEnable(); // AddComponent ran OnEnable before there was a lens to switch on
    }

    private void build(Material lensAsset)
    {
        if (lens != null)
        {
            // Another black hole: its disk's colours and shape, on the same square.
            lensMaterial.CopyPropertiesFromMaterial(lensAsset);
            lensMaterial.name = lensAsset.name;
            return;
        }
        GameObject go = new GameObject(LENS_NAME);
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = unitQuad();
        lens = go.AddComponent<MeshRenderer>();
        lensMaterial = new Material(lensAsset) { name = lensAsset.name };
        lens.sharedMaterial = lensMaterial;
        lens.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lens.receiveShadows = false;
    }

    void OnEnable()
    {
        if (lens == null)
            return;
        lens.gameObject.SetActive(true);
        // Only the ball in play bends the scene; a copy in the shop just shows its disk.
        if (CompareTag("Player") && Camera.main != null)
        {
            UniversalAdditionalCameraData data = Camera.main.GetUniversalAdditionalCameraData();
            if (!askedForOpaqueTexture)
                cameraWasSetTo = data.requiresColorOption;
            data.requiresColorOption = CameraOverrideOption.On;
            askedForOpaqueTexture = true;
            // The black sphere after the copy is taken, not in it: the bent light is read from
            // behind the hole, and with the sphere in the copy rays near it read black.
            GetComponent<Renderer>().material.renderQueue = AFTER_SCENE_COPY;
            // The trails start at the ball's centre, right behind the hole, so their bent image is
            // a ring round it: they are drawn after the copy too, and before the hole, which then
            // covers their start - the trail comes out of the bent light, not across the shadow.
            foreach (TrailRenderer trail in GetComponentsInChildren<TrailRenderer>(true))
            {
                if (!trailQueues.ContainsKey(trail)) // Apply runs this again on every ball change
                    trailQueues[trail] = trail.material.renderQueue;
                trail.material.renderQueue = AFTER_SCENE_COPY - 1;
            }
        }
        if (lensMaterial != null)
            lensMaterial.SetFloat(LensingId, askedForOpaqueTexture ? 1f : 0f);
    }

    void OnDisable()
    {
        if (lens != null)
            lens.gameObject.SetActive(false);
        if (askedForOpaqueTexture && Camera.main != null)
            Camera.main.GetUniversalAdditionalCameraData().requiresColorOption = cameraWasSetTo;
        askedForOpaqueTexture = false;
        foreach (KeyValuePair<TrailRenderer, int> t in trailQueues)
            if (t.Key != null)
                t.Key.material.renderQueue = t.Value;
        trailQueues.Clear();
    }

    // Rolling does not show on a black hole: the square is turned to the camera in the shader.
    private static Mesh unitQuad()
    {
        if (quad == null)
        {
            quad = new Mesh { name = "Black Hole Quad" };
            quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
            quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            quad.bounds = new Bounds(Vector3.zero, Vector3.one * 16f); // it is drawn around the ball, whichever way the quad faces
        }
        return quad;
    }
}
