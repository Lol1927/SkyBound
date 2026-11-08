using System.Collections.Generic;
using UnityEngine;

public class WorldGenerator : MonoBehaviour
{
    const string ResourcePath = "Worlds/sample_world";
    const float DiscLift = 0.35f;

    readonly List<IslandSpot> spots = new List<IslandSpot>();
    readonly List<Color> spotColors = new List<Color>();
    readonly List<Vector3> clusterAims = new List<Vector3>();

    public bool TryNearestCluster(Vector3 from, out Vector3 aim)
    {
        aim = Vector3.zero;
        float best = float.PositiveInfinity;
        bool found = false;
        Vector3 flatFrom = new Vector3(from.x, 0f, from.z);
        for (int i = 0; i < clusterAims.Count; i++)
        {
            Vector3 flat = new Vector3(clusterAims[i].x, 0f, clusterAims[i].z);
            float distance = (flat - flatFrom).sqrMagnitude;
            if (distance >= best)
                continue;

            best = distance;
            aim = clusterAims[i];
            found = true;
        }

        return found;
    }

    public int ClusterCount => spots.Count;

    public bool TryGetCluster(int index, out Vector3 center, out float radius)
    {
        center = Vector3.zero;
        radius = 0f;
        if (index < 0 || index >= spots.Count)
            return false;

        IslandSpot spot = spots[index];
        center = transform.TransformPoint(new Vector3(spot.center.x, 0f, spot.center.z));
        radius = spot.radius;
        return true;
    }

    public void Generate(WorldData data)
    {
        Clear();
        if (data == null)
            return;

        WorldLook.Apply(data);
        WorldAtmosphere.Apply(data);
        IslandGenerator.ApplyPalette();
        EnvironmentGenerator.ApplyPalette();
        if (data.islands == null)
            return;

        List<IslandSpot> generated = SpotGenerator.Generate(data);
        var bodies = new List<ClusterIsland>(16);
        int floating = 0;

        for (int i = 0; i < generated.Count; i++)
        {
            IslandSpot spot = generated[i];
            IslandData island = i < data.islands.Count ? data.islands[i] : null;
            string biome = BiomeAt(data, i);
            var cluster = new GameObject("Cluster " + (i + 1) + " " + biome);
            cluster.transform.SetParent(transform, false);
            cluster.transform.localPosition = new Vector3(spot.center.x, 0f, spot.center.z);

            bodies.Clear();
            Vector3 localAim = FloatingIslandGenerator.BuildCluster(cluster.transform, spot.radius, island, data.seed, i, bodies);
            clusterAims.Add(cluster.transform.TransformPoint(localAim));
            spots.Add(spot);
            spotColors.Add(ColorForBiome(biome));
            floating += bodies.Count;

            for (int piece = 0; piece < bodies.Count; piece++)
            {
                ClusterIsland body = bodies[piece];
                IslandData landmark = body.main ? island : null;
                EnvironmentGenerator.PopulateSurface(biome, data.seed, i * 32 + piece, body.root, body.surface, body.radius, landmark);
            }
        }

        var clouds = new GameObject("Clouds");
        clouds.transform.SetParent(transform, false);
        clouds.AddComponent<CloudGenerator>().Build(data);

        HideDebugLines();
        Debug.Log("Generated " + spots.Count + " clusters (" + floating + " floating islands) for " + data.worldName + " (seed " + data.seed + ", " + data.layout + ").");
    }

    public static void HideDebugLines()
    {
        LineRenderer[] lines = Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i] != null)
                lines[i].enabled = false;
        }
    }

    public void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            child.SetActive(false);
            DestroyGenerated(child);
        }

        spots.Clear();
        spotColors.Clear();
        clusterAims.Clear();
    }

    public static bool DrawingInGameView()
    {
        Camera camera = Camera.current;
        return camera != null && camera.cameraType == CameraType.Game;
    }

    void OnDrawGizmos()
    {
        if (DrawingInGameView())
            return;

        if (spots.Count > 0)
        {
            for (int i = 0; i < spots.Count; i++)
            {
                Color color = i < spotColors.Count ? spotColors[i] : Color.cyan;
                DrawSpotGizmo(spots[i], color);
            }
            return;
        }

        if (Application.isPlaying)
            return;

        TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null)
            return;

        WorldData data = JsonUtility.FromJson<WorldData>(asset.text);
        if (data == null || data.islands == null)
            return;

        WorldLook.Apply(data);
        List<IslandSpot> preview = SpotGenerator.Generate(data);
        for (int i = 0; i < preview.Count; i++)
            DrawSpotGizmo(preview[i], ColorForBiome(BiomeAt(data, i)));
    }

    void DrawSpotGizmo(IslandSpot spot, Color color)
    {
        Gizmos.color = color;
        Vector3 center = transform.TransformPoint(new Vector3(spot.center.x, DiscLift, spot.center.z));
        DrawCircle(center, spot.radius);
    }

    void OnDestroy()
    {
        EnvironmentGenerator.ReleaseMaterials();
        IslandGenerator.ReleaseMaterials();
        FloatingIslandGenerator.ReleaseMaterials();
    }

    static Color ColorForBiome(string biome)
    {
        if (biome == "snow")
            return WorldLook.Snow;
        if (biome == "volcanic")
            return Color.Lerp(WorldLook.Rock, WorldLook.Accent, 0.45f);
        return WorldLook.Grass;
    }

    static string BiomeAt(WorldData data, int index)
    {
        if (data.islands == null || index < 0 || index >= data.islands.Count || data.islands[index] == null)
            return "forest";

        string biome = data.islands[index].biome;
        return string.IsNullOrEmpty(biome) ? "forest" : biome;
    }

    static void DrawCircle(Vector3 center, float radius)
    {
        const int segments = 48;
        Vector3 previous = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }

    static void DestroyGenerated(Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(target);
        else
            UnityEngine.Object.DestroyImmediate(target);
    }
}

public class OwnedMesh : MonoBehaviour
{
    public Mesh mesh;

    void OnDestroy()
    {
        if (mesh == null)
            return;

        if (Application.isPlaying)
            Destroy(mesh);
        else
            DestroyImmediate(mesh);
    }
}
