using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CoPilot : MonoBehaviour
{
    public static CoPilot Instance { get; private set; }

    const float HintRange = 150f;
    const float IntroDelay = 1f;
    const float BoundaryCooldown = 10f;
    const float AskCooldown = 8f;
    const float CaptionFadeSeconds = 0.45f;

    const string BoundaryLine = "Whoa, let's turn back toward the islands!";
    const string CongratsLine = "That's all three landmarks. Nice flying!";
    const string AskFallback = "The radio cut out. Keep an eye on the horizon for me.";
    const string ThinkingLine = "Co-pilot is thinking...";

    const string AskSystem =
        "You are a friendly co-pilot in a peaceful airplane game about exploring floating sky islands. " +
        "The player just asked you for help. Reply with one spoken line under 20 words. " +
        "Be warm and useful, but do not give the exact answer away: no coordinates, no distances, and do not name the landmark. " +
        "A direction nudge is enough. No quotes, emoji, or stage directions.";

    enum SpeechKind
    {
        Normal,
        Hint,
        Lore,
        Boundary,
        Ask
    }

    class Speech
    {
        public string spoken;
        public string caption;
        public SpeechKind kind;
        public bool captionOnly;
        public bool fallbackOnMiss;
    }

    readonly List<Speech> queue = new List<Speech>();
    readonly List<Landmark> landmarks = new List<Landmark>();
    readonly HashSet<Landmark> hinted = new HashSet<Landmark>();

    AudioSource voice;
    FishAudioService speechService;
    WorldBoundary boundary;
    WorldData activeWorld;
    Speech active;
    Coroutine speaker;
    Coroutine introDelay;
    GameObject captionRoot;
    CanvasGroup captionGroup;
    TextMeshProUGUI captionLabel;
    bool interruptHint;
    bool askPending;
    bool congratsSaid;
    bool explorationBound;
    float nextBoundaryTime;
    float nextAskTime;
    int speechGeneration;

    public bool IsSpeaking =>
        speaker != null || active != null || queue.Count > 0 || introDelay != null || askPending;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindFirstObjectByType<CoPilot>() != null)
            return;

        var go = new GameObject("CoPilot");
        go.AddComponent<CoPilot>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.loop = false;
        voice.spatialBlend = 0f;
        speechService = gameObject.AddComponent<FishAudioService>();
        BuildUi();
    }

    void OnEnable()
    {
        if (Instance != this)
            return;

        WorldManager.WorldGenerated += OnWorldGenerated;
        BindExploration();
        BindBoundary();
    }

    void Start()
    {
        BindExploration();
        if (WorldManager.Instance != null && WorldManager.Instance.CurrentWorld != null)
            OnWorldGenerated(WorldManager.Instance.CurrentWorld);
    }

    void OnDisable()
    {
        WorldManager.WorldGenerated -= OnWorldGenerated;
        UnbindExploration();
        if (boundary != null)
            boundary.OutOfBoundsChanged -= OnOutOfBounds;
        boundary = null;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        BindExploration();
        if (Time.timeScale <= 0f)
            return;

        bool flying = !WorldManager.FlightPaused && (LivesManager.Instance == null || !LivesManager.Instance.IsDowned);
        if (flying && askPending && active == null && speaker == null)
            ShowCaption(ThinkingLine);

        if (!flying)
            return;

        BindBoundary();
        WatchLandmarks();
        if (boundary != null && boundary.IsOutOfBounds)
            TryBoundaryWarning();

        if (Input.GetKeyDown(KeyCode.Q))
            Ask();
    }

    void OnWorldGenerated(WorldData world)
    {
        if (world == null)
            return;

        bool fresh = world != activeWorld;
        activeWorld = world;
        speechGeneration++;
        StopTalking();
        askPending = false;
        nextAskTime = 0f;
        nextBoundaryTime = 0f;
        RefreshLandmarks();
        Prefetch(world);
        if (fresh)
            introDelay = StartCoroutine(IntroAfterDelay(world, speechGeneration));
    }

    void OnLandmarkDiscovered(Landmark landmark)
    {
        if (landmark == null)
            return;

        hinted.Add(landmark);
        if (!landmarks.Contains(landmark))
            landmarks.Add(landmark);
        if (!string.IsNullOrWhiteSpace(landmark.landmarkLore))
            Say(Excited(landmark.landmarkLore), SpeechKind.Lore);
    }

    void OnWorldCompleted()
    {
        if (congratsSaid)
            return;

        congratsSaid = true;
        Say(CongratsLine, SpeechKind.Normal);
    }

    void OnRunRestarted()
    {
        hinted.Clear();
        congratsSaid = false;
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

    void Ask()
    {
        if (askPending || Time.time < nextAskTime || activeWorld == null)
            return;

        askPending = true;
        nextAskTime = Time.time + AskCooldown;
        if (active == null)
            ShowCaption(ThinkingLine);
        StartCoroutine(AskRoutine(speechGeneration));
    }

    IEnumerator AskRoutine(int generation)
    {
        string prompt = BuildAskPrompt();
        string reply = null;
        yield return GeminiService.RequestText(AskSystem, prompt, value => reply = value);
        if (generation != speechGeneration)
            yield break;

        askPending = false;
        reply = LimitWords(reply, 20);
        if (string.IsNullOrWhiteSpace(reply))
        {
            Enqueue(new Speech
            {
                spoken = AskFallback,
                caption = AskFallback,
                kind = SpeechKind.Ask,
                captionOnly = true
            });
            yield break;
        }

        Enqueue(new Speech
        {
            spoken = reply,
            caption = Caption(reply),
            kind = SpeechKind.Ask,
            fallbackOnMiss = true
        });
    }

    IEnumerator IntroAfterDelay(WorldData world, int generation)
    {
        yield return new WaitForSeconds(IntroDelay);
        introDelay = null;
        if (generation != speechGeneration || world != activeWorld || WorldManager.FlightPaused)
            yield break;
        if (string.IsNullOrWhiteSpace(world.coPilotIntro))
            yield break;

        Say(world.coPilotIntro, SpeechKind.Normal);
    }

    void Prefetch(WorldData world)
    {
        if (speechService == null || world == null)
            return;

        speechService.Request(world.coPilotIntro, null, false);
        speechService.Request(BoundaryLine, null, false);
        speechService.Request(CongratsLine, null, false);
        if (world.islands == null)
            return;

        for (int i = 0; i < world.islands.Count; i++)
        {
            IslandData island = world.islands[i];
            if (island == null || !island.hasLandmark)
                continue;

            speechService.Request(island.landmarkHint, null, false);
            if (!string.IsNullOrWhiteSpace(island.landmarkLore))
                speechService.Request(Excited(island.landmarkLore), null, false);
        }
    }

    void WatchLandmarks()
    {
        PlaneController plane = FindPlane();
        if (plane == null)
            return;

        Vector3 position = plane.transform.position;
        float hintRangeSqr = HintRange * HintRange;
        for (int i = landmarks.Count - 1; i >= 0; i--)
        {
            Landmark landmark = landmarks[i];
            if (landmark == null)
            {
                landmarks.RemoveAt(i);
                continue;
            }

            if (landmark.discovered || hinted.Contains(landmark))
                continue;
            if ((landmark.transform.position - position).sqrMagnitude > hintRangeSqr)
                continue;

            hinted.Add(landmark);
            Say(landmark.landmarkHint, SpeechKind.Hint);
        }
    }

    void BindBoundary()
    {
        if (boundary != null)
            return;

        boundary = FindFirstObjectByType<WorldBoundary>();
        if (boundary != null)
            boundary.OutOfBoundsChanged += OnOutOfBounds;
    }

    void OnOutOfBounds(bool outside)
    {
        if (outside && !WorldManager.FlightPaused)
            TryBoundaryWarning();
    }

    void TryBoundaryWarning()
    {
        if (Time.time < nextBoundaryTime)
            return;

        nextBoundaryTime = Time.time + BoundaryCooldown;
        Say(BoundaryLine, SpeechKind.Boundary);
    }

    void RefreshLandmarks()
    {
        landmarks.Clear();
        hinted.Clear();
        congratsSaid = false;
        Landmark[] found = FindObjectsByType<Landmark>(FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null)
                landmarks.Add(found[i]);
        }
    }

    public void SayLine(string text)
    {
        Say(text, SpeechKind.Normal);
    }

    void Say(string text, SpeechKind kind)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        text = text.Trim();
        Enqueue(new Speech
        {
            spoken = text,
            caption = Caption(text),
            kind = kind
        });
    }

    void Enqueue(Speech speech)
    {
        if (speech == null || string.IsNullOrWhiteSpace(speech.caption))
            return;

        if (speech.kind == SpeechKind.Boundary && active != null && active.kind == SpeechKind.Hint)
        {
            interruptHint = true;
            if (voice != null && voice.isPlaying)
                voice.Stop();
            queue.Insert(0, speech);
            if (speaker == null)
                speaker = StartCoroutine(SpeakQueue());
            return;
        }

        if (speech.kind == SpeechKind.Boundary)
        {
            int index = queue.FindIndex(item => item.kind == SpeechKind.Hint);
            if (index < 0)
                queue.Add(speech);
            else
                queue.Insert(index, speech);
        }
        else
        {
            queue.Add(speech);
        }

        if (speaker == null)
            speaker = StartCoroutine(SpeakQueue());
    }

    IEnumerator SpeakQueue()
    {
        while (queue.Count > 0)
        {
            active = queue[0];
            queue.RemoveAt(0);
            yield return Speak(active);
            active = null;
        }

        speaker = null;
    }

    IEnumerator Speak(Speech speech)
    {
        if (interruptHint && speech.kind == SpeechKind.Hint)
            yield break;

        interruptHint = false;
        AudioClip clip = null;
        if (speech.captionOnly || speechService == null)
        {
            string caption = speech.fallbackOnMiss && speechService == null ? AskFallback : speech.caption;
            yield return Present(caption, null, speech.kind);
            yield break;
        }

        bool showThinking = speech.kind == SpeechKind.Ask && !speechService.HasClip(speech.spoken);
        ShowCaption(showThinking ? ThinkingLine : speech.caption);

        bool ready = false;
        speechService.Request(speech.spoken, value =>
        {
            clip = value;
            ready = true;
        }, true);

        while (!ready)
        {
            if (interruptHint && speech.kind == SpeechKind.Hint)
                yield break;
            yield return null;
        }

        if (interruptHint && speech.kind == SpeechKind.Hint)
            yield break;

        if (clip == null && speech.fallbackOnMiss)
        {
            yield return Present(AskFallback, null, speech.kind);
            yield break;
        }

        yield return Present(speech.caption, clip, speech.kind);
    }

    IEnumerator Present(string caption, AudioClip clip, SpeechKind kind)
    {
        if (string.IsNullOrWhiteSpace(caption))
            yield break;

        ShowCaption(caption);
        if (clip != null && voice != null)
        {
            voice.clip = clip;
            voice.Play();
            float limit = Mathf.Max(0.2f, clip.length) + 0.35f;
            float elapsed = 0f;
            while (voice.isPlaying && elapsed < limit)
            {
                if (interruptHint && kind == SpeechKind.Hint)
                {
                    voice.Stop();
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            float hold = Mathf.Clamp(caption.Length * 0.065f, 2.2f, 6f);
            float elapsed = 0f;
            while (elapsed < hold)
            {
                if (interruptHint && kind == SpeechKind.Hint)
                    yield break;
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        yield return FadeCaption(kind);
    }

    void StopTalking()
    {
        interruptHint = false;
        if (introDelay != null)
        {
            StopCoroutine(introDelay);
            introDelay = null;
        }

        if (speaker != null)
        {
            StopCoroutine(speaker);
            speaker = null;
        }

        queue.Clear();
        active = null;
        if (voice != null)
            voice.Stop();
        HideCaption();
    }

    string BuildAskPrompt()
    {
        WorldData world = activeWorld;
        string worldName = world != null && !string.IsNullOrWhiteSpace(world.worldName) ? world.worldName.Trim() : "this sky";
        PlaneController plane = FindPlane();
        Vector3 position = plane != null ? plane.transform.position : Vector3.zero;

        int found = 0;
        int total = 0;
        Landmark nearest = null;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < landmarks.Count; i++)
        {
            Landmark landmark = landmarks[i];
            if (landmark == null)
                continue;

            total++;
            if (landmark.discovered)
            {
                found++;
                continue;
            }

            float distance = (landmark.transform.position - position).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = landmark;
            }
        }

        var builder = new StringBuilder();
        builder.Append("World: ").Append(worldName).Append('\n');
        builder.Append("Landmarks found: ").Append(found).Append(" of ").Append(Mathf.Max(total, 3)).Append('\n');
        if (plane != null)
            builder.Append("Plane altitude: ").Append(Mathf.RoundToInt(plane.transform.position.y)).Append('\n');
        else
            builder.Append("Plane altitude: unknown\n");

        if (nearest == null)
        {
            builder.Append("Every landmark has already been found.\n");
        }
        else if (plane != null)
        {
            string horizontal;
            string vertical;
            DescribeDirection(plane.transform, nearest.transform.position, out horizontal, out vertical);
            string name = string.IsNullOrWhiteSpace(nearest.landmarkName) ? "an unnamed landmark" : nearest.landmarkName.Trim();
            builder.Append("Nearest undiscovered landmark: ").Append(name)
                .Append(". It is ").Append(horizontal)
                .Append(" and ").Append(vertical)
                .Append(" relative to the plane.\n");
        }
        else
        {
            builder.Append("A landmark is still undiscovered.\n");
        }

        builder.Append("Give one helpful co-pilot line.");
        return builder.ToString();
    }

    static void DescribeDirection(Transform plane, Vector3 target, out string horizontal, out string vertical)
    {
        Vector3 to = target - plane.position;
        Vector3 flatTo = Vector3.ProjectOnPlane(to, Vector3.up);
        Vector3 flatForward = Vector3.ProjectOnPlane(plane.forward, Vector3.up);
        if (flatTo.sqrMagnitude < 0.01f || flatForward.sqrMagnitude < 0.01f)
        {
            horizontal = "ahead";
        }
        else
        {
            float angle = Vector3.SignedAngle(flatForward, flatTo, Vector3.up);
            float abs = Mathf.Abs(angle);
            if (abs <= 45f)
                horizontal = "ahead";
            else if (abs >= 135f)
                horizontal = "behind";
            else
                horizontal = angle > 0f ? "right" : "left";
        }

        vertical = target.y >= plane.position.y ? "above" : "below";
    }

    static string Excited(string lore)
    {
        return "(excited) " + lore.Trim();
    }

    static string Caption(string line)
    {
        if (string.IsNullOrEmpty(line))
            return "";

        var builder = new StringBuilder(line.Length);
        bool pendingSpace = false;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '(')
            {
                int end = line.IndexOf(')', i + 1);
                if (end > i)
                {
                    i = end;
                    pendingSpace = builder.Length > 0;
                    continue;
                }
            }

            if (char.IsWhiteSpace(line[i]))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(line[i]);
        }

        return builder.ToString();
    }

    static string LimitWords(string text, int maxWords)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        text = text.Trim();
        int words = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsWhiteSpace(text[i]))
                continue;
            if (i != 0 && !char.IsWhiteSpace(text[i - 1]))
                continue;

            words++;
            if (words > maxWords)
                return text.Substring(0, i).Trim();
        }

        return text;
    }

    static PlaneController FindPlane()
    {
        return FindFirstObjectByType<PlaneController>();
    }

    void ShowCaption(string text)
    {
        if (captionRoot == null || captionLabel == null || captionGroup == null)
            return;
        if (captionRoot.activeSelf && captionLabel.text == text && captionGroup.alpha >= 0.99f)
            return;

        captionLabel.text = text ?? "";
        captionGroup.alpha = 1f;
        captionRoot.SetActive(true);
    }

    void HideCaption()
    {
        if (captionGroup != null)
            captionGroup.alpha = 0f;
        if (captionRoot != null)
            captionRoot.SetActive(false);
    }

    IEnumerator FadeCaption(SpeechKind kind)
    {
        if (captionGroup == null)
            yield break;

        float elapsed = 0f;
        while (elapsed < CaptionFadeSeconds)
        {
            if (interruptHint && kind == SpeechKind.Hint)
                yield break;

            elapsed += Time.deltaTime;
            captionGroup.alpha = 1f - Mathf.Clamp01(elapsed / CaptionFadeSeconds);
            yield return null;
        }

        HideCaption();
    }

    void BuildUi()
    {
        TMP_FontAsset font = LoadFont();
        if (font == null)
        {
            Debug.LogError("CoPilot could not create a TextMeshPro font.");
            return;
        }

        var canvasObject = new GameObject("CoPilot Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        captionRoot = new GameObject("Subtitle", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        captionRoot.transform.SetParent(canvasObject.transform, false);
        captionGroup = captionRoot.GetComponent<CanvasGroup>();
        captionGroup.blocksRaycasts = false;
        captionGroup.interactable = false;
        var background = captionRoot.GetComponent<Image>();
        background.color = new Color(0.05f, 0.08f, 0.14f, 0.72f);
        background.raycastTarget = false;
        var captionRect = captionRoot.GetComponent<RectTransform>();
        captionRect.anchorMin = new Vector2(0.5f, 0f);
        captionRect.anchorMax = new Vector2(0.5f, 0f);
        captionRect.pivot = new Vector2(0.5f, 0f);
        captionRect.anchoredPosition = new Vector2(0f, 72f);
        captionRect.sizeDelta = new Vector2(920f, 96f);

        captionLabel = CreateText("Subtitle Label", captionRoot.transform, font, "", 28, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
        Stretch(captionLabel.rectTransform);
        captionLabel.margin = new Vector4(22f, 10f, 22f, 10f);
        captionRoot.SetActive(false);
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
}
