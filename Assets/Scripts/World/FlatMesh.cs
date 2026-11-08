using System.Collections.Generic;
using UnityEngine;

public class FlatMesh
{
    readonly List<Vector3> vertices = new List<Vector3>(512);
    readonly List<int> triangles = new List<int>(768);

    public int VertexCount => vertices.Count;

    public int TriangleCount => triangles.Count;

    public void Box(Vector3 center, Vector3 size, Quaternion rotation)
    {
        int triangleStart = triangles.Count;
        Vector3 half = size * 0.5f;
        Vector3 P(float x, float y, float z)
        {
            return center + rotation * new Vector3(x * half.x, y * half.y, z * half.z);
        }

        Vector3 nnn = P(-1f, -1f, -1f);
        Vector3 pnn = P(1f, -1f, -1f);
        Vector3 npn = P(-1f, 1f, -1f);
        Vector3 ppn = P(1f, 1f, -1f);
        Vector3 nnp = P(-1f, -1f, 1f);
        Vector3 pnp = P(1f, -1f, 1f);
        Vector3 npp = P(-1f, 1f, 1f);
        Vector3 ppp = P(1f, 1f, 1f);

        Quad(npn, npp, ppp, ppn);
        Quad(nnn, pnn, pnp, nnp);
        Quad(nnp, pnp, ppp, npp);
        Quad(nnn, npn, ppn, pnn);
        Quad(pnn, ppn, ppp, pnp);
        Quad(nnn, nnp, npp, npn);
        OrientFrom(triangleStart, center);
    }

    public void Panel(Vector3 root, Vector3 tip, float rootChord, float tipChord, float rootThick, float tipThick, bool capRoot, bool capTip)
    {
        Vector3 span = tip - root;
        float length = span.magnitude;
        if (length < 0.0001f)
            return;

        int triangleStart = triangles.Count;
        Vector3 spanDir = span / length;
        Vector3 chordDir = Vector3.ProjectOnPlane(Vector3.forward, spanDir);
        if (chordDir.sqrMagnitude < 1e-6f)
            chordDir = Vector3.ProjectOnPlane(Vector3.up, spanDir);
        chordDir.Normalize();
        Vector3 thickDir = Vector3.Cross(chordDir, spanDir).normalized;

        Vector3 V(float along, float thickSign, float chordSign)
        {
            float t = along / length;
            float thick = Mathf.Lerp(rootThick, tipThick, t) * 0.5f;
            float chord = Mathf.Lerp(rootChord, tipChord, t) * 0.5f;
            return root + spanDir * along + thickDir * (thickSign * thick) + chordDir * (chordSign * chord);
        }

        Vector3 rymz = V(0f, 1f, -1f);
        Vector3 tymz = V(length, 1f, -1f);
        Vector3 typz = V(length, 1f, 1f);
        Vector3 rypz = V(0f, 1f, 1f);
        Vector3 rnmz = V(0f, -1f, -1f);
        Vector3 tnmz = V(length, -1f, -1f);
        Vector3 tnpz = V(length, -1f, 1f);
        Vector3 rnpz = V(0f, -1f, 1f);

        Quad(rymz, rypz, typz, tymz);
        Quad(rnmz, tnmz, tnpz, rnpz);
        Quad(rnpz, tnpz, typz, rypz);
        Quad(rnmz, rymz, tymz, tnmz);
        if (capTip)
            Quad(tnmz, tymz, typz, tnpz);
        if (capRoot)
            Quad(rnmz, rnpz, rypz, rymz);
        OrientFrom(triangleStart, (root + tip) * 0.5f);
    }

    public void LoftZ(Vector3[] centers, float[] radiusX, float[] radiusY, int sides)
    {
        if (centers == null || centers.Length < 2)
            return;

        int triangleStart = triangles.Count;
        sides = Mathf.Max(3, sides);
        int rings = centers.Length;
        var ring = new Vector3[rings, sides];
        for (int r = 0; r < rings; r++)
        {
            float rx = Mathf.Max(0.001f, radiusX[r]);
            float ry = Mathf.Max(0.001f, radiusY[r]);
            for (int s = 0; s < sides; s++)
            {
                float angle = (s + 0.5f) * Mathf.PI * 2f / sides;
                ring[r, s] = centers[r] + new Vector3(Mathf.Cos(angle) * rx, Mathf.Sin(angle) * ry, 0f);
            }
        }

        for (int r = 0; r < rings - 1; r++)
        {
            for (int s = 0; s < sides; s++)
            {
                int next = (s + 1) % sides;
                Quad(ring[r, s], ring[r, next], ring[r + 1, next], ring[r + 1, s]);
            }
        }

        for (int s = 0; s < sides; s++)
        {
            int next = (s + 1) % sides;
            Triangle(centers[0], ring[0, next], ring[0, s]);
            Triangle(centers[rings - 1], ring[rings - 1, s], ring[rings - 1, next]);
        }

        Vector3 mid = Vector3.zero;
        for (int r = 0; r < rings; r++)
            mid += centers[r];
        OrientFrom(triangleStart, mid / rings);
    }

    public void Frustum(Vector3 start, Vector3 end, float radiusStart, float radiusEnd, int sides)
    {
        Vector3 axis = end - start;
        float length = axis.magnitude;
        if (length < 0.0001f)
            return;

        int startVertex = vertices.Count;
        LoftZ(
            new[] { Vector3.zero, new Vector3(0f, 0f, length) },
            new[] { radiusStart, radiusEnd },
            new[] { radiusStart, radiusEnd },
            sides);
        Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, axis / length);
        TransformFrom(startVertex, Matrix4x4.TRS(start, rotation, Vector3.one));
    }

    public void Cone(Vector3 baseCenter, Vector3 tip, float radius, int sides)
    {
        Frustum(baseCenter, tip, radius, 0.001f, sides);
    }

    public void Ball(Vector3 center, float radiusX, float radiusY, float radiusZ, int sides, int bands)
    {
        bands = Mathf.Max(3, bands);
        sides = Mathf.Max(3, sides);
        int start = vertices.Count;
        var centers = new Vector3[bands];
        var rx = new float[bands];
        var ry = new float[bands];
        for (int i = 0; i < bands; i++)
        {
            float t = i / (float)(bands - 1);
            float lat = Mathf.Lerp(-Mathf.PI * 0.5f + 0.18f, Mathf.PI * 0.5f - 0.18f, t);
            float ring = Mathf.Cos(lat);
            centers[i] = new Vector3(0f, 0f, Mathf.Sin(lat));
            rx[i] = Mathf.Max(0.05f, ring);
            ry[i] = rx[i];
        }

        LoftZ(centers, rx, ry, sides);
        TransformFrom(start, Matrix4x4.TRS(center, Quaternion.Euler(-90f, 0f, 0f), new Vector3(radiusX, radiusZ, radiusY)));
    }

    public void Crystal(Vector3 basePoint, float height, float radius, Quaternion rotation, int sides)
    {
        int startVertex = vertices.Count;
        float h0 = height * 0.16f;
        float h1 = height * 0.58f;
        LoftZ(
            new[]
            {
                Vector3.zero,
                new Vector3(0f, 0f, h0),
                new Vector3(0f, 0f, h1),
                new Vector3(0f, 0f, height)
            },
            new[] { 0.02f, radius * 0.55f, radius, 0.02f },
            new[] { 0.02f, radius * 0.55f, radius, 0.02f },
            sides);
        Quaternion upright = rotation * Quaternion.Euler(-90f, 0f, 0f);
        TransformFrom(startVertex, Matrix4x4.TRS(basePoint, upright, Vector3.one));
    }

    public void TransformFrom(int start, Matrix4x4 matrix)
    {
        for (int i = Mathf.Max(0, start); i < vertices.Count; i++)
            vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
    }

    public Mesh Build(string name)
    {
        var mesh = new Mesh();
        mesh.name = string.IsNullOrEmpty(name) ? "FlatMesh" : name;
        if (vertices.Count < 3 || triangles.Count < 3)
        {
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = new Vector2[vertices.Count];
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        Triangle(a, b, c);
        Triangle(a, c, d);
    }

    void Triangle(Vector3 a, Vector3 b, Vector3 c)
    {
        int index = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        triangles.Add(index);
        triangles.Add(index + 1);
        triangles.Add(index + 2);
    }

    void OrientFrom(int triangleStart, Vector3 center)
    {
        for (int i = triangleStart; i < triangles.Count; i += 3)
        {
            Vector3 a = vertices[triangles[i]];
            Vector3 b = vertices[triangles[i + 1]];
            Vector3 c = vertices[triangles[i + 2]];
            Vector3 normal = Vector3.Cross(b - a, c - a);
            Vector3 face = (a + b + c) / 3f;
            if (Vector3.Dot(normal, face - center) >= 0f)
                continue;

            int swap = triangles[i + 1];
            triangles[i + 1] = triangles[i + 2];
            triangles[i + 2] = swap;
        }
    }
}
