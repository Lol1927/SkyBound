using UnityEngine;
using UnityEngine.Rendering;

public static class WorldAtmosphere
{
    struct Mood
    {
        public Vector3 sunEuler;
        public Color sunColor;
        public float sunIntensity;
        public float sunSize;
        public Color tint;
        public Color horizon;
        public float tintWeight;
        public float exposure;
        public float atmosphere;
        public float ambientSky;
        public float ambientEquator;
        public float ambientGround;
        public float fogDensity;
        public float fogShade;
    }

    const float ReadableSky = 0.42f;
    const float ReadableEquator = 0.36f;
    const float ReadableGround = 0.24f;

    static Material skyCopy;
    static Material oceanCopy;
    static Material starMaterial;
    static Texture2D starTexture;
    static Renderer oceanRenderer;
    static GameObject starField;

    public static void Apply(WorldData data)
    {
        Mood mood = MoodFor(WorldLook.SkyMood);
        ApplySky(mood);
        ApplySun(mood);
        ApplyFog(mood);
        ApplyAmbient(mood);
        ApplyOcean();
        ApplyStars(WorldLook.SkyMood == "night");
        DynamicGI.UpdateEnvironment();
    }

    static void ApplySky(Mood mood)
    {
        Material source = RenderSettings.skybox;
        if (source == null)
            return;

        if (skyCopy == null)
        {
            skyCopy = new Material(source);
            skyCopy.name = "Skybound Sky";
            RenderSettings.skybox = skyCopy;
        }

        Color tint = Color.Lerp(WorldLook.SkyTop, mood.tint, mood.tintWeight);
        Color horizon = Color.Lerp(WorldLook.SkyHorizon, mood.horizon, mood.tintWeight);
        SetColor(skyCopy, "_SkyTint", tint);
        SetColor(skyCopy, "_GroundColor", horizon);
        SetFloat(skyCopy, "_Exposure", mood.exposure);
        SetFloat(skyCopy, "_AtmosphereThickness", mood.atmosphere);
        SetFloat(skyCopy, "_SunSize", mood.sunSize);
    }

    static void ApplySun(Mood mood)
    {
        Light sun = FindSun();
        if (sun == null)
            return;

        sun.useColorTemperature = false;
        sun.color = mood.sunColor;
        sun.intensity = Mathf.Max(mood.sunIntensity, WorldLook.SkyMood == "night" ? 0.65f : 0.55f);
        sun.transform.rotation = Quaternion.Euler(mood.sunEuler);
        RenderSettings.sun = sun;
    }

    static void ApplyFog(Mood mood)
    {
        Color fog = WorldLook.Fog * mood.fogShade;
        fog.a = 1f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = fog;
        RenderSettings.fogDensity = mood.fogDensity;
    }

    static void ApplyAmbient(Mood mood)
    {
        Color sky = Color.Lerp(WorldLook.SkyTop, mood.tint, mood.tintWeight * 0.35f);
        Color equator = Color.Lerp(WorldLook.SkyHorizon, WorldLook.Fog, 0.4f);
        Color ground = Color.Lerp(WorldLook.Rock, WorldLook.Grass, 0.35f);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientIntensity = 1.15f;
        RenderSettings.ambientSkyColor = Lift(sky, Mathf.Max(ReadableSky, mood.ambientSky));
        RenderSettings.ambientEquatorColor = Lift(equator, Mathf.Max(ReadableEquator, mood.ambientEquator));
        RenderSettings.ambientGroundColor = Lift(ground, Mathf.Max(ReadableGround, mood.ambientGround));
    }

    static void ApplyOcean()
    {
        if (oceanRenderer == null)
        {
            GameObject ocean = GameObject.Find("Ocean");
            if (ocean == null)
                return;
            oceanRenderer = ocean.GetComponent<Renderer>();
        }

        if (oceanRenderer == null)
            return;

        if (oceanCopy == null)
        {
            oceanCopy = oceanRenderer.material;
            oceanCopy.name = "Skybound Ocean";
        }

        Color water = WorldLook.Water;
        water.a = 1f;
        oceanCopy.color = water;
        SetColor(oceanCopy, "_BaseColor", water);
        SetFloat(oceanCopy, "_Smoothness", 0.72f);
        Color emission = WorldLook.SkyMood == "night" ? water * 0.42f : Color.black;
        SetColor(oceanCopy, "_EmissionColor", emission);
        if (emission.maxColorComponent > 0.01f)
            oceanCopy.EnableKeyword("_EMISSION");
        else
            oceanCopy.DisableKeyword("_EMISSION");
    }

    static void ApplyStars(bool show)
    {
        if (!show)
        {
            if (starField != null)
                starField.SetActive(false);
            return;
        }

        if (starField == null)
        {
            starField = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            starField.name = "Star Field";
            Collider collider = starField.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);
            starField.transform.position = Vector3.zero;
            starField.transform.localScale = Vector3.one * 1800f;
            MeshRenderer renderer = starField.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = StarMaterial();
        }

        starField.SetActive(true);
    }

    static Material StarMaterial()
    {
        if (starMaterial != null)
            return starMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");
        starMaterial = new Material(shader);
        starMaterial.name = "Star Field";
        if (starTexture == null)
            starTexture = BuildStarTexture();
        starMaterial.mainTexture = starTexture;
        if (starMaterial.HasProperty("_BaseMap"))
            starMaterial.SetTexture("_BaseMap", starTexture);
        starMaterial.color = Color.white;
        if (starMaterial.HasProperty("_BaseColor"))
            starMaterial.SetColor("_BaseColor", Color.white);
        if (starMaterial.HasProperty("_Surface"))
            starMaterial.SetFloat("_Surface", 1f);
        if (starMaterial.HasProperty("_Cull"))
            starMaterial.SetFloat("_Cull", 1f);
        starMaterial.SetOverrideTag("RenderType", "Transparent");
        starMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        starMaterial.SetInt("_DstBlend", (int)BlendMode.One);
        starMaterial.SetInt("_ZWrite", 0);
        starMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        starMaterial.renderQueue = 3000;
        return starMaterial;
    }

    static Texture2D BuildStarTexture()
    {
        const int size = 512;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Stars";
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
        var pixels = new Color[size * size];
        var rng = new System.Random(12047);
        for (int i = 0; i < 260; i++)
        {
            int x = rng.Next(size);
            int y = rng.Next(size);
            float brightness = i < 28 ? 0.95f : (float)rng.NextDouble() * 0.55f + 0.25f;
            int span = i < 28 ? 2 : 1;
            for (int oy = 0; oy <= span; oy++)
            {
                for (int ox = 0; ox <= span; ox++)
                {
                    int px = (x + ox) % size;
                    int py = (y + oy) % size;
                    pixels[py * size + px] = new Color(0.9f, 0.94f, 1f, brightness);
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    static Color Lift(Color color, float minValue)
    {
        Color.RGBToHSV(color, out float h, out float s, out float v);
        Color lifted = Color.HSVToRGB(h, Mathf.Clamp01(s), Mathf.Max(v, minValue));
        lifted.a = 1f;
        return lifted;
    }

    static Light FindSun()
    {
        if (RenderSettings.sun != null)
            return RenderSettings.sun;

        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null && lights[i].type == LightType.Directional)
                return lights[i];
        }

        return null;
    }

    static Mood MoodFor(string mood)
    {
        if (mood == "sunrise")
        {
            return new Mood
            {
                sunEuler = new Vector3(16f, -75f, 0f),
                sunColor = new Color(1f, 0.62f, 0.34f),
                sunIntensity = 1.05f,
                sunSize = 0.045f,
                tint = new Color(1f, 0.55f, 0.32f),
                horizon = new Color(1f, 0.72f, 0.48f),
                tintWeight = 0.42f,
                exposure = 1.15f,
                atmosphere = 0.85f,
                ambientSky = 0.72f,
                ambientEquator = 0.58f,
                ambientGround = 0.32f,
                fogDensity = 0.012f,
                fogShade = 1f
            };
        }

        if (mood == "sunset")
        {
            return new Mood
            {
                sunEuler = new Vector3(12f, 85f, 0f),
                sunColor = new Color(1f, 0.46f, 0.22f),
                sunIntensity = 0.95f,
                sunSize = 0.05f,
                tint = new Color(0.95f, 0.38f, 0.22f),
                horizon = new Color(1f, 0.58f, 0.32f),
                tintWeight = 0.55f,
                exposure = 1.05f,
                atmosphere = 1.05f,
                ambientSky = 0.62f,
                ambientEquator = 0.5f,
                ambientGround = 0.3f,
                fogDensity = 0.014f,
                fogShade = 0.95f
            };
        }

        if (mood == "night")
        {
            Color moon = Color.Lerp(new Color(0.72f, 0.86f, 1f), WorldLook.SkyHorizon, 0.28f);
            return new Mood
            {
                sunEuler = new Vector3(58f, -40f, 0f),
                sunColor = Lift(moon, 0.75f),
                sunIntensity = 0.72f,
                sunSize = 0.04f,
                tint = new Color(0.14f, 0.18f, 0.42f),
                horizon = new Color(0.28f, 0.34f, 0.62f),
                tintWeight = 0.4f,
                exposure = 0.85f,
                atmosphere = 0.55f,
                ambientSky = 0.5f,
                ambientEquator = 0.42f,
                ambientGround = 0.28f,
                fogDensity = 0.01f,
                fogShade = 0.88f
            };
        }

        if (mood == "storm")
        {
            return new Mood
            {
                sunEuler = new Vector3(50f, -24f, 0f),
                sunColor = new Color(0.84f, 0.86f, 0.92f),
                sunIntensity = 0.85f,
                sunSize = 0.025f,
                tint = new Color(0.42f, 0.46f, 0.54f),
                horizon = new Color(0.62f, 0.66f, 0.72f),
                tintWeight = 0.35f,
                exposure = 0.95f,
                atmosphere = 1.15f,
                ambientSky = 0.55f,
                ambientEquator = 0.46f,
                ambientGround = 0.3f,
                fogDensity = 0.02f,
                fogShade = 0.88f
            };
        }

        return new Mood
        {
            sunEuler = new Vector3(58f, -30f, 0f),
            sunColor = new Color(1f, 0.96f, 0.9f),
            sunIntensity = 1.25f,
            sunSize = 0.03f,
            tint = WorldLook.SkyTop,
            horizon = WorldLook.SkyHorizon,
            tintWeight = 0.05f,
            exposure = 1.3f,
            atmosphere = 0.62f,
            ambientSky = 0.85f,
            ambientEquator = 0.7f,
            ambientGround = 0.38f,
            fogDensity = 0.008f,
            fogShade = 1f
        };
    }

    static void SetColor(Material material, string property, Color color)
    {
        if (material != null && material.HasProperty(property))
            material.SetColor(property, color);
    }

    static void SetFloat(Material material, string property, float value)
    {
        if (material != null && material.HasProperty(property))
            material.SetFloat(property, value);
    }
}
