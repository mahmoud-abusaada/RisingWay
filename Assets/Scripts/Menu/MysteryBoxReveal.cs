using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The mystery box opening: anticipation, a slowing spin through the prizes, the reveal, and
/// collecting. Replaces the old sequence (a flat black panel, 25 even flashes, a 1.3x pop and one
/// burst of 60 particles), which ShopMenu drove through four Animation clips.
///
/// Everything it draws is made here, in code: the backdrop is the Choices image the old sequence
/// already faded in; light rays, glow, shockwave ring and flash are RawImages with textures
/// generated at start-up; sparks, glitter and the collect trail are particle systems configured
/// here. No new assets, no scene changes - the clips are simply no longer played.
///
/// The prize is decided and paid out by ShopMenu BEFORE this starts (see MysteryBoxPrizes), so
/// the animation is only presentation: skipping it, or the app dying half-way, changes nothing.
///
/// All timing is unscaled: the menus can be open while the game's time scale is not 1.
/// </summary>
public class MysteryBoxReveal : MonoBehaviour
{
    // ---- Timing --------------------------------------------------------------------------------
    private const float ANTICIPATION = 0.35f;  // the box shakes before it opens
    private const int SPIN_TICKS = 20;
    private const float TICK_FIRST = 0.035f, TICK_LAST = 0.27f; // interval, first and last tick
    private const float TICK_CURVE = 2.4f;     // how late the slowing-down bites
    private const float COLLECT_HINT_AFTER = 0.8f;
    private const float AUTO_COLLECT_AFTER = 5f;

    // ---- Layout (Choices-local units; the old clips put the prizes at y = 190) ----------------
    private const float BUBBLE_SHOWN_Y = 190f, BUBBLE_HIDDEN_Y = 1601f;
    private static readonly Vector2 PRIZE_CENTRE = new Vector2(0f, 140f);
    private const float PARTICLE_Z = -190f; // just in front of the prize meshes (-150..-176)

    private static readonly Color BACKDROP = new Color(0.015f, 0.025f, 0.07f, 0.9f);

    public static Color RarityColor(PrizeRarity r)
    {
        switch (r)
        {
            case PrizeRarity.Rare: return new Color(0.28f, 0.68f, 1f);
            case PrizeRarity.Epic: return new Color(0.78f, 0.38f, 1f);
            case PrizeRarity.Jackpot: return new Color(1f, 0.78f, 0.22f);
            default: return new Color(0.78f, 0.88f, 1f);
        }
    }

    // ---- Scene references (from ShopMenu) -----------------------------------------------------
    private RectTransform choices, bubble, countRoot, boxButton;
    private TMP_Text countText;
    private Transform diamondsTarget;
    private GameObject hiddenWhileOpen;
    private MaterialsManager materials;
    private Image backdrop;
    private Animation legacyClips;

    // ---- Built here ----------------------------------------------------------------------------
    private RawImage rays, glow, ring, flash;
    private TextMeshProUGUI banner, hint;
    private ParticleSystem sparks, glitter, trail;
    private readonly Dictionary<Material, Material> stencilled = new Dictionary<Material, Material>();
    private bool built;

    // ---- State ---------------------------------------------------------------------------------
    public bool IsPlaying { get; private set; }
    private bool skipRequested, collectRequested, canCollect, prizeShown;
    private float raySpeed, punch, baseScale = 1f, glowPulse;
    private Color tint;
    private bool backdropWasRaycastTarget;

    /// <param name="hideWhileOpen">The shop list. Its items are 3D models drawn in front of every UI layer, so no
    /// backdrop can cover them; they are switched off while a box is open.</param>
    public void Init(RectTransform choices, RectTransform bubble, TMP_Text countText, RectTransform boxButton,
                     Transform diamondsTarget, MaterialsManager materials, ParticleSystem legacyEffect, GameObject hideWhileOpen)
    {
        hiddenWhileOpen = hideWhileOpen;
        this.choices = choices;
        this.bubble = bubble;
        this.countText = countText;
        this.countRoot = (RectTransform)countText.transform.parent;
        this.boxButton = boxButton;
        this.diamondsTarget = diamondsTarget;
        this.materials = materials;
        backdrop = choices.GetComponent<Image>();
        legacyClips = choices.GetComponent<Animation>();
        particleShaderSource = legacyEffect != null ? legacyEffect.GetComponent<ParticleSystemRenderer>().sharedMaterial : null;
    }

    private Material particleShaderSource;

    /// <param name="onCollected">When the prize lands - update counters, drop the shop lock.</param>
    /// <param name="onDone">When the overlay has gone and the shop is usable again.</param>
    public void Play(MysteryBoxPrize prize, Action onCollected, Action onDone)
    {
        if (!built) Build();
        StopAllCoroutines();
        StartCoroutine(Run(prize, onCollected, onDone));
    }

    /// <summary>A press anywhere on the overlay or the box: skips the spin; once the prize is
    /// shown, collects it. Presses while it collects and leaves are ignored.</summary>
    public void OnTap()
    {
        if (!IsPlaying) return;
        if (canCollect) collectRequested = true;
        else if (!prizeShown) skipRequested = true;
    }

    // ============================================================================================
    // The sequence
    // ============================================================================================

    private IEnumerator Run(MysteryBoxPrize prize, Action onCollected, Action onDone)
    {
        IsPlaying = true;
        skipRequested = collectRequested = canCollect = prizeShown = false;
        if (legacyClips != null) legacyClips.Stop();

        Color rarityColor = RarityColor(prize.rarity);
        tint = RarityColor(PrizeRarity.Common);
        backdropWasRaycastTarget = backdrop.raycastTarget;
        backdrop.raycastTarget = true; // swallow taps meant for the list, and feed OnTap
        foreach (Transform c in bubble) c.gameObject.SetActive(false);
        countRoot.gameObject.SetActive(false);
        SetAlpha(banner, 0f); SetAlpha(hint, 0f); SetAlpha(ring, 0f); SetAlpha(flash, 0f);
        SetAlpha(rays, 0f); SetAlpha(glow, 0f);
        baseScale = 1f; punch = 0f; raySpeed = 25f;

        // 1. Anticipation: the box shakes harder and swells, then gives.
        yield return Tween(ANTICIPATION, t =>
        {
            float amp = 11f * t * t;
            boxButton.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 70f) * amp);
            boxButton.localScale = Vector3.one * (1f + 0.14f * t);
        });
        boxButton.localRotation = Quaternion.identity;
        StartCoroutine(Tween(0.2f, t => boxButton.localScale = Vector3.one * Mathf.Lerp(1.14f, 1f, EaseOutBack(t))));

        // 2. The overlay comes in; the prizes drop into place while the spin starts.
        StartCoroutine(Tween(0.25f, t =>
        {
            backdrop.color = new Color(BACKDROP.r, BACKDROP.g, BACKDROP.b, BACKDROP.a * t);
            SetAlpha(rays, 0.22f * t);
            SetAlpha(glow, 0.35f * t);
            if (t >= 1f && hiddenWhileOpen != null) hiddenWhileOpen.SetActive(false); // gone once the backdrop is up
        }));
        bubble.anchoredPosition = new Vector2(0, BUBBLE_HIDDEN_Y);
        StartCoroutine(Tween(0.45f, t => bubble.anchoredPosition =
            new Vector2(0, Mathf.LerpUnclamped(BUBBLE_HIDDEN_Y, BUBBLE_SHOWN_Y, EaseOutBack(t)))));

        // 3. The spin: fast, then slowing, each change a tick and a small pop. The last few ticks
        //    shift the light towards the prize's rarity - the tell that something good is coming.
        int shown = -1;
        for (int i = 0; i < SPIN_TICKS && !skipRequested; i++)
        {
            shown = PickDisplay(shown);
            ShowDisplay(shown);
            punch = 0.12f;
            float progress = i / (float)(SPIN_TICKS - 1);
            SoundManager.Instance?.PlayRevealTick(Mathf.Lerp(0.85f, 1.4f, progress));
            raySpeed = Mathf.Lerp(160f, 40f, progress);
            if (i >= SPIN_TICKS - 5)
                tint = Color.Lerp(RarityColor(PrizeRarity.Common), rarityColor, (i - (SPIN_TICKS - 5) + 1) / 5f);
            float wait = Mathf.Lerp(TICK_FIRST, TICK_LAST, Mathf.Pow(progress, TICK_CURVE));
            for (float w = 0; w < wait && !skipRequested; w += Time.unscaledDeltaTime)
                yield return null;
        }
        bubble.anchoredPosition = new Vector2(0, BUBBLE_SHOWN_Y);

        // 4. The reveal. From here a press collects (it used to wait 0.35 s, and a quick second
        //    press in that time did nothing).
        tint = rarityColor;
        ShowPrize(prize);
        prizeShown = true;
        canCollect = true;
        int tier = (int)prize.rarity; // 0..3
        SoundManager.Instance?.PlayRevealWin(prize.rarity);
        StartCoroutine(Tween(0.28f, t => SetAlpha(flash, 0.75f * (1f - t) * (1f - t))));
        StartCoroutine(Tween(0.6f, t =>
        {
            ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.25f, 2.3f + 0.3f * tier, EaseOutCubic(t));
            SetAlpha(ring, 0.9f * (1f - t));
        }));
        Burst(sparks, 26 + 22 * tier, rarityColor);
        Burst(glitter, 18 + 14 * tier, Color.Lerp(rarityColor, Color.white, 0.35f));
        raySpeed = 140f;
        StartCoroutine(Tween(0.7f, t => baseScale = Mathf.LerpUnclamped(0.3f, 1f, EaseOutElastic(t))));
        StartCoroutine(Tween(0.35f, t =>
        {
            SetAlpha(rays, Mathf.Lerp(0.22f, 0.3f + 0.15f * tier, t));
            SetAlpha(glow, Mathf.Lerp(0.35f, 0.55f + 0.1f * tier, t));
        }));
        if (prize.rarity == PrizeRarity.Jackpot)
        {
            StartCoroutine(Shake(0.45f, 14f));
            StartCoroutine(Delayed(0.28f, () => Burst(sparks, 60, Color.white)));
        }

        banner.text = BannerText(prize);
        banner.color = Color.Lerp(rarityColor, Color.white, 0.25f);
        StartCoroutine(Tween(0.35f, t =>
        {
            banner.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(2.2f, 1f, EaseOutBack(t));
            SetAlpha(banner, t);
        }));

        if (prize.amount > 0)
            StartCoroutine(CountUp(prize.amount));

        // 5. Hold until the player collects (or after a while, on their behalf).
        float held = 0f;
        while (!collectRequested && held < AUTO_COLLECT_AFTER)
        {
            held += Time.unscaledDeltaTime;
            raySpeed = Mathf.MoveTowards(raySpeed, 22f, 160f * Time.unscaledDeltaTime);
            if (held > COLLECT_HINT_AFTER)
                SetAlpha(hint, Mathf.Clamp01((held - COLLECT_HINT_AFTER) * 3f) * (0.55f + 0.35f * Mathf.Sin(held * 4f)));
            yield return null;
        }
        canCollect = false;

        // 6. Collect: diamonds stream to the counter; anything else folds into a spark.
        SetAlpha(hint, 0f);
        if (prize.kind == PrizeKind.Diamonds && diamondsTarget != null)
        {
            StartCoroutine(Tween(0.22f, t => baseScale = Mathf.Lerp(1f, 0f, EaseInCubic(t))));
            yield return FlyTrail(diamondsTarget.position, rarityColor);
            onCollected?.Invoke();
            SoundManager.Instance?.PlayRevealCollect();
            StartCoroutine(Tween(0.3f, t => diamondsTarget.localScale = Vector3.one * Mathf.LerpUnclamped(1.3f, 1f, EaseOutBack(t))));
        }
        else
        {
            yield return Tween(0.25f, t => baseScale = Mathf.LerpUnclamped(1f, 0f, EaseInBack(t)));
            Burst(glitter, 24, Color.Lerp(rarityColor, Color.white, 0.5f));
            onCollected?.Invoke();
            SoundManager.Instance?.PlayRevealCollect();
        }

        // 7. Out.
        if (hiddenWhileOpen != null) hiddenWhileOpen.SetActive(true);
        float bannerA = banner.color.a, raysA = rays.color.a, glowA = glow.color.a;
        yield return Tween(0.3f, t =>
        {
            float k = 1f - t;
            backdrop.color = new Color(BACKDROP.r, BACKDROP.g, BACKDROP.b, BACKDROP.a * k);
            SetAlpha(banner, bannerA * k); SetAlpha(rays, raysA * k); SetAlpha(glow, glowA * k);
            countRoot.localScale = Vector3.one * k;
        });

        ClearBallParticles();
        foreach (Transform c in bubble) c.gameObject.SetActive(false);
        bubble.anchoredPosition = new Vector2(0, BUBBLE_HIDDEN_Y);
        baseScale = 1f;
        countRoot.gameObject.SetActive(false);
        countRoot.localScale = Vector3.one;
        backdrop.raycastTarget = backdropWasRaycastTarget;
        choices.anchoredPosition = Vector2.zero;
        IsPlaying = false;
        onDone?.Invoke();
    }

    void Update()
    {
        if (!IsPlaying || !built) return;
        float dt = Time.unscaledDeltaTime;
        rays.rectTransform.Rotate(0, 0, -raySpeed * dt);
        glowPulse += dt;
        glow.rectTransform.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(glowPulse * 3.2f));
        punch = Mathf.MoveTowards(punch, 0f, dt * 1.2f);
        bubble.localScale = Vector3.one * (baseScale * (1f + punch));
        rays.color = new Color(tint.r, tint.g, tint.b, rays.color.a);
        glow.color = new Color(tint.r, tint.g, tint.b, glow.color.a);
        ring.color = new Color(tint.r, tint.g, tint.b, ring.color.a);
    }

    // ============================================================================================
    // Prize display (the 3D models under BubbleParent: Diamonds, Bolts, DoublePoints, Chances,
    // Floor, Ball - the same order as PrizeKind)
    // ============================================================================================

    private int PickDisplay(int previous)
    {
        bool floors = materials.getLockedFloorsList().Count > 0;
        bool balls = materials.getLockedBallsList().Count > 0;
        for (int guard = 0; guard < 20; guard++)
        {
            int i = UnityEngine.Random.Range(0, 6);
            if (i == previous) continue;
            if (i == (int)PrizeKind.Floor && !floors) continue;
            if (i == (int)PrizeKind.Ball && !balls) continue;
            return i;
        }
        return 0;
    }

    private void ShowDisplay(int index)
    {
        BaseMaterial cosmetic = null;
        if (index == (int)PrizeKind.Floor)
        {
            List<BaseMaterial> f = materials.getLockedFloorsList();
            cosmetic = f[UnityEngine.Random.Range(0, f.Count)];
        }
        else if (index == (int)PrizeKind.Ball)
        {
            List<ColorMaterial> b = MysteryBoxPrizes.BallPool(materials);
            cosmetic = b[UnityEngine.Random.Range(0, b.Count)];
        }
        Show(index, cosmetic);
    }

    private void ShowPrize(MysteryBoxPrize prize) { Show((int)prize.kind, prize.cosmetic); }

    private void Show(int index, BaseMaterial cosmetic)
    {
        foreach (Transform c in bubble) c.gameObject.SetActive(false);
        Transform model = bubble.GetChild(index);
        Renderer r = model.GetComponent<Renderer>();

        Material source;
        if (cosmetic is PatternMaterial) source = ((PatternMaterial)cosmetic).landItem;
        else if (cosmetic is ColorMaterial) source = ((ColorMaterial)cosmetic).material;
        else source = r.sharedMaterial;
        r.sharedMaterial = Stencilled(source);

        Moons moons = bubble.GetChild((int)PrizeKind.Ball).GetComponent<Moons>();
        if (index == (int)PrizeKind.Ball)
        {
            model.gameObject.SetActive(true);
            moons.setMoons();
            model.GetComponent<PlayerLinkedObjectsController>().prepareLinkedObjects();
        }
        else
        {
            moons.clearMoons();
            ClearBallParticles();
            model.gameObject.SetActive(true);
        }
    }

    // The ball model carries its in-game ambient particles (world space), which kept drifting
    // across the overlay after the spin moved on from a ball.
    private void ClearBallParticles()
    {
        foreach (ParticleSystem ps in bubble.GetChild((int)PrizeKind.Ball).GetComponentsInChildren<ParticleSystem>(true))
            ps.Clear(true);
    }

    /// <summary>
    /// The prize models draw through the list's stencil mask. One stencilled copy per source
    /// material, made once: the old code took renderer.material (a new instance) on every flash
    /// of every opening and never released them.
    /// </summary>
    private Material Stencilled(Material source)
    {
        if (source == null) return null;
        if (stencilled.ContainsValue(source)) return source; // already one of ours
        Material m;
        if (!stencilled.TryGetValue(source, out m) || m == null)
        {
            m = materials.getStencilledMaterial(source, 3000, true);
            m.name = source.name; // other code matches on material names (Earth, Saturn, ...)
            stencilled[source] = m;
        }
        return m;
    }

    private static string BannerText(MysteryBoxPrize p)
    {
        switch (p.kind)
        {
            case PrizeKind.Ball: return MysteryBoxPrizes.IsBoxExclusive(p.cosmetic) ? "RARE BALL!" : "NEW BALL!";
            case PrizeKind.Floor: return "NEW FLOOR!";
            case PrizeKind.Diamonds:
                return p.rarity == PrizeRarity.Jackpot ? "JACKPOT!" : p.rarity == PrizeRarity.Rare ? "BIG WIN!" : "DIAMONDS";
            case PrizeKind.Bolts: return "BOLTS";
            case PrizeKind.DoublePoints: return "DOUBLE POINTS";
            default: return "CHANCES";
        }
    }

    private IEnumerator CountUp(int amount)
    {
        yield return Delayed(0.15f, null);
        countRoot.gameObject.SetActive(true);
        countRoot.anchoredPosition = new Vector2(0, PRIZE_CENTRE.y - 270f);
        Vector3 p = countRoot.localPosition; countRoot.localPosition = new Vector3(p.x, p.y, 0f); // the old clip parked it at z -2000
        const float dur = 0.6f;
        for (float e = 0; e < dur; e += Time.unscaledDeltaTime)
        {
            countText.text = "x " + Utility.getFormatedNumber(Mathf.RoundToInt(amount * EaseOutCubic(e / dur)));
            countRoot.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, EaseOutBack(Mathf.Clamp01(e / 0.25f)));
            yield return null;
        }
        countText.text = "x " + Utility.getFormatedNumber(amount);
        yield return Tween(0.22f, t => countRoot.localScale = Vector3.one * Mathf.LerpUnclamped(1.25f, 1f, EaseOutBack(t)));
    }

    // ============================================================================================
    // Effects
    // ============================================================================================

    private static void Burst(ParticleSystem ps, int count, Color color)
    {
        ParticleSystem.EmitParams e = new ParticleSystem.EmitParams { startColor = color, applyShapeToPosition = true };
        ps.Emit(e, count);
    }

    private IEnumerator FlyTrail(Vector3 targetWorld, Color color)
    {
        Transform t = trail.transform;
        Vector3 from = choices.TransformPoint(new Vector3(PRIZE_CENTRE.x, PRIZE_CENTRE.y, PARTICLE_Z));
        Vector3 to = new Vector3(targetWorld.x, targetWorld.y, from.z);
        Vector3 control = Vector3.Lerp(from, to, 0.5f) + choices.TransformVector(new Vector3(-260f, 80f, 0f));
        ParticleSystem.EmitParams e = new ParticleSystem.EmitParams { startColor = Color.Lerp(color, Color.white, 0.3f), applyShapeToPosition = true };
        const float dur = 0.5f;
        for (float el = 0; el < dur; el += Time.unscaledDeltaTime)
        {
            float k = EaseInOutCubic(Mathf.Clamp01(el / dur));
            t.position = (1 - k) * (1 - k) * from + 2 * (1 - k) * k * control + k * k * to;
            trail.Emit(e, 4);
            yield return null;
        }
        t.position = to;
        Burst(trail, 16, Color.white);
    }

    private IEnumerator Shake(float duration, float strength)
    {
        for (float e = 0; e < duration; e += Time.unscaledDeltaTime)
        {
            float a = strength * (1f - e / duration);
            choices.anchoredPosition = new Vector2(UnityEngine.Random.Range(-a, a), UnityEngine.Random.Range(-a, a));
            yield return null;
        }
        choices.anchoredPosition = Vector2.zero;
    }

    // ============================================================================================
    // Construction
    // ============================================================================================

    private void Build()
    {
        built = true;

        // Behind the prize models, in paint order: rays, glow, ring, flash. The banner and hint go
        // after the existing children, so they draw over the count.
        rays = Layer("RevealRays", RaysTexture(), new Vector2(1700, 1700), 0);
        glow = Layer("RevealGlow", GlowTexture(), new Vector2(950, 950), 1);
        ring = Layer("RevealRing", RingTexture(), new Vector2(600, 600), 2);
        flash = Layer("RevealFlash", Texture2D.whiteTexture, Vector2.zero, 3);
        flash.rectTransform.anchorMin = Vector2.zero; flash.rectTransform.anchorMax = Vector2.one;
        flash.rectTransform.sizeDelta = Vector2.zero; flash.rectTransform.anchoredPosition = Vector2.zero;

        banner = Text("RevealBanner", 92, new Vector2(0, PRIZE_CENTRE.y + 410f)); // clear of the tallest model, the bolt
        hint = Text("RevealHint", 40, new Vector2(0, PRIZE_CENTRE.y - 430f));
        hint.text = "TAP TO COLLECT";

        Material mat = ParticleMaterial();
        sparks = MakeParticles("RevealSparks", mat, false);
        {
            var main = sparks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(700f, 1700f);
            main.startSize = new ParticleSystem.MinMaxCurve(10f, 24f);
            var shape = sparks.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 30f;
            var drag = sparks.limitVelocityOverLifetime; drag.enabled = true; drag.drag = 3.5f; drag.multiplyDragByParticleSize = false; drag.multiplyDragByParticleVelocity = false;
            var r = sparks.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 3.2f; r.velocityScale = 0f;
        }
        glitter = MakeParticles("RevealGlitter", mat, false);
        {
            var main = glitter.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 2.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(60f, 320f);
            main.startSize = new ParticleSystem.MinMaxCurve(8f, 22f);
            var shape = glitter.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 120f;
            var vel = glitter.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(0f, 0f); vel.y = new ParticleSystem.MinMaxCurve(60f, 140f); vel.z = new ParticleSystem.MinMaxCurve(0f, 0f); // one mode for all three, or Unity rejects it
            var drag = glitter.limitVelocityOverLifetime; drag.enabled = true; drag.drag = 1.5f; drag.multiplyDragByParticleSize = false; drag.multiplyDragByParticleVelocity = false;
            var size = glitter.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.2f), new Keyframe(0.15f, 1f), new Keyframe(0.35f, 0.5f), new Keyframe(0.55f, 1f), new Keyframe(0.8f, 0.4f), new Keyframe(1f, 0f)));
        }
        trail = MakeParticles("RevealTrail", mat, true);
        {
            var main = trail.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(20f, 120f);
            main.startSize = new ParticleSystem.MinMaxCurve(10f, 26f);
            var shape = trail.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 12f;
        }
    }

    private RawImage Layer(string name, Texture tex, Vector2 size, int siblingIndex)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        go.layer = choices.gameObject.layer;
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(choices, false);
        rt.SetSiblingIndex(siblingIndex);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = PRIZE_CENTRE;
        RawImage img = go.GetComponent<RawImage>();
        img.texture = tex;
        img.raycastTarget = false;
        img.color = new Color(1, 1, 1, 0);
        return img;
    }

    private TextMeshProUGUI Text(string name, float size, Vector2 pos)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = choices.gameObject.layer;
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(choices, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1000, 140);
        rt.anchoredPosition = pos;
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        t.font = countText.font;
        t.fontSharedMaterial = countText.fontSharedMaterial; // the game's outlined title style
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = false;
        t.raycastTarget = false;
        t.color = new Color(1, 1, 1, 0);
        return t;
    }

    private ParticleSystem MakeParticles(string name, Material mat, bool worldSpace)
    {
        GameObject go = new GameObject(name);
        go.SetActive(false); // configure before it can play on awake
        go.layer = bubble.GetChild(0).gameObject.layer; // the prize models' layer, which the menu camera draws
        go.transform.SetParent(choices, false);
        go.transform.localPosition = new Vector3(PRIZE_CENTRE.x, PRIZE_CENTRE.y, PARTICLE_Z);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 1f;
        main.useUnscaledTime = true;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy; // sizes and speeds in canvas units
        main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
        main.maxParticles = 600;
        var emission = ps.emission; emission.enabled = false; // everything is emitted by hand
        var col = ps.colorOverLifetime; col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        go.SetActive(true);
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    private Material ParticleMaterial()
    {
        // The shader comes from the old reveal's own effect material (Legacy Particles/Additive):
        // a shader the build already ships, rather than a Shader.Find that stripping could break.
        Material m = particleShaderSource != null ? new Material(particleShaderSource) : new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
        m.name = "RevealParticles";
        m.mainTexture = DotTexture();
        m.mainTextureScale = Vector2.one;
        m.mainTextureOffset = Vector2.zero;
        if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f)); // neutral for this shader
        m.renderQueue = 3100; // after the prize models (3000), whatever their distance
        return m;
    }

    // ---- Generated textures --------------------------------------------------------------------

    private static Texture2D NewTex(int size)
    {
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        return t;
    }

    private static Texture2D Paint(int size, Func<float, float, float> alphaAt)
    {
        Texture2D t = NewTex(size);
        Color32[] px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Clamp01(alphaAt(r, Mathf.Atan2(v, u)));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        t.SetPixels32(px);
        t.Apply(false, true);
        return t;
    }

    private static Texture2D DotTexture()
    {
        return Paint(64, (r, _) => { float k = 1f - r; return k <= 0 ? 0 : k * k * (0.35f + 0.65f * k); });
    }

    private static Texture2D GlowTexture()
    {
        return Paint(128, (r, _) => r >= 1f ? 0f : Mathf.Exp(-r * r * 5f) * (1f - r));
    }

    private static Texture2D RingTexture()
    {
        return Paint(256, (r, _) => { float d = (r - 0.82f) / 0.07f; return Mathf.Exp(-d * d) * (r < 1f ? 1f : 0f); });
    }

    private static Texture2D RaysTexture()
    {
        const int RAYS = 12;
        return Paint(256, (r, a) =>
        {
            if (r >= 1f) return 0f;
            float beam = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(a * RAYS * 0.5f)), 6f); // soft-edged beams
            float falloff = Mathf.SmoothStep(0f, 1f, r / 0.12f) * (1f - r) * (1f - r);
            return beam * falloff * 1.4f;
        });
    }

    // ---- Tween helpers -------------------------------------------------------------------------

    private static IEnumerator Tween(float duration, Action<float> step)
    {
        for (float e = 0; e < duration; e += Time.unscaledDeltaTime)
        {
            step(Mathf.Clamp01(e / duration));
            yield return null;
        }
        step(1f);
    }

    private static IEnumerator Delayed(float seconds, Action then)
    {
        for (float e = 0; e < seconds; e += Time.unscaledDeltaTime)
            yield return null;
        then?.Invoke();
    }

    private static void SetAlpha(Graphic g, float a)
    {
        if (g == null) return;
        Color c = g.color; c.a = a; g.color = c;
    }

    private static float EaseOutCubic(float t) { t = 1f - t; return 1f - t * t * t; }
    private static float EaseInCubic(float t) { return t * t * t; }
    private static float EaseInOutCubic(float t) { return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f; }
    private static float EaseOutBack(float t) { const float c1 = 1.70158f, c3 = c1 + 1f; t -= 1f; return 1f + c3 * t * t * t + c1 * t * t; }
    private static float EaseInBack(float t) { const float c1 = 1.70158f, c3 = c1 + 1f; return c3 * t * t * t - c1 * t * t; }
    private static float EaseOutElastic(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * (2f * Mathf.PI / 3f)) + 1f;
    }
}
