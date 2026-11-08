using System.Collections.Generic;

[System.Serializable]
public class WorldData
{
    public string worldName;
    public int seed;
    public string layout;
    public float cloudDensity;
    public string skyMood = "noon";
    public PaletteData palette = new PaletteData();
    public float treeDensity = 0.7f;
    public float rockDensity = 0.5f;
    public float floatingRockDensity = 0.55f;
    public string coPilotIntro;
    public List<IslandData> islands = new List<IslandData>();
}

[System.Serializable]
public class PaletteData
{
    public string grass;
    public string rock;
    public string snow;
    public string water;
    public string skyTop;
    public string skyHorizon;
    public string fog;
    public string accent;
}

[System.Serializable]
public class IslandData
{
    public string biome;
    public string size;
    public bool hasLandmark;
    public string landmarkType;
    public string landmarkName;
    public string landmarkHint;
    public string landmarkLore;
    public string terrainStyle;
    public float heightScale = 1f;
    public float roughness = 1f;
    public float verticalSpread = 1f;
    public List<string> features = new List<string>();
}
