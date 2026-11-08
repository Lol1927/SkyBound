using System;
using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(-30)]
public class LivesManager : MonoBehaviour
{
    public const int MaxLives = 3;
    const float SafeInterval = 2f;
    const float SafeClearance = 25f;
    const float DownedSeconds = 1f;
    const float InvulnerableSeconds = 2f;
    const float BlinkInterval = 0.12f;

    static readonly Collider[] Nearby = new Collider[32];

    public static LivesManager Instance { get; private set; }

    public int Lives { get; private set; } = MaxLives;
    public bool Invulnerable { get; private set; }
    public bool IsDowned { get; private set; }
    public bool CanCrash => !IsDowned && !Invulnerable && Lives > 0 && !WorldManager.FlightPaused;

    public event Action LivesChanged;
    public event Action GameOver;

    PlaneController plane;
    PlaneCamera planeCamera;
    WorldBoundary bounds;
    AudioSource audioSource;
    AudioClip crashClip;
    Material burstMaterial;
    Vector3 safePoint;
    Quaternion safeRotation = Quaternion.identity;
    bool hasSafe;
    float safeTimer;
    bool explorationBound;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindFirstObjectByType<LivesManager>() != null)
            return;

        var go = new GameObject("Lives");
        go.AddComponent<LivesManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        crashClip = CreateCrashClip();
        CachePlane();
    }

    void OnEnable()
    {
        if (Instance != this)
            return;

        WorldManager.WorldGenerated += OnWorldGenerated;
    }

    void Start()
    {
        BindExploration();
    }

    void OnDisable()
    {
        WorldManager.WorldGenerated -= OnWorldGenerated;
        UnbindExploration();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        if (crashClip != null)
            Destroy(crashClip);
    }

    void Update()
    {
        BindExploration();
        RecordSafePoint();
    }

    public void Crash()
    {
        if (!CanCrash)
            return;
        if (ExplorationManager.Instance != null && ExplorationManager.Instance.IsComplete)
            return;

        CachePlane();
        if (plane == null)
            return;

        StartCoroutine(CrashRoutine(plane.transform.position));
    }

    public void Recover()
    {
        if (IsDowned || WorldManager.FlightPaused || Lives <= 0)
            return;
        if (ExplorationManager.Instance != null && ExplorationManager.Instance.IsComplete)
            return;

        StopAllCoroutines();
        Invulnerable = false;
        RespawnAtSafe();
        StartCoroutine(Blink());
    }

    void OnWorldGenerated(WorldData world)
    {
        StopAllCoroutines();
        RestoreFlight();
        Lives = MaxLives;
        hasSafe = false;
        safeTimer = 0f;
        LivesChanged?.Invoke();
        CloudGenerator.RestoreActive();
    }

    void OnRunRestarted()
    {
        StopAllCoroutines();
        RestoreFlight();
        Lives = MaxLives;
        hasSafe = false;
        safeTimer = 0f;
        LivesChanged?.Invoke();
        CloudGenerator.RestoreActive();
    }

    IEnumerator CrashRoutine(Vector3 point)
    {
        IsDowned = true;
        Invulnerable = false;
        if (plane != null)
        {
            plane.InputLocked = true;
            plane.SetRenderersVisible(false);
            plane.SetSolidColliders(false);
        }

        Lives = Mathf.Max(0, Lives - 1);
        LivesChanged?.Invoke();
        Burst(point);
        if (planeCamera == null)
            planeCamera = FindFirstObjectByType<PlaneCamera>();
        if (planeCamera != null)
            planeCamera.Shake(0.4f, 0.65f);
        if (audioSource != null && crashClip != null)
            audioSource.PlayOneShot(crashClip, 0.9f);
        CloudGenerator.RestoreActive();
        SpeakCrash();

        yield return new WaitForSeconds(DownedSeconds);

        if (Lives <= 0)
        {
            IsDowned = false;
            GameOver?.Invoke();
            yield break;
        }

        RespawnAtSafe();
        IsDowned = false;
        yield return Blink();
    }

    void SpeakCrash()
    {
        if (CoPilot.Instance == null)
            return;

        if (Lives <= 0)
            CoPilot.Instance.SayLine("We're going down.");
        else if (Lives == 1)
            CoPilot.Instance.SayLine("Last life. Make it count.");
        else
            CoPilot.Instance.SayLine("Ouch! Let's try that again.");
    }

    void RespawnAtSafe()
    {
        CachePlane();
        if (plane == null)
            return;

        Vector3 position = hasSafe ? safePoint : plane.SpawnPosition;
        plane.Teleport(position, Face(position));
        plane.InputLocked = false;
        plane.SetSolidColliders(true);
        plane.SetRenderersVisible(true);
        if (planeCamera == null)
            planeCamera = FindFirstObjectByType<PlaneCamera>();
        if (planeCamera != null)
            planeCamera.SnapToTarget();
        CloudGenerator.RestoreActive();
    }

    IEnumerator Blink()
    {
        Invulnerable = true;
        float end = Time.time + InvulnerableSeconds;
        bool visible = true;
        while (Time.time < end)
        {
            visible = !visible;
            if (plane != null)
                plane.SetRenderersVisible(visible);
            yield return new WaitForSeconds(BlinkInterval);
        }

        if (plane != null)
            plane.SetRenderersVisible(true);
        Invulnerable = false;
    }

    void RestoreFlight()
    {
        IsDowned = false;
        Invulnerable = false;
        CachePlane();
        if (plane == null)
            return;

        plane.InputLocked = false;
        plane.SetSolidColliders(true);
        plane.SetRenderersVisible(true);
    }

    void RecordSafePoint()
    {
        if (WorldManager.FlightPaused || Time.timeScale <= 0f || IsDowned || Invulnerable || Lives <= 0)
            return;

        CachePlane();
        if (plane == null || plane.InputLocked)
            return;

        safeTimer += Time.deltaTime;
        if (safeTimer < SafeInterval)
            return;

        safeTimer = 0f;
        Vector3 position = plane.transform.position;
        if (!IsSafe(position))
            return;

        safePoint = position;
        safeRotation = plane.transform.rotation;
        hasSafe = true;
    }

    bool IsSafe(Vector3 position)
    {
        if (bounds == null && plane != null)
            bounds = plane.GetComponent<WorldBoundary>();

        float radius = bounds != null ? bounds.Radius : 700f;
        float minAltitude = bounds != null ? bounds.MinAltitude : 10f;
        float maxAltitude = bounds != null ? bounds.MaxAltitude : 320f;
        if (new Vector2(position.x, position.z).magnitude > radius)
            return false;
        if (position.y < minAltitude + 4f || position.y > maxAltitude - 8f)
            return false;

        int count = Physics.OverlapSphereNonAlloc(position, SafeClearance, Nearby, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider hit = Nearby[i];
            Nearby[i] = null;
            if (hit == null || plane == null)
                continue;
            if (hit.transform == plane.transform || hit.transform.IsChildOf(plane.transform))
                continue;
            return false;
        }

        return count < Nearby.Length;
    }

    Quaternion Face(Vector3 position)
    {
        ExplorationManager run = ExplorationManager.Instance;
        if (run != null && run.TryNearestUndiscovered(position, out Landmark landmark, out _))
        {
            Vector3 toward = landmark.transform.position - position;
            toward.y = 0f;
            if (toward.sqrMagnitude > 4f)
                return Quaternion.LookRotation(toward.normalized, Vector3.up);
        }

        int count = Physics.OverlapSphereNonAlloc(position, 200f, Nearby, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float best = float.PositiveInfinity;
        Vector3 away = Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            Collider hit = Nearby[i];
            Nearby[i] = null;
            if (hit == null || plane == null)
                continue;
            if (hit.transform == plane.transform || hit.transform.IsChildOf(plane.transform))
                continue;

            Vector3 closest = hit.ClosestPoint(position);
            float distance = (closest - position).sqrMagnitude;
            if (distance >= best)
                continue;

            best = distance;
            away = position - closest;
        }

        away.y = 0f;
        if (away.sqrMagnitude > 0.25f)
            return Quaternion.LookRotation(away.normalized, Vector3.up);

        if (hasSafe)
        {
            Vector3 forward = safeRotation * Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.01f)
                return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        return Quaternion.LookRotation(Vector3.forward, Vector3.up);
    }

    void Burst(Vector3 point)
    {
        Material material = BurstMaterial();
        if (material == null)
            return;

        var go = new GameObject("Crash Burst");
        go.transform.position = point;

        var particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.35f;
        main.startLifetime = 0.55f;
        main.startSpeed = 9f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
        main.startColor = new Color(1f, 0.62f, 0.28f, 1f);
        main.maxParticles = 36;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.35f;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)28) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.8f;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        particles.Play();
        Destroy(go, 2f);
    }

    Material BurstMaterial()
    {
        if (burstMaterial != null)
            return burstMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            return null;

        burstMaterial = new Material(shader);
        burstMaterial.name = "Crash Burst";
        Texture2D texture = SoftCircle();
        burstMaterial.mainTexture = texture;
        if (burstMaterial.HasProperty("_BaseMap"))
            burstMaterial.SetTexture("_BaseMap", texture);
        Color color = new Color(1f, 0.55f, 0.2f, 1f);
        burstMaterial.color = color;
        if (burstMaterial.HasProperty("_BaseColor"))
            burstMaterial.SetColor("_BaseColor", color);
        return burstMaterial;
    }

    static Texture2D SoftCircle()
    {
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                byte alpha = (byte)(Mathf.Clamp01(1f - distance) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    static AudioClip CreateCrashClip()
    {
        const int rate = 22050;
        const int samples = 6000;
        var clip = AudioClip.Create("Crash", samples, 1, rate, false);
        var data = new float[samples];
        var rng = new System.Random(11);
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)samples;
            float env = Mathf.Exp(-t * 7f);
            data[i] = ((float)rng.NextDouble() * 2f - 1f) * env * 0.75f;
        }

        clip.SetData(data, 0);
        return clip;
    }

    void CachePlane()
    {
        if (plane == null)
            plane = FindFirstObjectByType<PlaneController>();
        if (bounds == null && plane != null)
            bounds = plane.GetComponent<WorldBoundary>();
    }

    void BindExploration()
    {
        if (explorationBound || ExplorationManager.Instance == null)
            return;

        ExplorationManager.Instance.RunRestarted += OnRunRestarted;
        explorationBound = true;
    }

    void UnbindExploration()
    {
        if (!explorationBound)
            return;

        explorationBound = false;
        if (ExplorationManager.Instance != null)
            ExplorationManager.Instance.RunRestarted -= OnRunRestarted;
    }
}
