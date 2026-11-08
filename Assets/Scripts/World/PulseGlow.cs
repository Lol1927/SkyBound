using UnityEngine;

public class PulseGlow : MonoBehaviour
{
    public float speed = 1.35f;
    public float depth = 0.24f;

    MeshRenderer meshRenderer;
    Material bound;
    Color baseEmission;
    Color baseAlbedo;

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
    }

    void Update()
    {
        if (meshRenderer == null)
            return;

        Material material = meshRenderer.sharedMaterial;
        if (material == null || !material.HasProperty("_EmissionColor"))
            return;

        if (bound != material)
        {
            bound = material;
            baseEmission = material.GetColor("_EmissionColor");
            baseAlbedo = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color;
        }

        float scale = 1f + Mathf.Sin(Time.time * speed) * depth;
        Color emission = baseEmission * scale;
        material.SetColor("_EmissionColor", emission);
        Color albedo = Color.Lerp(baseAlbedo, Color.white, Mathf.Clamp01((scale - 1f) * 0.35f));
        albedo.a = baseAlbedo.a;
        material.color = albedo;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", albedo);
    }
}
