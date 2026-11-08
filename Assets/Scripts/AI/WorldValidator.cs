using System.Collections.Generic;
using UnityEngine;

public static class WorldValidator
{
    static readonly string[] Layouts = { "scattered", "central", "ring" };
    static readonly string[] Biomes = { "forest", "snow", "volcanic" };
    static readonly string[] Sizes = { "small", "medium", "large" };
    static readonly string[] Terrains = { "rolling", "mountainous", "plateau", "canyon" };
    static readonly string[] FeatureOptions = { "terraces", "lake", "arch", "spires" };
    static readonly string[] LandmarkTypes = { "none", "crystal", "tower", "ruins" };
    static readonly string[] SkyMoods = { "sunrise", "noon", "sunset", "night", "storm" };

    const string DefaultGrass = "#3D9E42";
    const string DefaultRock = "#665C52";
    const string DefaultSnow = "#E6F0FF";
    const string DefaultWater = "#05295C";
    const string DefaultSkyTop = "#6BA3FF";
    const string DefaultSkyHorizon = "#669EE0";
    const string DefaultFog = "#9ECCF2";
    const string DefaultAccent = "#73F2FF";

    public static WorldData Validate(WorldData data)
    {
        if (data == null)
            data = new WorldData();

        data.worldName = string.IsNullOrWhiteSpace(data.worldName) ? "Unnamed Sky" : data.worldName.Trim();
        data.layout = Canonical(data.layout, Layouts, "scattered");
        data.cloudDensity = SanitizeDensity(data.cloudDensity);
        data.skyMood = Canonical(data.skyMood, SkyMoods, "noon");
        data.treeDensity = SanitizeUnit(data.treeDensity, 0.7f);
        data.rockDensity = SanitizeUnit(data.rockDensity, 0.5f);
        data.floatingRockDensity = SanitizeUnit(data.floatingRockDensity, 0.55f);
        data.palette = SanitizePalette(data.palette);
        if (data.coPilotIntro == null)
            data.coPilotIntro = "";

        if (data.islands == null)
            data.islands = new List<IslandData>();

        while (data.islands.Count > 6)
            data.islands.RemoveAt(data.islands.Count - 1);
        while (data.islands.Count < 3)
            data.islands.Add(DefaultIsland());

        for (int i = 0; i < data.islands.Count; i++)
        {
            if (data.islands[i] == null)
                data.islands[i] = DefaultIsland();
            NormalizeIsland(data.islands[i]);
        }

        EnforceLandmarkCount(data.islands);
        data.seed = Random.Range(1, int.MaxValue);
        return data;
    }

    static void NormalizeIsland(IslandData island)
    {
        island.biome = Canonical(island.biome, Biomes, "forest");
        island.size = Canonical(island.size, Sizes, "medium");
        island.terrainStyle = Canonical(island.terrainStyle, Terrains, "rolling");
        island.heightScale = SanitizeRange(island.heightScale, 0.5f, 2f, 1f, true);
        island.roughness = SanitizeRange(island.roughness, 0f, 1f, 1f, false);
        island.verticalSpread = SanitizeRange(island.verticalSpread, 0.3f, 1.5f, 1f, true);
        island.features = CleanFeatures(island.features);

        string type = Match(island.landmarkType, LandmarkTypes);
        bool real = type == "crystal" || type == "tower" || type == "ruins";
        if (real)
        {
            island.hasLandmark = true;
            island.landmarkType = type;
            EnsureStrings(island);
            return;
        }

        if (island.hasLandmark && type != "none")
        {
            island.hasLandmark = true;
            island.landmarkType = "crystal";
            EnsureStrings(island);
            return;
        }

        ClearLandmark(island);
    }

    static void EnforceLandmarkCount(List<IslandData> islands)
    {
        var landmarks = new List<int>();
        var open = new List<int>();
        for (int i = 0; i < islands.Count; i++)
        {
            if (islands[i].hasLandmark)
                landmarks.Add(i);
            else
                open.Add(i);
        }

        while (landmarks.Count > 3)
        {
            int index = landmarks[landmarks.Count - 1];
            ClearLandmark(islands[index]);
            landmarks.RemoveAt(landmarks.Count - 1);
        }

        while (landmarks.Count < 3 && open.Count > 0)
        {
            int index = open[0];
            open.RemoveAt(0);
            AssignLandmark(islands[index], ChooseType(islands));
            landmarks.Add(index);
        }

        for (int i = 0; i < landmarks.Count; i++)
            FillLandmarkCopy(islands[landmarks[i]]);
    }

    static string ChooseType(List<IslandData> islands)
    {
        bool crystal = false;
        bool tower = false;
        bool ruins = false;
        for (int i = 0; i < islands.Count; i++)
        {
            if (!islands[i].hasLandmark)
                continue;
            if (islands[i].landmarkType == "crystal")
                crystal = true;
            else if (islands[i].landmarkType == "tower")
                tower = true;
            else if (islands[i].landmarkType == "ruins")
                ruins = true;
        }

        if (!crystal)
            return "crystal";
        if (!tower)
            return "tower";
        if (!ruins)
            return "ruins";
        return "crystal";
    }

    static void AssignLandmark(IslandData island, string type)
    {
        island.hasLandmark = true;
        island.landmarkType = type;
        EnsureStrings(island);
    }

    static void FillLandmarkCopy(IslandData island)
    {
        if (string.IsNullOrWhiteSpace(island.landmarkName))
            island.landmarkName = NameFor(island.landmarkType);
        if (string.IsNullOrWhiteSpace(island.landmarkHint))
            island.landmarkHint = HintFor(island.landmarkType);
        if (string.IsNullOrWhiteSpace(island.landmarkLore))
            island.landmarkLore = LoreFor(island.landmarkType);
    }

    static void ClearLandmark(IslandData island)
    {
        island.hasLandmark = false;
        island.landmarkType = "none";
        island.landmarkName = "";
        island.landmarkHint = "";
        island.landmarkLore = "";
    }

    static void EnsureStrings(IslandData island)
    {
        if (island.landmarkName == null)
            island.landmarkName = "";
        if (island.landmarkHint == null)
            island.landmarkHint = "";
        if (island.landmarkLore == null)
            island.landmarkLore = "";
    }

    static IslandData DefaultIsland()
    {
        return new IslandData
        {
            biome = "forest",
            size = "medium",
            terrainStyle = "rolling",
            features = new List<string>(),
            hasLandmark = false,
            landmarkType = "none",
            landmarkName = "",
            landmarkHint = "",
            landmarkLore = ""
        };
    }

    static List<string> CleanFeatures(List<string> features)
    {
        var clean = new List<string>();
        if (features == null)
            return clean;

        for (int i = 0; i < features.Count; i++)
        {
            string feature = Match(features[i], FeatureOptions);
            if (feature == null || clean.Contains(feature))
                continue;
            clean.Add(feature);
        }

        return clean;
    }

    static PaletteData SanitizePalette(PaletteData palette)
    {
        if (palette == null)
            palette = new PaletteData();

        palette.grass = LiftBrightness(SanitizeHex(palette.grass, DefaultGrass), 0.35f);
        palette.rock = LiftBrightness(SanitizeHex(palette.rock, DefaultRock), 0.35f);
        palette.snow = LiftBrightness(SanitizeHex(palette.snow, DefaultSnow), 0.35f);
        palette.water = LiftBrightness(SanitizeHex(palette.water, DefaultWater), 0.35f);
        palette.skyTop = SanitizeHex(palette.skyTop, DefaultSkyTop);
        palette.skyHorizon = SanitizeHex(palette.skyHorizon, DefaultSkyHorizon);
        palette.fog = LiftBrightness(SanitizeHex(palette.fog, DefaultFog), 0.35f);
        palette.accent = SanitizeHex(palette.accent, DefaultAccent);
        SeparateHorizon(palette);
        return palette;
    }

    static string LiftBrightness(string hex, float minValue)
    {
        if (!ColorUtility.TryParseHtmlString(hex, out Color color))
            return hex;

        Color.RGBToHSV(color, out float h, out float s, out float v);
        if (v >= minValue)
            return hex;

        Color lifted = Color.HSVToRGB(h, Mathf.Clamp01(s), minValue);
        return "#" + ColorUtility.ToHtmlStringRGB(lifted);
    }

    static void SeparateHorizon(PaletteData palette)
    {
        if (!ColorUtility.TryParseHtmlString(palette.water, out Color water))
            return;
        if (!ColorUtility.TryParseHtmlString(palette.skyHorizon, out Color horizon))
            return;

        Color.RGBToHSV(water, out float waterHue, out _, out float waterValue);
        Color.RGBToHSV(horizon, out float horizonHue, out float horizonSat, out float horizonValue);
        float hueGap = Mathf.Abs(Mathf.DeltaAngle(waterHue * 360f, horizonHue * 360f)) / 360f;
        float valueGap = Mathf.Abs(waterValue - horizonValue);
        if (valueGap >= 0.18f || hueGap >= 0.08f)
            return;

        float hue = Mathf.Repeat(horizonHue + 0.08f, 1f);
        float value = waterValue >= 0.62f
            ? Mathf.Max(0.35f, waterValue - 0.22f)
            : Mathf.Min(1f, waterValue + 0.22f);
        Color shifted = Color.HSVToRGB(hue, Mathf.Clamp01(horizonSat), value);
        palette.skyHorizon = "#" + ColorUtility.ToHtmlStringRGB(shifted);
    }

    static string SanitizeHex(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        string hex = value.Trim();
        if (!hex.StartsWith("#"))
            hex = "#" + hex;
        if (!ColorUtility.TryParseHtmlString(hex, out Color color))
            return fallback;

        return "#" + ColorUtility.ToHtmlStringRGB(color);
    }

    static float SanitizeDensity(float value)
    {
        return SanitizeUnit(value, 0.5f);
    }

    static float SanitizeUnit(float value, float fallback)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return fallback;
        return Mathf.Clamp01(value);
    }

    static float SanitizeRange(float value, float min, float max, float fallback, bool zeroIsUnset)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || (zeroIsUnset && value <= 0f))
            return fallback;
        return Mathf.Clamp(value, min, max);
    }

    static string Canonical(string value, string[] options, string fallback)
    {
        string match = Match(value, options);
        return match ?? fallback;
    }

    static string Match(string value, string[] options)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string trimmed = value.Trim().ToLowerInvariant();
        for (int i = 0; i < options.Length; i++)
        {
            if (options[i] == trimmed)
                return options[i];
        }

        return null;
    }

    static string NameFor(string type)
    {
        if (type == "tower")
            return "Watch Tower";
        if (type == "ruins")
            return "Old Ruins";
        return "Pale Crystal";
    }

    static string HintFor(string type)
    {
        if (type == "tower")
            return "A tower stands above the island.";
        if (type == "ruins")
            return "Broken stones mark the old ruins.";
        return "A crystal glints on the high ground.";
    }

    static string LoreFor(string type)
    {
        if (type == "tower")
            return "The tower once guided ships through the clouds.";
        if (type == "ruins")
            return "Travelers left these stones as a waypoint.";
        return "Pilots say the crystal hums when the wind is quiet.";
    }
}
