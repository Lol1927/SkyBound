using UnityEngine;

public static class LandmarkModels
{
    public static GameObject CreateCrystal(ModelKit kit)
    {
        var root = new GameObject("Crystal");
        root.AddComponent<PaletteBinder>();

        var rock = new FlatMesh();
        rock.Box(new Vector3(0f, 0.7f, 0f), new Vector3(5.4f, 1.4f, 4.6f), Quaternion.Euler(0f, 8f, 0f));
        rock.Box(new Vector3(0.9f, 1.35f, 0.45f), new Vector3(3.3f, 1.5f, 2.7f), Quaternion.Euler(7f, 18f, 5f));
        rock.Box(new Vector3(-1.15f, 1.2f, -0.25f), new Vector3(2.9f, 1.45f, 2.5f), Quaternion.Euler(-6f, -22f, 4f));
        rock.Box(new Vector3(0.15f, 1.05f, -1.25f), new Vector3(2.6f, 1.2f, 2.3f), Quaternion.Euler(5f, 28f, -6f));
        rock.Box(new Vector3(-0.4f, 0.85f, 1.15f), new Vector3(2.2f, 1.1f, 1.8f), Quaternion.Euler(-4f, 12f, 8f));
        Tint(ModelLibrary.Part(root.transform, "Base", rock.Build("CrystalBase"), kit.rock, true), PaletteRole.Rock);

        var shards = new FlatMesh();
        Shard(shards, new Vector3(0.1f, 1.15f, 0.05f), 13.5f, 0.72f, new Vector3(7f, 18f, 0f));
        Shard(shards, new Vector3(1.45f, 1.2f, 0.55f), 8.6f, 0.46f, new Vector3(14f, 30f, 16f));
        Shard(shards, new Vector3(-1.35f, 1.15f, 0.4f), 7.4f, 0.4f, new Vector3(-12f, 12f, -18f));
        Shard(shards, new Vector3(-0.45f, 1.25f, -0.85f), 10.4f, 0.52f, new Vector3(-8f, 48f, -10f));
        Shard(shards, new Vector3(0.85f, 1.1f, -1.15f), 5.6f, 0.3f, new Vector3(18f, -24f, 12f));
        Shard(shards, new Vector3(-0.15f, 1.3f, 1.05f), 8.8f, 0.36f, new Vector3(10f, 70f, -8f));
        GameObject cluster = ModelLibrary.Part(root.transform, "Cluster", shards.Build("CrystalShards"), kit.glow, true);
        Tint(cluster, PaletteRole.Glow);
        cluster.AddComponent<PulseGlow>();
        var spin = cluster.AddComponent<SlowRotate>();
        spin.degreesPerSecond = 12f;
        return root;
    }

    public static GameObject CreateTower(ModelKit kit)
    {
        var root = new GameObject("Tower");
        root.AddComponent<PaletteBinder>();

        var stone = new FlatMesh();
        stone.Box(new Vector3(0f, 0.7f, 0f), new Vector3(8.2f, 1.4f, 8.2f), Quaternion.identity);
        stone.Box(new Vector3(0f, 2f, 0f), new Vector3(6.3f, 1.2f, 6.3f), Quaternion.Euler(0f, 6f, 0f));
        stone.Box(new Vector3(0f, 3.4f, 0f), new Vector3(4.7f, 1.6f, 4.7f), Quaternion.identity);
        stone.Box(new Vector3(-2.5f, 1.5f, -2.5f), new Vector3(1.1f, 3f, 1.1f), Quaternion.identity);
        stone.Box(new Vector3(2.5f, 1.5f, -2.5f), new Vector3(1.1f, 3f, 1.1f), Quaternion.identity);
        stone.Box(new Vector3(-2.5f, 1.5f, 2.5f), new Vector3(1.1f, 3f, 1.1f), Quaternion.identity);
        stone.Box(new Vector3(2.5f, 1.5f, 2.5f), new Vector3(1.1f, 3f, 1.1f), Quaternion.identity);
        Tint(ModelLibrary.Part(root.transform, "Stone", stone.Build("TowerStone"), kit.rock, true), PaletteRole.Rock);

        var wood = new FlatMesh();
        wood.Box(new Vector3(0f, 8.6f, 0f), new Vector3(2.9f, 9.2f, 2.9f), Quaternion.identity);
        wood.Box(new Vector3(0f, 6.4f, 0f), new Vector3(3.35f, 0.38f, 3.35f), Quaternion.identity);
        wood.Box(new Vector3(0f, 10.6f, 0f), new Vector3(3.35f, 0.38f, 3.35f), Quaternion.identity);
        wood.Box(new Vector3(0f, 13.25f, 0f), new Vector3(5.6f, 0.48f, 5.6f), Quaternion.identity);
        AddRail(wood);
        wood.Box(new Vector3(0f, 14.7f, 0f), new Vector3(0.28f, 2.5f, 0.28f), Quaternion.identity);
        Tint(ModelLibrary.Part(root.transform, "Wood", wood.Build("TowerWood"), kit.wood, true), PaletteRole.Wood);

        var window = new FlatMesh();
        window.Box(new Vector3(0f, 8.2f, 1.52f), new Vector3(0.55f, 0.8f, 0.12f), Quaternion.identity);
        Tint(ModelLibrary.Part(root.transform, "Window", window.Build("TowerWindow"), kit.glow, false), PaletteRole.Glow);

        var pivot = new GameObject("Beacon");
        pivot.transform.SetParent(root.transform, false);
        pivot.transform.localPosition = new Vector3(0f, 15.15f, 0f);
        var spin = pivot.AddComponent<SlowRotate>();
        spin.degreesPerSecond = 20f;

        var lamp = new FlatMesh();
        lamp.Crystal(new Vector3(0f, -0.2f, 0f), 2.5f, 0.62f, Quaternion.identity, 6);
        lamp.Box(new Vector3(1.15f, 0.85f, 0f), new Vector3(1.5f, 0.12f, 0.12f), Quaternion.identity);
        lamp.Box(new Vector3(-1.15f, 0.85f, 0f), new Vector3(1.5f, 0.12f, 0.12f), Quaternion.identity);
        lamp.Box(new Vector3(0f, 0.85f, 1.15f), new Vector3(0.12f, 0.12f, 1.5f), Quaternion.identity);
        lamp.Box(new Vector3(0f, 0.85f, -1.15f), new Vector3(0.12f, 0.12f, 1.5f), Quaternion.identity);
        GameObject lampObject = ModelLibrary.Part(pivot.transform, "Lamp", lamp.Build("Beacon"), kit.glow, false);
        Tint(lampObject, PaletteRole.Glow);
        return root;
    }

    public static GameObject CreateRuins(ModelKit kit)
    {
        var root = new GameObject("Ruins");
        root.AddComponent<PaletteBinder>();

        var stone = new FlatMesh();
        var moss = new FlatMesh();
        AddPillars(stone, moss);
        AddFallenArch(stone, moss);
        AddRubble(stone, moss);
        Tint(ModelLibrary.Part(root.transform, "Stone", stone.Build("RuinStone"), kit.rock, true), PaletteRole.Rock);
        Tint(ModelLibrary.Part(root.transform, "Moss", moss.Build("RuinMoss"), kit.moss, true), PaletteRole.Moss);
        return root;
    }

    static void Shard(FlatMesh mesh, Vector3 position, float height, float radius, Vector3 euler)
    {
        mesh.Crystal(position, height, radius, Quaternion.Euler(euler), 6);
    }

    static void AddRail(FlatMesh wood)
    {
        const float reach = 2.35f;
        const float post = 1.15f;
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI * 0.25f;
            float x = Mathf.Cos(angle) * reach;
            float z = Mathf.Sin(angle) * reach;
            wood.Box(new Vector3(x, 13.95f, z), new Vector3(0.22f, post, 0.22f), Quaternion.identity);
        }

        wood.Box(new Vector3(0f, 14.45f, reach), new Vector3(reach * 2f, 0.14f, 0.14f), Quaternion.identity);
        wood.Box(new Vector3(0f, 14.45f, -reach), new Vector3(reach * 2f, 0.14f, 0.14f), Quaternion.identity);
        wood.Box(new Vector3(reach, 14.45f, 0f), new Vector3(0.14f, 0.14f, reach * 2f), Quaternion.identity);
        wood.Box(new Vector3(-reach, 14.45f, 0f), new Vector3(0.14f, 0.14f, reach * 2f), Quaternion.identity);
    }

    static void AddPillars(FlatMesh stone, FlatMesh moss)
    {
        float[] angles = { 8f, 58f, 112f, 164f, 214f, 268f, 322f };
        float[] heights = { 7.8f, 5.2f, 8.6f, 3.3f, 6.5f, 4.6f, 2.5f };
        float[] leans = { 0.18f, -0.42f, 0.22f, 0.7f, -0.3f, 0.48f, -0.85f };
        const float ring = 7.3f;
        for (int i = 0; i < angles.Length; i++)
        {
            float rad = angles[i] * Mathf.Deg2Rad;
            float x = Mathf.Cos(rad) * ring;
            float z = Mathf.Sin(rad) * ring;
            float radius = i % 2 == 0 ? 0.62f : 0.5f;
            Vector3 bottom = new Vector3(x, 0f, z);
            Vector3 top = bottom + new Vector3(Mathf.Cos(rad + 1.2f) * leans[i], heights[i], Mathf.Sin(rad + 1.2f) * leans[i]);
            stone.Frustum(bottom, top, radius, radius * 0.72f, 6);
            if (heights[i] < 4f)
            {
                stone.Box(bottom + new Vector3(0.4f, 0.35f, 0.2f), new Vector3(1.1f, 0.7f, 0.9f), Quaternion.Euler(0f, angles[i], 12f));
                continue;
            }

            Vector3 cap = top + Vector3.up * 0.02f;
            moss.Frustum(cap, cap + Vector3.up * 0.22f, radius * 1.28f, radius * 1.05f, 6);
        }
    }

    static void AddFallenArch(FlatMesh stone, FlatMesh moss)
    {
        const int stones = 5;
        const float radius = 3.15f;
        for (int i = 0; i < stones; i++)
        {
            float u = (i - (stones - 1) * 0.5f) / ((stones - 1) * 0.5f);
            float arc = u * 0.95f;
            var position = new Vector3(Mathf.Sin(arc) * radius, 0.62f, 2.15f + Mathf.Cos(arc) * 1.15f);
            Quaternion rotation = Quaternion.Euler(82f, -arc * Mathf.Rad2Deg, 0f);
            stone.Box(position, new Vector3(1.45f, 0.95f, 0.72f), rotation);
            if (i == stones / 2)
            {
                Vector3 cap = position + Vector3.up * 0.55f;
                moss.Frustum(cap, cap + Vector3.up * 0.16f, 0.62f, 0.48f, 5);
            }
        }

        stone.Box(new Vector3(-3.15f, 0.85f, 1.7f), new Vector3(0.7f, 1.7f, 0.7f), Quaternion.Euler(16f, 8f, 68f));
        stone.Box(new Vector3(3.15f, 0.7f, 1.7f), new Vector3(0.66f, 1.45f, 0.66f), Quaternion.Euler(-12f, -6f, -62f));
    }

    static void AddRubble(FlatMesh stone, FlatMesh moss)
    {
        stone.Box(new Vector3(3.5f, 0.38f, -2.4f), new Vector3(1.6f, 0.75f, 1.15f), Quaternion.Euler(0f, 24f, 10f));
        stone.Box(new Vector3(-2.8f, 0.55f, -3.5f), new Vector3(1.35f, 1.1f, 1.45f), Quaternion.Euler(8f, -36f, 0f));
        stone.Box(new Vector3(1.2f, 0.3f, 5.6f), new Vector3(1.9f, 0.55f, 1.05f), Quaternion.Euler(0f, 12f, 18f));
        stone.Box(new Vector3(-4.6f, 0.42f, 1.1f), new Vector3(1.05f, 0.85f, 1.6f), Quaternion.Euler(-8f, 18f, 4f));
        stone.Box(new Vector3(5.1f, 0.32f, 2.8f), new Vector3(1.4f, 0.6f, 0.95f), Quaternion.Euler(0f, 48f, -8f));
        stone.Box(new Vector3(-0.6f, 0.28f, -5.4f), new Vector3(1.7f, 0.5f, 1.2f), Quaternion.Euler(4f, -12f, 0f));

        moss.Frustum(new Vector3(-2.8f, 1.05f, -3.5f), new Vector3(-2.8f, 1.28f, -3.5f), 0.85f, 0.7f, 5);
        moss.Frustum(new Vector3(3.5f, 0.72f, -2.4f), new Vector3(3.5f, 0.92f, -2.4f), 0.7f, 0.55f, 5);
    }

    static void Tint(GameObject go, PaletteRole role)
    {
        var part = go.AddComponent<PalettePart>();
        part.role = role;
    }
}
