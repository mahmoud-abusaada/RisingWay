using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A diamond picked up breaks into pieces: a handful of small faceted shards in the diamond's own
/// material burst out of it, tumble, fall and shrink away in about half a second. (Before, the
/// diamond just vanished behind a puff of particles.)
///
/// One object for the whole game, made on first use, with a pool of shards moved by hand each
/// frame - no rigidbodies, no allocation per pickup: diamonds come several a second at speed.
/// Game time, so a pause holds them where they are.
/// </summary>
public class DiamondShatter : MonoBehaviour
{
    private const int PIECES = 8;
    private const int POOL = 72;          // nine diamonds' worth in flight at once
    private const float GRAVITY = 5f;
    private const float DRAG = 1.6f;

    private class Shard
    {
        public Transform t;
        public MeshRenderer r;
        public Vector3 velocity, spin;
        public float age, life, size;
        public bool on;
    }

    private static DiamondShatter instance;
    private readonly List<Shard> shards = new List<Shard>();
    private Mesh[] shapes;
    private int next;

    /// <summary>Breaks a diamond of radius <paramref name="size"/> at <paramref name="at"/>.</summary>
    public static void Burst(Vector3 at, float size, Material material, int layer)
    {
        if (instance == null)
        {
            instance = new GameObject("DiamondShatter").AddComponent<DiamondShatter>();
            instance.build();
        }
        instance.burst(at, size, material, layer);
    }

    private void build()
    {
        shapes = new Mesh[4];
        for (int i = 0; i < shapes.Length; i++)
            shapes[i] = shardMesh(new System.Random(17 + i * 31));
        for (int i = 0; i < POOL; i++)
        {
            GameObject go = new GameObject("Shard");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = shapes[i % shapes.Length];
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);
            shards.Add(new Shard { t = go.transform, r = r });
        }
    }

    private void burst(Vector3 at, float size, Material material, int layer)
    {
        for (int i = 0; i < PIECES; i++)
        {
            Shard s = shards[next];
            next = (next + 1) % shards.Count; // the oldest goes first if they are all in flight
            Vector3 dir = Random.onUnitSphere;
            dir.y = Mathf.Abs(dir.y) * 0.8f + 0.2f; // mostly out and up
            s.t.gameObject.layer = layer;
            s.r.sharedMaterial = material;
            s.t.position = at + dir * size * 0.3f;
            s.t.rotation = Random.rotationUniform;
            s.velocity = dir.normalized * size * Random.Range(5f, 9f);
            s.spin = Random.insideUnitSphere * 900f;
            s.size = size * Random.Range(0.35f, 0.6f);
            s.life = Random.Range(0.4f, 0.65f);
            s.age = 0f;
            s.t.localScale = Vector3.one * s.size;
            s.on = true;
            s.t.gameObject.SetActive(true);
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;
        float drag = Mathf.Clamp01(1f - DRAG * dt);
        foreach (Shard s in shards)
        {
            if (!s.on)
                continue;
            s.age += dt;
            if (s.age >= s.life)
            {
                s.on = false;
                s.t.gameObject.SetActive(false);
                continue;
            }
            s.velocity = s.velocity * drag + Vector3.down * GRAVITY * dt;
            s.t.position += s.velocity * dt;
            s.t.rotation = Quaternion.Euler(s.spin * dt) * s.t.rotation;
            float k = s.age / s.life;
            s.t.localScale = Vector3.one * (s.size * (1f - k * k)); // shrink away, faster at the end
        }
    }

    // A small irregular cut stone: a pointed bottom and a flat-topped crown, every face flat (its
    // own vertices), so it catches the light like the diamond's facets.
    private static Mesh shardMesh(System.Random rnd)
    {
        float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
        int sides = 4 + rnd.Next(2);
        Vector3 bottom = new Vector3(R(-0.1f, 0.1f), -R(0.45f, 0.6f), R(-0.1f, 0.1f));
        Vector3 top = new Vector3(R(-0.08f, 0.08f), R(0.2f, 0.3f), R(-0.08f, 0.08f));
        Vector3[] ring = new Vector3[sides];
        for (int i = 0; i < sides; i++)
        {
            float a = (i + R(-0.2f, 0.2f)) / sides * Mathf.PI * 2f;
            float rad = R(0.3f, 0.5f);
            ring[i] = new Vector3(Mathf.Cos(a) * rad, R(-0.05f, 0.08f), Mathf.Sin(a) * rad);
        }
        List<Vector3> v = new List<Vector3>();
        // Each face wound to face out (Unity's front faces: cross(b - a, c - a) towards the viewer).
        void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) < 0f) { Vector3 x = b; b = c; c = x; }
            v.Add(a); v.Add(b); v.Add(c);
        }
        for (int i = 0; i < sides; i++)
        {
            Vector3 p = ring[i], q = ring[(i + 1) % sides];
            Tri(bottom, q, p); // pavilion
            Tri(top, p, q);    // crown
        }
        Mesh m = new Mesh { name = "Diamond Shard" };
        m.SetVertices(v);
        int[] idx = new int[v.Count];
        for (int i = 0; i < idx.Length; i++) idx[i] = i;
        m.SetTriangles(idx, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }
}
