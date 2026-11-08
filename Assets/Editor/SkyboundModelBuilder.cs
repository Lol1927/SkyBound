using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SkyboundModelBuilder
{
    [MenuItem("Skybound/Build Low Poly Models")]
    public static void BuildAll()
    {
        EnsureFolder("Assets/Materials");
        EnsureFolder("Assets/Prefabs");
        EnsureFolder("Assets/Models");

        ModelKit kit = SaveMaterials();
        SavePrefab(PlaneModel.Create(kit), ModelLibrary.PlanePrefab, "Assets/Models/Plane");
        SavePrefab(LandmarkModels.CreateCrystal(kit), ModelLibrary.CrystalPrefab, "Assets/Models/Crystal");
        SavePrefab(LandmarkModels.CreateTower(kit), ModelLibrary.TowerPrefab, "Assets/Models/Tower");
        SavePrefab(LandmarkModels.CreateRuins(kit), ModelLibrary.RuinsPrefab, "Assets/Models/Ruins");
        SaveVegetation();
        WirePlane();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Skybound low-poly models saved.");
    }

    static ModelKit SaveMaterials()
    {
        Color glow = PaletteBinder.GlowAlbedo(WorldLook.DefaultAccent, false, out Color emission);
        return new ModelKit
        {
            body = SaveLit(ModelLibrary.BodyPath, ModelLibrary.BodyColor, 0.04f, Color.black, false),
            stripe = SaveLit(ModelLibrary.StripePath, ModelLibrary.StripeColor, 0.04f, Color.black, false),
            accent = SaveLit(ModelLibrary.AccentPath, ModelLibrary.AccentColor, 0.06f, Color.black, false),
            glass = SaveLit(ModelLibrary.GlassPath, ModelLibrary.GlassColor, 0.62f, Color.black, true),
            dark = SaveLit(ModelLibrary.DarkPath, ModelLibrary.DarkColor, 0.08f, Color.black, false),
            gold = SaveLit(ModelLibrary.GoldPath, ModelLibrary.GoldColor, 0.38f, Color.black, true),
            rock = SaveLit(ModelLibrary.RockPath, PaletteBinder.RockColor(WorldLook.DefaultRock), 0.08f, Color.black, false),
            wood = SaveLit(ModelLibrary.WoodPath, PaletteBinder.WoodColor(WorldLook.DefaultRock), 0.06f, Color.black, false),
            moss = SaveLit(ModelLibrary.MossPath, PaletteBinder.MossColor(WorldLook.DefaultGrass), 0.12f, Color.black, false),
            glow = SaveLit(ModelLibrary.GlowPath, glow, 0.42f, emission, true)
        };
    }

    static void SaveVegetation()
    {
        if (Shader.Find("Skybound/InstancedFlat") == null)
            Debug.LogError("Skybound/InstancedFlat shader is missing. Foliage fell back to the lit shader.");

        Material wood = SaveInstanced("Assets/Materials/FoliageWood.mat", PaletteBinder.WoodColor(WorldLook.DefaultRock), 0.08f);
        Material leaf = SaveInstanced("Assets/Materials/FoliageLeaf.mat", WorldLook.DefaultGrass, 0.12f);
        Material frost = SaveInstanced("Assets/Materials/FoliageFrost.mat", Color.Lerp(WorldLook.DefaultGrass, WorldLook.DefaultSnow, 0.48f), 0.14f);
        Material snow = SaveInstanced("Assets/Materials/FoliageSnow.mat", WorldLook.DefaultSnow, 0.16f);
        Material rock = SaveInstanced("Assets/Materials/FoliageRock.mat", WorldLook.DefaultRock, 0.12f);

        SavePrefab(FoliageModels.BroadleafA(wood, leaf), "Assets/Prefabs/TreeBroadleafA.prefab", "Assets/Models/Foliage/BroadleafA");
        SavePrefab(FoliageModels.BroadleafB(wood, leaf), "Assets/Prefabs/TreeBroadleafB.prefab", "Assets/Models/Foliage/BroadleafB");
        SavePrefab(FoliageModels.PineA(wood, leaf), "Assets/Prefabs/TreePineA.prefab", "Assets/Models/Foliage/PineA");
        SavePrefab(FoliageModels.PineB(wood, leaf), "Assets/Prefabs/TreePineB.prefab", "Assets/Models/Foliage/PineB");
        SavePrefab(FoliageModels.SnowPineA(wood, frost, snow), "Assets/Prefabs/TreeSnowPineA.prefab", "Assets/Models/Foliage/SnowPineA");
        SavePrefab(FoliageModels.SnowPineB(wood, frost, snow), "Assets/Prefabs/TreeSnowPineB.prefab", "Assets/Models/Foliage/SnowPineB");
        SavePrefab(FoliageModels.DeadA(wood), "Assets/Prefabs/TreeDeadA.prefab", "Assets/Models/Foliage/DeadA");
        SavePrefab(FoliageModels.DeadB(wood), "Assets/Prefabs/TreeDeadB.prefab", "Assets/Models/Foliage/DeadB");
        SavePrefab(FoliageModels.RockA(rock), "Assets/Prefabs/RockA.prefab", "Assets/Models/Foliage/RockA");
        SavePrefab(FoliageModels.RockB(rock), "Assets/Prefabs/RockB.prefab", "Assets/Models/Foliage/RockB");
        SavePrefab(FoliageModels.RockC(rock), "Assets/Prefabs/RockC.prefab", "Assets/Models/Foliage/RockC");
        SavePrefab(FoliageModels.RockD(rock), "Assets/Prefabs/RockD.prefab", "Assets/Models/Foliage/RockD");

        const string catalogPath = "Assets/Resources/VegetationCatalog.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<VegetationCatalog>(catalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<VegetationCatalog>();
            AssetDatabase.CreateAsset(catalog, catalogPath);
        }

        catalog.broadleafA = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TreeBroadleafA.prefab");
        catalog.broadleafB = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TreeBroadleafB.prefab");
        catalog.pineA = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TreePineA.prefab");
        catalog.pineB = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TreePineB.prefab");
        catalog.snowPineA = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TreeSnowPineA.prefab");
        catalog.snowPineB = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TreeSnowPineB.prefab");
        catalog.deadA = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TreeDeadA.prefab");
        catalog.deadB = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TreeDeadB.prefab");
        catalog.rockA = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/RockA.prefab");
        catalog.rockB = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/RockB.prefab");
        catalog.rockC = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/RockC.prefab");
        catalog.rockD = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/RockD.prefab");
        EditorUtility.SetDirty(catalog);
    }

    static Material SaveInstanced(string path, Color color, float smoothness)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Skybound/InstancedFlat");
        if (material == null)
        {
            material = ModelLibrary.CreateInstanced(color, smoothness);
            material.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            if (shader != null)
                material.shader = shader;
            ModelLibrary.Paint(material, color, smoothness, Color.black, false);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
        }

        return material;
    }

    static Material SaveLit(string path, Color color, float smoothness, Color emission, bool specular)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = ModelLibrary.CreateLit(color, smoothness, emission, specular);
            material.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            ModelLibrary.Paint(material, color, smoothness, emission, specular);
            EditorUtility.SetDirty(material);
        }

        return material;
    }

    static void SavePrefab(GameObject root, string prefabPath, string meshFolder)
    {
        try
        {
            EnsureFolder(meshFolder);
            StoreMeshes(root, meshFolder);
            CheckClosed(root);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            if (root != null)
                Object.DestroyImmediate(root);
        }

        AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport);
        GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (saved == null)
        {
            Debug.LogError("Failed to save " + prefabPath);
            return;
        }

        MeshFilter[] filters = saved.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            if (filters[i].sharedMesh == null)
                Debug.LogError(prefabPath + " is missing a mesh on " + filters[i].name);
        }
    }

    static void StoreMeshes(GameObject root, string meshFolder)
    {
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        var used = new HashSet<string>();
        for (int i = 0; i < filters.Length; i++)
        {
            Mesh mesh = filters[i].sharedMesh;
            if (mesh == null || AssetDatabase.Contains(mesh))
                continue;

            string name = filters[i].transform.name;
            Transform walk = filters[i].transform.parent;
            while (walk != null && walk != root.transform)
            {
                name = walk.name + "_" + name;
                walk = walk.parent;
            }

            string path = meshFolder + "/" + name + ".asset";
            int suffix = 2;
            while (!used.Add(path))
            {
                path = meshFolder + "/" + name + "_" + suffix + ".asset";
                suffix++;
            }

            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null)
                AssetDatabase.DeleteAsset(path);

            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            filters[i].sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }
    }

    static void CheckClosed(GameObject root)
    {
        var box = new FlatMesh();
        box.Box(Vector3.zero, Vector3.one, Quaternion.identity);
        Mesh boxMesh = box.Build("BoxTest");
        ReportInward(boxMesh, Vector3.zero, "Unit box");
        Object.DestroyImmediate(boxMesh);

        MeshFilter fuselage = FindNamed(root, "Fuselage");
        if (fuselage != null && fuselage.sharedMesh != null)
            ReportInward(fuselage.sharedMesh, new Vector3(0f, 0.05f, 0.05f), root.name + " fuselage");
    }

    static MeshFilter FindNamed(GameObject root, string name)
    {
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            if (filters[i].name == name)
                return filters[i];
        }

        return null;
    }

    static void ReportInward(Mesh mesh, Vector3 center, string label)
    {
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        int[] triangles = mesh.triangles;
        int inward = 0;
        int count = 0;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 face = (vertices[triangles[i]] + vertices[triangles[i + 1]] + vertices[triangles[i + 2]]) / 3f;
            if (Vector3.Dot(normals[triangles[i]], face - center) < -0.001f)
                inward++;
            count++;
        }

        if (count > 0 && inward * 4 > count)
            Debug.LogError(label + " has " + inward + " inward faces out of " + count + ".");
        else
            Debug.Log(label + " faces outward (" + inward + "/" + count + " inward).");
    }

    static void WirePlane()
    {
        PlaneController controller = Object.FindFirstObjectByType<PlaneController>();
        if (controller == null)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            controller = Object.FindFirstObjectByType<PlaneController>();
        }

        if (controller == null)
        {
            Debug.LogError("PlaneController was not found, so the placeholder visual was not replaced.");
            return;
        }

        GameObject plane = controller.gameObject;
        for (int i = plane.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = plane.transform.GetChild(i);
            if (child.name == "PlaneVisual")
                Object.DestroyImmediate(child.gameObject);
        }

        MeshFilter filter = plane.GetComponent<MeshFilter>();
        if (filter != null)
            Object.DestroyImmediate(filter);
        MeshRenderer renderer = plane.GetComponent<MeshRenderer>();
        if (renderer != null)
            Object.DestroyImmediate(renderer);

        plane.transform.localScale = Vector3.one;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelLibrary.PlanePrefab);
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        visual.transform.SetParent(plane.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        BoxCollider box = plane.GetComponent<BoxCollider>();
        if (box == null)
            box = plane.AddComponent<BoxCollider>();
        box.isTrigger = false;
        box.center = new Vector3(0f, 0.02f, 0.05f);
        box.size = new Vector3(1.2f, 1.05f, 3.35f);

        EditorUtility.SetDirty(plane);
        Scene scene = plane.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(parent))
            return;
        parent = parent.Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
