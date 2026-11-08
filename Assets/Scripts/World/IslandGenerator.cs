using System.Collections.Generic;
using UnityEngine;

public static class IslandGenerator
{
    const int Octaves = 4;
    const float Lacunarity = 2f;
    const float Persistence = 0.5f;
    const float Seabed = -8f;
    const float TerraceStep = 8f;
    const float LakeWaterY = 0.85f;
    const float LakeFloorY = 0.35f;
    const float ArchMajor = 33f;
    const float ArchTube = 5.5f;
    const float ArchFoot = 0.5f;
    const float CellSize = 4f;

    static Material waterMaterial;
    static Material stoneMaterial;
    static Material spireMaterial;
    static Mesh archMesh;
    static Mesh waterMesh;

    public static float Height(float x, float z, float radius, IslandData island, int seed)
    {
        string style = StyleOf(island);
        float land = SurfaceHeight(x, z, radius, island, seed);
        float dist = radius > 0.01f ? Mathf.Sqrt(x * x + z * z) / radius : 0f;
        float coast = CoastScale(x, z, seed);
        float h = Mathf.Lerp(Seabed, land, EdgeMask(dist / coast, style));
        float rim = Smooth01(0.9f, 1.05f, dist / coast);
        return Mathf.Lerp(h, Seabed, rim);
    }

    public static void ApplyPalette()
    {
        EnsureMaterials();
        Paint(waterMaterial, WorldLook.Water, 0.72f);
        Paint(stoneMaterial, WorldLook.Rock, 0.18f);
        Paint(spireMaterial, WorldLook.Rock * 0.72f, 0.12f);
    }

    public static float SurfaceHeight(float x, float z, float radius, IslandData island, int seed)
    {
        string style = StyleOf(island);
        float rough = WorldLook.RoughnessOf(island);
        float land = LandHeight(x, z, radius, style, seed, rough) * WorldLook.HeightScaleOf(island);
        if (HasFeature(island, "terraces"))
            land = Terrace(land);
        if (style == "canyon")
            land = CarveCanyons(land, x, z, radius, seed);
        if (HasFeature(island, "lake"))
            land = DigLake(land, x, z, radius, seed);
        return land;
    }

    public static void AddSurfaceFeatures(Transform islandRoot, float radius, IslandData island, int seed)
    {
        if (islandRoot == null || island == null)
            return;

        EnsureMaterials();
        if (HasFeature(island, "lake"))
            BuildLake(islandRoot, radius, seed);
        if (HasFeature(island, "arch"))
            BuildArch(islandRoot, radius, island, seed);
        if (HasFeature(island, "spires"))
            BuildSpires(islandRoot, radius, island, seed);
    }

    public static void Build(Transform islandRoot, float radius, IslandData island, int worldSeed, int islandIndex, Material groundMaterial)
    {
        if (islandRoot == null)
            return;

        int seed = Mix(worldSeed, islandIndex, 17);
        EnsureMaterials();
        BuildGround(islandRoot, radius, island, seed, groundMaterial);

        if (HasFeature(island, "lake"))
            BuildLake(islandRoot, radius, seed);
        if (HasFeature(island, "arch"))
            BuildArch(islandRoot, radius, island, seed);
        if (HasFeature(island, "spires"))
            BuildSpires(islandRoot, radius, island, seed);
    }

    public static void ReleaseMaterials()
    {
        DestroyMaterial(ref waterMaterial);
        DestroyMaterial(ref stoneMaterial);
        DestroyMaterial(ref spireMaterial);
    }

    static void BuildGround(Transform islandRoot, float radius, IslandData island, int seed, Material groundMaterial)
    {
        float extent = radius * 1.06f;
        int quads = Mathf.Clamp(Mathf.CeilToInt(extent * 2f / CellSize), 18, 64);
        int side = quads + 1;
        var vertices = new Vector3[side * side];
        for (int z = 0; z < side; z++)
        {
            float v = (z / (float)quads) * 2f - 1f;
            for (int x = 0; x < side; x++)
            {
                float u = (x / (float)quads) * 2f - 1f;
                float px = u * extent;
                float pz = v * extent;
                float py = Height(px, pz, radius, island, seed);
                vertices[z * side + x] = new Vector3(px, py, pz);
            }
        }

        float keep = radius * 1.03f;
        float keepSqr = keep * keep;
        var triangles = new List<int>(quads * quads * 6);
        for (int z = 0; z < quads; z++)
        {
            for (int x = 0; x < quads; x++)
            {
                int a = z * side + x;
                int b = a + side;
                int c = a + 1;
                int d = b + 1;
                Vector3 mid = (vertices[a] + vertices[b] + vertices[c] + vertices[d]) * 0.25f;
                if (mid.x * mid.x + mid.z * mid.z > keepSqr)
                    continue;

                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(c);
                triangles.Add(b);
                triangles.Add(d);
            }
        }

        var mesh = new Mesh();
        mesh.name = "Island";
        mesh.vertices = vertices;
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var ground = new GameObject("Ground");
        ground.transform.SetParent(islandRoot, false);
        var filter = ground.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = ground.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = groundMaterial;
        var collider = ground.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
        var owned = ground.AddComponent<OwnedMesh>();
        owned.mesh = mesh;
    }

    static void BuildLake(Transform islandRoot, float radius, int seed)
    {
        LakeShape(radius, seed, out Vector2 center, out float lakeRadius);
        var lake = new GameObject("Lake");
        lake.transform.SetParent(islandRoot, false);
        lake.transform.localPosition = new Vector3(center.x, LakeWaterY, center.y);
        float span = lakeRadius * 0.7f;
        lake.transform.localScale = new Vector3(span, 1f, span);

        Mesh mesh = SharedWaterMesh();
        var filter = lake.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = lake.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = waterMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static void BuildArch(Transform islandRoot, float radius, IslandData island, int seed)
    {
        var rng = new System.Random(Mix(seed, 3, 11));
        Vector3 local = Vector3.zero;
        float groundY = 0f;
        bool placed = false;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            float angle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
            float distance = radius * (0.62f + (float)rng.NextDouble() * 0.12f);
            float x = Mathf.Cos(angle) * distance;
            float z = Mathf.Sin(angle) * distance;
            float y = SurfaceHeight(x, z, radius, island, seed);
            if (y < 2f)
                continue;

            local = new Vector3(x, y - ArchFoot, z);
            groundY = y;
            placed = true;
            break;
        }

        if (!placed)
        {
            local = new Vector3(radius * 0.7f, 2f, 0f);
            groundY = SurfaceHeight(local.x, local.z, radius, island, seed);
            local.y = Mathf.Max(0.5f, groundY - ArchFoot);
        }

        var arch = new GameObject("Arch");
        arch.transform.SetParent(islandRoot, false);
        arch.transform.localPosition = local;
        Vector3 radial = new Vector3(local.x, 0f, local.z);
        if (radial.sqrMagnitude < 0.01f)
            radial = Vector3.forward;
        arch.transform.localRotation = Quaternion.LookRotation(radial.normalized, Vector3.up);

        Mesh mesh = SharedArchMesh();
        var filter = arch.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = arch.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = stoneMaterial;
        var collider = arch.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
    }

    static void BuildSpires(Transform islandRoot, float radius, IslandData island, int seed)
    {
        var rng = new System.Random(Mix(seed, 5, 23));
        int count = 3 + rng.Next(4);
        float start = (float)(rng.NextDouble() * Mathf.PI * 2.0);
        for (int i = 0; i < count; i++)
        {
            float angle = start + (i + 0.5f) * Mathf.PI * 2f / count + ((float)rng.NextDouble() - 0.5f) * 0.25f;
            float distance = radius * (0.34f + (float)rng.NextDouble() * 0.38f);
            float x = Mathf.Cos(angle) * distance;
            float z = Mathf.Sin(angle) * distance;
            float floor = SurfaceHeight(x, z, radius, island, seed);
            float height = 24f + (float)rng.NextDouble() * 16f;
            float width = 2.1f + (float)rng.NextDouble() * 1.3f;

            GameObject pillar = EnvironmentGenerator.CreatePrimitiveObject(PrimitiveType.Cube, spireMaterial, islandRoot);
            pillar.name = "Spire";
            pillar.transform.localPosition = new Vector3(x, floor + height * 0.5f, z);
            pillar.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            pillar.transform.localScale = new Vector3(width, height, width);
        }
    }

    static float LandHeight(float x, float z, float radius, string style, int seed, float roughness)
    {
        Offsets(seed, out float ox, out float oz);
        float rough = Mathf.Clamp01(roughness);
        if (style == "mountainous")
        {
            float ridge = Ridged(x, z, radius, ox, oz);
            return 10f + ridge * 42f * rough;
        }

        float fbm = Fbm(x, z, radius, ox, oz) * rough;
        if (style == "plateau")
            return Mathf.Min(34f + fbm * 12f, 30f);
        if (style == "canyon")
            return 12f + fbm * 9f;
        return 8f + fbm * 6f;
    }

    static float Fbm(float x, float z, float radius, float ox, float oz)
    {
        Warp(ref x, ref z, radius, ox, oz);
        float sum = 0f;
        float amplitude = 1f;
        float frequency = 1.7f / Mathf.Max(24f, radius);
        float weight = 0f;
        for (int octave = 0; octave < Octaves; octave++)
        {
            float n = Noise(x * frequency + ox, z * frequency + oz) * 2f - 1f;
            sum += n * amplitude;
            weight += amplitude;
            amplitude *= Persistence;
            frequency *= Lacunarity;
        }

        return weight > 0f ? sum / weight : 0f;
    }

    static float Ridged(float x, float z, float radius, float ox, float oz)
    {
        Warp(ref x, ref z, radius, ox + 19f, oz + 19f);
        float sum = 0f;
        float amplitude = 1f;
        float frequency = 1.7f / Mathf.Max(24f, radius);
        float weight = 0f;
        for (int octave = 0; octave < Octaves; octave++)
        {
            float n = Noise(x * frequency + ox, z * frequency + oz) * 2f - 1f;
            n = 1f - Mathf.Abs(n);
            sum += n * amplitude;
            weight += amplitude;
            amplitude *= Persistence;
            frequency *= Lacunarity;
        }

        return weight > 0f ? sum / weight : 0f;
    }

    static void Warp(ref float x, ref float z, float radius, float ox, float oz)
    {
        float reach = radius * 0.12f;
        float wx = (Noise(x * 0.02f + ox, z * 0.02f + oz) - 0.5f) * reach;
        float wz = (Noise(x * 0.02f + oz + 8f, z * 0.02f + ox + 8f) - 0.5f) * reach;
        x += wx;
        z += wz;
    }

    static float Smooth01(float edge0, float edge1, float value)
    {
        float t = Mathf.InverseLerp(edge0, edge1, value);
        return t * t * (3f - 2f * t);
    }

    static float EdgeMask(float dist, string style)
    {
        float start = 0.68f;
        float end = 1.04f;
        if (style == "plateau")
        {
            start = 0.76f;
            end = 0.94f;
        }
        else if (style == "rolling")
        {
            start = 0.62f;
            end = 1.02f;
        }

        float t = Mathf.InverseLerp(start, end, dist);
        return 1f - Mathf.SmoothStep(0f, 1f, t);
    }

    static float CoastScale(float x, float z, int seed)
    {
        Offsets(seed + 101, out float ox, out float oz);
        float angle = Mathf.Atan2(z, x);
        float n = Noise(Mathf.Cos(angle) * 1.7f + ox, Mathf.Sin(angle) * 1.7f + oz);
        return 1f + (n - 0.5f) * 0.1f;
    }

    static float CarveCanyons(float height, float x, float z, float radius, int seed)
    {
        var rng = new System.Random(Mix(seed, 9, 4));
        int valleys = 1 + rng.Next(2);
        for (int valley = 0; valley < valleys; valley++)
        {
            float heading = (float)(rng.NextDouble() * Mathf.PI * 2.0);
            float dirX = Mathf.Cos(heading);
            float dirZ = Mathf.Sin(heading);
            float along = x * dirX + z * dirZ;
            float across = x * -dirZ + z * dirX;
            Offsets(seed + 30 + valley * 17, out float ox, out float oz);
            float wobble = (Noise(along * (1.4f / Mathf.Max(24f, radius)) + ox, oz) - 0.5f) * radius * 0.65f;
            float width = radius * (0.11f + valley * 0.035f);
            float carve = 1f - Smooth01(width * 0.28f, width, Mathf.Abs(across - wobble));
            height = Mathf.Lerp(height, 1.5f, carve);
        }

        return height;
    }

    static float DigLake(float height, float x, float z, float radius, int seed)
    {
        LakeShape(radius, seed, out Vector2 center, out float lakeRadius);
        float distance = Vector2.Distance(new Vector2(x, z), center);
        if (distance >= lakeRadius)
            return height;

        float bowl = 1f - Smooth01(lakeRadius * 0.78f, lakeRadius, distance);
        return Mathf.Lerp(height, LakeFloorY, bowl);
    }

    static void LakeShape(float radius, int seed, out Vector2 center, out float lakeRadius)
    {
        var rng = new System.Random(Mix(seed, 2, 8));
        float angle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
        float distance = radius * (0.1f + (float)rng.NextDouble() * 0.18f);
        center = new Vector2(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance);
        lakeRadius = Mathf.Clamp(radius * 0.2f, 12f, radius * 0.3f);
    }

    static float Terrace(float height)
    {
        const float lip = 0.2f;
        float scaled = height / TerraceStep;
        float band = Mathf.Floor(scaled);
        float frac = scaled - band;
        float ramp = Smooth01(1f - lip, 1f, frac);
        return (band + ramp) * TerraceStep;
    }

    static Mesh SharedArchMesh()
    {
        if (archMesh != null)
            return archMesh;

        const int steps = 14;
        const int sides = 8;
        int row = sides + 1;
        var vertices = new Vector3[(steps + 1) * row];
        var triangles = new int[steps * sides * 6];
        int write = 0;
        for (int i = 0; i <= steps; i++)
        {
            float u = Mathf.PI * i / steps;
            var radial = new Vector3(Mathf.Cos(u), Mathf.Sin(u), 0f);
            Vector3 center = radial * ArchMajor;
            for (int j = 0; j <= sides; j++)
            {
                float v = Mathf.PI * 2f * j / sides;
                Vector3 offset = radial * (Mathf.Cos(v) * ArchTube) + Vector3.forward * (Mathf.Sin(v) * ArchTube);
                vertices[i * row + j] = center + offset;
            }
        }

        for (int i = 0; i < steps; i++)
        {
            for (int j = 0; j < sides; j++)
            {
                int a = i * row + j;
                int b = a + row;
                int c = a + 1;
                int d = b + 1;
                triangles[write++] = a;
                triangles[write++] = b;
                triangles[write++] = c;
                triangles[write++] = c;
                triangles[write++] = b;
                triangles[write++] = d;
            }
        }

        archMesh = new Mesh();
        archMesh.name = "Arch";
        archMesh.vertices = vertices;
        archMesh.triangles = triangles;
        archMesh.RecalculateNormals();
        archMesh.RecalculateBounds();
        return archMesh;
    }

    static Mesh SharedWaterMesh()
    {
        if (waterMesh != null)
            return waterMesh;

        waterMesh = new Mesh();
        waterMesh.name = "LakeSurface";
        waterMesh.vertices = new[]
        {
            new Vector3(-1f, 0f, -1f),
            new Vector3(-1f, 0f, 1f),
            new Vector3(1f, 0f, -1f),
            new Vector3(1f, 0f, 1f)
        };
        waterMesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
        waterMesh.RecalculateNormals();
        waterMesh.RecalculateBounds();
        return waterMesh;
    }

    static void EnsureMaterials()
    {
        if (waterMaterial == null)
        {
            waterMaterial = CreateLit(new Color(0.12f, 0.42f, 0.78f));
            if (waterMaterial.HasProperty("_Smoothness"))
                waterMaterial.SetFloat("_Smoothness", 0.72f);
        }
        if (stoneMaterial == null)
            stoneMaterial = CreateLit(new Color(0.45f, 0.42f, 0.38f));
        if (spireMaterial == null)
            spireMaterial = CreateLit(new Color(0.28f, 0.27f, 0.26f));
    }

    static void Paint(Material material, Color color, float smoothness)
    {
        if (material == null)
            return;

        color.a = 1f;
        material.color = color;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", smoothness);
    }

    static Material CreateLit(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        var material = new Material(shader);
        material.color = color;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.18f);
        return material;
    }

    static bool HasFeature(IslandData island, string feature)
    {
        if (island == null || island.features == null)
            return false;

        for (int i = 0; i < island.features.Count; i++)
        {
            if (island.features[i] == feature)
                return true;
        }

        return false;
    }

    static string StyleOf(IslandData island)
    {
        if (island == null || string.IsNullOrEmpty(island.terrainStyle))
            return "rolling";
        return island.terrainStyle;
    }

    static float Noise(float x, float z)
    {
        return Mathf.PerlinNoise(x + 1000f, z + 1000f);
    }

    static void Offsets(int seed, out float ox, out float oz)
    {
        unchecked
        {
            int a = seed * 73856093;
            int b = seed * 19349663;
            ox = (a & 4095) * 0.173f;
            oz = (b & 4095) * 0.173f;
        }
    }

    static int Mix(int seed, int index, int salt)
    {
        unchecked
        {
            int value = seed;
            value = value * 31 + index;
            value = value * 31 + salt;
            return value;
        }
    }

    static void DestroyMaterial(ref Material material)
    {
        if (material == null)
            return;

        if (Application.isPlaying)
            Object.Destroy(material);
        else
            Object.DestroyImmediate(material);
        material = null;
    }
}
