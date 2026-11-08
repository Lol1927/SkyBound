using UnityEngine;

public static class WorldLook
{
    public static readonly Color DefaultGrass = new Color(0.24f, 0.62f, 0.26f);
    public static readonly Color DefaultRock = new Color(0.40f, 0.36f, 0.32f);
    public static readonly Color DefaultSnow = new Color(0.90f, 0.94f, 1f);
    public static readonly Color DefaultWater = new Color(0.02f, 0.16f, 0.36f);
    public static readonly Color DefaultSkyTop = new Color(0.42f, 0.64f, 1f);
    public static readonly Color DefaultSkyHorizon = new Color(0.40f, 0.62f, 0.88f);
    public static readonly Color DefaultFog = new Color(0.62f, 0.80f, 0.95f);
    public static readonly Color DefaultAccent = new Color(0.45f, 0.95f, 1f);

    public static Color Grass = DefaultGrass;
    public static Color Rock = DefaultRock;
    public static Color Snow = DefaultSnow;
    public static Color Water = DefaultWater;
    public static Color SkyTop = DefaultSkyTop;
    public static Color SkyHorizon = DefaultSkyHorizon;
    public static Color Fog = DefaultFog;
    public static Color Accent = DefaultAccent;
    public static float TreeDensity = 0.7f;
    public static float RockDensity = 0.5f;
    public static float FloatingRockDensity = 0.55f;
    public static string SkyMood = "noon";

    public static void Apply(WorldData data)
    {
        if (data == null)
            return;

        PaletteData palette = data.palette;
        Grass = Parse(palette != null ? palette.grass : null, DefaultGrass);
        Rock = Parse(palette != null ? palette.rock : null, DefaultRock);
        Snow = Parse(palette != null ? palette.snow : null, DefaultSnow);
        Water = Parse(palette != null ? palette.water : null, DefaultWater);
        SkyTop = Parse(palette != null ? palette.skyTop : null, DefaultSkyTop);
        SkyHorizon = Parse(palette != null ? palette.skyHorizon : null, DefaultSkyHorizon);
        Fog = Parse(palette != null ? palette.fog : null, DefaultFog);
        Accent = Parse(palette != null ? palette.accent : null, DefaultAccent);
        TreeDensity = Mathf.Clamp01(data.treeDensity);
        RockDensity = Mathf.Clamp01(data.rockDensity);
        FloatingRockDensity = Mathf.Clamp01(data.floatingRockDensity);
        SkyMood = string.IsNullOrEmpty(data.skyMood) ? "noon" : data.skyMood;
    }

    public static float HeightScaleOf(IslandData island)
    {
        if (island == null || island.heightScale <= 0f)
            return 1f;
        return Mathf.Clamp(island.heightScale, 0.5f, 2f);
    }

    public static float RoughnessOf(IslandData island)
    {
        if (island == null)
            return 1f;
        return Mathf.Clamp01(island.roughness);
    }

    public static float VerticalSpreadOf(IslandData island)
    {
        if (island == null || island.verticalSpread <= 0f)
            return 1f;
        return Mathf.Clamp(island.verticalSpread, 0.3f, 1.5f);
    }

    public static Color Parse(string hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return fallback;

        string text = hex.Trim();
        if (!text.StartsWith("#"))
            text = "#" + text;
        if (!ColorUtility.TryParseHtmlString(text, out Color color))
            return fallback;

        color.a = 1f;
        return color;
    }
}
