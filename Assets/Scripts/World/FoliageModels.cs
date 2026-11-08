using UnityEngine;

public static class FoliageModels
{
    public static GameObject Create(string id, Material wood, Material leaf, Material frost, Material snow, Material rock)
    {
        switch (id)
        {
            case "broadleafA": return BroadleafA(wood, leaf);
            case "broadleafB": return BroadleafB(wood, leaf);
            case "pineA": return PineA(wood, leaf);
            case "pineB": return PineB(wood, leaf);
            case "snowPineA": return SnowPineA(wood, frost, snow);
            case "snowPineB": return SnowPineB(wood, frost, snow);
            case "deadA": return DeadA(wood);
            case "deadB": return DeadB(wood);
            case "rockA": return RockA(rock);
            case "rockB": return RockB(rock);
            case "rockC": return RockC(rock);
            case "rockD": return RockD(rock);
            default: return BroadleafA(wood, leaf);
        }
    }

    public static GameObject BroadleafA(Material wood, Material leaf)
    {
        var trunk = new FlatMesh();
        Trunk(trunk, 2.15f, 0.4f, 0.16f, 0f);
        var crown = new FlatMesh();
        crown.Ball(new Vector3(0f, 3.55f, 0f), 1.65f, 1.38f, 1.55f, 6, 5);
        crown.Ball(new Vector3(1.15f, 3.15f, 0.2f), 1.05f, 0.92f, 1f, 6, 4);
        crown.Ball(new Vector3(-1.08f, 3.22f, -0.28f), 1.12f, 0.96f, 1.02f, 6, 4);
        crown.Ball(new Vector3(0.12f, 3.28f, -1.12f), 0.98f, 0.86f, 0.96f, 5, 4);
        crown.Ball(new Vector3(-0.18f, 3.32f, 1.08f), 0.92f, 0.82f, 0.9f, 5, 4);
        crown.Ball(new Vector3(0.22f, 4.55f, 0.04f), 0.96f, 0.78f, 0.88f, 6, 4);
        return Assemble("TreeBroadleafA", trunk, wood, crown, PropTint.Leaf, leaf, null, snowMaterial: null);
    }

    public static GameObject BroadleafB(Material wood, Material leaf)
    {
        var trunk = new FlatMesh();
        Trunk(trunk, 1.65f, 0.48f, 0.2f, 0.08f);
        var crown = new FlatMesh();
        crown.Ball(new Vector3(0f, 2.65f, 0f), 1.5f, 1.05f, 1.35f, 6, 4);
        crown.Ball(new Vector3(-1.45f, 2.42f, 0.18f), 1.22f, 0.95f, 1.1f, 6, 4);
        crown.Ball(new Vector3(1.38f, 2.5f, -0.22f), 1.16f, 0.9f, 1.08f, 6, 4);
        crown.Ball(new Vector3(0.18f, 2.48f, 1.32f), 1.05f, 0.82f, 1f, 5, 4);
        crown.Ball(new Vector3(-0.28f, 2.55f, -1.28f), 1.12f, 0.86f, 1.02f, 5, 4);
        crown.Ball(new Vector3(0.08f, 3.45f, 0.05f), 0.88f, 0.68f, 0.82f, 6, 4);
        return Assemble("TreeBroadleafB", trunk, wood, crown, PropTint.Leaf, leaf, null, snowMaterial: null);
    }

    public static GameObject PineA(Material wood, Material leaf)
    {
        var trunk = new FlatMesh();
        Trunk(trunk, 4.7f, 0.3f, 0.08f, 0f);
        var crown = new FlatMesh();
        crown.Cone(new Vector3(0f, 1.65f, 0f), new Vector3(0f, 3.65f, 0f), 1.85f, 6);
        crown.Cone(new Vector3(0f, 2.85f, 0f), new Vector3(0f, 5.1f, 0f), 1.38f, 6);
        crown.Cone(new Vector3(0f, 4.1f, 0f), new Vector3(0f, 6.35f, 0f), 0.92f, 6);
        crown.Cone(new Vector3(0f, 5.45f, 0f), new Vector3(0f, 7.25f, 0f), 0.42f, 5);
        return Assemble("TreePineA", trunk, wood, crown, PropTint.Leaf, leaf, null, snowMaterial: null);
    }

    public static GameObject PineB(Material wood, Material leaf)
    {
        var trunk = new FlatMesh();
        Trunk(trunk, 5.4f, 0.34f, 0.09f, 0.32f);
        var crown = new FlatMesh();
        crown.Cone(new Vector3(0.04f, 1.25f, 0f), new Vector3(0.1f, 3.45f, 0.02f), 2.15f, 6);
        crown.Cone(new Vector3(0.14f, 2.75f, 0.04f), new Vector3(0.26f, 5.15f, 0.06f), 1.52f, 6);
        crown.Cone(new Vector3(0.24f, 4.35f, 0.06f), new Vector3(0.42f, 6.75f, 0.1f), 0.92f, 6);
        return Assemble("TreePineB", trunk, wood, crown, PropTint.Leaf, leaf, null, snowMaterial: null);
    }

    public static GameObject SnowPineA(Material wood, Material frost, Material snow)
    {
        var trunk = new FlatMesh();
        Trunk(trunk, 3.7f, 0.36f, 0.12f, 0f);
        var crown = new FlatMesh();
        crown.Cone(new Vector3(0f, 1.25f, 0f), new Vector3(0f, 3.15f, 0f), 1.95f, 6);
        crown.Cone(new Vector3(0f, 2.45f, 0f), new Vector3(0f, 4.45f, 0f), 1.4f, 6);
        crown.Cone(new Vector3(0f, 3.65f, 0f), new Vector3(0f, 5.55f, 0f), 0.82f, 5);
        var caps = new FlatMesh();
        Clumps(caps, 2.15f, 1.05f, 0.48f, 4, 0.4f);
        Clumps(caps, 3.35f, 0.72f, 0.4f, 3, 1.2f);
        Clumps(caps, 4.45f, 0.38f, 0.32f, 3, 0.2f);
        caps.Ball(new Vector3(0f, 5.15f, 0f), 0.42f, 0.22f, 0.38f, 5, 3);
        return Assemble("TreeSnowPineA", trunk, wood, crown, PropTint.Frost, frost, caps, snow);
    }

    public static GameObject SnowPineB(Material wood, Material frost, Material snow)
    {
        var trunk = new FlatMesh();
        Trunk(trunk, 2.8f, 0.4f, 0.14f, 0.18f);
        var crown = new FlatMesh();
        crown.Cone(new Vector3(0.05f, 0.9f, 0f), new Vector3(0.08f, 2.85f, 0.04f), 2.25f, 6);
        crown.Cone(new Vector3(0.12f, 2.15f, 0.04f), new Vector3(0.22f, 4.35f, 0.08f), 1.35f, 6);
        var caps = new FlatMesh();
        Clumps(caps, 1.7f, 1.25f, 0.55f, 5, 0.7f);
        Clumps(caps, 2.95f, 0.7f, 0.42f, 4, 0.15f);
        caps.Ball(new Vector3(0.16f, 3.85f, 0.05f), 0.62f, 0.28f, 0.55f, 5, 3);
        return Assemble("TreeSnowPineB", trunk, wood, crown, PropTint.Frost, frost, caps, snow);
    }

    public static GameObject DeadA(Material wood)
    {
        var trunk = new FlatMesh();
        Trunk(trunk, 4.5f, 0.36f, 0.07f, 0.16f);
        trunk.Frustum(new Vector3(0.06f, 1.7f, 0f), new Vector3(1.75f, 2.85f, 0.25f), 0.13f, 0.04f, 5);
        trunk.Frustum(new Vector3(0.02f, 2.55f, 0.02f), new Vector3(-1.45f, 3.45f, -0.35f), 0.11f, 0.035f, 5);
        trunk.Frustum(new Vector3(0.1f, 3.45f, 0f), new Vector3(0.85f, 4.35f, -0.85f), 0.08f, 0.025f, 4);
        trunk.Frustum(new Vector3(0.12f, 4.25f, 0.02f), new Vector3(-0.35f, 5.25f, 0.4f), 0.06f, 0.02f, 4);
        trunk.Box(new Vector3(1.62f, 2.78f, 0.22f), new Vector3(0.34f, 0.1f, 0.1f), Quaternion.Euler(8f, 18f, 62f));
        return Assemble("TreeDeadA", trunk, wood, null, PropTint.Leaf, null, null, snowMaterial: null);
    }

    public static GameObject DeadB(Material wood)
    {
        var trunk = new FlatMesh();
        trunk.Frustum(Vector3.zero, new Vector3(0.05f, 2.1f, 0f), 0.42f, 0.2f, 6);
        trunk.Frustum(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.4f, 0f), 0.62f, 0.38f, 6);
        trunk.Frustum(new Vector3(0.04f, 1.9f, 0f), new Vector3(1.15f, 4.4f, 0.2f), 0.16f, 0.04f, 5);
        trunk.Frustum(new Vector3(0.02f, 1.85f, 0f), new Vector3(-0.95f, 3.7f, -0.45f), 0.14f, 0.035f, 5);
        trunk.Frustum(new Vector3(0.7f, 3.2f, 0.12f), new Vector3(1.7f, 3.55f, 0.85f), 0.07f, 0.025f, 4);
        trunk.Frustum(new Vector3(-0.15f, 0.7f, 0.05f), new Vector3(-1.55f, 0.85f, 0.7f), 0.1f, 0.03f, 4);
        trunk.Box(new Vector3(1.05f, 4.25f, 0.16f), new Vector3(0.22f, 0.16f, 0.16f), Quaternion.Euler(18f, -12f, 24f));
        return Assemble("TreeDeadB", trunk, wood, null, PropTint.Leaf, null, null, snowMaterial: null);
    }

    public static GameObject RockA(Material rock)
    {
        var mesh = new FlatMesh();
        mesh.Box(new Vector3(0f, 0.48f, 0f), new Vector3(2.7f, 0.95f, 1.85f), Quaternion.Euler(5f, 8f, 7f));
        mesh.Box(new Vector3(0.72f, 0.72f, 0.32f), new Vector3(1.55f, 1.15f, 1.25f), Quaternion.Euler(-9f, 22f, 14f));
        mesh.Box(new Vector3(-0.82f, 0.42f, -0.22f), new Vector3(1.35f, 0.72f, 1.4f), Quaternion.Euler(11f, -16f, -7f));
        mesh.Box(new Vector3(0.15f, 0.38f, -0.85f), new Vector3(1.15f, 0.55f, 0.85f), Quaternion.Euler(8f, 28f, -4f));
        return Assemble("RockA", mesh, rock, null, PropTint.Rock, null, null, snowMaterial: null);
    }

    public static GameObject RockB(Material rock)
    {
        var mesh = new FlatMesh();
        mesh.Box(new Vector3(0f, 1.15f, 0f), new Vector3(1.25f, 2.25f, 1.15f), Quaternion.Euler(2f, 12f, 9f));
        mesh.Box(new Vector3(0.38f, 1.65f, 0.18f), new Vector3(0.85f, 1.65f, 0.95f), Quaternion.Euler(7f, -22f, -12f));
        mesh.Box(new Vector3(-0.32f, 0.62f, 0.38f), new Vector3(1.45f, 0.85f, 1.2f), Quaternion.Euler(-5f, 18f, 6f));
        mesh.Box(new Vector3(0.12f, 2.35f, -0.08f), new Vector3(0.58f, 0.72f, 0.52f), Quaternion.Euler(14f, 8f, 20f));
        return Assemble("RockB", mesh, rock, null, PropTint.Rock, null, null, snowMaterial: null);
    }

    public static GameObject RockC(Material rock)
    {
        var mesh = new FlatMesh();
        mesh.Box(new Vector3(-0.72f, 0.75f, 0.05f), new Vector3(1.35f, 1.45f, 1.25f), Quaternion.Euler(2f, 16f, 15f));
        mesh.Box(new Vector3(0.78f, 0.58f, 0.18f), new Vector3(1.45f, 1.05f, 1.35f), Quaternion.Euler(-7f, -12f, -13f));
        mesh.Box(new Vector3(0.08f, 0.36f, -0.72f), new Vector3(1.15f, 0.58f, 0.95f), Quaternion.Euler(9f, 32f, 5f));
        mesh.Box(new Vector3(-0.15f, 1.35f, 0.22f), new Vector3(0.7f, 0.55f, 0.62f), Quaternion.Euler(-12f, 8f, 18f));
        return Assemble("RockC", mesh, rock, null, PropTint.Rock, null, null, snowMaterial: null);
    }

    public static GameObject RockD(Material rock)
    {
        var mesh = new FlatMesh();
        mesh.Box(new Vector3(0f, 0.5f, 0f), new Vector3(2.25f, 0.75f, 1.65f), Quaternion.Euler(3f, -14f, 5f));
        mesh.Box(new Vector3(0.22f, 1.08f, 0.12f), new Vector3(1.25f, 0.95f, 1.05f), Quaternion.Euler(18f, 10f, 24f));
        mesh.Box(new Vector3(-0.88f, 0.88f, 0.42f), new Vector3(0.95f, 1.25f, 0.85f), Quaternion.Euler(-12f, 26f, -18f));
        mesh.Box(new Vector3(0.95f, 0.42f, -0.55f), new Vector3(0.85f, 0.55f, 0.95f), Quaternion.Euler(10f, -22f, 12f));
        mesh.Box(new Vector3(-0.22f, 0.32f, 0.95f), new Vector3(0.65f, 0.42f, 0.58f), Quaternion.Euler(22f, 12f, -8f));
        return Assemble("RockD", mesh, rock, null, PropTint.Rock, null, null, snowMaterial: null);
    }

    static void Trunk(FlatMesh mesh, float height, float rootRadius, float topRadius, float lean)
    {
        mesh.Frustum(Vector3.zero, new Vector3(0f, 0.42f, 0f), rootRadius * 1.45f, rootRadius * 0.92f, 6);
        mesh.Frustum(new Vector3(0f, 0.28f, 0f), new Vector3(lean, height, lean * 0.25f), rootRadius, topRadius, 6);
    }

    static void Clumps(FlatMesh mesh, float y, float radius, float size, int count, float phase)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = phase + i * Mathf.PI * 2f / count;
            var center = new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
            mesh.Ball(center, size, size * 0.42f, size * 0.86f, 5, 3);
        }
    }

    static GameObject Assemble(string name, FlatMesh wood, Material woodMaterial, FlatMesh leaves, PropTint leafTint, Material leafMaterial, FlatMesh snow, Material snowMaterial)
    {
        var root = new GameObject(name);
        PropTint woodTint = leafTint == PropTint.Rock ? PropTint.Rock : PropTint.Wood;
        AddPart(root.transform, woodTint == PropTint.Rock ? "Stone" : "Wood", wood, woodMaterial, woodTint);
        AddPart(root.transform, "Leaves", leaves, leafMaterial, leafTint);
        AddPart(root.transform, "Snow", snow, snowMaterial, PropTint.Snow);
        return root;
    }

    static void AddPart(Transform parent, string partName, FlatMesh mesh, Material material, PropTint tint)
    {
        if (mesh == null || mesh.VertexCount < 3 || material == null)
            return;

        GameObject part = ModelLibrary.Part(parent, partName, mesh.Build(partName), material, true);
        var slot = part.AddComponent<PropPart>();
        slot.tint = tint;
    }
}
