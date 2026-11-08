using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public enum CloudQuality
{
    Low,
    Medium,
    High
}

public class CloudGenerator : MonoBehaviour
{
    const int PrototypeCount = 6;
    const int BatchLimit = 1023;
    const int DensityScale = 4;
    const float WrapRadius = 740f;
    const float WindSpeed = 4.5f;
    const float SeaMinY = 22f;
    const float SeaMaxY = 48f;
    const float KeepClear = 60f;
    const float PlaceCell = 40f;
    const float PlaceOrigin = -820f;
    const int PlaceDim = 42;

    struct Cloud
    {
        public Vector3 position;
        public float yaw;
        public float scale;
        public float radius;
        public int prototype;
    }

    readonly List<Vector3> clusterCenters = new List<Vector3>();
    readonly List<float> clusterRadii = new List<float>();

    Mesh[] prototypes;
    Mesh[] lodPrototypes;
    Matrix4x4[][] nearBatches;
    Matrix4x4[][] farBatches;
    int[] nearCounts;
    int[] farCounts;
    Cloud[] clouds;
    Material cloudMaterial;
    Vector3 wind;
    Vector3[] reserved;
    int[] placeHead;
    int[] placeNext;
    const int GridDim = 24;
    const float GridOrigin = -960f;
    const float GridCell = 80f;

    public CloudQuality cloudQuality = CloudQuality.High;

    static int fogGeneration;

    Image veilImage;
    float baseFog = 0.01f;
    Color baseFogColor = Color.white;
    float shownFog = 0.01f;
    float veil;
    PlaneController plane;
    bool captured;
    bool holdingBase;
    int fogToken;
    int[] gridHead;
    int[] gridNext;
    Text debugText;
    bool debugVisible = true;
    float shownFps = 60f;
    float rateSum;
    int rateSamples;
    float rateWindow;
    float slowSeconds;
    int drawnCount;

    public static CloudGenerator Active { get; private set; }
    public int CloudCount => clouds != null ? clouds.Length : 0;

    public static void SetHoldBase(bool hold)
    {
        if (Active != null)
            Active.HoldBase(hold);
    }

    public static void RestoreActive()
    {
        if (Active != null)
            Active.RestoreBase();
    }

    public void HoldBase(bool hold)
    {
        holdingBase = hold;
        RestoreBase();
    }

    public void RestoreBase()
    {
        if (!captured)
            return;

        shownFog = baseFog;
        veil = 0f;
        ApplyShown();
    }

    public void Build(WorldData data)
    {
        ClearClouds();
        if (data == null)
            return;

        Physics.SyncTransforms();
        EnsureMaterial();
        ApplyTint();
        EnsurePrototypes(data.seed);
        CollectClusters();

        float density = Mathf.Clamp01(data.cloudDensity);
        int total = Mathf.RoundToInt(Mathf.Lerp(150f, 300f, density));
        int huge = Mathf.Clamp(Mathf.RoundToInt(total * 0.06f), 4, 14) * DensityScale;
        int medium = Mathf.RoundToInt(total * 0.36f) * DensityScale;
        int sea = (total - Mathf.Clamp(Mathf.RoundToInt(total * 0.06f), 4, 14) - Mathf.RoundToInt(total * 0.36f)) * DensityScale;
        var rng = new System.Random(unchecked(data.seed * 397) ^ 0x2C10D);
        float heading = (float)(rng.NextDouble() * System.Math.PI * 2.0);
        wind = new Vector3(Mathf.Cos(heading), 0f, Mathf.Sin(heading)) * WindSpeed;

        CollectReserved();
        BeginPlacement(sea + medium + huge + 32);
        var built = new List<Cloud>(sea + medium + huge);
        PlaceSea(rng, sea, built);
        PlaceMedium(rng, medium, built);
        PlaceHuge(rng, huge, built);
        clouds = built.ToArray();

        nearBatches = new Matrix4x4[PrototypeCount][];
        farBatches = new Matrix4x4[PrototypeCount][];
        nearCounts = new int[PrototypeCount];
        farCounts = new int[PrototypeCount];
        for (int i = 0; i < PrototypeCount; i++)
        {
            nearBatches[i] = new Matrix4x4[BatchLimit];
            farBatches[i] = new Matrix4x4[BatchLimit];
        }

        CaptureBase();
        EnsureVeil();
        EnsureDebug();
        EnsureGrid();
    }

    void LateUpdate()
    {
        if (Input.GetKeyDown(KeyCode.F3))
            debugVisible = !debugVisible;

        TrackFrameRate();
        if (clouds == null || clouds.Length == 0 || cloudMaterial == null)
        {
            RefreshDebug();
            return;
        }

        Drift();
        BlendFog();
        Draw();
        RefreshDebug();
    }

    void OnDestroy()
    {
        if (captured && fogToken == fogGeneration)
        {
            RenderSettings.fogDensity = baseFog;
            RenderSettings.fogColor = baseFogColor;
        }

        if (Active == this)
            Active = null;

        if (veilImage != null && veilImage.canvas != null)
            DestroyGenerated(veilImage.canvas.gameObject);
        if (debugText != null && debugText.canvas != null)
            DestroyGenerated(debugText.canvas.gameObject);
        DestroyGenerated(cloudMaterial);
        cloudMaterial = null;
        ReleasePrototypes();
    }

    void PlaceSea(System.Random rng, int count, List<Cloud> built)
    {
        int clumps = Mathf.RoundToInt(count * 0.4f);
        PlaceEven(rng, count - clumps, SeaMinY, SeaMaxY, 1.05f, 1.85f, 34f, SpotGenerator.WorldRadius * 0.94f, built);
        PlaceNearClusters(rng, clumps, SeaMinY, SeaMaxY, 1.05f, 1.85f, 22f, 12f, 110f, built);
    }

    void PlaceMedium(System.Random rng, int count, List<Cloud> built)
    {
        int clumps = Mathf.RoundToInt(count * 0.7f);
        PlaceNearClusters(rng, clumps, 52f, 230f, 1.35f, 2.5f, 26f, 24f, 170f, built);
        PlaceEven(rng, count - clumps, 64f, 220f, 1.35f, 2.4f, 38f, SpotGenerator.WorldRadius * 0.9f, built);
    }

    void PlaceHuge(System.Random rng, int count, List<Cloud> built)
    {
        if (count <= 0)
            return;

        int placed = 0;
        int attempts = count * 8;
        int step = 0;
        while (placed < count && attempts-- > 0)
        {
            float angle = (step + Next01(rng)) / count * Mathf.PI * 2f;
            step++;
            float dist = Lerp(rng, SpotGenerator.WorldRadius * 0.72f, SpotGenerator.WorldRadius + 24f);
            float scale = Lerp(rng, 4.2f, 6.4f);
            var position = new Vector3(Mathf.Cos(angle) * dist, Lerp(rng, 90f, 220f), Mathf.Sin(angle) * dist);
            if (!CanPlace(position, scale, 70f, SpotGenerator.WorldRadius + 40f, built))
            {
                position.y = Lerp(rng, 140f, 250f);
                if (!CanPlace(position, scale, 70f, SpotGenerator.WorldRadius + 40f, built))
                    continue;
            }

            Accept(rng, position, scale, built);
            placed++;
        }
    }

    void PlaceEven(System.Random rng, int count, float minY, float maxY, float minScale, float maxScale, float spacing, float disk, List<Cloud> built)
    {
        if (count <= 0)
            return;

        int columns = Mathf.Max(4, Mathf.CeilToInt(Mathf.Sqrt(count * 1.35f)));
        float step = disk * 2f / columns;
        int placed = 0;
        for (int z = 0; z < columns && placed < count; z++)
        {
            for (int x = 0; x < columns && placed < count; x++)
            {
                float px = -disk + (x + 0.5f) * step + (Next01(rng) - 0.5f) * step * 0.85f;
                float pz = -disk + (z + 0.5f) * step + (Next01(rng) - 0.5f) * step * 0.85f;
                if (px * px + pz * pz > disk * disk)
                    continue;

                float scale = Lerp(rng, minScale, maxScale);
                var position = new Vector3(px, Lerp(rng, minY, maxY), pz);
                if (!CanPlace(position, scale, spacing, disk + 8f, built))
                    continue;

                Accept(rng, position, scale, built);
                placed++;
            }
        }

        int attempts = (count - placed) * 24;
        while (placed < count && attempts-- > 0)
        {
            float dist = Mathf.Sqrt(Next01(rng)) * disk;
            float angle = Next01(rng) * Mathf.PI * 2f;
            float scale = Lerp(rng, minScale, maxScale);
            var position = new Vector3(Mathf.Cos(angle) * dist, Lerp(rng, minY, maxY), Mathf.Sin(angle) * dist);
            if (!CanPlace(position, scale, spacing, disk + 8f, built))
                continue;

            Accept(rng, position, scale, built);
            placed++;
        }
    }

    void PlaceNearClusters(System.Random rng, int count, float minY, float maxY, float minScale, float maxScale, float spacing, float reachMin, float reachMax, List<Cloud> built)
    {
        if (count <= 0)
            return;

        int placed = 0;
        int attempts = count * 20;
        while (placed < count && attempts-- > 0)
        {
            float scale = Lerp(rng, minScale, maxScale);
            Vector3 position;
            if (clusterCenters.Count > 0)
            {
                int index = rng.Next(clusterCenters.Count);
                Vector3 center = clusterCenters[index];
                float reach = clusterRadii[index] + Lerp(rng, reachMin, reachMax);
                float angle = Next01(rng) * Mathf.PI * 2f;
                position = new Vector3(center.x + Mathf.Cos(angle) * reach, Lerp(rng, minY, maxY), center.z + Mathf.Sin(angle) * reach);
            }
            else
            {
                float dist = Mathf.Sqrt(Next01(rng)) * SpotGenerator.WorldRadius * 0.9f;
                float angle = Next01(rng) * Mathf.PI * 2f;
                position = new Vector3(Mathf.Cos(angle) * dist, Lerp(rng, minY, maxY), Mathf.Sin(angle) * dist);
            }

            if (!CanPlace(position, scale, spacing, SpotGenerator.WorldRadius + 20f, built))
                continue;

            Accept(rng, position, scale, built);
            placed++;
        }
    }

    void Accept(System.Random rng, Vector3 position, float scale, List<Cloud> built)
    {
        int index = built.Count;
        built.Add(MakeCloud(rng, position, scale, index));
        IndexPlaced(index, position);
    }

    bool CanPlace(Vector3 position, float scale, float spacing, float limit, List<Cloud> built)
    {
        float flat = position.x * position.x + position.z * position.z;
        if (flat > limit * limit)
            return false;
        if (Reserved(position, scale * 11f))
            return false;
        if (Occupied(position, spacing, built))
            return false;
        return !HitsIsland(position, Mathf.Max(6f, scale * 7f));
    }

    Cloud MakeCloud(System.Random rng, Vector3 position, float scale, int index)
    {
        return new Cloud
        {
            position = position,
            yaw = Next01(rng) * 360f,
            scale = scale,
            radius = scale * 11f,
            prototype = index % PrototypeCount
        };
    }

    void Drift()
    {
        float step = Time.deltaTime;
        if (step <= 0f)
            return;

        for (int i = 0; i < clouds.Length; i++)
        {
            Cloud cloud = clouds[i];
            cloud.position += wind * step;
            Vector3 flat = new Vector3(cloud.position.x, 0f, cloud.position.z);
            if (flat.magnitude > WrapRadius)
            {
                Vector3 inbound = -flat.normalized * (WrapRadius - 40f);
                cloud.position.x = inbound.x;
                cloud.position.z = inbound.z;
            }

            clouds[i] = cloud;
        }
    }

    void Draw()
    {
        if (nearBatches == null || farBatches == null)
            return;

        for (int i = 0; i < PrototypeCount; i++)
        {
            nearCounts[i] = 0;
            farCounts[i] = 0;
        }

        Camera view = Camera.main;
        Vector3 eye = view != null ? view.transform.position : Vector3.zero;
        float cull = QualityCull(FogReach());
        float lod = QualityLod(cull);
        float cullSqr = cull * cull;
        float lodSqr = lod * lod;
        drawnCount = 0;

        for (int i = 0; i < clouds.Length; i++)
        {
            Cloud cloud = clouds[i];
            float distSqr = (cloud.position - eye).sqrMagnitude;
            if (distSqr > cullSqr)
                continue;

            int slot = cloud.prototype;
            if (slot < 0 || slot >= PrototypeCount)
                continue;

            bool far = distSqr > lodSqr;
            Mesh mesh = far ? lodPrototypes[slot] : prototypes[slot];
            if (mesh == null)
                mesh = prototypes[slot];
            if (mesh == null)
                continue;

            Matrix4x4[] batch = far ? farBatches[slot] : nearBatches[slot];
            int[] counts = far ? farCounts : nearCounts;
            int count = counts[slot];
            batch[count] = Matrix4x4.TRS(cloud.position, Quaternion.Euler(0f, cloud.yaw, 0f), Vector3.one * cloud.scale);
            count++;
            drawnCount++;
            if (count >= BatchLimit)
            {
                Submit(mesh, batch, count);
                count = 0;
            }

            counts[slot] = count;
        }

        for (int i = 0; i < PrototypeCount; i++)
        {
            if (nearCounts[i] > 0 && prototypes[i] != null)
                Submit(prototypes[i], nearBatches[i], nearCounts[i]);
            Mesh farMesh = lodPrototypes != null ? lodPrototypes[i] : null;
            if (farMesh == null)
                farMesh = prototypes[i];
            if (farCounts[i] > 0 && farMesh != null)
                Submit(farMesh, farBatches[i], farCounts[i]);
        }
    }

    void Submit(Mesh mesh, Matrix4x4[] batch, int count)
    {
        if (cloudMaterial == null || mesh == null || count <= 0)
            return;

        Graphics.DrawMeshInstanced(
            mesh,
            0,
            cloudMaterial,
            batch,
            count,
            null,
            ShadowCastingMode.Off,
            false,
            0,
            null);
    }

    float FogReach()
    {
        float density = RenderSettings.fog ? RenderSettings.fogDensity : 0.01f;
        density = Mathf.Max(density, 0.0015f);
        float reach = -Mathf.Log(0.035f) / density;
        return Mathf.Clamp(reach, 220f, 860f);
    }

    float QualityCull(float fogReach)
    {
        if (cloudQuality == CloudQuality.Low)
            return fogReach * 0.5f;
        if (cloudQuality == CloudQuality.Medium)
            return fogReach * 0.75f;
        return fogReach;
    }

    float QualityLod(float cull)
    {
        if (cloudQuality == CloudQuality.Low)
            return cull * 0.16f;
        if (cloudQuality == CloudQuality.Medium)
            return cull * 0.32f;
        return cull * 0.46f;
    }

    void TrackFrameRate()
    {
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0.0001f || dt > 0.5f)
            return;

        float fps = 1f / dt;
        shownFps = Mathf.Lerp(shownFps, fps, 1f - Mathf.Exp(-1.6f * dt));
        rateWindow += dt;
        rateSum += fps;
        rateSamples++;
        if (rateWindow < 0.5f)
            return;

        float average = rateSum / Mathf.Max(1, rateSamples);
        if (average < 40f)
            slowSeconds += rateWindow;
        else
            slowSeconds = 0f;

        if (slowSeconds >= 3f && cloudQuality != CloudQuality.Low)
        {
            cloudQuality = (CloudQuality)((int)cloudQuality - 1);
            slowSeconds = 0f;
        }

        rateWindow = 0f;
        rateSum = 0f;
        rateSamples = 0;
    }

    void CaptureBase()
    {
        fogToken = ++fogGeneration;
        Active = this;
        baseFog = RenderSettings.fogDensity;
        baseFogColor = RenderSettings.fogColor;
        shownFog = baseFog;
        veil = 0f;
        holdingBase = false;
        captured = true;
        ApplyShown();
    }

    void BlendFog()
    {
        if (!captured)
            return;

        if (!holdingBase && clouds != null && clouds.Length > 0)
        {
            if (plane == null)
                plane = FindFirstObjectByType<PlaneController>();

            RebuildGrid();
            bool inside = plane != null && InsideCloud(plane.transform.position);
            float targetFog = inside ? baseFog + 0.03f : baseFog;
            float targetVeil = inside ? 1f : 0f;
            float blend = 1f - Mathf.Exp(-2.8f * Time.unscaledDeltaTime);
            shownFog = Mathf.Lerp(shownFog, targetFog, blend);
            veil = Mathf.Lerp(veil, targetVeil, blend);
        }

        ApplyShown();
    }

    void ApplyShown()
    {
        if (!captured)
            return;

        RenderSettings.fogDensity = shownFog;
        RenderSettings.fogColor = baseFogColor;
        TintVeil(veil * 0.32f);
    }

    void EnsureGrid()
    {
        int cells = GridDim * GridDim;
        if (gridHead == null || gridHead.Length != cells)
            gridHead = new int[cells];
        if (clouds != null && (gridNext == null || gridNext.Length < clouds.Length))
            gridNext = new int[Mathf.Max(1, clouds.Length)];
    }

    void RebuildGrid()
    {
        EnsureGrid();
        if (gridHead == null || gridNext == null || clouds == null)
            return;

        for (int i = 0; i < gridHead.Length; i++)
            gridHead[i] = -1;

        for (int i = 0; i < clouds.Length; i++)
        {
            int cell = CellIndex(clouds[i].position);
            if (cell < 0)
            {
                gridNext[i] = -1;
                continue;
            }

            gridNext[i] = gridHead[cell];
            gridHead[cell] = i;
        }
    }

    bool InsideCloud(Vector3 pos)
    {
        if (gridHead == null || gridNext == null)
            return false;

        int cx = Mathf.FloorToInt((pos.x - GridOrigin) / GridCell);
        int cz = Mathf.FloorToInt((pos.z - GridOrigin) / GridCell);
        for (int dz = -2; dz <= 2; dz++)
        {
            for (int dx = -2; dx <= 2; dx++)
            {
                int x = cx + dx;
                int z = cz + dz;
                if (x < 0 || z < 0 || x >= GridDim || z >= GridDim)
                    continue;

                for (int i = gridHead[z * GridDim + x]; i >= 0; i = gridNext[i])
                {
                    float reach = clouds[i].radius * 0.72f;
                    if ((clouds[i].position - pos).sqrMagnitude <= reach * reach)
                        return true;
                }
            }
        }

        return false;
    }

    static int CellIndex(Vector3 pos)
    {
        int x = Mathf.FloorToInt((pos.x - GridOrigin) / GridCell);
        int z = Mathf.FloorToInt((pos.z - GridOrigin) / GridCell);
        if (x < 0 || z < 0 || x >= GridDim || z >= GridDim)
            return -1;
        return z * GridDim + x;
    }

    void EnsureVeil()
    {
        if (veilImage != null)
            return;

        var canvasObject = new GameObject("Cloud Veil", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var imageObject = new GameObject("Fade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);
        veilImage = imageObject.GetComponent<Image>();
        veilImage.color = new Color(1f, 1f, 1f, 0f);
        veilImage.raycastTarget = false;
        var rect = veilImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    void TintVeil(float alpha)
    {
        if (veilImage == null)
            return;

        Color color = veilImage.color;
        color.a = alpha;
        veilImage.color = color;
    }

    void EnsureMaterial()
    {
        if (cloudMaterial != null)
            return;

        Shader shader = Shader.Find("Skybound/Cloud");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        cloudMaterial = new Material(shader);
        cloudMaterial.name = "Cloud";
        cloudMaterial.enableInstancing = true;
        ApplyTint();
    }

    void ApplyTint()
    {
        if (cloudMaterial == null)
            return;

        Color tint = Color.Lerp(Color.white, Color.Lerp(WorldLook.Fog, WorldLook.SkyHorizon, 0.6f), 0.38f);
        string mood = WorldLook.SkyMood;
        if (mood == "sunset")
            tint = Color.Lerp(tint, new Color(1f, 0.55f, 0.32f), 0.5f);
        else if (mood == "sunrise")
            tint = Color.Lerp(tint, new Color(1f, 0.78f, 0.5f), 0.38f);
        else if (mood == "storm")
            tint = Color.Lerp(tint, new Color(0.42f, 0.46f, 0.52f), 0.7f);
        else if (mood == "night")
            tint = Color.Lerp(tint, new Color(0.68f, 0.76f, 0.95f), 0.62f);

        tint.a = 1f;
        cloudMaterial.color = tint;
        if (cloudMaterial.HasProperty("_BaseColor"))
            cloudMaterial.SetColor("_BaseColor", tint);
    }

    void EnsurePrototypes(int seed)
    {
        if (prototypes != null && lodPrototypes != null && prototypes.Length == PrototypeCount && prototypes[0] != null && lodPrototypes[0] != null)
            return;

        ReleasePrototypes();
        prototypes = new Mesh[PrototypeCount];
        lodPrototypes = new Mesh[PrototypeCount];
        for (int i = 0; i < PrototypeCount; i++)
        {
            prototypes[i] = BuildPrototype(seed + i * 97);
            lodPrototypes[i] = BuildLodPrototype(seed + i * 97 + 13);
        }
    }

    static Mesh BuildPrototype(int seed)
    {
        var rng = new System.Random(seed);
        int puffs = 5 + rng.Next(3);
        var parts = new CombineInstance[puffs];
        var meshes = new Mesh[puffs];
        for (int i = 0; i < puffs; i++)
        {
            meshes[i] = BuildPuff(rng, 8, 5, Lerp(rng, 0.12f, 0.28f));
            parts[i].mesh = meshes[i];
            Vector3 offset = new Vector3(Lerp(rng, -7.5f, 7.5f), Lerp(rng, -2.2f, 2.4f), Lerp(rng, -6.5f, 6.5f));
            Vector3 scale = new Vector3(Lerp(rng, 5.2f, 9f), Lerp(rng, 3.1f, 5.2f), Lerp(rng, 5f, 8.4f));
            parts[i].transform = Matrix4x4.TRS(offset, Quaternion.Euler(0f, Lerp(rng, 0f, 360f), 0f), scale);
        }

        var mesh = new Mesh();
        mesh.name = "Cloud";
        mesh.CombineMeshes(parts, true, true);
        for (int i = 0; i < meshes.Length; i++)
            DestroyGenerated(meshes[i]);

        mesh.RecalculateNormals();
        FaceOutward(mesh);
        PaintSoft(mesh);
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh BuildLodPrototype(int seed)
    {
        var rng = new System.Random(seed);
        var parts = new CombineInstance[2];
        var meshes = new Mesh[2];
        for (int i = 0; i < 2; i++)
        {
            meshes[i] = BuildPuff(rng, 5, 3, 0.08f);
            parts[i].mesh = meshes[i];
            Vector3 offset = new Vector3(Lerp(rng, -4.5f, 4.5f), Lerp(rng, -1.1f, 1.2f), Lerp(rng, -3.5f, 3.5f));
            Vector3 scale = new Vector3(Lerp(rng, 6.5f, 9.5f), Lerp(rng, 2.4f, 3.6f), Lerp(rng, 6f, 9f));
            parts[i].transform = Matrix4x4.TRS(offset, Quaternion.Euler(0f, Lerp(rng, 0f, 360f), 0f), scale);
        }

        var mesh = new Mesh();
        mesh.name = "Cloud Lod";
        mesh.CombineMeshes(parts, true, true);
        for (int i = 0; i < meshes.Length; i++)
            DestroyGenerated(meshes[i]);

        mesh.RecalculateNormals();
        FaceOutward(mesh);
        PaintSoft(mesh);
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh BuildPuff(System.Random rng, int lon, int lat, float deform)
    {
        float noiseX = Lerp(rng, 0f, 5f);
        float noiseZ = Lerp(rng, 0f, 5f);
        int rings = lat + 1;
        int columns = lon + 1;
        var vertices = new Vector3[rings * columns];
        var triangles = new int[lat * lon * 6];

        for (int y = 0; y < rings; y++)
        {
            float v = y / (float)lat;
            float polar = v * Mathf.PI;
            float ring = Mathf.Sin(polar);
            float height = Mathf.Cos(polar);
            for (int x = 0; x < columns; x++)
            {
                float u = x / (float)lon * Mathf.PI * 2f;
                float px = Mathf.Cos(u) * ring;
                float pz = Mathf.Sin(u) * ring;
                float wobble = 1f + (Mathf.PerlinNoise(px * 1.6f + noiseX, pz * 1.6f + noiseZ) - 0.5f) * deform;
                vertices[y * columns + x] = new Vector3(px, height * 0.78f, pz) * wobble;
            }
        }

        int write = 0;
        for (int y = 0; y < lat; y++)
        {
            for (int x = 0; x < lon; x++)
            {
                int a = y * columns + x;
                int b = a + 1;
                int c = a + columns;
                int d = c + 1;
                triangles[write++] = a;
                triangles[write++] = c;
                triangles[write++] = b;
                triangles[write++] = b;
                triangles[write++] = c;
                triangles[write++] = d;
            }
        }

        var mesh = new Mesh();
        mesh.name = "Puff";
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void FaceOutward(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        if (vertices == null || normals == null || vertices.Length == 0)
            return;

        float sum = 0f;
        int samples = Mathf.Min(vertices.Length, 48);
        for (int i = 0; i < samples; i++)
        {
            Vector3 direction = vertices[i];
            if (direction.sqrMagnitude < 0.0001f)
                continue;
            sum += Vector3.Dot(direction.normalized, normals[i]);
        }

        if (sum >= 0f)
            return;

        int[] triangles = mesh.triangles;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int swap = triangles[i + 1];
            triangles[i + 1] = triangles[i + 2];
            triangles[i + 2] = swap;
        }

        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }

    static void PaintSoft(Mesh mesh)
    {
        Vector3[] normals = mesh.normals;
        var colors = new Color[normals.Length];
        for (int i = 0; i < normals.Length; i++)
        {
            float light = Mathf.Clamp01(normals[i].y * 0.5f + 0.58f);
            colors[i] = Color.Lerp(new Color(0.62f, 0.68f, 0.78f), Color.white, light);
        }

        mesh.colors = colors;
    }

    void CollectClusters()
    {
        clusterCenters.Clear();
        clusterRadii.Clear();
        WorldGenerator generator = GetComponentInParent<WorldGenerator>();
        if (generator == null)
            generator = FindFirstObjectByType<WorldGenerator>();
        if (generator == null)
            return;

        for (int i = 0; i < generator.ClusterCount; i++)
        {
            if (!generator.TryGetCluster(i, out Vector3 center, out float radius))
                continue;

            clusterCenters.Add(center);
            clusterRadii.Add(radius);
        }
    }

    bool Occupied(Vector3 position, float spacing, List<Cloud> built)
    {
        if (placeHead == null || placeNext == null)
            return false;

        float limit = spacing * spacing;
        int span = Mathf.Max(1, Mathf.CeilToInt(spacing / PlaceCell));
        int cx = Mathf.FloorToInt((position.x - PlaceOrigin) / PlaceCell);
        int cz = Mathf.FloorToInt((position.z - PlaceOrigin) / PlaceCell);
        for (int dz = -span; dz <= span; dz++)
        {
            for (int dx = -span; dx <= span; dx++)
            {
                int cell = PlaceIndex(cx + dx, cz + dz);
                if (cell < 0)
                    continue;

                for (int i = placeHead[cell]; i >= 0; i = placeNext[i])
                {
                    Vector3 delta = built[i].position - position;
                    delta.y *= 0.35f;
                    if (delta.sqrMagnitude < limit)
                        return true;
                }
            }
        }

        return false;
    }

    void BeginPlacement(int capacity)
    {
        int cells = PlaceDim * PlaceDim;
        if (placeHead == null || placeHead.Length != cells)
            placeHead = new int[cells];
        for (int i = 0; i < placeHead.Length; i++)
            placeHead[i] = -1;

        if (placeNext == null || placeNext.Length < capacity)
            placeNext = new int[Mathf.Max(capacity, 16)];
    }

    void IndexPlaced(int cloudIndex, Vector3 position)
    {
        if (placeHead == null || placeNext == null || cloudIndex < 0 || cloudIndex >= placeNext.Length)
            return;

        int x = Mathf.FloorToInt((position.x - PlaceOrigin) / PlaceCell);
        int z = Mathf.FloorToInt((position.z - PlaceOrigin) / PlaceCell);
        int cell = PlaceIndex(x, z);
        if (cell < 0)
        {
            placeNext[cloudIndex] = -1;
            return;
        }

        placeNext[cloudIndex] = placeHead[cell];
        placeHead[cell] = cloudIndex;
    }

    static int PlaceIndex(int x, int z)
    {
        if (x < 0 || z < 0 || x >= PlaceDim || z >= PlaceDim)
            return -1;
        return z * PlaceDim + x;
    }

    void CollectReserved()
    {
        var points = new List<Vector3>(8);
        Landmark[] landmarks = Object.FindObjectsByType<Landmark>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < landmarks.Length; i++)
        {
            if (landmarks[i] != null)
                points.Add(landmarks[i].transform.position);
        }

        PlaneController craft = Object.FindFirstObjectByType<PlaneController>();
        if (craft != null)
        {
            points.Add(craft.SpawnPosition);
            points.Add(craft.transform.position);
        }

        points.Add(new Vector3(0f, 120f, -(SpotGenerator.WorldRadius + 20f)));
        reserved = points.ToArray();
    }

    bool Reserved(Vector3 position, float cloudRadius)
    {
        if (reserved == null)
            return false;

        float limit = KeepClear + cloudRadius * 0.5f;
        float limitSqr = limit * limit;
        for (int i = 0; i < reserved.Length; i++)
        {
            if ((reserved[i] - position).sqrMagnitude < limitSqr)
                return true;
        }

        return false;
    }

    static bool HitsIsland(Vector3 position, float radius)
    {
        return Physics.CheckSphere(position, Mathf.Max(6f, radius), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }

    void ClearClouds()
    {
        clouds = null;
        veil = 0f;
        TintVeil(0f);
    }

    void ReleasePrototypes()
    {
        if (prototypes == null)
            return;

        for (int i = 0; i < prototypes.Length; i++)
            DestroyGenerated(prototypes[i]);
        prototypes = null;
        if (lodPrototypes != null)
        {
            for (int i = 0; i < lodPrototypes.Length; i++)
                DestroyGenerated(lodPrototypes[i]);
            lodPrototypes = null;
        }
    }

    static void DestroyGenerated(Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    static float Next01(System.Random rng)
    {
        return (float)rng.NextDouble();
    }

    static float Lerp(System.Random rng, float min, float max)
    {
        return Mathf.Lerp(min, max, Next01(rng));
    }

    void EnsureDebug()
    {
        if (debugText != null)
            return;

        var canvasObject = new GameObject("Cloud Debug", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var textObject = new GameObject("Readout", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(canvasObject.transform, false);
        debugText = textObject.GetComponent<Text>();
        debugText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (debugText.font == null)
            debugText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        debugText.fontSize = 18;
        debugText.color = new Color(0.95f, 0.97f, 1f, 0.92f);
        debugText.alignment = TextAnchor.LowerLeft;
        debugText.horizontalOverflow = HorizontalWrapMode.Overflow;
        debugText.verticalOverflow = VerticalWrapMode.Overflow;
        debugText.raycastTarget = false;
        var rect = debugText.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(18f, 18f);
        rect.sizeDelta = new Vector2(360f, 78f);
        RefreshDebug();
    }

    void RefreshDebug()
    {
        if (debugText == null)
            return;

        debugText.gameObject.SetActive(debugVisible);
        if (!debugVisible)
            return;

        debugText.text = "Clouds " + CloudCount + "\nFPS " + Mathf.RoundToInt(shownFps) + "\n" + cloudQuality;
    }
}
