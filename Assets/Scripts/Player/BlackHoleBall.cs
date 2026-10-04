using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
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
    // camera's copy of the opaque scene. The trails go just after it (in a run the UI has its own
    // camera, so the transparent queue holds nothing else there).
    private const int AFTER_SCENE_COPY = 2998;
    private const int AFTER_LENS = 3000;
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
    // The ball's SortingGroup puts it (and so the disk, a child) in a sorting layer above the
    // menus' canvases, which outranks the render queue: the disk was drawn over the main menu's
    // buttons and the shop's title. While it is a black hole the group is in the menus' layer, so
    // the queues decide (the disk at Transparent-1, the menus at 3000, after it).
    private SortingGroup group;
    private int groupLayer, groupOrder;
    private bool groupMoved;

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
        // shop, the mystery box) the square sits as far in front, clear of its own floor (which
        // writes depth: the near half of the disk was cut off by the floor's front), and is drawn
        // straight after its ball: its lock, drawn later, covers it, and so does the floor of the
        // item above (ShopMenu queues items that way).
        bool player = ball.CompareTag("Player");
        effect.lensMaterial.SetFloat(ZTestId, (float)(player ? UnityEngine.Rendering.CompareFunction.Always
                                                             : UnityEngine.Rendering.CompareFunction.LessEqual));
        effect.lensMaterial.SetFloat(LiftId, effect.lensMaterial.GetFloat("_Reach"));
        effect.lensMaterial.renderQueue = player ? -1 : ballMaterial.renderQueue + 1;
        // Away from play (the shop, the box) no light past white: on a phone it bloomed over
        // what is beside it.
        effect.lensMaterial.SetFloat("_MaxLight", player ? 64f : 1f);
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
        if (group == null)
            group = GetComponent<SortingGroup>();
        if (group != null && !groupMoved)
        {
            groupLayer = group.sortingLayerID;
            groupOrder = group.sortingOrder;
            group.sortingLayerID = 0; // Default, the menus' layer
            group.sortingOrder = 0;
            groupMoved = true;
        }
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
            // The trails are left out of the copy (in it, their bent image was a ring round the
            // hole) and drawn after the hole: they lie between it and the camera, so they show
            // over the disk right from the ball (drawn before it, the disk hid their first
            // stretch). The black sphere, drawn before the hole, still hides what is behind it.
            foreach (TrailRenderer trail in GetComponentsInChildren<TrailRenderer>(true))
            {
                if (!trailQueues.ContainsKey(trail)) // Apply runs this again on every ball change
                    trailQueues[trail] = trail.material.renderQueue;
                trail.material.renderQueue = AFTER_LENS;
            }
        }
        // The bending only for the ball in play. In the shop and the box the floor under the ball
        // is in front of the hole, and bent with the sky it showed twice (or, drawn after the
        // scene's copy, the sky was laid over it): there the disk and its light, nothing bent.
        if (lensMaterial != null)
            lensMaterial.SetFloat(LensingId, askedForOpaqueTexture && CompareTag("Player") ? 1f : 0f);
    }

    void OnDisable()
    {
        if (lens != null)
            lens.gameObject.SetActive(false);
        if (group != null && groupMoved)
        {
            group.sortingLayerID = groupLayer;
            group.sortingOrder = groupOrder;
            groupMoved = false;
        }
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
