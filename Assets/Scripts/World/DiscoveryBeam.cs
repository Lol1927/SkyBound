using UnityEngine;

public class DiscoveryBeam : MonoBehaviour
{
    const float Height = 96f;
    const float GrowTime = 0.42f;
    const float HoldTime = 2.35f;
    const float Duration = 3.7f;

    Material material;
    Mesh mesh;
    Color accent;
    float age;
    bool playing;

    public void Begin(Color color)
    {
        if (playing)
            return;

        playing = true;
        accent = color.maxColorComponent < 0.08f ? WorldLook.DefaultAccent : color;
        accent.a = 1f;

        mesh = BuildMesh();
        material = CreateMaterial(accent);

        var filter = gameObject.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        transform.localScale = new Vector3(1f, 0.02f, 1f);
    }

    void Update()
    {
        if (!playing)
            return;

        age += Time.deltaTime;
        float grow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / GrowTime));
        float fade = age <= HoldTime ? 1f : 1f - Mathf.Clamp01((age - HoldTime) / (Duration - HoldTime));
        transform.localScale = new Vector3(1f, grow, 1f);
        if (material != null)
        {
            Color color = Color.Lerp(accent, Color.white, 0.35f);
            color.a = Mathf.Clamp01(fade);
            material.SetColor("_BaseColor", color);
            material.color = color;
        }

        if (age >= Duration)
            Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (mesh != null)
            Destroy(mesh);
        if (material != null)
            Destroy(material);
    }

    static Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Skybound/Beam");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        var beam = new Material(shader);
        beam.name = "Discovery Beam";
        color.a = 1f;
        beam.color = color;
        if (beam.HasProperty("_BaseColor"))
            beam.SetColor("_BaseColor", color);
        return beam;
    }

    static Mesh BuildMesh()
    {
        var vertices = new System.Collections.Generic.List<Vector3>();
        var colors = new System.Collections.Generic.List<Color>();
        var triangles = new System.Collections.Generic.List<int>();
        AddCross(vertices, colors, triangles, 1.7f, new Color(0.72f, 0.9f, 1f, 0.42f));
        AddCross(vertices, colors, triangles, 0.38f, new Color(1f, 1f, 1f, 1f));

        var mesh = new Mesh();
        mesh.name = "Discovery Beam";
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    static void AddCross(System.Collections.Generic.List<Vector3> vertices, System.Collections.Generic.List<Color> colors, System.Collections.Generic.List<int> triangles, float width, Color tint)
    {
        for (int i = 0; i < 3; i++)
            AddCard(vertices, colors, triangles, i * 60f, width, tint);
    }

    static void AddCard(System.Collections.Generic.List<Vector3> vertices, System.Collections.Generic.List<Color> colors, System.Collections.Generic.List<int> triangles, float yaw, float width, Color tint)
    {
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 right = rotation * Vector3.right * width;
        int index = vertices.Count;
        vertices.Add(-right);
        vertices.Add(right);
        vertices.Add(right + Vector3.up * Height);
        vertices.Add(-right + Vector3.up * Height);

        Color top = tint;
        top.a = 0f;
        colors.Add(tint);
        colors.Add(tint);
        colors.Add(top);
        colors.Add(top);

        triangles.Add(index);
        triangles.Add(index + 2);
        triangles.Add(index + 1);
        triangles.Add(index);
        triangles.Add(index + 3);
        triangles.Add(index + 2);
    }
}
