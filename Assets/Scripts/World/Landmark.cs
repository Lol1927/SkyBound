using System.Collections.Generic;
using UnityEngine;

public class Landmark : MonoBehaviour
{
    public string landmarkName;
    public string landmarkHint;
    public string landmarkLore;
    public bool discovered;

    class GlowSlot
    {
        public Renderer renderer;
        public Material[] shared;
    }

    static Material burstMaterial;

    readonly List<GlowSlot> glowSlots = new List<GlowSlot>();
    readonly List<Material> ownedMaterials = new List<Material>();
    bool celebrated;

    void OnTriggerEnter(Collider other)
    {
        if (discovered || other == null)
            return;
        if (other.GetComponentInParent<PlaneController>() == null)
            return;
        if (ExplorationManager.Instance != null)
            ExplorationManager.Instance.Discover(this);
    }

    public void ShowDiscovered()
    {
        discovered = true;
        if (celebrated)
            return;

        celebrated = true;
        ApplyGlow();
        PlayBurst();
        PlayBeam();
    }

    public void ClearDiscovered()
    {
        discovered = false;
        celebrated = false;
        RestoreGlow();
        Transform beam = transform.Find("Discovery Beam");
        if (beam != null)
            Destroy(beam.gameObject);
    }

    void OnDestroy()
    {
        RestoreGlow();
    }

    void OnDrawGizmos()
    {
        if (WorldGenerator.DrawingInGameView())
            return;

        Gizmos.color = discovered
            ? new Color(1f, 0.82f, 0.28f, 0.45f)
            : new Color(0.75f, 0.9f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, 40f);
    }

    void ApplyGlow()
    {
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
        for (int r = 0; r < renderers.Length; r++)
        {
            MeshRenderer renderer = renderers[r];
            if (renderer == null)
                continue;

            Material[] source = renderer.sharedMaterials;
            var copies = new Material[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == null)
                    continue;

                var copy = new Material(source[i]);
                Brighten(copy);
                copies[i] = copy;
                ownedMaterials.Add(copy);
            }

            glowSlots.Add(new GlowSlot { renderer = renderer, shared = source });
            renderer.sharedMaterials = copies;
        }
    }

    void RestoreGlow()
    {
        for (int i = 0; i < glowSlots.Count; i++)
        {
            GlowSlot slot = glowSlots[i];
            if (slot.renderer != null)
                slot.renderer.sharedMaterials = slot.shared;
        }

        glowSlots.Clear();
        for (int i = 0; i < ownedMaterials.Count; i++)
        {
            if (ownedMaterials[i] != null)
                Destroy(ownedMaterials[i]);
        }

        ownedMaterials.Clear();
    }

    void PlayBurst()
    {
        Material material = BurstMaterial();
        if (material == null)
            return;

        var go = new GameObject("Discovery Burst");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 6f, 0f);

        var particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.45f;
        main.startLifetime = 0.9f;
        main.startSpeed = 6.5f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.5f);
        Color burst = Color.Lerp(WorldLook.Accent, Color.white, 0.35f);
        burst.a = 1f;
        main.startColor = burst;
        main.maxParticles = 40;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.18f;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)30) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 1.5f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.Lerp(WorldLook.Accent, Color.white, 0.55f), 0f),
                new GradientColorKey(WorldLook.Accent, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        particles.Play();
        Destroy(go, 2.2f);
    }

    void PlayBeam()
    {
        if (transform.Find("Discovery Beam") != null)
            return;

        var go = new GameObject("Discovery Beam");
        go.transform.SetParent(transform, false);
        go.AddComponent<DiscoveryBeam>().Begin(WorldLook.Accent);
    }

    static void Brighten(Material material)
    {
        Color baseColor = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color;
        Color bright = Color.Lerp(baseColor, Color.Lerp(WorldLook.Accent, Color.white, 0.4f), 0.42f);
        bright.a = baseColor.a;
        material.color = bright;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", bright);

        Color emission = WorldLook.Accent * 1.4f;
        if (material.HasProperty("_EmissionColor"))
        {
            Color existing = material.GetColor("_EmissionColor");
            if (existing.maxColorComponent > 0.05f)
                emission = existing * 1.65f;
            material.SetColor("_EmissionColor", emission);
        }

        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", Mathf.Max(material.GetFloat("_Smoothness"), 0.42f));
    }

    static Material BurstMaterial()
    {
        if (burstMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
                return null;

            burstMaterial = new Material(shader);
            burstMaterial.name = "Discovery Burst";
            Texture2D texture = SoftCircle();
            burstMaterial.mainTexture = texture;
            if (burstMaterial.HasProperty("_BaseMap"))
                burstMaterial.SetTexture("_BaseMap", texture);
        }

        Color color = Color.Lerp(WorldLook.Accent, Color.white, 0.25f);
        color.a = 1f;
        burstMaterial.color = color;
        if (burstMaterial.HasProperty("_BaseColor"))
            burstMaterial.SetColor("_BaseColor", color);
        return burstMaterial;
    }

    static Texture2D SoftCircle()
    {
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Discovery Burst";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        float radius = size * 0.5f - 0.5f;
        var center = new Vector2(radius, radius);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                byte alpha = (byte)Mathf.Clamp((radius - distance) / 2f * 255f, 0f, 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }
}
