using System.Collections.Generic;
using UnityEngine;

public class PaletteBinder : MonoBehaviour
{
    readonly List<Material> owned = new List<Material>();

    void Awake()
    {
        PalettePart[] parts = GetComponentsInChildren<PalettePart>(true);
        for (int i = 0; i < parts.Length; i++)
        {
            MeshRenderer renderer = parts[i].GetComponent<MeshRenderer>();
            if (renderer == null || renderer.sharedMaterial == null)
                continue;

            var copy = new Material(renderer.sharedMaterial);
            copy.name = parts[i].name + " Palette";
            Apply(copy, parts[i].role);
            renderer.sharedMaterial = copy;
            owned.Add(copy);
        }
    }

    void OnDestroy()
    {
        for (int i = 0; i < owned.Count; i++)
        {
            if (owned[i] == null)
                continue;
            if (Application.isPlaying)
                Destroy(owned[i]);
            else
                DestroyImmediate(owned[i]);
        }

        owned.Clear();
    }

    public static void Apply(Material material, PaletteRole role)
    {
        switch (role)
        {
            case PaletteRole.Rock:
                ModelLibrary.Paint(material, RockColor(WorldLook.Rock), 0.08f, Color.black, false);
                break;
            case PaletteRole.Wood:
                ModelLibrary.Paint(material, WoodColor(WorldLook.Rock), 0.06f, Color.black, false);
                break;
            case PaletteRole.Moss:
                ModelLibrary.Paint(material, MossColor(WorldLook.Grass), 0.12f, Color.black, false);
                break;
            default:
                Color glow = GlowAlbedo(WorldLook.Accent, WorldLook.SkyMood == "night", out Color emission);
                ModelLibrary.Paint(material, glow, 0.42f, emission, true);
                break;
        }
    }

    public static Color RockColor(Color rock)
    {
        return Color.Lerp(rock, Color.white, 0.1f);
    }

    public static Color WoodColor(Color rock)
    {
        return Color.Lerp(new Color(0.55f, 0.33f, 0.16f), rock, 0.3f);
    }

    public static Color MossColor(Color grass)
    {
        return Color.Lerp(grass, new Color(0.16f, 0.36f, 0.15f), 0.22f);
    }

    public static Color GlowAlbedo(Color accent, bool night, out Color emission)
    {
        Color.RGBToHSV(accent, out float h, out float s, out float v);
        Color glow = Color.HSVToRGB(h, Mathf.Clamp(s, 0.42f, 1f), Mathf.Max(v, 0.78f));
        glow.a = 1f;
        emission = glow * (night ? 1.35f : 0.62f);
        return glow;
    }
}
