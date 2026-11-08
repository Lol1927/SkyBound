using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DefaultExecutionOrder(-50)]
public class WorldManager : MonoBehaviour
{
    public static WorldManager Instance { get; private set; }
    public static bool FlightPaused { get; private set; } = true;

    public WorldData CurrentWorld { get; private set; }
    public bool IsStartPanelOpen => menu != null && menu.activeSelf;
    public bool IsTitleOpen => titleRoot != null && titleRoot.activeSelf;
    public bool IsHowToOpen => howToRoot != null && howToRoot.activeSelf;
    public bool IsMenuOpen => IsStartPanelOpen || IsTitleOpen || IsHowToOpen;
    public static event System.Action<WorldData> WorldGenerated;

    WorldGenerator generator;
    PlaneController plane;
    PlaneCamera planeCamera;
    GameObject menu;
    GameObject titleRoot;
    GameObject howToRoot;
    TMP_InputField promptField;
    Button generateButton;
    Button backButton;
    TextMeshProUGUI statusLabel;
    TextMeshProUGUI worldNameLabel;
    bool busy;
    bool orbiting;
    bool backdropReady;
    float orbitYaw;
    int nameToken;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindFirstObjectByType<WorldManager>() != null)
            return;

        var go = new GameObject("WorldManager");
        go.AddComponent<WorldManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        generator = FindFirstObjectByType<WorldGenerator>();
        plane = FindFirstObjectByType<PlaneController>();
        planeCamera = FindFirstObjectByType<PlaneCamera>();
        BuildUi();
    }

    void Start()
    {
        ApiKeys.Load();
        ShowTitle();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void LateUpdate()
    {
        if (!orbiting)
            return;

        if (planeCamera == null)
            planeCamera = FindFirstObjectByType<PlaneCamera>();
        Camera cam = planeCamera != null ? planeCamera.GetComponent<Camera>() : Camera.main;
        if (cam == null)
            cam = Camera.main;
        if (cam == null)
            return;

        orbitYaw += 8f * Time.unscaledDeltaTime;
        float rad = orbitYaw * Mathf.Deg2Rad;
        Vector3 position = new Vector3(Mathf.Sin(rad) * 480f, 150f, Mathf.Cos(rad) * 480f);
        cam.transform.SetPositionAndRotation(position, Quaternion.LookRotation(new Vector3(0f, 70f, 0f) - position, Vector3.up));
    }

    void Update()
    {
        if (busy || IsMenuOpen)
            return;

        if (LivesManager.Instance != null && LivesManager.Instance.IsDowned)
            return;

        if (Input.GetKeyDown(KeyCode.G))
        {
            RegenerateNewSeed();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
            ForceStyle("rolling");
        else if (Input.GetKeyDown(KeyCode.Alpha2))
            ForceStyle("mountainous");
        else if (Input.GetKeyDown(KeyCode.Alpha3))
            ForceStyle("plateau");
        else if (Input.GetKeyDown(KeyCode.Alpha4))
            ForceStyle("canyon");

        if (Input.GetKeyDown(KeyCode.Tab))
            ShowPanel();
    }

    void RegenerateNewSeed()
    {
        if (CurrentWorld == null)
            CurrentWorld = GeminiService.LoadFallback();
        if (CurrentWorld == null)
            return;

        CurrentWorld.seed = Random.Range(1, int.MaxValue);
        Rebuild();
    }

    void ForceStyle(string style)
    {
        if (CurrentWorld == null || CurrentWorld.islands == null)
            return;

        for (int i = 0; i < CurrentWorld.islands.Count; i++)
        {
            if (CurrentWorld.islands[i] == null)
                continue;
            CurrentWorld.islands[i].terrainStyle = style;
        }

        Rebuild();
    }

    void Rebuild()
    {
        if (generator == null)
            generator = FindFirstObjectByType<WorldGenerator>();
        if (generator == null || CurrentWorld == null)
            return;

        generator.Generate(CurrentWorld);
        EnterFlight(false);
    }

    public void OpenStartPanel()
    {
        ShowPanel();
    }

    public void SetFlightPaused(bool paused)
    {
        FlightPaused = paused;
        CloudGenerator.SetHoldBase(paused);
    }

    public void ShowTitle()
    {
        orbiting = true;
        nameToken++;
        if (worldNameLabel != null)
            worldNameLabel.gameObject.SetActive(false);
        if (menu != null)
            menu.SetActive(false);
        if (howToRoot != null)
            howToRoot.SetActive(false);
        if (titleRoot != null)
            titleRoot.SetActive(true);

        Time.timeScale = 1f;
        AudioListener.pause = false;
        EnsureBackdrop();
        SetFlightPaused(true);
        if (planeCamera == null)
            planeCamera = FindFirstObjectByType<PlaneCamera>();
        if (planeCamera != null)
            planeCamera.SuspendFollow = true;
    }

    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    public void ResetPlaneToStart()
    {
        if (plane == null)
            plane = FindFirstObjectByType<PlaneController>();
        if (plane != null)
            plane.ResetPlane();

        if (planeCamera == null)
            planeCamera = FindFirstObjectByType<PlaneCamera>();
        if (planeCamera != null)
            planeCamera.SnapToTarget();
    }

    void ShowPanel()
    {
        nameToken++;
        if (worldNameLabel != null)
            worldNameLabel.gameObject.SetActive(false);
        if (titleRoot != null)
            titleRoot.SetActive(false);
        if (howToRoot != null)
            howToRoot.SetActive(false);
        if (menu != null)
            menu.SetActive(true);
        SetFlightPaused(true);
        if (planeCamera == null)
            planeCamera = FindFirstObjectByType<PlaneCamera>();
        if (planeCamera != null)
            planeCamera.SuspendFollow = orbiting;
        SetWaiting(false);
        if (promptField != null)
            promptField.ActivateInputField();
    }

    void ShowHowTo()
    {
        orbiting = true;
        if (titleRoot != null)
            titleRoot.SetActive(false);
        if (menu != null)
            menu.SetActive(false);
        if (howToRoot != null)
            howToRoot.SetActive(true);
        SetFlightPaused(true);
        if (planeCamera == null)
            planeCamera = FindFirstObjectByType<PlaneCamera>();
        if (planeCamera != null)
            planeCamera.SuspendFollow = true;
    }

    void EnterFlight(bool showName)
    {
        PlacePlane();
        orbiting = false;
        if (titleRoot != null)
            titleRoot.SetActive(false);
        if (howToRoot != null)
            howToRoot.SetActive(false);
        if (menu != null)
            menu.SetActive(false);
        if (planeCamera == null)
            planeCamera = FindFirstObjectByType<PlaneCamera>();
        if (planeCamera != null)
            planeCamera.SuspendFollow = false;
        if (showName && CurrentWorld != null)
            ShowWorldName(CurrentWorld.worldName);
        SetFlightPaused(false);
        WorldGenerated?.Invoke(CurrentWorld);
    }

    void EnsureBackdrop()
    {
        if (backdropReady || CurrentWorld != null)
        {
            backdropReady = true;
            return;
        }

        if (generator == null)
            generator = FindFirstObjectByType<WorldGenerator>();
        if (generator == null)
            return;

        WorldData data = GeminiService.LoadFallback();
        if (data == null)
            return;

        generator.Generate(data);
        backdropReady = true;
    }

    void BeginGenerate()
    {
        if (busy)
            return;

        busy = true;
        SetWaiting(true);
        string description = promptField != null ? promptField.text : "";
        StartCoroutine(GeminiService.RequestWorld(description, OnWorldReady));
    }

    void OnWorldReady(WorldData data)
    {
        busy = false;
        if (data == null)
        {
            SetWaiting(false);
            return;
        }

        CurrentWorld = data;
        if (generator == null)
            generator = FindFirstObjectByType<WorldGenerator>();
        if (generator == null)
        {
            Debug.LogError("WorldManager could not find a WorldGenerator.");
            SetWaiting(false);
            return;
        }

        generator.Generate(data);
        EnterFlight(true);
    }

    void PlacePlane()
    {
        if (plane == null)
            plane = FindFirstObjectByType<PlaneController>();
        if (plane == null)
            return;

        float distance = SpotGenerator.WorldRadius + 20f;
        var position = new Vector3(0f, 120f, -distance);
        Vector3 aim = new Vector3(0f, 120f, 0f);
        if (generator != null && generator.TryNearestCluster(position, out Vector3 cluster))
            aim = cluster;
        Vector3 look = aim - position;
        if (look.sqrMagnitude < 0.01f)
            look = Vector3.forward;
        plane.Place(position, Quaternion.LookRotation(look.normalized, Vector3.up));

        if (planeCamera == null)
            planeCamera = FindFirstObjectByType<PlaneCamera>();
        if (planeCamera != null)
            planeCamera.SnapToTarget();
    }

    void ShowWorldName(string worldName)
    {
        if (worldNameLabel == null)
            return;

        worldNameLabel.text = string.IsNullOrEmpty(worldName) ? "" : worldName;
        worldNameLabel.gameObject.SetActive(true);
        int token = ++nameToken;
        StartCoroutine(HideWorldName(token));
    }

    IEnumerator HideWorldName(int token)
    {
        yield return new WaitForSeconds(3f);
        if (token != nameToken || worldNameLabel == null)
            yield break;
        worldNameLabel.gameObject.SetActive(false);
    }

    void SetWaiting(bool waiting)
    {
        if (statusLabel != null)
            statusLabel.text = waiting ? "Charting the skies..." : "";
        if (generateButton != null)
            generateButton.interactable = !waiting;
        if (backButton != null)
            backButton.interactable = !waiting;
        if (promptField != null)
            promptField.interactable = !waiting;
    }

    void BuildUi()
    {
        TMP_FontAsset font = LoadFont();
        if (font == null)
        {
            Debug.LogError("WorldManager could not create a TextMeshPro font.");
            return;
        }

        EnsureEventSystem();

        var canvasGo = new GameObject("Skybound Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        worldNameLabel = CreateText("World Name", canvasGo.transform, font, "", 40, Color.white, FontStyles.Bold, TextAlignmentOptions.Center, TextWrappingModes.Normal);
        SetRect(worldNameLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(1100f, 80f));
        worldNameLabel.gameObject.SetActive(false);

        BuildTitle(canvasGo.transform, font);
        BuildHowTo(canvasGo.transform, font);

        menu = new GameObject("Start Panel", typeof(RectTransform));
        menu.transform.SetParent(canvasGo.transform, false);
        var menuRect = menu.GetComponent<RectTransform>();
        Stretch(menuRect);

        var dim = CreateImage("Dim", menu.transform, new Color(0.02f, 0.05f, 0.1f, 0.55f));
        Stretch(dim.rectTransform);

        var card = CreateImage("Card", menu.transform, new Color(0.06f, 0.11f, 0.18f, 0.94f));
        SetRect(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 420f));

        var title = CreateText("Title", card.transform, font, "New world", 48, Color.white, FontStyles.Bold, TextAlignmentOptions.Center, TextWrappingModes.NoWrap);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(640f, 80f));

        promptField = CreateInput(card.transform, font);
        SetRect(promptField.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 16f), new Vector2(600f, 68f));
        promptField.onSubmit.AddListener(_ => BeginGenerate());

        generateButton = CreateButton(card.transform, font, "Generate", BeginGenerate);
        SetRect(generateButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-130f, 86f), new Vector2(240f, 56f));

        backButton = CreateButton(card.transform, font, "Back", ShowTitle);
        SetRect(backButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(160f, 86f), new Vector2(180f, 56f));

        statusLabel = CreateText("Status", card.transform, font, "", 24, new Color(0.95f, 0.86f, 0.62f), FontStyles.Italic, TextAlignmentOptions.Center, TextWrappingModes.NoWrap);
        SetRect(statusLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(600f, 40f));
        menu.SetActive(false);
    }

    void BuildTitle(Transform parent, TMP_FontAsset font)
    {
        titleRoot = new GameObject("Title Screen", typeof(RectTransform));
        titleRoot.transform.SetParent(parent, false);
        Stretch(titleRoot.GetComponent<RectTransform>());

        var dim = CreateImage("Dim", titleRoot.transform, new Color(0.02f, 0.05f, 0.1f, 0.28f));
        Stretch(dim.rectTransform);

        var heading = CreateText("Heading", titleRoot.transform, font, "SKYBOUND", 92, Color.white, FontStyles.Bold, TextAlignmentOptions.Center, TextWrappingModes.NoWrap);
        SetRect(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1200f, 120f));
        heading.outlineWidth = 0.16f;
        heading.outlineColor = new Color(0.02f, 0.05f, 0.1f, 0.9f);

        var subtitle = CreateText("Subtitle", titleRoot.transform, font, "Describe a sky. Fly through it.", 30, new Color(0.95f, 0.86f, 0.62f), FontStyles.Italic, TextAlignmentOptions.Center, TextWrappingModes.NoWrap);
        SetRect(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(1000f, 48f));
        subtitle.outlineWidth = 0.14f;
        subtitle.outlineColor = new Color(0.02f, 0.05f, 0.1f, 0.85f);

        Button start = CreateButton(titleRoot.transform, font, "Start", ShowPanel);
        SetRect(start.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 36f), new Vector2(340f, 64f));

        Button howTo = CreateButton(titleRoot.transform, font, "How to Play", ShowHowTo);
        SetRect(howTo.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -48f), new Vector2(340f, 64f));

        Button quit = CreateButton(titleRoot.transform, font, "Quit", QuitGame);
        SetRect(quit.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -132f), new Vector2(340f, 64f));
    }

    void BuildHowTo(Transform parent, TMP_FontAsset font)
    {
        howToRoot = new GameObject("How To Play", typeof(RectTransform));
        howToRoot.transform.SetParent(parent, false);
        Stretch(howToRoot.GetComponent<RectTransform>());

        var dim = CreateImage("Dim", howToRoot.transform, new Color(0.02f, 0.05f, 0.1f, 0.55f));
        Stretch(dim.rectTransform);

        var card = CreateImage("Card", howToRoot.transform, new Color(0.06f, 0.11f, 0.18f, 0.94f));
        SetRect(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 640f));

        var heading = CreateText("Heading", card.transform, font, "How to Play", 48, Color.white, FontStyles.Bold, TextAlignmentOptions.Center, TextWrappingModes.NoWrap);
        SetRect(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(740f, 72f));

        string body =
            "Find 3 landmarks. You have 3 lives.\n\n" +
            "W / S      Pitch\n" +
            "A / D      Turn\n" +
            "Shift      Boost\n" +
            "R          Reset to a safe point\n" +
            "Q          Ask the co-pilot\n" +
            "Tab        New world\n" +
            "Esc        Pause\n\n" +
            "Hitting an island, a rock, or the ocean costs a life.";
        var copy = CreateText("Body", card.transform, font, body, 26, new Color(0.96f, 0.94f, 0.88f), FontStyles.Normal, TextAlignmentOptions.TopLeft, TextWrappingModes.Normal);
        SetRect(copy.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(700f, 420f));

        Button back = CreateButton(card.transform, font, "Back", ShowTitle);
        SetRect(back.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(220f, 56f));
        howToRoot.SetActive(false);
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

        var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        events.transform.SetParent(null);
    }

    TMP_InputField CreateInput(Transform parent, TMP_FontAsset font)
    {
        var root = CreateImage("Prompt", parent, new Color(0.96f, 0.97f, 0.98f, 1f));
        var field = root.gameObject.AddComponent<TMP_InputField>();

        var viewport = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(root.transform, false);
        var viewportRect = viewport.GetComponent<RectTransform>();
        Stretch(viewportRect);
        viewportRect.offsetMin = new Vector2(16f, 8f);
        viewportRect.offsetMax = new Vector2(-16f, -8f);

        var placeholder = CreateText("Placeholder", viewport.transform, font, "Describe the sky you want to fly in...", 24, new Color(0.35f, 0.42f, 0.5f, 0.85f), FontStyles.Italic, TextAlignmentOptions.MidlineLeft, TextWrappingModes.NoWrap);
        Stretch(placeholder.rectTransform);
        var text = CreateText("Text", viewport.transform, font, "", 24, new Color(0.1f, 0.14f, 0.18f), FontStyles.Normal, TextAlignmentOptions.MidlineLeft, TextWrappingModes.NoWrap);
        Stretch(text.rectTransform);

        field.enabled = false;
        field.textViewport = viewportRect;
        field.textComponent = text;
        field.placeholder = placeholder;
        field.fontAsset = font;
        field.pointSize = 24;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.characterLimit = 240;
        field.caretColor = new Color(0.1f, 0.14f, 0.18f);
        field.customCaretColor = true;
        field.enabled = true;
        return field;
    }

    Button CreateButton(Transform parent, TMP_FontAsset font, string label, UnityEngine.Events.UnityAction onClick)
    {
        var image = CreateImage(label + " Button", parent, new Color(0.93f, 0.78f, 0.42f, 1f));
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

        var text = CreateText("Label", image.transform, font, label, 26, new Color(0.16f, 0.12f, 0.08f), FontStyles.Bold, TextAlignmentOptions.Center, TextWrappingModes.NoWrap);
        Stretch(text.rectTransform);
        return button;
    }

    static Image CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, string value, float size, Color color, FontStyles style, TextAlignmentOptions alignment, TextWrappingModes wrapping)
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
        text.textWrappingMode = wrapping;
        text.raycastTarget = false;
        return text;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
