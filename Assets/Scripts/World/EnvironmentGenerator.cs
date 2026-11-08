using System.Collections.Generic;
using UnityEngine;

public static class EnvironmentGenerator
{
    const float WaterLevel = 0.5f;
    const float MinUpDot = 0.82f;
    const float SampleInset = 0.9f;
    const float LandmarkClearance = 20f;
    const int HitCapacity = 32;

    static readonly RaycastHit[] HitBuffer = new RaycastHit[HitCapacity];
    static readonly Dictionary<PrimitiveType, Mesh> PrimitiveMeshes = new Dictionary<PrimitiveType, Mesh>();
    static readonly Dictionary<string, GameObject> PropPrototypes = new Dictionary<string, GameObject>();
    static readonly string[] ForestTrees = { "broadleafA", "broadleafB", "pineA", "pineB" };
    static readonly string[] SnowTrees = { "snowPineA", "snowPineB" };
    static readonly string[] DeadTrees = { "deadA", "deadB" };
    static readonly string[] RockIds = { "rockA", "rockB", "rockC", "rockD" };

    static Material trunkMaterial;
    static Material canopyMaterial;
    static Material frostMaterial;
    static Material snowCanopyMaterial;
    static Material forestRockMaterial;
    static Material snowRockMaterial;
    static Material volcanicRockMaterial;
    static Material crystalMaterial;
    static Material towerMaterial;
    static Material roofMaterial;
    static Material ruinsMaterial;
    static Material glowCanopyMaterial;
    static Material moteMaterial;

    public static void ApplyPalette()
    {
        EnsureMaterials();
        bool night = WorldLook.SkyMood == "night";
        Color accentGlow = GlowColor(WorldLook.Accent);
        Paint(canopyMaterial, WorldLook.Grass, 0.12f, Color.black);
        Paint(frostMaterial, Color.Lerp(WorldLook.Grass, WorldLook.Snow, 0.48f), 0.14f, Color.black);
        Paint(snowCanopyMaterial, WorldLook.Snow, 0.16f, Color.black);
        Paint(glowCanopyMaterial, Color.Lerp(WorldLook.Grass, WorldLook.Accent, 0.55f), 0.4f, accentGlow * (night ? 2.2f : 0.35f));
        Paint(trunkMaterial, PaletteBinder.WoodColor(WorldLook.Rock), 0.08f, Color.black);
        Paint(forestRockMaterial, WorldLook.Rock, 0.16f, Color.black);
        Paint(snowRockMaterial, Color.Lerp(WorldLook.Rock, WorldLook.Snow, 0.45f), 0.2f, Color.black);
        Paint(volcanicRockMaterial, WorldLook.Rock * 0.45f, 0.08f, Color.black);
        Paint(crystalMaterial, WorldLook.Accent, 0.82f, accentGlow * (night ? 3.6f : 2.4f));
        Paint(towerMaterial, Color.Lerp(WorldLook.Rock, WorldLook.Accent, night ? 0.35f : 0.18f), 0.12f, night ? accentGlow * 0.9f : Color.black);
        Color roof = WorldLook.Accent;
        Paint(roofMaterial, roof, 0.35f, accentGlow * (night ? 1.8f : 0.65f));
        Paint(ruinsMaterial, Color.Lerp(WorldLook.Rock, WorldLook.Accent, night ? 0.4f : 0.28f), 0.14f, accentGlow * (night ? 1.15f : 0.2f));
        if (crystalMaterial != null)
            crystalMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EnableInstancing(trunkMaterial);
        EnableInstancing(canopyMaterial);
        EnableInstancing(frostMaterial);
        EnableInstancing(snowCanopyMaterial);
        EnableInstancing(forestRockMaterial);
        EnableInstancing(snowRockMaterial);
        EnableInstancing(volcanicRockMaterial);
    }

    public static void Populate(WorldData data, int islandIndex, Transform islandRoot, Collider surface, float radius)
    {
        if (data == null || data.islands == null || islandIndex < 0 || islandIndex >= data.islands.Count)
            return;

        IslandData island = data.islands[islandIndex];
        if (island == null)
            return;

        string biome = string.IsNullOrEmpty(island.biome) ? "forest" : island.biome;
        PopulateSurface(biome, data.seed, islandIndex, islandRoot, surface, radius, island);
    }

    public static void PopulateSurface(string biome, int worldSeed, int salt, Transform islandRoot, Collider surface, float radius, IslandData landmarkIsland)
    {
        if (islandRoot == null || surface == null)
            return;

        Physics.SyncTransforms();
        EnsureMaterials();

        var environment = new GameObject("Environment");
        environment.transform.SetParent(islandRoot, false);

        Vector3 center = islandRoot.position;
        center.y = 0f;
        bool hasLandmark = IsLandmark(landmarkIsland);
        Vector3 landmarkPoint = center;
        bool foundSurface = TryHighestPoint(center, radius, surface, out landmarkPoint);
        if (!foundSurface)
        {
            Debug.LogWarning("EnvironmentGenerator found no ground on " + islandRoot.name + ".");
            return;
        }

        if (hasLandmark)
            CreateLandmark(landmarkIsland, islandRoot, landmarkPoint, Seed(worldSeed, salt, 2));

        if (string.IsNullOrEmpty(biome))
            biome = "forest";
        CountsFor(biome, radius, out int treeCount, out int rockCount);
        treeCount = Mathf.Max(0, Mathf.RoundToInt(treeCount * Mathf.Clamp01(WorldLook.TreeDensity)));
        rockCount = Mathf.Max(0, Mathf.RoundToInt(rockCount * Mathf.Clamp01(WorldLook.RockDensity)));
        var rng = new System.Random(Seed(worldSeed, salt, 1));
        var occupied = new List<Vector3>();
        if (hasLandmark)
            occupied.Add(landmarkPoint);

        PlaceProps(rng, environment.transform, surface, center, radius, occupied, hasLandmark, landmarkPoint, treeCount, true, biome);
        PlaceProps(rng, environment.transform, surface, center, radius, occupied, hasLandmark, landmarkPoint, rockCount, false, biome);
        SpawnNightMotes(islandRoot, radius);
    }

    public static GameObject CreatePrimitiveObject(PrimitiveType type, Material material, Transform parent)
    {
        var prop = new GameObject(type.ToString());
        prop.transform.SetParent(parent, false);
        var filter = prop.AddComponent<MeshFilter>();
        filter.sharedMesh = SharedPrimitive(type);
        var renderer = prop.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        return prop;
    }

    public static void ReleaseMaterials()
    {
        DestroyMaterial(ref trunkMaterial);
        DestroyMaterial(ref canopyMaterial);
        DestroyMaterial(ref frostMaterial);
        DestroyMaterial(ref snowCanopyMaterial);
        DestroyMaterial(ref forestRockMaterial);
        DestroyMaterial(ref snowRockMaterial);
        DestroyMaterial(ref volcanicRockMaterial);
        DestroyMaterial(ref crystalMaterial);
        DestroyMaterial(ref towerMaterial);
        DestroyMaterial(ref roofMaterial);
        DestroyMaterial(ref ruinsMaterial);
        DestroyMaterial(ref glowCanopyMaterial);
        DestroyMaterial(ref moteMaterial);
    }

    static void PlaceProps(System.Random rng, Transform parent, Collider surface, Vector3 center, float radius, List<Vector3> occupied, bool hasLandmark, Vector3 landmarkPoint, int count, bool trees, string biome)
    {
        float spacing = trees ? 7f : 5f;
        int attempts = count * 14;
        int placed = 0;
        while (placed < count && attempts-- > 0)
        {
            float angle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
            float distance = Mathf.Sqrt((float)rng.NextDouble()) * radius * SampleInset;
            var sample = new Vector3(center.x + Mathf.Cos(angle) * distance, 0f, center.z + Mathf.Sin(angle) * distance);
            if (TooClose(sample, occupied, spacing, hasLandmark, landmarkPoint))
                continue;
            if (!TryHitSurface(sample, surface, out RaycastHit hit))
                continue;
            if (!IsWalkable(hit))
                continue;

            if (trees)
                CreateTree(parent, hit, rng, biome);
            else
                CreateRock(parent, hit, rng, biome);

            occupied.Add(hit.point);
            placed++;
        }
    }

    static bool IsLandmark(IslandData island)
    {
        if (island == null || !island.hasLandmark)
            return false;
        if (string.IsNullOrEmpty(island.landmarkType) || island.landmarkType == "none")
            return false;

        return island.landmarkType == "crystal" || island.landmarkType == "tower" || island.landmarkType == "ruins";
    }

    static void CreateLandmark(IslandData island, Transform islandRoot, Vector3 point, int seed)
    {
        if (island.landmarkType == "none" || string.IsNullOrEmpty(island.landmarkType))
            return;

        var root = new GameObject(string.IsNullOrEmpty(island.landmarkName) ? "Landmark" : island.landmarkName);
        root.transform.SetParent(islandRoot, false);
        root.transform.position = point;

        var trigger = root.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 40f;

        var landmark = root.AddComponent<Landmark>();
        landmark.landmarkName = island.landmarkName;
        landmark.landmarkHint = island.landmarkHint;
        landmark.landmarkLore = island.landmarkLore;
        landmark.discovered = false;

        if (island.landmarkType == "crystal")
            AttachModel(root.transform, ModelLibrary.CrystalPrefab, seed, () => LandmarkModels.CreateCrystal(ModelLibrary.Load()));
        else if (island.landmarkType == "tower")
            AttachModel(root.transform, ModelLibrary.TowerPrefab, seed, () => LandmarkModels.CreateTower(ModelLibrary.Load()));
        else if (island.landmarkType == "ruins")
            AttachModel(root.transform, ModelLibrary.RuinsPrefab, seed, () => LandmarkModels.CreateRuins(ModelLibrary.Load()));
        else
            Debug.LogWarning("Unknown landmark type '" + island.landmarkType + "'.");
    }

    static void AttachModel(Transform parent, string path, int seed, System.Func<GameObject> fallback)
    {
        GameObject model = null;
#if UNITY_EDITOR
        GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab != null)
            model = Object.Instantiate(prefab);
#endif
        bool procedural = model == null;
        if (procedural)
            model = fallback();

        model.transform.SetParent(parent, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(0f, Mathf.Repeat(seed, 360f), 0f);
        model.transform.localScale = Vector3.one;
        if (procedural)
            ClaimGeneratedMeshes(model);
    }

    static void ClaimGeneratedMeshes(GameObject model)
    {
        MeshFilter[] filters = model.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            if (filters[i] == null || filters[i].sharedMesh == null)
                continue;
            var owned = filters[i].gameObject.AddComponent<OwnedMesh>();
            owned.mesh = filters[i].sharedMesh;
        }
    }

    static void CreateTree(Transform parent, RaycastHit hit, System.Random rng, string biome)
    {
        string[] choices = ForestTrees;
        if (biome == "snow")
            choices = SnowTrees;
        else if (biome == "volcanic")
            choices = DeadTrees;

        float scale = Range(rng, 0.78f, 1.38f);
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, hit.normal)
            * Quaternion.Euler(Range(rng, -4f, 4f), Range(rng, 0f, 360f), Range(rng, -4f, 4f));
        SpawnProp(choices[rng.Next(choices.Length)], parent, hit.point, rotation, Vector3.one * scale, biome);
    }

    static void CreateRock(Transform parent, RaycastHit hit, System.Random rng, string biome)
    {
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, hit.normal)
            * Quaternion.Euler(Range(rng, -22f, 22f), Range(rng, 0f, 360f), Range(rng, -22f, 22f));
        Vector3 scale = new Vector3(Range(rng, 0.7f, 1.65f), Range(rng, 0.65f, 1.5f), Range(rng, 0.7f, 1.55f));
        Vector3 position = hit.point - hit.normal * 0.18f;
        SpawnProp(RockIds[rng.Next(RockIds.Length)], parent, position, rotation, scale, biome);
    }

    static void SpawnProp(string id, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale, string biome)
    {
        EnsureMaterials();
        GameObject source = PropSource(id);
        if (source == null)
            return;

        GameObject prop = Object.Instantiate(source, parent);
        prop.name = source.name;
        prop.SetActive(true);
        ClearHideFlags(prop);
        prop.transform.SetPositionAndRotation(position, rotation);
        prop.transform.localScale = scale;

        PropPart[] parts = prop.GetComponentsInChildren<PropPart>(true);
        for (int i = 0; i < parts.Length; i++)
        {
            MeshRenderer renderer = parts[i].GetComponent<MeshRenderer>();
            if (renderer == null)
                continue;
            renderer.sharedMaterial = MaterialFor(parts[i].tint, biome);
        }
    }

    static GameObject PropSource(string id)
    {
        VegetationCatalog catalog = Resources.Load<VegetationCatalog>("VegetationCatalog");
        if (catalog != null)
        {
            GameObject prefab = catalog.Find(id);
            if (prefab != null)
                return prefab;
        }

        if (PropPrototypes.TryGetValue(id, out GameObject cached) && cached != null)
            return cached;

        GameObject created = FoliageModels.Create(id, trunkMaterial, canopyMaterial, frostMaterial, snowCanopyMaterial, forestRockMaterial);
        created.hideFlags = HideFlags.HideAndDontSave;
        created.SetActive(false);
        PropPrototypes[id] = created;
        return created;
    }

    static Material MaterialFor(PropTint tint, string biome)
    {
        switch (tint)
        {
            case PropTint.Leaf:
                return canopyMaterial;
            case PropTint.Frost:
                return frostMaterial != null ? frostMaterial : canopyMaterial;
            case PropTint.Snow:
                return snowCanopyMaterial;
            case PropTint.Rock:
                if (biome == "snow")
                    return snowRockMaterial;
                if (biome == "volcanic")
                    return volcanicRockMaterial;
                return forestRockMaterial;
            default:
                return trunkMaterial;
        }
    }

    static void ClearHideFlags(GameObject prop)
    {
        Transform[] transforms = prop.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
            transforms[i].gameObject.hideFlags = HideFlags.None;
    }

    static void EnableInstancing(Material material)
    {
        if (material != null)
            material.enableInstancing = true;
    }

    static void SpawnNightMotes(Transform islandRoot, float radius)
    {
        if (WorldLook.SkyMood != "night" || islandRoot == null)
            return;

        var go = new GameObject("Night Motes");
        go.transform.SetParent(islandRoot, false);
        go.transform.localPosition = new Vector3(0f, 6f, 0f);

        var particles = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = true;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.28f);
        Color glow = GlowColor(WorldLook.Accent);
        glow.a = 0.5f;
        main.startColor = glow;
        main.gravityModifier = -0.015f;
        main.maxParticles = 40;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 3.5f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Clamp(radius * 0.45f, 8f, 36f);

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.4f;
        noise.frequency = 0.22f;
        noise.scrollSpeed = 0.12f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(glow, 0f),
                new GradientColorKey(Color.Lerp(glow, Color.white, 0.35f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.7f, 0.2f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = MoteMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    static Color GlowColor(Color accent)
    {
        Color.RGBToHSV(accent, out float h, out float s, out float v);
        Color glow = Color.HSVToRGB(h, Mathf.Clamp(s, 0.35f, 1f), Mathf.Max(v, 0.72f));
        glow.a = 1f;
        return glow;
    }

    static Material MoteMaterial()
    {
        if (moteMaterial != null)
            return moteMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        moteMaterial = new Material(shader);
        moteMaterial.name = "Night Motes";
        return moteMaterial;
    }

    static bool TryHighestPoint(Vector3 center, float radius, Collider surface, out Vector3 point)
    {
        point = center;
        bool found = false;
        float bestY = float.NegativeInfinity;
        const int grid = 9;
        for (int z = 0; z < grid; z++)
        {
            for (int x = 0; x < grid; x++)
            {
                float u = ((x + 0.5f) / grid) * 2f - 1f;
                float v = ((z + 0.5f) / grid) * 2f - 1f;
                if (u * u + v * v > SampleInset * SampleInset)
                    continue;

                var sample = new Vector3(center.x + u * radius, 0f, center.z + v * radius);
                if (!TryHitSurface(sample, surface, out RaycastHit hit) || !IsWalkable(hit))
                    continue;
                if (found && hit.point.y <= bestY)
                    continue;

                found = true;
                bestY = hit.point.y;
                point = hit.point;
            }
        }

        if (TryHitSurface(center, surface, out RaycastHit centerHit) && IsWalkable(centerHit) && (!found || centerHit.point.y >= bestY - 0.05f))
        {
            point = centerHit.point;
            found = true;
        }

        return found;
    }

    static bool TryHitSurface(Vector3 xz, Collider surface, out RaycastHit hit)
    {
        hit = default;
        var origin = new Vector3(xz.x, 800f, xz.z);
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, HitBuffer, 1200f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        bool found = false;
        float bestY = float.NegativeInfinity;
        for (int i = 0; i < count; i++)
        {
            if (HitBuffer[i].collider != surface || HitBuffer[i].point.y <= bestY)
                continue;

            bestY = HitBuffer[i].point.y;
            hit = HitBuffer[i];
            found = true;
        }

        return found;
    }

    static bool IsWalkable(RaycastHit hit)
    {
        return hit.point.y > WaterLevel && hit.normal.y >= MinUpDot;
    }

    static bool TooClose(Vector3 sample, List<Vector3> occupied, float spacing, bool hasLandmark, Vector3 landmarkPoint)
    {
        float spacingSqr = spacing * spacing;
        float clearSqr = LandmarkClearance * LandmarkClearance;
        for (int i = 0; i < occupied.Count; i++)
        {
            Vector3 delta = sample - occupied[i];
            delta.y = 0f;
            float limit = i == 0 && hasLandmark ? clearSqr : spacingSqr;
            if (delta.sqrMagnitude < limit)
                return true;
        }

        return false;
    }

    static void CountsFor(string biome, float radius, out int trees, out int rocks)
    {
        float area = radius * radius;
        if (biome == "volcanic")
        {
            trees = Mathf.Clamp(Mathf.RoundToInt(area / 1200f), 4, 16);
            rocks = Mathf.Clamp(Mathf.RoundToInt(area / 380f), 8, 30);
            return;
        }

        if (biome == "snow")
        {
            trees = Mathf.Clamp(Mathf.RoundToInt(area / 1600f), 4, 14);
            rocks = Mathf.Clamp(Mathf.RoundToInt(area / 480f), 8, 26);
            return;
        }

        trees = Mathf.Clamp(Mathf.RoundToInt(area / 180f), 14, 64);
        rocks = Mathf.Clamp(Mathf.RoundToInt(area / 900f), 5, 16);
    }

    static void EnsureMaterials()
    {
        if (trunkMaterial == null)
            trunkMaterial = ModelLibrary.CreateInstanced(new Color(0.36f, 0.24f, 0.13f), 0.08f);
        if (canopyMaterial == null)
            canopyMaterial = ModelLibrary.CreateInstanced(new Color(0.16f, 0.45f, 0.18f), 0.12f);
        if (frostMaterial == null)
            frostMaterial = ModelLibrary.CreateInstanced(new Color(0.55f, 0.72f, 0.62f), 0.14f);
        if (snowCanopyMaterial == null)
            snowCanopyMaterial = ModelLibrary.CreateInstanced(new Color(0.92f, 0.96f, 1f), 0.16f);
        if (forestRockMaterial == null)
            forestRockMaterial = ModelLibrary.CreateInstanced(new Color(0.38f, 0.37f, 0.34f), 0.12f);
        if (snowRockMaterial == null)
            snowRockMaterial = ModelLibrary.CreateInstanced(new Color(0.76f, 0.8f, 0.84f), 0.16f);
        if (volcanicRockMaterial == null)
            volcanicRockMaterial = ModelLibrary.CreateInstanced(new Color(0.16f, 0.14f, 0.15f), 0.08f);
        if (towerMaterial == null)
            towerMaterial = CreateLit(new Color(0.32f, 0.3f, 0.28f));
        if (roofMaterial == null)
            roofMaterial = CreateLit(new Color(0.22f, 0.18f, 0.16f));
        if (ruinsMaterial == null)
            ruinsMaterial = CreateLit(new Color(0.45f, 0.42f, 0.36f));
        if (glowCanopyMaterial == null)
        {
            glowCanopyMaterial = CreateLit(new Color(0.45f, 0.95f, 1f));
            glowCanopyMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        if (crystalMaterial == null)
        {
            crystalMaterial = CreateLit(new Color(0.45f, 0.95f, 1f));
            var glow = new Color(0.25f, 3.4f, 3.8f);
            if (crystalMaterial.HasProperty("_EmissionColor"))
                crystalMaterial.SetColor("_EmissionColor", glow);
            crystalMaterial.EnableKeyword("_EMISSION");
            crystalMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
    }

    static void Paint(Material material, Color color, float smoothness, Color emission)
    {
        if (material == null)
            return;

        color.a = 1f;
        material.color = color;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", smoothness);

        bool glow = emission.maxColorComponent > 0.01f;
        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", glow ? emission : Color.black);
        if (glow)
            material.EnableKeyword("_EMISSION");
        else
            material.DisableKeyword("_EMISSION");
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
            material.SetFloat("_Smoothness", 0.12f);
        return material;
    }

    static Mesh SharedPrimitive(PrimitiveType type)
    {
        if (PrimitiveMeshes.TryGetValue(type, out Mesh cached) && cached != null)
            return cached;

        GameObject temp = GameObject.CreatePrimitive(type);
        temp.hideFlags = HideFlags.HideAndDontSave;
        Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(temp);
        PrimitiveMeshes[type] = mesh;
        return mesh;
    }

    static int Seed(int worldSeed, int islandIndex, int salt)
    {
        unchecked
        {
            int value = worldSeed;
            value = value * 31 + islandIndex;
            value = value * 31 + salt;
            return value;
        }
    }

    static float Range(System.Random rng, float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
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
