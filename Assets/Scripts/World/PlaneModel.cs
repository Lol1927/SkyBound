using UnityEngine;

public static class PlaneModel
{
    struct Airfoil
    {
        public Vector3 root;
        public Vector3 tip;
        public float rootChord;
        public float tipChord;
        public float rootThick;
        public float tipThick;

        public Vector3 At(float t)
        {
            return Vector3.Lerp(root, tip, t);
        }

        public float Chord(float t)
        {
            return Mathf.Lerp(rootChord, tipChord, t);
        }

        public float Thick(float t)
        {
            return Mathf.Lerp(rootThick, tipThick, t);
        }
    }

    static readonly float[] BodyZ = { -1.72f, -1.15f, -0.4f, 0.3f, 0.95f, 1.38f, 1.74f };
    static readonly float[] BodyX = { 0.07f, 0.3f, 0.46f, 0.52f, 0.42f, 0.28f, 0.08f };
    static readonly float[] BodyYRadius = { 0.07f, 0.28f, 0.42f, 0.48f, 0.38f, 0.24f, 0.08f };

    const float BodyLift = 0.05f;

    public static GameObject Create(ModelKit kit)
    {
        var root = new GameObject("PlaneVisual");
        var body = new FlatMesh();
        body.LoftZ(BodyCenters(0f), BodyX, BodyYRadius, 8);
        ModelLibrary.Part(root.transform, "Fuselage", body.Build("Fuselage"), kit.body, true);

        var stripe = new FlatMesh();
        Spine(stripe, -1.5f, 0.02f);
        ModelLibrary.Part(root.transform, "Stripe", stripe.Build("Stripe"), kit.stripe, true);

        var accent = new FlatMesh();
        Cowling(accent);
        ModelLibrary.Part(root.transform, "Cowling", accent.Build("Cowling"), kit.accent, true);

        var glass = new FlatMesh();
        Canopy(glass);
        ModelLibrary.Part(root.transform, "Cockpit", glass.Build("Cockpit"), kit.glass, false);

        Airfoil wing = WingSpec(1f);
        var wingBody = new FlatMesh();
        var wingTip = new FlatMesh();
        var wingStripe = new FlatMesh();
        AddWing(wingBody, wingTip, wingStripe, wing);
        AddWing(wingBody, wingTip, wingStripe, WingSpec(-1f));
        ModelLibrary.Part(root.transform, "Wings", wingBody.Build("Wings"), kit.body, true);
        ModelLibrary.Part(root.transform, "WingTips", wingTip.Build("WingTips"), kit.accent, true);
        ModelLibrary.Part(root.transform, "WingStripe", wingStripe.Build("WingStripe"), kit.stripe, true);

        var tail = new FlatMesh();
        var finTip = new FlatMesh();
        AddTail(tail, finTip);
        ModelLibrary.Part(root.transform, "Tail", tail.Build("Tail"), kit.body, true);
        ModelLibrary.Part(root.transform, "FinTip", finTip.Build("FinTip"), kit.accent, true);

        var gear = new FlatMesh();
        var hubs = new FlatMesh();
        AddGear(gear, hubs);
        ModelLibrary.Part(root.transform, "Gear", gear.Build("Gear"), kit.dark, true);
        ModelLibrary.Part(root.transform, "Hubs", hubs.Build("Hubs"), kit.gold, true);

        AddAileron(root.transform, wing, "AileronRight", 1f, kit.body);
        AddAileron(root.transform, WingSpec(-1f), "AileronLeft", -1f, kit.body);
        AddPropeller(root.transform, kit);
        return root;
    }

    static Vector3[] BodyCenters(float lift)
    {
        var centers = new Vector3[BodyZ.Length];
        for (int i = 0; i < BodyZ.Length; i++)
            centers[i] = new Vector3(0f, BodyLift + lift, BodyZ[i]);
        return centers;
    }

    static float SampleRadiusY(float z)
    {
        if (z <= BodyZ[0])
            return BodyYRadius[0];
        for (int i = 1; i < BodyZ.Length; i++)
        {
            if (z > BodyZ[i])
                continue;
            float t = Mathf.InverseLerp(BodyZ[i - 1], BodyZ[i], z);
            return Mathf.Lerp(BodyYRadius[i - 1], BodyYRadius[i], t);
        }

        return BodyYRadius[BodyYRadius.Length - 1];
    }

    static void Spine(FlatMesh mesh, float fromZ, float toZ)
    {
        const int steps = 5;
        var centers = new Vector3[steps];
        var rx = new float[steps];
        var ry = new float[steps];
        for (int i = 0; i < steps; i++)
        {
            float t = i / (steps - 1f);
            float z = Mathf.Lerp(fromZ, toZ, t);
            centers[i] = new Vector3(0f, BodyLift + SampleRadiusY(z) + 0.04f, z);
            rx[i] = 0.12f;
            ry[i] = 0.022f;
        }

        mesh.LoftZ(centers, rx, ry, 6);
    }

    static void Cowling(FlatMesh mesh)
    {
        float[] z = { 1.22f, 1.48f, 1.78f };
        var centers = new Vector3[z.Length];
        var rx = new float[z.Length];
        var ry = new float[z.Length];
        for (int i = 0; i < z.Length; i++)
        {
            centers[i] = new Vector3(0f, BodyLift, z[i]);
            rx[i] = SampleRadiusY(z[i]) * 0.92f + 0.05f;
            ry[i] = SampleRadiusY(z[i]) + 0.045f;
        }

        mesh.LoftZ(centers, rx, ry, 8);
    }

    static void Canopy(FlatMesh mesh)
    {
        float[] z = { 1.16f, 0.86f, 0.52f, 0.2f };
        var centers = new Vector3[z.Length];
        var rx = new float[] { 0.1f, 0.3f, 0.32f, 0.14f };
        var ry = new float[] { 0.08f, 0.2f, 0.22f, 0.09f };
        for (int i = 0; i < z.Length; i++)
            centers[i] = new Vector3(0f, BodyLift + SampleRadiusY(z[i]) + 0.05f, z[i]);
        mesh.LoftZ(centers, rx, ry, 8);
    }

    static Airfoil WingSpec(float side)
    {
        return new Airfoil
        {
            root = new Vector3(side * 0.38f, 0.12f, 0.1f),
            tip = new Vector3(side * 2.58f, 0.42f, -0.05f),
            rootChord = 1.2f,
            tipChord = 0.46f,
            rootThick = 0.12f,
            tipThick = 0.04f
        };
    }

    static void AddWing(FlatMesh body, FlatMesh tips, FlatMesh stripe, Airfoil foil)
    {
        const float split = 0.78f;
        body.Panel(foil.At(0f), foil.At(split), foil.Chord(0f), foil.Chord(split), foil.Thick(0f), foil.Thick(split), true, false);
        tips.Panel(foil.At(split), foil.At(1f), foil.Chord(split), foil.Chord(1f), foil.Thick(split), foil.Thick(1f), false, true);

        Vector3 spanDir = (foil.tip - foil.root).normalized;
        Vector3 chordDir = Vector3.ProjectOnPlane(Vector3.forward, spanDir).normalized;
        Vector3 up = Vector3.Cross(chordDir, spanDir);
        if (Vector3.Dot(up, Vector3.up) < 0f)
            up = -up;

        const float t0 = 0.18f;
        const float t1 = 0.68f;
        Vector3 lift0 = up * (foil.Thick(t0) * 0.5f + 0.012f);
        Vector3 lift1 = up * (foil.Thick(t1) * 0.5f + 0.01f);
        stripe.Panel(
            foil.At(t0) + lift0,
            foil.At(t1) + lift1,
            foil.Chord(t0) * 0.3f,
            foil.Chord(t1) * 0.36f,
            0.028f,
            0.022f,
            true,
            true);
    }

    static void AddTail(FlatMesh body, FlatMesh tip)
    {
        var right = new Airfoil
        {
            root = new Vector3(0.12f, 0.2f, -1.38f),
            tip = new Vector3(0.98f, 0.36f, -1.52f),
            rootChord = 0.62f,
            tipChord = 0.28f,
            rootThick = 0.05f,
            tipThick = 0.028f
        };
        var left = right;
        left.root.x = -right.root.x;
        left.tip.x = -right.tip.x;
        body.Panel(right.root, right.tip, right.rootChord, right.tipChord, right.rootThick, right.tipThick, true, true);
        body.Panel(left.root, left.tip, left.rootChord, left.tipChord, left.rootThick, left.tipThick, true, true);

        Vector3 finRoot = new Vector3(0f, 0.32f, -1.32f);
        Vector3 finTip = new Vector3(0f, 1.16f, -1.64f);
        const float split = 0.68f;
        Vector3 mid = Vector3.Lerp(finRoot, finTip, split);
        body.Panel(finRoot, mid, 0.72f, 0.4f, 0.07f, 0.045f, true, false);
        tip.Panel(mid, finTip, 0.4f, 0.16f, 0.045f, 0.025f, false, true);
    }

    static void AddGear(FlatMesh dark, FlatMesh gold)
    {
        dark.Box(new Vector3(-0.34f, -0.22f, 0.16f), new Vector3(0.05f, 0.46f, 0.05f), Quaternion.identity);
        dark.Box(new Vector3(0.34f, -0.22f, 0.16f), new Vector3(0.05f, 0.46f, 0.05f), Quaternion.identity);
        dark.Box(new Vector3(0f, -0.42f, 0.16f), new Vector3(0.78f, 0.045f, 0.045f), Quaternion.identity);
        dark.Frustum(new Vector3(-0.5f, -0.42f, 0.16f), new Vector3(-0.3f, -0.42f, 0.16f), 0.16f, 0.16f, 8);
        dark.Frustum(new Vector3(0.3f, -0.42f, 0.16f), new Vector3(0.5f, -0.42f, 0.16f), 0.16f, 0.16f, 8);
        dark.Frustum(new Vector3(-0.06f, -0.16f, -1.48f), new Vector3(0.06f, -0.16f, -1.48f), 0.07f, 0.07f, 6);
        dark.Box(new Vector3(0f, -0.02f, -1.48f), new Vector3(0.04f, 0.22f, 0.04f), Quaternion.identity);

        gold.Frustum(new Vector3(-0.46f, -0.42f, 0.16f), new Vector3(-0.34f, -0.42f, 0.16f), 0.07f, 0.07f, 6);
        gold.Frustum(new Vector3(0.34f, -0.42f, 0.16f), new Vector3(0.46f, -0.42f, 0.16f), 0.07f, 0.07f, 6);
    }

    static void AddAileron(Transform parent, Airfoil foil, string name, float raiseSign, Material material)
    {
        const float t0 = 0.58f;
        const float t1 = 0.92f;
        Vector3 inner = foil.At(t0) - Vector3.forward * (foil.Chord(t0) * 0.5f);
        inner.z += 0.08f;
        float length = Vector3.Distance(foil.At(t0), foil.At(t1));
        float side = Mathf.Sign(foil.tip.x);
        float dihedral = Mathf.Atan2(foil.tip.y - foil.root.y, Mathf.Abs(foil.tip.x - foil.root.x)) * Mathf.Rad2Deg;

        var hinge = new GameObject(name);
        hinge.transform.SetParent(parent, false);
        hinge.transform.localPosition = inner;
        hinge.transform.localRotation = Quaternion.Euler(0f, 0f, side * dihedral);

        const float depth = 0.22f;
        float thick = Mathf.Max(0.04f, foil.Thick(t0) * 0.5f);
        var mesh = new FlatMesh();
        mesh.Box(new Vector3(side * length * 0.5f, 0f, -depth * 0.5f), new Vector3(length, thick, depth), Quaternion.identity);
        ModelLibrary.Part(hinge.transform, "Surface", mesh.Build(name), material, true);

        var tilt = hinge.AddComponent<AileronTilt>();
        tilt.raiseSign = raiseSign;
        tilt.maxAngle = 13f;
    }

    static void AddPropeller(Transform parent, ModelKit kit)
    {
        var hub = new GameObject("Propeller");
        hub.transform.SetParent(parent, false);
        hub.transform.localPosition = new Vector3(0f, BodyLift, 1.9f);
        hub.AddComponent<PropellerSpin>();

        var blades = new FlatMesh();
        for (int i = 0; i < 3; i++)
        {
            Quaternion around = Quaternion.AngleAxis(i * 120f, Vector3.forward);
            Quaternion pitch = Quaternion.Euler(20f, 0f, 0f);
            Vector3 center = around * new Vector3(0.46f, 0f, 0f);
            blades.Box(center, new Vector3(0.78f, 0.28f, 0.055f), around * pitch);
        }

        ModelLibrary.Part(hub.transform, "Blades", blades.Build("Blades"), kit.dark, true);

        var spinner = new FlatMesh();
        spinner.Frustum(new Vector3(0f, 0f, -0.02f), new Vector3(0f, 0f, 0.1f), 0.15f, 0.15f, 8);
        spinner.Cone(new Vector3(0f, 0f, 0.08f), new Vector3(0f, 0f, 0.36f), 0.12f, 8);
        ModelLibrary.Part(hub.transform, "Spinner", spinner.Build("Spinner"), kit.gold, true);
    }
}
