using System;
using System.Collections.Generic;
using UnityEngine;

public struct IslandSpot
{
    public Vector3 center;
    public float radius;
}

public static class SpotGenerator
{
    public const float WorldRadius = 650f;
    public const float MinEdgeGap = 120f;

    const int AttemptsPerTry = 80;
    const int MaxShrinks = 8;
    const float ShrinkFactor = 0.85f;
    const float MinRadius = 18f;
    const float FitSlack = 0.05f;
    const float RingAngleJitter = 0.07f;
    const float RingRadialJitter = 10f;

    public static float RadiusForSize(string size)
    {
        if (size == "small")
            return 50f;
        if (size == "large")
            return 120f;
        return 80f;
    }

    public static List<IslandSpot> Generate(WorldData data)
    {
        var spots = new List<IslandSpot>();
        if (data == null || data.islands == null || data.islands.Count == 0)
            return spots;

        var rng = new System.Random(data.seed);
        int count = data.islands.Count;
        var radii = new float[count];
        for (int i = 0; i < count; i++)
        {
            IslandData island = data.islands[i];
            radii[i] = RadiusForSize(island != null ? island.size : "medium");
        }

        string layout = string.IsNullOrEmpty(data.layout) ? "scattered" : data.layout.Trim();
        IslandSpot[] placed = layout == "central" ? PlaceCentral(radii, rng)
            : layout == "ring" ? PlaceRing(radii, rng)
            : PlaceScattered(radii, rng);

        spots.AddRange(placed);
        return spots;
    }

    static IslandSpot[] PlaceScattered(float[] radii, System.Random rng)
    {
        var spots = new IslandSpot[radii.Length];
        var placed = new List<int>(radii.Length);
        int[] order = LargestFirst(radii);

        for (int i = 0; i < order.Length; i++)
        {
            int index = order[i];
            spots[index] = PlaceWithShrink(radii[index], spots, placed, rng, SampleDisk);
            placed.Add(index);
        }

        return spots;
    }

    static IslandSpot[] PlaceCentral(float[] radii, System.Random rng)
    {
        var spots = new IslandSpot[radii.Length];
        int centerIndex = IndexOfMax(radii);
        float centerRadius = Mathf.Min(radii[centerIndex], WorldRadius);
        spots[centerIndex] = MakeSpot(Vector3.zero, centerRadius);

        var placed = new List<int> { centerIndex };
        int[] order = LargestFirst(radii);
        for (int i = 0; i < order.Length; i++)
        {
            int index = order[i];
            if (index == centerIndex)
                continue;

            float anchor = spots[centerIndex].radius;
            spots[index] = PlaceWithShrink(radii[index], spots, placed, rng,
                (random, radius) => SampleAnnulus(random, anchor, radius));
            placed.Add(index);
        }

        return spots;
    }

    static IslandSpot[] PlaceRing(float[] radii, System.Random rng)
    {
        int count = radii.Length;
        var spots = new IslandSpot[count];
        if (count == 1)
        {
            spots[0] = MakeSpot(Vector3.zero, Mathf.Min(radii[0], WorldRadius));
            return spots;
        }

        var fitted = (float[])radii.Clone();
        FitRadiiToRing(fitted);
        float ringRadius = ComfortableRingRadius(fitted);

        var placed = new List<int>(count);
        for (int i = 0; i < count; i++)
        {
            float baseAngle = i * Mathf.PI * 2f / count;
            float radius = fitted[i];
            Vector3 chosen = SlotOnRing(baseAngle, ringRadius, radius);
            bool found = false;

            for (int attempt = 0; attempt < 20; attempt++)
            {
                float angle = baseAngle + NextRange(rng, -RingAngleJitter, RingAngleJitter);
                float radial = ringRadius + NextRange(rng, -RingRadialJitter, RingRadialJitter);
                float maxDist = Mathf.Max(0f, WorldRadius - radius);
                radial = Mathf.Clamp(radial, 0f, maxDist);
                Vector3 candidate = new Vector3(Mathf.Cos(angle) * radial, 0f, Mathf.Sin(angle) * radial);
                if (!IsValid(candidate, radius, spots, placed))
                    continue;

                chosen = candidate;
                found = true;
                break;
            }

            if (!found)
            {
                for (int shrink = 0; shrink <= MaxShrinks && !IsValid(chosen, radius, spots, placed); shrink++)
                {
                    if (radius <= MinRadius)
                        break;

                    radius = Mathf.Max(MinRadius, radius * ShrinkFactor);
                    chosen = SlotOnRing(baseAngle, ringRadius, radius);
                }
            }

            spots[i] = MakeSpot(chosen, radius);
            placed.Add(i);
        }

        return spots;
    }

    static IslandSpot PlaceWithShrink(float startingRadius, IslandSpot[] spots, List<int> placed, System.Random rng, Func<System.Random, float, Vector3> sample)
    {
        float radius = Mathf.Clamp(startingRadius, MinRadius, WorldRadius);

        for (int shrink = 0; shrink <= MaxShrinks; shrink++)
        {
            if (TryPickBest(rng, radius, spots, placed, sample, out Vector3 center))
                return MakeSpot(center, radius);

            if (radius <= MinRadius + 0.001f)
                break;

            radius = Mathf.Max(MinRadius, radius * ShrinkFactor);
        }

        Vector3 fallback = Vector3.zero;
        for (int shrink = 0; shrink <= MaxShrinks; shrink++)
        {
            fallback = SearchLeastOverlap(rng, radius, spots, placed);
            if (OverlapAmount(fallback, radius, spots, placed) <= FitSlack)
                break;

            if (radius <= MinRadius + 0.001f)
                break;

            radius = Mathf.Max(MinRadius, radius * ShrinkFactor);
        }

        return MakeSpot(fallback, radius);
    }

    static bool TryPickBest(System.Random rng, float radius, IslandSpot[] spots, List<int> placed, Func<System.Random, float, Vector3> sample, out Vector3 center)
    {
        center = Vector3.zero;
        float bestScore = float.NegativeInfinity;
        bool found = false;

        for (int attempt = 0; attempt < AttemptsPerTry; attempt++)
        {
            Vector3 candidate = sample(rng, radius);
            candidate.y = 0f;
            if (!IsValid(candidate, radius, spots, placed))
                continue;

            float score = Isolation(candidate, radius, spots, placed);
            if (found && score <= bestScore)
                continue;

            found = true;
            bestScore = score;
            center = candidate;
        }

        return found;
    }

    static Vector3 SearchLeastOverlap(System.Random rng, float radius, IslandSpot[] spots, List<int> placed)
    {
        Vector3 best = Vector3.zero;
        float bestPenalty = float.MaxValue;

        for (int attempt = 0; attempt < AttemptsPerTry; attempt++)
        {
            Vector3 candidate = PushInside(SampleDisk(rng, radius), radius);
            float penalty = OverlapAmount(candidate, radius, spots, placed);
            if (penalty >= bestPenalty)
                continue;

            bestPenalty = penalty;
            best = candidate;
            if (penalty <= FitSlack)
                break;
        }

        return best;
    }

    static void FitRadiiToRing(float[] radii)
    {
        for (int guard = 0; guard < 24; guard++)
        {
            float required = RequiredRingRadius(radii);
            float maxRing = WorldRadius - MaxValue(radii);
            if (maxRing >= 0f && required <= maxRing + FitSlack)
                return;

            int largest = IndexOfMax(radii);
            if (radii[largest] <= MinRadius)
            {
                for (int i = 0; i < radii.Length; i++)
                    radii[i] = Mathf.Max(8f, radii[i] * ShrinkFactor);
                continue;
            }

            radii[largest] = Mathf.Max(MinRadius, radii[largest] * ShrinkFactor);
        }
    }

    static float RequiredRingRadius(float[] radii)
    {
        int count = radii.Length;
        float required = 0f;
        for (int i = 0; i < count; i++)
        {
            for (int j = i + 1; j < count; j++)
            {
                int steps = Mathf.Min(j - i, count - (j - i));
                float sine = Mathf.Sin(Mathf.PI * steps / count);
                if (sine < 0.0001f)
                    continue;

                float need = (radii[i] + radii[j] + MinEdgeGap) / (2f * sine);
                if (need > required)
                    required = need;
            }
        }

        return required;
    }

    static float ComfortableRingRadius(float[] radii)
    {
        float required = RequiredRingRadius(radii);
        float maxRing = Mathf.Max(0f, WorldRadius - MaxValue(radii));
        if (required > maxRing)
            return maxRing;

        return Mathf.Lerp(required, maxRing, 0.25f);
    }

    static Vector3 SlotOnRing(float angle, float ringRadius, float islandRadius)
    {
        float maxDist = Mathf.Max(0f, WorldRadius - islandRadius);
        float dist = Mathf.Clamp(ringRadius, 0f, maxDist);
        return new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
    }

    static Vector3 SampleDisk(System.Random rng, float radius)
    {
        float maxDist = Mathf.Max(0f, WorldRadius - radius);
        float angle = (float)(rng.NextDouble() * Math.PI * 2.0);
        float dist = Mathf.Sqrt((float)rng.NextDouble()) * maxDist;
        return new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
    }

    static Vector3 SampleAnnulus(System.Random rng, float centerRadius, float radius)
    {
        float angle = (float)(rng.NextDouble() * Math.PI * 2.0);
        float unit = (float)rng.NextDouble();
        float outer = WorldRadius - radius;
        if (outer < 0f)
            return Vector3.zero;

        float inner = centerRadius + radius + MinEdgeGap;
        if (inner > outer)
            inner = outer;

        float dist = Mathf.Sqrt(Mathf.Lerp(inner * inner, outer * outer, unit));
        return new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
    }

    static bool IsValid(Vector3 center, float radius, IslandSpot[] spots, List<int> placed)
    {
        if (center.magnitude + radius > WorldRadius + FitSlack)
            return false;

        for (int i = 0; i < placed.Count; i++)
        {
            IslandSpot other = spots[placed[i]];
            float minDist = radius + other.radius + MinEdgeGap - FitSlack;
            if ((center - other.center).sqrMagnitude < minDist * minDist)
                return false;
        }

        return true;
    }

    static float OverlapAmount(Vector3 center, float radius, IslandSpot[] spots, List<int> placed)
    {
        float amount = Mathf.Max(0f, center.magnitude + radius - WorldRadius);
        for (int i = 0; i < placed.Count; i++)
        {
            IslandSpot other = spots[placed[i]];
            float minDist = radius + other.radius + MinEdgeGap;
            float dist = Vector3.Distance(center, other.center);
            amount += Mathf.Max(0f, minDist - dist);
        }

        return amount;
    }

    static float Isolation(Vector3 center, float radius, IslandSpot[] spots, List<int> placed)
    {
        if (placed.Count == 0)
            return 0f;

        float nearest = float.PositiveInfinity;
        for (int i = 0; i < placed.Count; i++)
        {
            IslandSpot other = spots[placed[i]];
            float gap = Vector3.Distance(center, other.center) - radius - other.radius;
            if (gap < nearest)
                nearest = gap;
        }

        return nearest;
    }

    static Vector3 PushInside(Vector3 center, float radius)
    {
        center.y = 0f;
        float maxDist = Mathf.Max(0f, WorldRadius - radius);
        float dist = center.magnitude;
        if (dist <= maxDist || dist < 0.0001f)
            return center;

        Vector3 direction = center / dist;
        return new Vector3(direction.x * maxDist, 0f, direction.z * maxDist);
    }

    static IslandSpot MakeSpot(Vector3 center, float radius)
    {
        center.y = 0f;
        return new IslandSpot { center = center, radius = radius };
    }

    static int[] LargestFirst(float[] radii)
    {
        var order = new int[radii.Length];
        for (int i = 0; i < order.Length; i++)
            order[i] = i;

        Array.Sort(order, (a, b) =>
        {
            int compare = radii[b].CompareTo(radii[a]);
            return compare != 0 ? compare : a.CompareTo(b);
        });
        return order;
    }

    static int IndexOfMax(float[] values)
    {
        int index = 0;
        for (int i = 1; i < values.Length; i++)
        {
            if (values[i] > values[index])
                index = i;
        }

        return index;
    }

    static float MaxValue(float[] values)
    {
        return values[IndexOfMax(values)];
    }

    static float NextRange(System.Random rng, float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }
}
