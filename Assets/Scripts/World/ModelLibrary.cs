using UnityEngine;
using UnityEngine.Rendering;

public class ModelKit
{
    public Material body;
    public Material stripe;
    public Material accent;
    public Material glass;
    public Material dark;
    public Material gold;
    public Material rock;
    public Material wood;
    public Material moss;
    public Material glow;
}

public static class ModelLibrary
{
    public const string PlanePrefab = "Assets/Prefabs/PlaneVisual.prefab";
    public const string CrystalPrefab = "Assets/Prefabs/LandmarkCrystal.prefab";
    public const string TowerPrefab = "Assets/Prefabs/LandmarkTower.prefab";
    public const string RuinsPrefab = "Assets/Prefabs/LandmarkRuins.prefab";

    public const string BodyPath = "Assets/Materials/PlaneBody.mat";
    public const string StripePath = "Assets/Materials/PlaneStripe.mat";
    public const string AccentPath = "Assets/Materials/PlaneAccent.mat";
    public const string GlassPath = "Assets/Materials/PlaneGlass.mat";
    public const string DarkPath = "Assets/Materials/PlaneDark.mat";
    public const string GoldPath = "Assets/Materials/PlaneGold.mat";
    public const string RockPath = "Assets/Materials/LandmarkRock.mat";
    public const string WoodPath = "Assets/Materials/LandmarkWood.mat";
    public const string MossPath = "Assets/Materials/LandmarkMoss.mat";
    public const string GlowPath = "Assets/Materials/LandmarkGlow.mat";

    public static readonly Color BodyColor = new Color(0.90f, 0.33f, 0.22f);
    public static readonly Color StripeColor = new Color(0.96f, 0.95f, 0.90f);
    public static readonly Color AccentColor = new Color(0.17f, 0.36f, 0.82f);
    public static readonly Color GlassColor = new Color(0.74f, 0.91f, 1f);
    public static readonly Color DarkColor = new Color(0.15f, 0.13f, 0.12f);
    public static readonly Color GoldColor = new Color(0.94f, 0.70f, 0.16f);

    static ModelKit runtimeKit;

    public static ModelKit Load()
    {
#if UNITY_EDITOR
        Material body = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(BodyPath);
        if (body != null)
        {
            return new ModelKit
            {
                body = body,
                stripe = LoadAsset(StripePath, body),
                accent = LoadAsset(AccentPath, body),
                glass = LoadAsset(GlassPath, body),
                dark = LoadAsset(DarkPath, body),
                gold = LoadAsset(GoldPath, body),
                rock = LoadAsset(RockPath, body),
                wood = LoadAsset(WoodPath, body),
                moss = LoadAsset(MossPath, body),
                glow = LoadAsset(GlowPath, body)
            };
        }
#endif
        if (runtimeKit == null)
            runtimeKit = CreateRuntime();
        return runtimeKit;
    }

    public static GameObject Part(Transform parent, string name, Mesh mesh, Material material, bool castShadows)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        return go;
    }

    public static void Paint(Material material, Color color, float smoothness, Color emission, bool specular)
    {
        if (material == null)
            return;

        color.a = 1f;
        material.color = color;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_SpecularHighlights"))
            material.SetFloat("_SpecularHighlights", specular ? 1f : 0f);
        if (material.HasProperty("_EnvironmentReflections"))
            material.SetFloat("_EnvironmentReflections", specular ? 1f : 0f);

        if (specular)
        {
            material.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        }
        else
        {
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        }

        bool glow = emission.maxColorComponent > 0.01f;
        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", glow ? emission : Color.black);
        if (glow)
            material.EnableKeyword("_EMISSION");
        else
            material.DisableKeyword("_EMISSION");
        material.globalIlluminationFlags = glow
            ? MaterialGlobalIlluminationFlags.RealtimeEmissive
            : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
    }

    public static Material CreateInstanced(Color color, float smoothness)
    {
        Shader shader = Shader.Find("Skybound/InstancedFlat");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        var material = new Material(shader);
        material.enableInstancing = true;
        Paint(material, color, smoothness, Color.black, false);
        return material;
    }

    public static Material CreateLit(Color color, float smoothness, Color emission, bool specular)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        var material = new Material(shader);
        Paint(material, color, smoothness, emission, specular);
        return material;
    }

    static ModelKit CreateRuntime()
    {
        Color glow = PaletteBinder.GlowAlbedo(WorldLook.DefaultAccent, false, out Color emission);
        return new ModelKit
        {
            body = CreateLit(BodyColor, 0.04f, Color.black, false),
            stripe = CreateLit(StripeColor, 0.04f, Color.black, false),
            accent = CreateLit(AccentColor, 0.06f, Color.black, false),
            glass = CreateLit(GlassColor, 0.62f, Color.black, true),
            dark = CreateLit(DarkColor, 0.08f, Color.black, false),
            gold = CreateLit(GoldColor, 0.38f, Color.black, true),
            rock = CreateLit(PaletteBinder.RockColor(WorldLook.DefaultRock), 0.08f, Color.black, false),
            wood = CreateLit(PaletteBinder.WoodColor(WorldLook.DefaultRock), 0.06f, Color.black, false),
            moss = CreateLit(PaletteBinder.MossColor(WorldLook.DefaultGrass), 0.12f, Color.black, false),
            glow = CreateLit(glow, 0.4f, emission, true)
        };
    }

#if UNITY_EDITOR
    static Material LoadAsset(string path, Material fallback)
    {
        Material material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
        return material != null ? material : fallback;
    }
#endif
}
