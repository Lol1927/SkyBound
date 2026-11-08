using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public struct ClusterIsland
{
    public Transform root;
    public Collider surface;
    public float radius;
    public bool main;
    public Vector2 center;
    public float topY;
    public float bottomY;
}

public static class FloatingIslandGenerator
{
    const float MinBottom = 12f;
    const float MaxTop = 220f;
    const float LayerGap = 30f;
    const float HorizontalPad = 8f;

    static Material vertexMaterial;
    static Material dropMaterial;

    class Body
    {
        public Vector2 center;
        public float radius;
        public float baseY;
        public float depth;
        public float peak;
        public float rim;
        public bool main;
        public bool features;
        public float deckY;
        public float Top;
        public float Bottom;
    }

    public static Vector3 BuildCluster(Transform clusterRoot, float spotRadius, IslandData source, int worldSeed, int spotIndex, List<ClusterIsland> built)
    {
        if (built == null)
            built = new List<ClusterIsland>();

        IslandData island = source ?? new IslandData { biome = "forest", terrainStyle = "rolling" };
        string style = string.IsNullOrEmpty(island.terrainStyle) ? "rolling" : island.terrainStyle;
        string biome = string.IsNullOrEmpty(island.biome) ? "forest" : island.biome;
        var rng = new System.Random(Mix(worldSeed, spotIndex, 41));
        List<Body> bodies = Layout(style, spotRadius, rng);
        ScalePeaks(bodies, WorldLook.HeightScaleOf(island));
        MarkMain(bodies);
        RaiseForFeatures(bodies, island);
        Separate(bodies, spotRadius);
        ApplyVerticalSpread(bodies, WorldLook.VerticalSpreadOf(island));

        Vector3 aim = Vector3.zero;
        var links = new List<Body>();
        for (int i = 0; i < bodies.Count; i++)
        {
            Body body = bodies[i];
            int meshSeed = Mix(worldSeed, spotIndex * 20 + i, 17);
            IslandData piece = PieceData(island, body.features);
            Transform root = CreateIsland(clusterRoot, body, piece, meshSeed, biome, out float deckY);
            body.deckY = body.baseY + deckY;
            links.Add(body);

            var clusterIsland = new ClusterIsland
            {
                root = root,
                surface = root.Find("Ground").GetComponent<MeshCollider>(),
                radius = body.radius,
                main = body.main,
                center = body.center,
                topY = body.Top,
                bottomY = body.Bottom
            };
            built.Add(clusterIsland);
            if (body.main)
                aim = new Vector3(body.center.x, body.baseY + body.peak * 0.55f, body.center.y);
        }

        BuildBridges(clusterRoot, links, rng, biome);
        BuildFloatingRocks(clusterRoot, bodies, spotRadius, rng, biome);
        return aim;
    }

    public static void ReleaseMaterials()
    {
        DestroyMaterial(ref vertexMaterial);
        DestroyMaterial(ref dropMaterial);
    }

    static List<Body> Layout(string style, float spotRadius, System.Random rng)
    {
        if (style == "mountainous")
            return LayoutMountain(spotRadius, rng);
        if (style == "plateau")
            return LayoutPlateau(spotRadius, rng);
        if (style == "canyon")
            return LayoutCanyon(spotRadius, rng);
        return LayoutRolling(spotRadius, rng);
    }

    static List<Body> LayoutRolling(float spotRadius, System.Random rng)
    {
        int count = 3 + rng.Next(2);
        var bodies = new List<Body>(count);
        Body main = NewBody(spotRadius * 0.4f, "rolling", rng);
        main.center = Vector2.zero;
        SetTop(main, 76f + Unit(rng) * 16f);
        bodies.Add(main);

        for (int i = 1; i < count; i++)
        {
            float satRadius = main.radius * Mathf.Lerp(0.62f, 0.8f, Unit(rng));
            float maxSat = (spotRadius - main.radius - 18f) * 0.5f;
            satRadius = Mathf.Clamp(satRadius, 12f, Mathf.Max(12f, maxSat));
            Body sat = NewBody(satRadius, "rolling", rng);
            float angle = (i - 0.5f) * Mathf.PI * 2f / (count - 1) + (Unit(rng) - 0.5f) * 0.3f;
            float dist = main.radius + sat.radius + 16f;
            sat.center = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
            ClampInside(sat, spotRadius);
            SetTop(sat, 62f + Unit(rng) * 26f);
            bodies.Add(sat);
        }

        Separate(bodies, spotRadius);
        return bodies;
    }

    static List<Body> LayoutMountain(float spotRadius, System.Random rng)
    {
        int count = spotRadius < 80f ? 3 + rng.Next(2) : 5 + rng.Next(3);
        var bodies = new List<Body>(count);
        Body main = NewBody(spotRadius * 0.4f, "mountainous", rng);
        main.center = RandomPoint(rng, spotRadius * 0.2f);
        SetTop(main, 168f + Unit(rng) * 40f);
        bodies.Add(main);

        for (int i = 1; i < count; i++)
        {
            float scale = Mathf.Lerp(0.28f, 0.52f, Unit(rng));
            Body sat = NewBody(main.radius * scale, "mountainous", rng);
            sat.center = RandomPoint(rng, Mathf.Max(0f, spotRadius - sat.radius));
            float band = Mathf.Lerp(58f, 206f, i / (float)(count - 1));
            SetTop(sat, band + (Unit(rng) - 0.5f) * 12f);
            bodies.Add(sat);
        }

        Separate(bodies, spotRadius);
        EnsureCount(bodies, count, spotRadius, "mountainous", rng);
        return bodies;
    }

    static List<Body> LayoutPlateau(float spotRadius, System.Random rng)
    {
        int count = 4 + rng.Next(2);
        float angle = Unit(rng) * Mathf.PI * 2f;
        var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        var side = new Vector2(-dir.y, dir.x);
        var bodies = new List<Body>(count);
        for (int i = 0; i < count; i++)
        {
            float along = count == 1 ? 0.5f : i / (float)(count - 1);
            float radius = spotRadius * Mathf.Lerp(0.38f, 0.24f, along);
            Body body = NewBody(radius, "plateau", rng);
            if (i < 3)
                body.center = dir * ((i - 1f) * (radius + spotRadius * 0.08f));
            else
                body.center = side * (spotRadius * 0.42f) * (i == 3 ? 1f : -1f);
            ClampInside(body, spotRadius);
            bodies.Add(body);
        }

        int stacked = 1;
        SetTop(bodies[0], 72f + Unit(rng) * 16f);
        int stairCount = Mathf.Min(3, bodies.Count);
        for (int i = 1; i < stairCount; i++)
        {
            float thickness = Occupied(bodies[i]);
            float desired = bodies[i - 1].Top + LayerGap + thickness;
            if (desired > MaxTop - 4f)
            {
                bodies[i].depth = Mathf.Max(bodies[i].radius * 0.4f, bodies[i].depth * 0.6f);
                thickness = Occupied(bodies[i]);
                desired = bodies[i - 1].Top + LayerGap + thickness;
            }

            if (desired > MaxTop - 2f)
                break;

            SetTop(bodies[i], desired);
            stacked = i + 1;
        }

        for (int i = stacked; i < bodies.Count; i++)
            SetTop(bodies[i], 58f + Unit(rng) * 36f);

        Separate(bodies, spotRadius);
        return bodies;
    }

    static List<Body> LayoutCanyon(float spotRadius, System.Random rng)
    {
        int pieces = spotRadius >= 100f && rng.NextDouble() > 0.4 ? 3 : 2;
        float gap = Mathf.Lerp(18f, 28f, Mathf.Clamp01((spotRadius - 50f) / 80f));
        float pieceRadius = pieces == 2
            ? (spotRadius - gap * 0.5f) / 2.2f
            : (spotRadius - gap) / 3.2f;
        pieceRadius = Mathf.Clamp(pieceRadius, 14f, spotRadius * 0.46f);
        float spacing = pieceRadius * 2f + gap;
        float angle = Unit(rng) * Mathf.PI * 2f;
        var axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        float mid = (pieces - 1) * 0.5f;
        float sharedTop = 100f + Unit(rng) * 28f;

        var bodies = new List<Body>(7);
        for (int i = 0; i < pieces; i++)
        {
            Body piece = NewBody(pieceRadius, "canyon", rng);
            piece.center = axis * ((i - mid) * spacing);
            ClampInside(piece, spotRadius);
            piece.features = i == 0;
            SetTop(piece, sharedTop);
            bodies.Add(piece);
        }

        int target = Mathf.Clamp(pieces + 1 + rng.Next(3), 3, 7);
        while (bodies.Count < target)
        {
            Body sat = NewBody(pieceRadius * Mathf.Lerp(0.38f, 0.62f, Unit(rng)), "canyon", rng);
            sat.center = RandomPoint(rng, Mathf.Max(0f, spotRadius - sat.radius));
            SetTop(sat, 56f + Unit(rng) * 120f);
            bodies.Add(sat);
        }

        Separate(bodies, spotRadius);
        return bodies;
    }

    static void EnsureCount(List<Body> bodies, int count, float spotRadius, string style, System.Random rng)
    {
        while (bodies.Count < Mathf.Max(3, count) && bodies.Count < 7)
        {
            Body extra = NewBody(bodies[0].radius * 0.4f, style, rng);
            extra.center = bodies[0].center;
            float thickness = Occupied(extra);
            SetTop(extra, bodies[0].Top + LayerGap + thickness);
            ClampInside(extra, spotRadius);
            bodies.Add(extra);
        }
    }

    static Body NewBody(float radius, string style, System.Random rng)
    {
        var body = new Body();
        body.radius = Mathf.Max(12f, radius);
        body.peak = Peak(style);
        body.rim = RimLow(style);
        float t = Unit(rng);
        body.depth = body.radius * Mathf.Lerp(0.8f, 1.5f, t);
        float maxDepth = MaxTop - MinBottom - CliffOf(body) - 6f;
        if (body.depth > maxDepth)
            body.depth = Mathf.Max(body.radius * 0.55f, maxDepth);
        Refresh(body);
        return body;
    }

    static void RaiseForFeatures(List<Body> bodies, IslandData island)
    {
        if (!HasFeature(island, "spires") && !HasFeature(island, "arch"))
            return;

        for (int i = 0; i < bodies.Count; i++)
        {
            if (!bodies[i].main)
                continue;

            bodies[i].peak += 42f * WorldLook.HeightScaleOf(island);
            FitVertical(bodies[i]);
            return;
        }
    }

    static void SetTop(Body body, float top)
    {
        top = Mathf.Clamp(top, 48f, MaxTop);
        body.baseY = top - body.peak;
        FitVertical(body);
    }

    static void FitVertical(Body body)
    {
        Refresh(body);
        if (body.Bottom < MinBottom)
        {
            body.baseY += MinBottom - body.Bottom;
            Refresh(body);
        }

        if (body.Top <= MaxTop)
            return;

        float over = body.Top - MaxTop;
        body.depth = Mathf.Max(body.radius * 0.45f, body.depth - over);
        body.baseY -= over;
        Refresh(body);
        if (body.Bottom < MinBottom)
        {
            body.baseY += MinBottom - body.Bottom;
            Refresh(body);
        }
    }

    static float CliffOf(Body body)
    {
        return Mathf.Clamp(body.radius * 0.15f, 5f, 16f);
    }

    static float Occupied(Body body)
    {
        return body.peak - body.rim + CliffOf(body) + body.depth * 1.15f;
    }

    static void Refresh(Body body)
    {
        body.Top = body.baseY + body.peak;
        body.Bottom = body.baseY + body.rim - CliffOf(body) - body.depth * 1.15f;
    }

    static void Separate(List<Body> bodies, float spotRadius)
    {
        for (int pass = 0; pass < 10; pass++)
        {
            bool moved = false;
            for (int i = 0; i < bodies.Count; i++)
            {
                for (int j = i + 1; j < bodies.Count; j++)
                {
                    Body a = bodies[i];
                    Body b = bodies[j];
                    float dist = Vector2.Distance(a.center, b.center);
                    float need = a.radius + b.radius + HorizontalPad;
                    if (dist >= need)
                        continue;

                    Body lower = a.baseY <= b.baseY ? a : b;
                    Body upper = a.baseY <= b.baseY ? b : a;
                    float gap = upper.Bottom - lower.Top;
                    if (gap >= LayerGap)
                        continue;

                    float push = LayerGap - gap;
                    float previous = upper.baseY;
                    upper.baseY += push;
                    FitVertical(upper);
                    if (upper.baseY < previous + push * 0.5f || upper.Bottom - lower.Top < LayerGap)
                    {
                        upper.baseY = previous;
                        float floor = Mathf.Max(8f, upper.radius * 0.35f);
                        float shorten = (upper.depth - floor) * 1.15f;
                        if (shorten >= push)
                        {
                            upper.depth -= push / 1.15f;
                            Refresh(upper);
                        }
                        else
                        {
                            if (shorten > 1f)
                                upper.depth = floor;
                            Refresh(upper);
                            Vector2 away = upper.center - lower.center;
                            if (away.sqrMagnitude < 0.04f)
                                away = new Vector2(1f, 0.2f);
                            away.Normalize();
                            upper.center += away * (need - dist + 2f);
                            ClampInside(upper, spotRadius);
                        }
                    }

                    moved = true;
                }
            }

            if (!moved)
                break;
        }

        for (int pass = 0; pass < 4; pass++)
        {
            for (int i = 0; i < bodies.Count; i++)
            {
                for (int j = i + 1; j < bodies.Count; j++)
                {
                    Body a = bodies[i];
                    Body b = bodies[j];
                    float dist = Vector2.Distance(a.center, b.center);
                    float need = a.radius + b.radius + 10f;
                    if (dist >= need)
                        continue;

                    Body lower = a.baseY <= b.baseY ? a : b;
                    Body upper = lower == a ? b : a;
                    if (upper.Bottom - lower.Top >= LayerGap)
                        continue;

                    Body small = a.radius <= b.radius ? a : b;
                    Vector2 away = small.center - (small == a ? b.center : a.center);
                    if (away.sqrMagnitude < 0.04f)
                        away = new Vector2(0f, 1f);
                    away.Normalize();
                    small.radius = Mathf.Min(small.radius, Mathf.Max(12f, spotRadius * 0.22f));
                    small.depth = Mathf.Max(8f, small.radius * 0.35f);
                    small.center = away * Mathf.Max(0f, spotRadius - small.radius - 2f);
                    SetTop(small, 48f);
                }
            }
        }
    }

    static void MarkMain(List<Body> bodies)
    {
        int main = 0;
        for (int i = 1; i < bodies.Count; i++)
        {
            if (bodies[i].radius > bodies[main].radius)
                main = i;
        }

        for (int i = 0; i < bodies.Count; i++)
        {
            bodies[i].main = i == main;
            if (i != main)
                bodies[i].features = false;
        }

        bodies[main].features = true;
    }

    static Transform CreateIsland(Transform clusterRoot, Body body, IslandData piece, int meshSeed, string biome, out float deckY)
    {
        var root = new GameObject(body.main ? "Main" : "Satellite");
        root.transform.SetParent(clusterRoot, false);
        root.transform.localPosition = new Vector3(body.center.x, body.baseY, body.center.y);

        Mesh mesh = BuildMesh(body, piece, meshSeed, biome, out Vector3 spill, out deckY);
        var ground = new GameObject("Ground");
        ground.transform.SetParent(root.transform, false);
        var filter = ground.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = ground.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = VertexMaterial();
        var collider = ground.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
        var owned = ground.AddComponent<OwnedMesh>();
        owned.mesh = mesh;

        if (body.features)
        {
            IslandGenerator.AddSurfaceFeatures(root.transform, body.radius, piece, meshSeed);
            if (HasFeature(piece, "lake"))
                CreateWaterfall(root.transform, spill);
        }

        return root.transform;
    }

    static Mesh BuildMesh(Body body, IslandData piece, int seed, string biome, out Vector3 spill, out float deckY)
    {
        int cells = body.radius >= 70f ? 18 : body.radius >= 36f ? 14 : 11;
        int side = cells + 1;
        float extent = body.radius * 1.05f;
        float cliffDrop = Mathf.Clamp(body.radius * 0.14f, 4.5f, 14f);
        var vertices = new List<Vector3>();
        var stone = new List<byte>();
        var triangles = new List<int>();
        var boundary = new Dictionary<long, int>();
        var points = new Vector3[side, side];
        var land = new bool[side, side];

        for (int z = 0; z < side; z++)
        {
            float v = z / (float)cells * 2f - 1f;
            for (int x = 0; x < side; x++)
            {
                float u = x / (float)cells * 2f - 1f;
                float px = u * extent;
                float pz = v * extent;
                float distance = Mathf.Sqrt(px * px + pz * pz);
                float angle = Mathf.Atan2(pz, px);
                float coast = CoastRadius(angle, body.radius, seed);
                land[x, z] = distance <= coast;
                points[x, z] = new Vector3(px, Sample(px, pz, body, piece, seed), pz);
            }
        }

        var ids = new int[side, side];
        for (int z = 0; z < side; z++)
        {
            for (int x = 0; x < side; x++)
                ids[x, z] = -1;
        }

        for (int z = 0; z < cells; z++)
        {
            for (int x = 0; x < cells; x++)
            {
                if (!land[x, z] || !land[x + 1, z] || !land[x, z + 1] || !land[x + 1, z + 1])
                    continue;

                int a = TopVert(vertices, stone, ids, points, x, z);
                int c = TopVert(vertices, stone, ids, points, x + 1, z);
                int d = TopVert(vertices, stone, ids, points, x + 1, z + 1);
                int b = TopVert(vertices, stone, ids, points, x, z + 1);
                AddFacing(triangles, vertices, a, c, d, Vector3.up, boundary);
                AddFacing(triangles, vertices, a, d, b, Vector3.up, boundary);
            }
        }

        spill = Vector3.zero;
        deckY = 0f;
        List<List<int>> loops = WalkLoops(boundary);
        if (loops.Count == 0)
            return ClosedFallback(body, biome, seed, out spill, out deckY);

        Offsets(seed + 23, out float ox, out float oz);
        float deckSum = 0f;
        int deckCount = 0;
        float spillY = float.PositiveInfinity;
        for (int loopIndex = 0; loopIndex < loops.Count; loopIndex++)
        {
            List<int> loop = loops[loopIndex];
            if (loop.Count < 3)
                continue;

            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < loop.Count; i++)
            {
                Vector3 point = vertices[loop[i]];
                centroid += point;
                deckSum += point.y;
                deckCount++;
                if (point.y < spillY)
                {
                    spillY = point.y;
                    spill = point;
                }
            }

            centroid /= loop.Count;
            CapUnderside(vertices, stone, triangles, loop, centroid, body.depth, cliffDrop, ox, oz);
        }

        deckY = deckCount > 0 ? deckSum / deckCount : 0f;
        if (float.IsPositiveInfinity(spillY))
            spill = new Vector3(0f, deckY, 0f);
        return Flatten(vertices, triangles, stone, biome, seed);
    }

    static int TopVert(List<Vector3> vertices, List<byte> stone, int[,] ids, Vector3[,] points, int x, int z)
    {
        if (ids[x, z] >= 0)
            return ids[x, z];

        ids[x, z] = AddVert(vertices, stone, points[x, z], false);
        return ids[x, z];
    }

    static void CapUnderside(List<Vector3> vertices, List<byte> stone, List<int> triangles, List<int> loop, Vector3 centroid, float depth, float cliffDrop, float ox, float oz)
    {
        const int rings = 7;
        int segments = loop.Count;
        var bottom = new int[rings, segments];
        var radial = new Vector3[segments];
        var coastY = new float[segments];
        for (int i = 0; i < segments; i++)
        {
            Vector3 point = vertices[loop[i]];
            coastY[i] = point.y;
            radial[i] = point - centroid;
            radial[i].y = 0f;
        }

        for (int ring = 0; ring < rings; ring++)
        {
            float t = (ring + 1f) / (rings + 1f);
            float shrink = ring == 0 ? 1.01f : Mathf.Lerp(0.9f, 0.1f, t * t);
            for (int i = 0; i < segments; i++)
            {
                float rocky = (Mathf.PerlinNoise(ox + i * 0.31f, oz + ring * 0.47f) - 0.5f) * depth * 0.2f * t;
                float y = ring == 0
                    ? coastY[i] - cliffDrop
                    : coastY[i] - cliffDrop - depth * t * t + rocky;
                Vector3 point = centroid + radial[i] * shrink;
                point.y = y;
                bottom[ring, i] = AddVert(vertices, stone, point, true);
            }
        }

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            Vector3 mid = (vertices[loop[i]] + vertices[loop[next]]) * 0.5f;
            Vector3 outward = mid - centroid;
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.01f)
                outward = Vector3.right;
            AddFacing(triangles, vertices, loop[i], loop[next], bottom[0, next], outward, null);
            AddFacing(triangles, vertices, loop[i], bottom[0, next], bottom[0, i], outward, null);
        }

        for (int ring = 0; ring < rings - 1; ring++)
        {
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                Vector3 mid = (vertices[bottom[ring, i]] + vertices[bottom[ring + 1, i]]) * 0.5f;
                Vector3 outward = mid - centroid;
                outward.y = 0f;
                if (outward.sqrMagnitude < 0.01f)
                    outward = Vector3.down;
                outward = outward.normalized + Vector3.down;
                AddFacing(triangles, vertices, bottom[ring, i], bottom[ring, next], bottom[ring + 1, next], outward, null);
                AddFacing(triangles, vertices, bottom[ring, i], bottom[ring + 1, next], bottom[ring + 1, i], outward, null);
            }
        }

        float tipY = float.PositiveInfinity;
        int last = rings - 1;
        for (int i = 0; i < segments; i++)
            tipY = Mathf.Min(tipY, vertices[bottom[last, i]].y);
        tipY -= Mathf.Max(2.5f, depth * 0.14f);
        int tip = AddVert(vertices, stone, new Vector3(centroid.x, tipY, centroid.z), true);
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            AddFacing(triangles, vertices, bottom[last, i], bottom[last, next], tip, Vector3.down, null);
        }
    }

    static Mesh ClosedFallback(Body body, string biome, int seed, out Vector3 spill, out float deckY)
    {
        deckY = Sample(0f, 0f, body, null, seed);
        spill = new Vector3(body.radius * 0.8f, deckY, 0f);
        return ChunkMesh(RockColor(biome, seed, 1), seed, 0.35f);
    }

    static float Sample(float x, float z, Body body, IslandData piece, int seed)
    {
        return IslandGenerator.SurfaceHeight(x, z, body.radius, piece, seed);
    }

    static float CoastRadius(float angle, float radius, int seed)
    {
        Offsets(seed + 11, out float ox, out float oz);
        float c = Mathf.Cos(angle);
        float s = Mathf.Sin(angle);
        float broad = Mathf.PerlinNoise(c * 1.7f + ox, s * 1.7f + oz);
        float mid = Mathf.PerlinNoise(c * 4.4f + ox + 3.2f, s * 4.4f + oz + 1.4f);
        float fine = Mathf.PerlinNoise(c * 7.5f + oz, s * 7.5f + ox);
        float shape = 0.56f + broad * 0.32f + (mid - 0.5f) * 0.18f + (fine - 0.5f) * 0.08f;
        return radius * Mathf.Clamp(shape, 0.46f, 0.98f);
    }

    static void BuildBridges(Transform clusterRoot, List<Body> bodies, System.Random rng, string biome)
    {
        var pairs = new List<int>();
        for (int i = 0; i < bodies.Count; i++)
        {
            for (int j = i + 1; j < bodies.Count; j++)
            {
                float dist = Vector2.Distance(bodies[i].center, bodies[j].center);
                float gap = dist - bodies[i].radius - bodies[j].radius;
                if (gap < 6f || gap > 80f)
                    continue;
                if (Mathf.Abs(bodies[i].deckY - bodies[j].deckY) > 36f)
                    continue;
                pairs.Add(i);
                pairs.Add(j);
            }
        }

        int made = 0;
        for (int p = 0; p + 1 < pairs.Count && made < 2; p += 2)
        {
            if (Unit(rng) < 0.25f && pairs.Count > 4)
                continue;
            CreateBridge(clusterRoot, bodies[pairs[p]], bodies[pairs[p + 1]], biome, rng);
            made++;
        }
    }

    static void CreateBridge(Transform clusterRoot, Body a, Body b, string biome, System.Random rng)
    {
        Vector3 from = new Vector3(a.center.x, 0f, a.center.y);
        Vector3 to = new Vector3(b.center.x, 0f, b.center.y);
        Vector3 flat = to - from;
        flat.y = 0f;
        if (flat.sqrMagnitude < 1f)
            return;

        Vector3 direction = flat.normalized;
        Vector3 start = from + direction * a.radius * 0.78f;
        Vector3 end = to - direction * b.radius * 0.78f;
        start.y = a.deckY;
        end.y = b.deckY;
        Vector3 span = end - start;
        float length = span.magnitude;
        if (length < 4f)
            return;

        var bridge = new GameObject("Bridge");
        bridge.transform.SetParent(clusterRoot, false);
        bridge.transform.localPosition = (start + end) * 0.5f;
        bridge.transform.localRotation = Quaternion.LookRotation(span.normalized, Vector3.up);

        float width = Mathf.Lerp(7f, 11f, Unit(rng));
        float thickness = Mathf.Lerp(3.4f, 5.2f, Unit(rng));
        Mesh mesh = BeamMesh(length, thickness, width, RockColor(biome, rng.Next(), 0), rng.Next());
        var filter = bridge.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = bridge.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = VertexMaterial();
        var collider = bridge.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
        collider.convex = true;
        var owned = bridge.AddComponent<OwnedMesh>();
        owned.mesh = mesh;
    }

    static void BuildFloatingRocks(Transform clusterRoot, List<Body> bodies, float spotRadius, System.Random rng, string biome)
    {
        int count = Mathf.Max(0, Mathf.RoundToInt((4 + rng.Next(4)) * Mathf.Clamp01(WorldLook.FloatingRockDensity)));
        int placed = 0;
        int attempts = count * 8;
        while (placed < count && attempts-- > 0)
        {
            float angle = Unit(rng) * Mathf.PI * 2f;
            float dist = spotRadius * Mathf.Lerp(0.78f, 1.02f, Unit(rng));
            var point = new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);
            float rockRadius = Mathf.Lerp(3.2f, 7.5f, Unit(rng));
            bool blocked = false;
            for (int i = 0; i < bodies.Count; i++)
            {
                if (Vector2.Distance(point, bodies[i].center) < bodies[i].radius + rockRadius + 4f)
                {
                    blocked = true;
                    break;
                }
            }

            if (blocked)
                continue;

            float y = Mathf.Lerp(34f, 188f, Unit(rng));
            CreateFloatRock(clusterRoot, new Vector3(point.x, y, point.y), rockRadius, biome, rng);
            placed++;
        }
    }

    static void CreateFloatRock(Transform clusterRoot, Vector3 localPosition, float radius, string biome, System.Random rng)
    {
        var rock = new GameObject("FloatRock");
        rock.transform.SetParent(clusterRoot, false);
        rock.transform.localPosition = localPosition;
        rock.transform.localRotation = Quaternion.Euler(Unit(rng) * 40f, Unit(rng) * 360f, Unit(rng) * 40f);
        rock.transform.localScale = new Vector3(
            radius * Mathf.Lerp(0.8f, 1.35f, Unit(rng)),
            radius * Mathf.Lerp(0.55f, 1.1f, Unit(rng)),
            radius * Mathf.Lerp(0.8f, 1.3f, Unit(rng)));

        Mesh mesh = ChunkMesh(RockColor(biome, rng.Next(), 2), rng.Next(), 0.28f);
        var filter = rock.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = rock.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = VertexMaterial();
        var collider = rock.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
        collider.convex = true;
        var owned = rock.AddComponent<OwnedMesh>();
        owned.mesh = mesh;
    }

    static void CreateWaterfall(Transform islandRoot, Vector3 localSpill)
    {
        var fall = new GameObject("Waterfall");
        fall.transform.SetParent(islandRoot, false);
        fall.transform.localPosition = localSpill + new Vector3(0f, 0.4f, 0f);
        fall.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        var particles = fall.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.playOnAwake = true;
        main.loop = true;
        main.startLifetime = 4.2f;
        main.startSpeed = 1.4f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.8f);
        Color drop = Color.Lerp(WorldLook.Water, Color.white, 0.55f);
        drop.a = 0.7f;
        main.startColor = drop;
        main.gravityModifier = 1.35f;
        main.maxParticles = 280;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = particles.emission;
        emission.rateOverTime = 46f;

        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 7f;
        shape.radius = 1.8f;

        var renderer = fall.GetComponent<ParticleSystemRenderer>();
        renderer.material = DropMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    static Mesh Flatten(List<Vector3> vertices, List<int> triangles, List<byte> stone, string biome, int seed)
    {
        int count = triangles.Count;
        var flatVertices = new Vector3[count];
        var flatColors = new Color[count];
        var flatTriangles = new int[count];
        for (int i = 0; i < count; i += 3)
        {
            int a = triangles[i];
            int b = triangles[i + 1];
            int c = triangles[i + 2];
            bool rock = stone[a] != 0 || stone[b] != 0 || stone[c] != 0;
            Color color = rock ? RockColor(biome, seed, i) : TopColor(biome, seed, i);
            flatVertices[i] = vertices[a];
            flatVertices[i + 1] = vertices[b];
            flatVertices[i + 2] = vertices[c];
            flatColors[i] = color;
            flatColors[i + 1] = color;
            flatColors[i + 2] = color;
            flatTriangles[i] = i;
            flatTriangles[i + 1] = i + 1;
            flatTriangles[i + 2] = i + 2;
        }

        return FinishFlat("FloatingIsland", flatVertices, flatColors, flatTriangles);
    }

    static Mesh BeamMesh(float length, float thickness, float width, Color color, int seed)
    {
        const int slices = 4;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var rings = new int[slices + 1, 4];
        Offsets(seed + 5, out float ox, out float oz);
        float halfW = width * 0.5f;
        float halfT = thickness * 0.5f;
        for (int slice = 0; slice <= slices; slice++)
        {
            float u = slice / (float)slices;
            float z = Mathf.Lerp(-length * 0.5f, length * 0.5f, u);
            float arch = Mathf.Sin(u * Mathf.PI) * thickness * 0.45f;
            float sway = (Mathf.PerlinNoise(ox + u * 2.2f, oz) - 0.5f) * width * 0.12f;
            Vector3[] corners =
            {
                new Vector3(-halfW + sway, -halfT, z),
                new Vector3(halfW + sway, -halfT * 0.85f, z),
                new Vector3(halfW * 0.92f + sway, halfT + arch, z),
                new Vector3(-halfW * 0.92f + sway, halfT * 0.9f + arch, z)
            };
            for (int corner = 0; corner < 4; corner++)
                rings[slice, corner] = AddPlain(vertices, corners[corner]);
        }

        for (int slice = 0; slice < slices; slice++)
        {
            for (int corner = 0; corner < 4; corner++)
            {
                int next = (corner + 1) % 4;
                Vector3 mid = (vertices[rings[slice, corner]] + vertices[rings[slice, next]] + vertices[rings[slice + 1, corner]]) / 3f;
                Vector3 outward = mid;
                outward.z = 0f;
                if (outward.sqrMagnitude < 0.01f)
                    outward = Vector3.down;
                AddFacing(triangles, vertices, rings[slice, corner], rings[slice, next], rings[slice + 1, next], outward, null);
                AddFacing(triangles, vertices, rings[slice, corner], rings[slice + 1, next], rings[slice + 1, corner], outward, null);
            }
        }

        AddCap(triangles, vertices, rings, 0, Vector3.back);
        AddCap(triangles, vertices, rings, slices, Vector3.forward);
        return Paint(vertices, triangles, color, seed, "Bridge");
    }

    static void AddCap(List<int> triangles, List<Vector3> vertices, int[,] rings, int slice, Vector3 outward)
    {
        AddFacing(triangles, vertices, rings[slice, 0], rings[slice, 1], rings[slice, 2], outward, null);
        AddFacing(triangles, vertices, rings[slice, 0], rings[slice, 2], rings[slice, 3], outward, null);
    }

    static Mesh ChunkMesh(Color color, int seed, float jitter)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var rng = new System.Random(seed == 0 ? 1 : seed);
        Vector3[] corners = new Vector3[8];
        int write = 0;
        for (int y = 0; y < 2; y++)
        {
            for (int z = 0; z < 2; z++)
            {
                for (int x = 0; x < 2; x++)
                {
                    corners[write++] = new Vector3(
                        (x == 0 ? -1f : 1f) + Jitter(rng, jitter),
                        (y == 0 ? -1f : 1f) + Jitter(rng, jitter),
                        (z == 0 ? -1f : 1f) + Jitter(rng, jitter));
                }
            }
        }

        int[] quads =
        {
            0, 1, 3, 2,
            4, 6, 7, 5,
            2, 3, 7, 6,
            1, 0, 4, 5,
            1, 3, 7, 5,
            2, 0, 4, 6
        };
        Vector3[] hints =
        {
            Vector3.down, Vector3.up, Vector3.forward, Vector3.back, Vector3.right, Vector3.left
        };
        for (int face = 0; face < 6; face++)
        {
            int o = face * 4;
            Vector3 a = corners[quads[o]];
            Vector3 b = corners[quads[o + 1]];
            Vector3 c = corners[quads[o + 2]];
            Vector3 d = corners[quads[o + 3]];
            Vector3 center = (a + b + c + d) * 0.25f + hints[face] * (0.28f + (float)rng.NextDouble() * 0.22f);
            int ia = AddPlain(vertices, a);
            int ib = AddPlain(vertices, b);
            int ic = AddPlain(vertices, c);
            int id = AddPlain(vertices, d);
            int im = AddPlain(vertices, center);
            AddFacing(triangles, vertices, ia, ib, im, hints[face], null);
            AddFacing(triangles, vertices, ib, ic, im, hints[face], null);
            AddFacing(triangles, vertices, ic, id, im, hints[face], null);
            AddFacing(triangles, vertices, id, ia, im, hints[face], null);
        }

        return Paint(vertices, triangles, color, seed, "FloatRock");
    }

    static float Jitter(System.Random rng, float amount)
    {
        return ((float)rng.NextDouble() - 0.5f) * amount * 2f;
    }

    static int AddPlain(List<Vector3> vertices, Vector3 position)
    {
        vertices.Add(position);
        return vertices.Count - 1;
    }

    static void AddFacing(List<int> triangles, List<Vector3> vertices, int a, int b, int c, Vector3 outward, Dictionary<long, int> boundary)
    {
        Vector3 normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
        if (Vector3.Dot(normal, outward) < 0f)
        {
            int swap = b;
            b = c;
            c = swap;
        }

        triangles.Add(a);
        triangles.Add(b);
        triangles.Add(c);
        if (boundary == null)
            return;

        NoteEdge(boundary, a, b);
        NoteEdge(boundary, b, c);
        NoteEdge(boundary, c, a);
    }

    static void NoteEdge(Dictionary<long, int> boundary, int from, int to)
    {
        long forward = ((long)from << 32) | (uint)to;
        long back = ((long)to << 32) | (uint)from;
        if (boundary.ContainsKey(back))
        {
            boundary.Remove(back);
            return;
        }

        boundary[forward] = to;
    }

    static List<List<int>> WalkLoops(Dictionary<long, int> boundary)
    {
        var nexts = new Dictionary<int, List<int>>();
        foreach (var pair in boundary)
        {
            int from = (int)(pair.Key >> 32);
            if (!nexts.TryGetValue(from, out List<int> list))
            {
                list = new List<int>(2);
                nexts[from] = list;
            }

            list.Add(pair.Value);
        }

        var loops = new List<List<int>>();
        while (nexts.Count > 0)
        {
            int start = 0;
            foreach (int key in nexts.Keys)
            {
                start = key;
                break;
            }

            var loop = new List<int>();
            int current = start;
            int guard = 0;
            while (guard++ < 100000 && nexts.TryGetValue(current, out List<int> list) && list.Count > 0)
            {
                loop.Add(current);
                int next = list[list.Count - 1];
                list.RemoveAt(list.Count - 1);
                if (list.Count == 0)
                    nexts.Remove(current);
                if (next == start)
                    break;
                current = next;
            }

            if (loop.Count >= 3)
                loops.Add(loop);
        }

        return loops;
    }

    static Mesh Paint(List<Vector3> vertices, List<int> triangles, Color color, int seed, string name)
    {
        int count = triangles.Count;
        var flatVertices = new Vector3[count];
        var flatColors = new Color[count];
        var flatTriangles = new int[count];
        for (int i = 0; i < count; i += 3)
        {
            float shift = (Hash(seed, i) - 0.5f) * 0.08f;
            Color face = new Color(
                Mathf.Clamp01(color.r + shift),
                Mathf.Clamp01(color.g + shift * 0.6f),
                Mathf.Clamp01(color.b + shift * 0.4f));
            flatVertices[i] = vertices[triangles[i]];
            flatVertices[i + 1] = vertices[triangles[i + 1]];
            flatVertices[i + 2] = vertices[triangles[i + 2]];
            flatColors[i] = face;
            flatColors[i + 1] = face;
            flatColors[i + 2] = face;
            flatTriangles[i] = i;
            flatTriangles[i + 1] = i + 1;
            flatTriangles[i + 2] = i + 2;
        }

        return FinishFlat(name, flatVertices, flatColors, flatTriangles);
    }

    static Mesh FinishFlat(string name, Vector3[] vertices, Color[] colors, int[] triangles)
    {
        var mesh = new Mesh();
        mesh.name = name;
        if (vertices.Length > 65535)
            mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static int AddVert(List<Vector3> vertices, List<byte> stone, Vector3 position, bool rock)
    {
        vertices.Add(position);
        stone.Add(rock ? (byte)1 : (byte)0);
        return vertices.Count - 1;
    }

    static IslandData PieceData(IslandData source, bool features)
    {
        var copy = new IslandData();
        copy.biome = source.biome;
        copy.size = source.size;
        copy.terrainStyle = string.IsNullOrEmpty(source.terrainStyle) ? "rolling" : source.terrainStyle;
        copy.heightScale = source.heightScale;
        copy.roughness = source.roughness;
        copy.verticalSpread = source.verticalSpread;
        copy.features = features && source.features != null ? source.features : new List<string>();
        copy.hasLandmark = false;
        copy.landmarkType = "none";
        copy.landmarkName = "";
        copy.landmarkHint = "";
        copy.landmarkLore = "";
        return copy;
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

    static float Peak(string style)
    {
        if (style == "mountainous")
            return 52f;
        if (style == "plateau")
            return 30f;
        if (style == "canyon")
            return 22f;
        return 14f;
    }

    static float RimLow(string style)
    {
        if (style == "plateau")
            return 16f;
        return 0f;
    }

    static Color TopColor(string biome, int seed, int index)
    {
        float shift = (Hash(seed, index) - 0.5f) * 0.12f;
        if (biome == "snow")
            return Vary(WorldLook.Snow, shift);
        if (biome == "volcanic")
            return Vary(Color.Lerp(WorldLook.Rock * 0.5f, WorldLook.Accent, 0.22f), shift);
        return Vary(WorldLook.Grass, shift);
    }

    static Color RockColor(string biome, int seed, int index)
    {
        float shift = (Hash(seed, index) - 0.5f) * 0.08f;
        if (biome == "snow")
            return Vary(Color.Lerp(WorldLook.Rock, WorldLook.Snow, 0.42f), shift);
        if (biome == "volcanic")
            return Vary(WorldLook.Rock * 0.62f, shift);
        return Vary(WorldLook.Rock, shift);
    }

    static Color Vary(Color color, float shift)
    {
        return new Color(
            Mathf.Clamp01(color.r + shift),
            Mathf.Clamp01(color.g + shift * 0.65f),
            Mathf.Clamp01(color.b + shift * 0.4f),
            1f);
    }

    static Material VertexMaterial()
    {
        if (vertexMaterial != null)
            return vertexMaterial;

        Shader shader = Shader.Find("Skybound/VertexColor");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        vertexMaterial = new Material(shader);
        vertexMaterial.name = "FloatingIsland";
        vertexMaterial.color = Color.white;
        if (vertexMaterial.HasProperty("_BaseColor"))
            vertexMaterial.SetColor("_BaseColor", Color.white);
        return vertexMaterial;
    }

    static Material DropMaterial()
    {
        if (dropMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");

            dropMaterial = new Material(shader);
            dropMaterial.name = "Waterfall";
        }

        Color color = Color.Lerp(WorldLook.Water, Color.white, 0.45f);
        color.a = 0.72f;
        dropMaterial.color = color;
        if (dropMaterial.HasProperty("_BaseColor"))
            dropMaterial.SetColor("_BaseColor", color);
        if (dropMaterial.HasProperty("_Color"))
            dropMaterial.SetColor("_Color", color);
        return dropMaterial;
    }

    static void ScalePeaks(List<Body> bodies, float scale)
    {
        if (bodies == null || Mathf.Abs(scale - 1f) < 0.001f)
            return;

        for (int i = 0; i < bodies.Count; i++)
        {
            bodies[i].peak *= scale;
            FitVertical(bodies[i]);
        }
    }

    static void ApplyVerticalSpread(List<Body> bodies, float spread)
    {
        if (bodies == null || bodies.Count == 0 || Mathf.Abs(spread - 1f) < 0.001f)
            return;

        float anchor = bodies[0].baseY;
        for (int i = 0; i < bodies.Count; i++)
        {
            if (!bodies[i].main)
                continue;
            anchor = bodies[i].baseY;
            break;
        }

        for (int i = 0; i < bodies.Count; i++)
        {
            Body body = bodies[i];
            body.baseY = anchor + (body.baseY - anchor) * spread;
            FitVertical(body);
        }
    }

    static void ClampInside(Body body, float spotRadius)
    {
        float max = Mathf.Max(0f, spotRadius - body.radius);
        float dist = body.center.magnitude;
        if (dist > max && dist > 0.001f)
            body.center *= max / dist;
    }

    static Vector2 RandomPoint(System.Random rng, float radius)
    {
        float angle = Unit(rng) * Mathf.PI * 2f;
        float dist = Mathf.Sqrt(Unit(rng)) * Mathf.Max(0f, radius);
        return new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);
    }

    static float Unit(System.Random rng)
    {
        return (float)rng.NextDouble();
    }

    static float Hash(int seed, int index)
    {
        unchecked
        {
            int n = seed * 374761393 + index * 668265263;
            n = (n ^ (n >> 13)) * 1274126177;
            return (n & 65535) / 65535f;
        }
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

