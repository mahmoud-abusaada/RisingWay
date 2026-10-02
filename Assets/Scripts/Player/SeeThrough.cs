using UnityEngine;

/// <summary>
/// The player's ball and what goes round it - moons, Saturn's ring, the Earth's layers, the orbit
/// lines - are always seen, even where the track is in front of them (a moon passing under it).
///
/// The track writes depth like any solid thing, so the stars, the Sun's glow and the planets'
/// trails stay behind it. Each renderer of the ball group gets one more material,
/// "See Through" (Resources, SeeThrough.shader), queued just before the group: it pushes the depth
/// under that renderer's shape back to the far plane, and the group drawn after it is then only
/// tested against itself.
///
/// It replaced keeping the near track parts from writing depth, which let the ball and moons show
/// through them - and every star, glow and trail as well.
/// </summary>
public static class SeeThrough
{
    /// <summary>The queue the ball group draws in: after every track part (2000 + its place) and
    /// the sky's lights, just after the See Through material's own (2420).</summary>
    public const int GROUP_QUEUE = 2421;

    private static Material punch;

    private static Material Punch
    {
        get
        {
            if (punch == null)
                punch = Resources.Load<Material>("See Through");
            return punch;
        }
    }

    private static bool isPunch(Material m)
    {
        return m != null && Punch != null && m.shader == Punch.shader;
    }

    /// <summary>Makes <paramref name="r"/> show through the track (once, however often it is called).</summary>
    public static void Add(Renderer r)
    {
        if (r == null || Punch == null)
            return;
        Material[] materials = r.sharedMaterials;
        foreach (Material m in materials)
            if (isPunch(m))
                return;
        Material[] more = new Material[materials.Length + 1];
        materials.CopyTo(more, 0);
        more[materials.Length] = Punch;
        r.sharedMaterials = more;
    }

    /// <summary>Takes it off again: a moon going back to the pool, to be used in the shop next.</summary>
    public static void Remove(Renderer r)
    {
        if (r == null || Punch == null)
            return;
        Material[] materials = r.sharedMaterials;
        int kept = 0;
        foreach (Material m in materials)
            if (!isPunch(m))
                kept++;
        if (kept == materials.Length)
            return;
        Material[] fewer = new Material[kept];
        int i = 0;
        foreach (Material m in materials)
            if (!isPunch(m))
                fewer[i++] = m;
        r.sharedMaterials = fewer;
    }
}
