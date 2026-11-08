using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-40)]
public class ExplorationManager : MonoBehaviour
{
    public const int LandmarkGoal = 3;
    public const int PointsPerLandmark = 1000;
    public const int MaxTimeBonus = 3000;

    const float TimeBonusWindow = 180f;

    public static ExplorationManager Instance { get; private set; }

    public int DiscoveredCount { get; private set; }
    public float ElapsedSeconds { get; private set; }
    public int TimeBonus { get; private set; }
    public bool IsComplete { get; private set; }
    public string WorldName { get; private set; } = "";

    public int Score => DiscoveredCount * PointsPerLandmark + (IsComplete ? TimeBonus : 0);

    public event Action<Landmark> LandmarkDiscovered;
    public event Action WorldCompleted;
    public event Action RunRestarted;

    readonly List<Landmark> landmarks = new List<Landmark>(LandmarkGoal);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindFirstObjectByType<ExplorationManager>() != null)
            return;

        var go = new GameObject("Exploration");
        go.AddComponent<ExplorationManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (GetComponent<GameHud>() == null)
            gameObject.AddComponent<GameHud>();
    }

    void OnEnable()
    {
        if (Instance != this)
            return;

        WorldManager.WorldGenerated += OnWorldGenerated;
    }

    void Start()
    {
        if (Instance != this)
            return;
        if (WorldManager.Instance != null && WorldManager.Instance.CurrentWorld != null)
            OnWorldGenerated(WorldManager.Instance.CurrentWorld);
    }

    void OnDisable()
    {
        WorldManager.WorldGenerated -= OnWorldGenerated;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (IsComplete || WorldManager.FlightPaused || Time.timeScale <= 0f)
            return;

        ElapsedSeconds += Time.deltaTime;
    }

    public void Discover(Landmark landmark)
    {
        if (landmark == null || landmark.discovered || IsComplete || WorldManager.FlightPaused)
            return;

        if (!landmarks.Contains(landmark))
            landmarks.Add(landmark);

        landmark.discovered = true;
        landmark.ShowDiscovered();
        DiscoveredCount++;
        LandmarkDiscovered?.Invoke(landmark);

        if (DiscoveredCount >= LandmarkGoal)
            CompleteWorld();
    }

    public void FlyAgain()
    {
        if (!IsComplete)
            return;

        RestartAttempt();
    }

    public void RestartAttempt()
    {
        for (int i = landmarks.Count - 1; i >= 0; i--)
        {
            Landmark landmark = landmarks[i];
            if (landmark == null)
            {
                landmarks.RemoveAt(i);
                continue;
            }

            landmark.ClearDiscovered();
        }

        DiscoveredCount = 0;
        ElapsedSeconds = 0f;
        TimeBonus = 0;
        IsComplete = false;

        if (WorldManager.Instance != null)
        {
            WorldManager.Instance.ResetPlaneToStart();
            WorldManager.Instance.SetFlightPaused(false);
        }

        RunRestarted?.Invoke();
    }

    public bool TryNearestUndiscovered(Vector3 from, out Landmark landmark, out float distance)
    {
        landmark = null;
        distance = 0f;
        float best = float.PositiveInfinity;
        for (int i = 0; i < landmarks.Count; i++)
        {
            Landmark candidate = landmarks[i];
            if (candidate == null || candidate.discovered)
                continue;

            float candidateDistance = Vector3.Distance(from, candidate.transform.position);
            if (candidateDistance >= best)
                continue;

            best = candidateDistance;
            landmark = candidate;
        }

        if (landmark == null)
            return false;

        distance = best;
        return true;
    }

    public static int TimeBonusFor(float seconds)
    {
        float remaining = 1f - Mathf.Clamp01(Mathf.Max(0f, seconds) / TimeBonusWindow);
        return Mathf.Clamp(Mathf.RoundToInt(MaxTimeBonus * remaining), 0, MaxTimeBonus);
    }

    void CompleteWorld()
    {
        if (IsComplete)
            return;

        IsComplete = true;
        TimeBonus = TimeBonusFor(ElapsedSeconds);
        WorldCompleted?.Invoke();
    }

    void OnWorldGenerated(WorldData world)
    {
        landmarks.Clear();
        DiscoveredCount = 0;
        ElapsedSeconds = 0f;
        TimeBonus = 0;
        IsComplete = false;
        WorldName = world != null && !string.IsNullOrWhiteSpace(world.worldName)
            ? world.worldName.Trim()
            : "Unnamed Sky";

        Landmark[] found = FindObjectsByType<Landmark>(FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null)
                landmarks.Add(found[i]);
        }
    }
}
