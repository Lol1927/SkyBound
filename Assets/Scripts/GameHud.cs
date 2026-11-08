using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GameHud : MonoBehaviour
{
    const float MapSize = 210f;
    const float BannerHold = 2.15f;
    const float BannerFade = 0.4f;
    const float CompletionTimeout = 20f;

    static readonly Color Gold = new Color(0.96f, 0.78f, 0.32f, 1f);
    static readonly Color MapIsland = new Color(0.35f, 0.96f, 1f, 1f);
    static readonly Color MapHidden = new Color(1f, 0.96f, 0.78f, 1f);
    static readonly Color MapFound = new Color(1f, 0.78f, 0.18f, 1f);
    static Material mapMaterial;
    static readonly Color Ink = new Color(0.96f, 0.94f, 0.88f, 0.96f);
    static readonly Color Muted = new Color(0.95f, 0.86f, 0.62f, 0.95f);

    static readonly Color HeartFull = new Color(0.92f, 0.28f, 0.34f, 1f);
    static readonly Color HeartEmpty = new Color(1f, 1f, 1f, 0.28f);

    static Sprite circleSprite;
    static Sprite diamondSprite;
    static Sprite arrowSprite;
    static Sprite heartSprite;

    class MapPin
    {
        public Landmark landmark;
        public Image image;
    }

    readonly List<GameObject> mapItems = new List<GameObject>();
    readonly List<MapPin> pins = new List<MapPin>();

    AudioSource chime;
    AudioClip chimeClip;
    GameObject flightRoot;
    GameObject bannerRoot;
    CanvasGroup bannerGroup;
    TextMeshProUGUI bannerLabel;
    TextMeshProUGUI worldLabel;
    TextMeshProUGUI landmarkLabel;
    TextMeshProUGUI timerLabel;
    TextMeshProUGUI scoreLabel;
    GameObject compassRoot;
    RectTransform compassArrow;
    TextMeshProUGUI compassDistance;
    RectTransform mapRoot;
    RectTransform planeArrow;
    GameObject pauseRoot;
    GameObject completionRoot;
    TextMeshProUGUI completionTitle;
    TextMeshProUGUI completionTime;
    TextMeshProUGUI completionScore;
    TextMeshProUGUI completionBonus;
    TextMeshProUGUI completionLives;
    GameObject gameOverRoot;
    TextMeshProUGUI gameOverName;
    TextMeshProUGUI gameOverLandmarks;
    TextMeshProUGUI gameOverTime;
    TextMeshProUGUI gameOverScore;
    Image[] hearts;

    PlaneController plane;
    WorldGenerator generator;
    Coroutine completionWait;
    int completionToken;
    bool paused;
    bool explorationBound;
    bool livesBound;
    bool menuWasOpen;
    float bannerLeft;
    float bannerFadeLeft;

    void Awake()
    {
        chime = gameObject.AddComponent<AudioSource>();
        chime.playOnAwake = false;
        chime.loop = false;
        chime.spatialBlend = 0f;
        chimeClip = CreateChime();
        BuildUi();
    }

    void OnEnable()
    {
        WorldManager.WorldGenerated += OnWorldGenerated;
        BindExploration();
    }

    void Start()
    {
        BindExploration();
        if (WorldManager.Instance != null && WorldManager.Instance.CurrentWorld != null)
            RebuildMinimap();
    }

    void OnDisable()
    {
        WorldManager.WorldGenerated -= OnWorldGenerated;
        UnbindExploration();
        UnbindLives();
    }

    void OnDestroy()
    {
        if (paused)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        if (chimeClip != null)
            Destroy(chimeClip);
    }

    void Update()
    {
        BindExploration();
        BindLives();

        bool worldMenu = WorldManager.Instance != null && WorldManager.Instance.IsMenuOpen;
        if (worldMenu && !menuWasOpen)
        {
            CancelCompletion();
            HideGameOver();
        }

        menuWasOpen = worldMenu;
        if (worldMenu && paused)
            SetPaused(false);

        bool completionOpen = completionRoot != null && completionRoot.activeSelf;
        bool gameOverOpen = gameOverRoot != null && gameOverRoot.activeSelf;
        bool showFlight = !worldMenu && !completionOpen && !gameOverOpen;
        if (flightRoot != null && flightRoot.activeSelf != showFlight)
            flightRoot.SetActive(showFlight);

        if (flightRoot != null && flightRoot.activeSelf)
            RefreshFlightHud();

        TickBanner();

        if (Input.GetKeyDown(KeyCode.Escape))
            TogglePause();

        bool locked = paused || completionOpen || gameOverOpen || worldMenu;
        if (WorldManager.Instance != null && WorldManager.FlightPaused != locked)
            WorldManager.Instance.SetFlightPaused(locked);
    }

    void OnWorldGenerated(WorldData world)
    {
        CancelCompletion();
        if (paused)
            SetPaused(false);
        HideBanner();
        RebuildMinimap();
    }

    void OnLandmarkDiscovered(Landmark landmark)
    {
        ShowBanner(landmark);
        if (chime != null && chimeClip != null)
            chime.PlayOneShot(chimeClip, 0.85f);
        RefreshPinColors();
    }

    void OnWorldCompleted()
    {
        if (completionWait != null)
            StopCoroutine(completionWait);
        completionWait = StartCoroutine(ShowCompletionWhenQuiet(++completionToken));
    }

    void OnRunRestarted()
    {
        CancelCompletion();
        HideGameOver();
        HideBanner();
        RefreshPinColors();
        RefreshHearts();
    }

    IEnumerator ShowCompletionWhenQuiet(int token)
    {
        yield return null;
        float waited = 0f;
        while (token == completionToken && waited < CompletionTimeout)
        {
            if (BlocksCompletion())
                yield break;

            CoPilot copilot = CoPilot.Instance;
            if (copilot == null || !copilot.IsSpeaking)
                break;

            waited += Time.deltaTime;
            yield return null;
        }

        if (token != completionToken || BlocksCompletion())
            yield break;

        ShowCompletion();
        if (paused)
            SetPaused(false);
        completionWait = null;
    }

    void RefreshFlightHud()
    {
        ExplorationManager run = ExplorationManager.Instance;
        if (run == null)
            return;

        int found = Mathf.Min(run.DiscoveredCount, ExplorationManager.LandmarkGoal);
        SetText(worldLabel, run.WorldName);
        SetText(landmarkLabel, "Landmarks " + found + "/" + ExplorationManager.LandmarkGoal);
        SetText(timerLabel, FormatClock(run.ElapsedSeconds));
        SetText(scoreLabel, "Score " + run.Score);
        RefreshHearts();
        UpdateCompass(run);
        UpdateMinimap();
    }

    void UpdateCompass(ExplorationManager run)
    {
        if (compassRoot == null)
            return;

        if (plane == null)
            plane = FindFirstObjectByType<PlaneController>();
        if (run.IsComplete || plane == null || !run.TryNearestUndiscovered(plane.transform.position, out Landmark nearest, out float distance))
        {
            compassRoot.SetActive(false);
            return;
        }

        compassRoot.SetActive(true);
        Vector3 forward = Flat(plane.transform.forward);
        Vector3 to = Flat(nearest.transform.position - plane.transform.position);
        float angle = forward.sqrMagnitude < 0.0001f || to.sqrMagnitude < 0.0001f
            ? 0f
            : Vector3.SignedAngle(forward, to, Vector3.up);
        if (compassArrow != null)
            compassArrow.localRotation = Quaternion.Euler(0f, 0f, -angle);
        SetText(compassDistance, Mathf.RoundToInt(distance) + " m");
    }

    void UpdateMinimap()
    {
        RefreshPinColors();
        if (planeArrow == null)
            return;
        if (plane == null)
            plane = FindFirstObjectByType<PlaneController>();
        if (plane == null)
        {
            planeArrow.gameObject.SetActive(false);
            return;
        }

        planeArrow.gameObject.SetActive(true);
        planeArrow.anchoredPosition = ClampToMap(plane.transform.position);
        Vector3 forward = Flat(plane.transform.forward);
        float angle = forward.sqrMagnitude < 0.0001f ? 0f : Vector3.SignedAngle(Vector3.forward, forward, Vector3.up);
        planeArrow.localRotation = Quaternion.Euler(0f, 0f, -angle);
    }

    void RebuildMinimap()
    {
        if (mapRoot == null)
            return;

        for (int i = 0; i < mapItems.Count; i++)
        {
            if (mapItems[i] != null)
                Destroy(mapItems[i]);
        }

        mapItems.Clear();
        pins.Clear();

        if (generator == null)
            generator = FindFirstObjectByType<WorldGenerator>();
        if (generator != null)
        {
            for (int i = 0; i < generator.ClusterCount; i++)
            {
                if (!generator.TryGetCluster(i, out Vector3 center, out float radius))
                    continue;

                float t = Mathf.InverseLerp(50f, 120f, radius);
                float size = Mathf.Lerp(16f, 34f, t);
                Image dot = CreateDot("Cluster", mapRoot, CircleSprite(), MapIsland, size);
                dot.rectTransform.anchoredPosition = ToMap(center);
                mapItems.Add(dot.gameObject);
            }
        }

        Landmark[] found = FindObjectsByType<Landmark>(FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            Landmark landmark = found[i];
            if (landmark == null)
                continue;

            Image icon = CreateDot("Landmark", mapRoot, DiamondSprite(), landmark.discovered ? MapFound : MapHidden, 16f);
            icon.rectTransform.anchoredPosition = ToMap(landmark.transform.position);
            pins.Add(new MapPin { landmark = landmark, image = icon });
            mapItems.Add(icon.gameObject);
        }

        if (planeArrow != null)
            planeArrow.SetAsLastSibling();
    }

    void RefreshPinColors()
    {
        for (int i = 0; i < pins.Count; i++)
        {
            MapPin pin = pins[i];
            if (pin.image == null || pin.landmark == null)
                continue;
            pin.image.color = pin.landmark.discovered ? MapFound : MapHidden;
        }
    }

    void ShowBanner(Landmark landmark)
    {
        if (bannerRoot == null || bannerLabel == null || bannerGroup == null)
            return;

        string name = landmark != null && !string.IsNullOrWhiteSpace(landmark.landmarkName)
            ? landmark.landmarkName.Trim()
            : "Landmark";
        bannerLabel.text = "DISCOVERED: " + name;
        bannerGroup.alpha = 1f;
        bannerRoot.SetActive(true);
        bannerLeft = BannerHold;
        bannerFadeLeft = BannerFade;
    }

    void TickBanner()
    {
        if (bannerRoot == null || !bannerRoot.activeSelf)
            return;

        if (bannerLeft > 0f)
        {
            bannerLeft -= Time.deltaTime;
            return;
        }

        bannerFadeLeft -= Time.deltaTime;
        if (bannerGroup != null)
            bannerGroup.alpha = Mathf.Clamp01(bannerFadeLeft / BannerFade);
        if (bannerFadeLeft <= 0f)
            bannerRoot.SetActive(false);
    }

    void HideBanner()
    {
        bannerLeft = 0f;
        bannerFadeLeft = 0f;
        if (bannerGroup != null)
            bannerGroup.alpha = 0f;
        if (bannerRoot != null)
            bannerRoot.SetActive(false);
    }

    void ShowCompletion()
    {
        if (completionRoot == null || ExplorationManager.Instance == null)
            return;

        ExplorationManager run = ExplorationManager.Instance;
        SetText(completionTitle, run.WorldName);
        SetText(completionTime, "Time  " + FormatClock(run.ElapsedSeconds));
        SetText(completionScore, "Score  " + run.Score);
        SetText(completionBonus, "Time bonus  " + run.TimeBonus);
        int lives = LivesManager.Instance != null ? LivesManager.Instance.Lives : LivesManager.MaxLives;
        SetText(completionLives, "Lives  " + lives + "/" + LivesManager.MaxLives);
        completionRoot.SetActive(true);
        if (flightRoot != null)
            flightRoot.SetActive(false);
        if (WorldManager.Instance != null)
            WorldManager.Instance.SetFlightPaused(true);
    }

    void CancelCompletion()
    {
        completionToken++;
        if (completionWait != null)
        {
            StopCoroutine(completionWait);
            completionWait = null;
        }

        if (completionRoot != null)
            completionRoot.SetActive(false);
    }

    void TogglePause()
    {
        if (WorldManager.Instance != null && WorldManager.Instance.IsMenuOpen)
            return;
        if (completionRoot != null && completionRoot.activeSelf)
            return;
        if (gameOverRoot != null && gameOverRoot.activeSelf)
            return;
        if (LivesManager.Instance != null && LivesManager.Instance.IsDowned)
            return;

        SetPaused(!paused);
    }

    void SetPaused(bool value)
    {
        paused = value;
        Time.timeScale = value ? 0f : 1f;
        AudioListener.pause = value;
        if (pauseRoot != null)
            pauseRoot.SetActive(value);
        ApplyFlightLock();
    }

    void ApplyFlightLock()
    {
        if (WorldManager.Instance == null)
            return;

        bool locked = paused
            || (completionRoot != null && completionRoot.activeSelf)
            || (gameOverRoot != null && gameOverRoot.activeSelf)
            || WorldManager.Instance.IsMenuOpen;
        if (WorldManager.FlightPaused != locked)
            WorldManager.Instance.SetFlightPaused(locked);
    }

    void OnFlyAgain()
    {
        if (ExplorationManager.Instance != null)
            ExplorationManager.Instance.FlyAgain();
    }

    void OnNewWorld()
    {
        CancelCompletion();
        HideGameOver();
        if (WorldManager.Instance != null)
            WorldManager.Instance.OpenStartPanel();
        if (paused)
            SetPaused(false);
    }

    void OnTitleScreen()
    {
        CancelCompletion();
        HideGameOver();
        if (WorldManager.Instance != null)
            WorldManager.Instance.ShowTitle();
        if (paused)
            SetPaused(false);
    }

    void OnRestartWorld()
    {
        CancelCompletion();
        HideGameOver();
        if (paused)
            SetPaused(false);
        if (ExplorationManager.Instance != null)
            ExplorationManager.Instance.RestartAttempt();
    }

    void OnRetryWorld()
    {
        HideGameOver();
        if (paused)
            SetPaused(false);
        if (ExplorationManager.Instance != null)
            ExplorationManager.Instance.RestartAttempt();
    }

    void BindExploration()
    {
        if (explorationBound || ExplorationManager.Instance == null)
            return;

        ExplorationManager.Instance.LandmarkDiscovered += OnLandmarkDiscovered;
        ExplorationManager.Instance.WorldCompleted += OnWorldCompleted;
        ExplorationManager.Instance.RunRestarted += OnRunRestarted;
        explorationBound = true;
    }

    void UnbindExploration()
    {
        if (!explorationBound)
            return;

        explorationBound = false;
        ExplorationManager manager = ExplorationManager.Instance;
        if (manager == null)
            return;

        manager.LandmarkDiscovered -= OnLandmarkDiscovered;
        manager.WorldCompleted -= OnWorldCompleted;
        manager.RunRestarted -= OnRunRestarted;
    }

    bool BlocksCompletion()
    {
        if (WorldManager.Instance != null && WorldManager.Instance.IsMenuOpen)
            return true;
        if (gameOverRoot != null && gameOverRoot.activeSelf)
            return true;
        return LivesManager.Instance != null && LivesManager.Instance.Lives <= 0;
    }

    void BindLives()
    {
        if (livesBound || LivesManager.Instance == null)
            return;

        LivesManager.Instance.LivesChanged += RefreshHearts;
        LivesManager.Instance.GameOver += ShowGameOver;
        livesBound = true;
        RefreshHearts();
    }

    void UnbindLives()
    {
        if (!livesBound)
            return;

        livesBound = false;
        if (LivesManager.Instance == null)
            return;

        LivesManager.Instance.LivesChanged -= RefreshHearts;
        LivesManager.Instance.GameOver -= ShowGameOver;
    }

    void BuildHearts()
    {
        hearts = new Image[LivesManager.MaxLives];
        Sprite sprite = HeartSprite();
        for (int i = 0; i < hearts.Length; i++)
        {
            Image heart = CreateImage("Heart", flightRoot.transform, HeartFull, sprite);
            Place(heart.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f + i * 36f, -100f), new Vector2(28f, 26f));
            hearts[i] = heart;
        }
    }

    void RefreshHearts()
    {
        if (hearts == null)
            return;

        int lives = LivesManager.Instance != null ? LivesManager.Instance.Lives : LivesManager.MaxLives;
        for (int i = 0; i < hearts.Length; i++)
        {
            if (hearts[i] != null)
                hearts[i].color = i < lives ? HeartFull : HeartEmpty;
        }
    }

    void ShowGameOver()
    {
        if (gameOverRoot == null)
            return;

        CancelCompletion();
        ExplorationManager run = ExplorationManager.Instance;
        int found = run != null ? Mathf.Min(run.DiscoveredCount, ExplorationManager.LandmarkGoal) : 0;
        SetText(gameOverName, run != null ? run.WorldName : "");
        SetText(gameOverLandmarks, "Landmarks  " + found + "/" + ExplorationManager.LandmarkGoal);
        SetText(gameOverTime, "Time  " + FormatClock(run != null ? run.ElapsedSeconds : 0f));
        SetText(gameOverScore, "Score  " + (run != null ? run.Score : 0));
        gameOverRoot.SetActive(true);
        if (flightRoot != null)
            flightRoot.SetActive(false);
        if (paused)
            SetPaused(false);
        if (WorldManager.Instance != null)
            WorldManager.Instance.SetFlightPaused(true);
    }

    void HideGameOver()
    {
        if (gameOverRoot != null)
            gameOverRoot.SetActive(false);
    }

    void BuildUi()
    {
        TMP_FontAsset font = LoadFont();
        if (font == null)
        {
            Debug.LogError("GameHud could not create a TextMeshPro font.");
            return;
        }

        EnsureEventSystem();

        var flightCanvas = CreateCanvas("Flight HUD", 70);
        flightRoot = new GameObject("Flight HUD", typeof(RectTransform));
        flightRoot.transform.SetParent(flightCanvas.transform, false);
        Stretch(flightRoot.GetComponent<RectTransform>());

        worldLabel = CreateText("World Name", flightRoot.transform, font, "", 30, Ink, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        Place(worldLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -22f), new Vector2(640f, 40f));
        Outline(worldLabel);

        landmarkLabel = CreateText("Landmarks", flightRoot.transform, font, "Landmarks 0/3", 22, Ink, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        Place(landmarkLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -60f), new Vector2(420f, 32f));
        Outline(landmarkLabel);
        BuildHearts();

        timerLabel = CreateText("Timer", flightRoot.transform, font, "0:00", 30, Ink, FontStyles.Bold, TextAlignmentOptions.TopRight);
        Place(timerLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -22f), new Vector2(280f, 40f));
        Outline(timerLabel);

        scoreLabel = CreateText("Score", flightRoot.transform, font, "Score 0", 22, Ink, FontStyles.Normal, TextAlignmentOptions.TopRight);
        Place(scoreLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -60f), new Vector2(280f, 32f));
        Outline(scoreLabel);

        var hint = CreateText("Controls", flightRoot.transform, font, "Q: Ask co-pilot   Tab: New world", 22, Ink, FontStyles.Normal, TextAlignmentOptions.BottomRight);
        Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 22f), new Vector2(560f, 36f));
        Outline(hint);

        BuildCompass(font);
        BuildBanner(font);
        BuildMinimap();

        var overlay = CreateCanvas("Flight Overlay", 96);
        BuildPause(overlay.transform, font);
        BuildCompletion(overlay.transform, font);
        BuildGameOver(overlay.transform, font);
        flightRoot.SetActive(false);
    }

    void BuildCompass(TMP_FontAsset font)
    {
        compassRoot = new GameObject("Compass", typeof(RectTransform));
        compassRoot.transform.SetParent(flightRoot.transform, false);
        var root = compassRoot.GetComponent<RectTransform>();
        root.anchorMin = new Vector2(0.5f, 1f);
        root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 1f);
        root.anchoredPosition = new Vector2(0f, -124f);
        root.sizeDelta = new Vector2(160f, 96f);

        var backdrop = CreateImage("Compass Back", compassRoot.transform, new Color(0.04f, 0.07f, 0.12f, 0.4f), CircleSprite());
        Place(backdrop.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(54f, 54f));

        var arrow = CreateImage("Compass Arrow", compassRoot.transform, Color.white, ArrowSprite());
        arrow.preserveAspect = true;
        compassArrow = arrow.rectTransform;
        compassArrow.anchorMin = new Vector2(0.5f, 1f);
        compassArrow.anchorMax = new Vector2(0.5f, 1f);
        compassArrow.pivot = new Vector2(0.5f, 0.5f);
        compassArrow.anchoredPosition = new Vector2(0f, -34f);
        compassArrow.sizeDelta = new Vector2(30f, 36f);

        compassDistance = CreateText("Compass Distance", compassRoot.transform, font, "", 20, Ink, FontStyles.Normal, TextAlignmentOptions.Center);
        Place(compassDistance.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -68f), new Vector2(150f, 28f));
        Outline(compassDistance);
        compassRoot.SetActive(false);
    }

    void BuildBanner(TMP_FontAsset font)
    {
        bannerRoot = new GameObject("Discovery Banner", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        bannerRoot.transform.SetParent(flightRoot.transform, false);
        bannerGroup = bannerRoot.GetComponent<CanvasGroup>();
        bannerGroup.blocksRaycasts = false;
        bannerGroup.interactable = false;
        var background = bannerRoot.GetComponent<Image>();
        background.color = new Color(0.05f, 0.08f, 0.14f, 0.82f);
        background.raycastTarget = false;
        var rect = bannerRoot.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -228f);
        rect.sizeDelta = new Vector2(760f, 64f);

        bannerLabel = CreateText("Banner Label", bannerRoot.transform, font, "", 30, Gold, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(bannerLabel.rectTransform);
        bannerLabel.margin = new Vector4(18f, 6f, 18f, 6f);
        bannerRoot.SetActive(false);
    }

    void BuildMinimap()
    {
        var map = CreateImage("Minimap", flightRoot.transform, new Color(0.07f, 0.12f, 0.22f, 0.9f), null);
        UseMapMaterial(map);
        mapRoot = map.rectTransform;
        mapRoot.anchorMin = new Vector2(0f, 0f);
        mapRoot.anchorMax = new Vector2(0f, 0f);
        mapRoot.pivot = new Vector2(0f, 0f);
        mapRoot.anchoredPosition = new Vector2(24f, 24f);
        mapRoot.sizeDelta = new Vector2(MapSize, MapSize);

        var arrow = CreateImage("Plane", mapRoot, Color.white, ArrowSprite());
        UseMapMaterial(arrow);
        arrow.preserveAspect = true;
        planeArrow = arrow.rectTransform;
        planeArrow.anchorMin = new Vector2(0.5f, 0.5f);
        planeArrow.anchorMax = new Vector2(0.5f, 0.5f);
        planeArrow.pivot = new Vector2(0.5f, 0.5f);
        planeArrow.sizeDelta = new Vector2(16f, 20f);
        planeArrow.gameObject.SetActive(false);
    }

    void BuildPause(Transform parent, TMP_FontAsset font)
    {
        pauseRoot = new GameObject("Pause", typeof(RectTransform));
        pauseRoot.transform.SetParent(parent, false);
        Stretch(pauseRoot.GetComponent<RectTransform>());

        var dim = CreateImage("Dim", pauseRoot.transform, new Color(0.02f, 0.04f, 0.08f, 0.45f), null);
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        var card = CreateImage("Card", pauseRoot.transform, new Color(0.06f, 0.11f, 0.18f, 0.94f), null);
        card.raycastTarget = true;
        Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 420f));

        var heading = CreateText("Paused Label", card.transform, font, "Paused", 56, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(440f, 80f));

        Button resume = CreateButton(card.transform, font, "Resume", () => SetPaused(false));
        Place(resume.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 28f), new Vector2(320f, 56f));

        Button restart = CreateButton(card.transform, font, "Restart world", OnRestartWorld);
        Place(restart.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -48f), new Vector2(320f, 56f));

        Button title = CreateButton(card.transform, font, "Title", OnTitleScreen);
        Place(title.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -124f), new Vector2(320f, 56f));
        pauseRoot.SetActive(false);
    }

    void BuildCompletion(Transform parent, TMP_FontAsset font)
    {
        completionRoot = new GameObject("Completion", typeof(RectTransform));
        completionRoot.transform.SetParent(parent, false);
        Stretch(completionRoot.GetComponent<RectTransform>());

        var dim = CreateImage("Dim", completionRoot.transform, new Color(0.02f, 0.05f, 0.1f, 0.55f), null);
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        var card = CreateImage("Card", completionRoot.transform, new Color(0.06f, 0.11f, 0.18f, 0.94f), null);
        card.raycastTarget = true;
        Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, 520f));

        var eyebrow = CreateText("Status", card.transform, font, "Flight complete", 22, Muted, FontStyles.Italic, TextAlignmentOptions.Center);
        Place(eyebrow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(820f, 36f));

        completionTitle = CreateText("World", card.transform, font, "", 42, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        Place(completionTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(840f, 80f));
        completionTitle.richText = false;

        completionTime = CreateText("Time", card.transform, font, "", 28, Ink, FontStyles.Normal, TextAlignmentOptions.Center);
        Place(completionTime.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 48f), new Vector2(760f, 40f));

        completionScore = CreateText("Score", card.transform, font, "", 28, Ink, FontStyles.Bold, TextAlignmentOptions.Center);
        Place(completionScore.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(760f, 40f));

        completionBonus = CreateText("Bonus", card.transform, font, "", 22, Muted, FontStyles.Italic, TextAlignmentOptions.Center);
        Place(completionBonus.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -34f), new Vector2(760f, 34f));

        completionLives = CreateText("Lives", card.transform, font, "", 22, Muted, FontStyles.Italic, TextAlignmentOptions.Center);
        Place(completionLives.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -72f), new Vector2(760f, 34f));

        Button again = CreateButton(card.transform, font, "Fly again (same world)", OnFlyAgain);
        Place(again.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-270f, 36f), new Vector2(320f, 56f));

        Button next = CreateButton(card.transform, font, "New world", OnNewWorld);
        Place(next.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(20f, 36f), new Vector2(200f, 56f));

        Button title = CreateButton(card.transform, font, "Title", OnTitleScreen);
        Place(title.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(250f, 36f), new Vector2(160f, 56f));

        completionRoot.SetActive(false);
    }

    void BuildGameOver(Transform parent, TMP_FontAsset font)
    {
        gameOverRoot = new GameObject("Game Over", typeof(RectTransform));
        gameOverRoot.transform.SetParent(parent, false);
        Stretch(gameOverRoot.GetComponent<RectTransform>());

        var dim = CreateImage("Dim", gameOverRoot.transform, new Color(0.02f, 0.05f, 0.1f, 0.55f), null);
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        var card = CreateImage("Card", gameOverRoot.transform, new Color(0.06f, 0.11f, 0.18f, 0.94f), null);
        card.raycastTarget = true;
        Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, 520f));

        var heading = CreateText("Heading", card.transform, font, "GAME OVER", 56, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(820f, 80f));

        gameOverName = CreateText("World", card.transform, font, "", 32, Muted, FontStyles.Italic, TextAlignmentOptions.Center);
        Place(gameOverName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(800f, 48f));

        gameOverLandmarks = CreateText("Landmarks", card.transform, font, "", 28, Ink, FontStyles.Normal, TextAlignmentOptions.Center);
        Place(gameOverLandmarks.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 36f), new Vector2(760f, 40f));

        gameOverTime = CreateText("Time", card.transform, font, "", 28, Ink, FontStyles.Normal, TextAlignmentOptions.Center);
        Place(gameOverTime.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(760f, 40f));

        gameOverScore = CreateText("Score", card.transform, font, "", 28, Ink, FontStyles.Bold, TextAlignmentOptions.Center);
        Place(gameOverScore.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -52f), new Vector2(760f, 40f));

        Button retry = CreateButton(card.transform, font, "Retry this world", OnRetryWorld);
        Place(retry.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-270f, 36f), new Vector2(300f, 56f));

        Button next = CreateButton(card.transform, font, "New world", OnNewWorld);
        Place(next.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(20f, 36f), new Vector2(200f, 56f));

        Button title = CreateButton(card.transform, font, "Title", OnTitleScreen);
        Place(title.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(250f, 36f), new Vector2(160f, 56f));

        gameOverRoot.SetActive(false);
    }

    Image CreateDot(string name, Transform parent, Sprite sprite, Color color, float size)
    {
        Image image = CreateImage(name, parent, color, sprite);
        UseMapMaterial(image);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        return image;
    }

    static Vector2 ToMap(Vector3 world)
    {
        float half = MapSize * 0.5f - 16f;
        float scale = half / SpotGenerator.WorldRadius;
        return new Vector2(world.x * scale, world.z * scale);
    }

    static Vector2 ClampToMap(Vector3 world)
    {
        Vector2 mapped = ToMap(world);
        float limit = MapSize * 0.5f - 12f;
        mapped.x = Mathf.Clamp(mapped.x, -limit, limit);
        mapped.y = Mathf.Clamp(mapped.y, -limit, limit);
        return mapped;
    }

    static Vector3 Flat(Vector3 vector)
    {
        vector.y = 0f;
        return vector;
    }

    static string FormatClock(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return (total / 60) + ":" + (total % 60).ToString("00");
    }

    static void SetText(TMP_Text label, string value)
    {
        if (label != null && label.text != value)
            label.text = value ?? "";
    }

    static Sprite CircleSprite()
    {
        if (circleSprite != null)
            return circleSprite;

        circleSprite = SpriteFromTexture(SoftDisc(64, false), "Hud Circle");
        return circleSprite;
    }

    static Sprite DiamondSprite()
    {
        if (diamondSprite != null)
            return diamondSprite;

        diamondSprite = SpriteFromTexture(SoftDisc(64, true), "Hud Diamond");
        return diamondSprite;
    }

    static Sprite HeartSprite()
    {
        if (heartSprite != null)
            return heartSprite;

        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Hud Heart";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = (x + 0.5f) / size * 2f - 1f;
                float py = (y + 0.5f) / size * 2f - 1f;
                float hx = px * 1.15f;
                float hy = -py * 1.15f + 0.2f;
                float a = hx * hx + hy * hy - 1f;
                bool inside = a * a * a - hx * hx * hy * hy * hy <= 0f;
                pixels[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        heartSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
        return heartSprite;
    }

    static Sprite ArrowSprite()
    {
        if (arrowSprite != null)
            return arrowSprite;

        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Hud Arrow";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        float cx = (size - 1) * 0.5f;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            float t = y / (float)(size - 1);
            float half = (1f - t) * (size * 0.42f);
            for (int x = 0; x < size; x++)
            {
                float edge = half - Mathf.Abs(x - cx);
                byte alpha = (byte)Mathf.Clamp(edge * 255f, 0f, 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        arrowSprite = SpriteFromTexture(texture, "Hud Arrow");
        return arrowSprite;
    }

    static Texture2D SoftDisc(int size, bool diamond)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        float radius = size * 0.5f - 1.5f;
        var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - center.x);
                float dy = Mathf.Abs(y - center.y);
                float distance = diamond ? dx + dy : Mathf.Sqrt(dx * dx + dy * dy);
                byte alpha = (byte)(Mathf.Clamp01((radius - distance) / 2.2f) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        return texture;
    }

    static Sprite SpriteFromTexture(Texture2D texture, string name)
    {
        texture.name = name;
        texture.Apply(false, false);
        var sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect);
        sprite.name = name;
        return sprite;
    }

    static AudioClip CreateChime()
    {
        const int sampleRate = 44100;
        const float duration = 0.48f;
        int count = Mathf.CeilToInt(sampleRate * duration);
        var data = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float attack = Mathf.Clamp01(t / 0.012f);
            float env = attack * Mathf.Exp(-3.2f * t);
            float tone = Mathf.Sin(2f * Mathf.PI * 784f * t) * 0.55f
                + Mathf.Sin(2f * Mathf.PI * 1175f * t) * 0.28f
                + Mathf.Sin(2f * Mathf.PI * 1568f * t) * 0.12f;
            data[i] = tone * env * 0.22f;
        }

        AudioClip clip = AudioClip.Create("Discover Chime", count, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    Canvas CreateCanvas(string name, int sortingOrder)
    {
        var canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    static TMP_FontAsset LoadFont()
    {
        TMP_Settings.LoadDefaultSettings();
        if (TMP_Settings.instance != null && TMP_Settings.defaultFontAsset != null)
            return TMP_Settings.defaultFontAsset;

        TMP_FontAsset resource = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (resource != null)
            return resource;

        Font builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (builtin == null)
            builtin = Font.CreateDynamicFontFromOSFont("Arial", 90);
        if (builtin == null)
            return null;

        return TMP_FontAsset.CreateFontAsset(builtin);
    }

    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    static void UseMapMaterial(Image image)
    {
        if (image == null)
            return;
        image.material = MapMaterial();
    }

    static Material MapMaterial()
    {
        if (mapMaterial != null)
            return mapMaterial;

        Shader shader = Shader.Find("UI/Default");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        mapMaterial = new Material(shader);
        mapMaterial.name = "Minimap Unlit";
        return mapMaterial;
    }

    static Image CreateImage(string name, Transform parent, Color color, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    Button CreateButton(Transform parent, TMP_FontAsset font, string label, UnityEngine.Events.UnityAction onClick)
    {
        var image = CreateImage(label + " Button", parent, new Color(0.93f, 0.78f, 0.42f, 1f), null);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.97f, 0.9f);
        colors.pressedColor = new Color(0.86f, 0.74f, 0.48f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
        button.colors = colors;
        var navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
        button.onClick.AddListener(onClick);

        var text = CreateText("Label", image.transform, font, label, 22, new Color(0.16f, 0.12f, 0.08f), FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        return button;
    }

    static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, string value, float size, Color color, FontStyles style, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        text.richText = false;
        return text;
    }

    static void Outline(TMP_Text text)
    {
        text.outlineWidth = 0.14f;
        text.outlineColor = new Color(0.02f, 0.05f, 0.1f, 0.9f);
        text.extraPadding = true;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = anchorMin;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
