using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public static class GeminiService
{
    const string Endpoint = "https://generativelanguage.googleapis.com/v1beta/models/";
    const int TimeoutSeconds = 20;

    const string SystemInstruction =
        "Act as an art director. Choose palette colors, skyMood, and numeric values that strongly match the player's description. " +
        "If the player asks for something not in the enums (e.g. desert, alien, candy), pick the closest biome and express the idea through palette, skyMood, and numbers.\n" +
        "Design a world with 3 to 6 clusters based on the player's description. Each entry in islands is one cluster of floating islands.\n" +
        "Exactly 3 clusters must have landmarks. Clusters without landmarks use \"none\" for landmarkType and empty strings for landmarkName, landmarkHint, and landmarkLore.\n" +
        "Write coPilotIntro, landmarkHint, and landmarkLore as short, friendly lines a co-pilot would say out loud (under 25 words each).\n" +
        "Colors are hex strings like #RRGGBB. heightScale is 0.5 to 2. roughness, treeDensity, rockDensity, and floatingRockDensity are 0 to 1. verticalSpread is 0.3 to 1.5.";

    const string ResponseSchema =
        "{" +
        "\"type\":\"object\"," +
        "\"properties\":{" +
            "\"worldName\":{\"type\":\"string\"}," +
            "\"seed\":{\"type\":\"integer\"}," +
            "\"layout\":{\"type\":\"string\",\"enum\":[\"scattered\",\"central\",\"ring\"]}," +
            "\"cloudDensity\":{\"type\":\"number\"}," +
            "\"skyMood\":{\"type\":\"string\",\"enum\":[\"sunrise\",\"noon\",\"sunset\",\"night\",\"storm\"]}," +
            "\"palette\":{" +
                "\"type\":\"object\"," +
                "\"properties\":{" +
                    "\"grass\":{\"type\":\"string\"}," +
                    "\"rock\":{\"type\":\"string\"}," +
                    "\"snow\":{\"type\":\"string\"}," +
                    "\"water\":{\"type\":\"string\"}," +
                    "\"skyTop\":{\"type\":\"string\"}," +
                    "\"skyHorizon\":{\"type\":\"string\"}," +
                    "\"fog\":{\"type\":\"string\"}," +
                    "\"accent\":{\"type\":\"string\"}" +
                "}," +
                "\"required\":[\"grass\",\"rock\",\"snow\",\"water\",\"skyTop\",\"skyHorizon\",\"fog\",\"accent\"]" +
            "}," +
            "\"treeDensity\":{\"type\":\"number\"}," +
            "\"rockDensity\":{\"type\":\"number\"}," +
            "\"floatingRockDensity\":{\"type\":\"number\"}," +
            "\"coPilotIntro\":{\"type\":\"string\"}," +
            "\"islands\":{" +
                "\"type\":\"array\"," +
                "\"items\":{" +
                    "\"type\":\"object\"," +
                    "\"properties\":{" +
                        "\"biome\":{\"type\":\"string\",\"enum\":[\"forest\",\"snow\",\"volcanic\"]}," +
                        "\"size\":{\"type\":\"string\",\"enum\":[\"small\",\"medium\",\"large\"]}," +
                        "\"terrainStyle\":{\"type\":\"string\",\"enum\":[\"rolling\",\"mountainous\",\"plateau\",\"canyon\"]}," +
                        "\"heightScale\":{\"type\":\"number\"}," +
                        "\"roughness\":{\"type\":\"number\"}," +
                        "\"verticalSpread\":{\"type\":\"number\"}," +
                        "\"features\":{\"type\":\"array\",\"items\":{\"type\":\"string\",\"enum\":[\"terraces\",\"lake\",\"arch\",\"spires\"]}}," +
                        "\"hasLandmark\":{\"type\":\"boolean\"}," +
                        "\"landmarkType\":{\"type\":\"string\",\"enum\":[\"none\",\"crystal\",\"tower\",\"ruins\"]}," +
                        "\"landmarkName\":{\"type\":\"string\"}," +
                        "\"landmarkHint\":{\"type\":\"string\"}," +
                        "\"landmarkLore\":{\"type\":\"string\"}" +
                    "}," +
                    "\"required\":[\"biome\",\"size\",\"terrainStyle\",\"heightScale\",\"roughness\",\"verticalSpread\",\"features\",\"hasLandmark\",\"landmarkType\",\"landmarkName\",\"landmarkHint\",\"landmarkLore\"]" +
                "}" +
            "}" +
        "}," +
        "\"required\":[\"worldName\",\"seed\",\"layout\",\"cloudDensity\",\"skyMood\",\"palette\",\"treeDensity\",\"rockDensity\",\"floatingRockDensity\",\"coPilotIntro\",\"islands\"]" +
        "}";

    [Serializable]
    class GeminiResponse
    {
        public GeminiCandidate[] candidates;
    }

    [Serializable]
    class GeminiCandidate
    {
        public GeminiContent content;
    }

    [Serializable]
    class GeminiContent
    {
        public GeminiPart[] parts;
    }

    [Serializable]
    class GeminiPart
    {
        public string text;
        public bool thought;
    }

    public static IEnumerator RequestWorld(string description, Action<WorldData> onComplete)
    {
        ApiKeys.Load();
        if (!ApiKeys.HasGeminiCredentials || !ApiKeys.IsModelId(ApiKeys.GeminiModel))
        {
            UseFallback("Gemini credentials are unavailable.", onComplete);
            yield break;
        }

        string url = Endpoint + ApiKeys.GeminiModel + ":generateContent";
        byte[] body = Encoding.UTF8.GetBytes(BuildBody(description));
        using (var request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = TimeoutSeconds;
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("x-goog-api-key", ApiKeys.GeminiApiKey);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                UseFallback(FailureReason(request), onComplete);
                yield break;
            }

            WorldData parsed = ParseWorld(request.downloadHandler.text);
            if (parsed == null)
            {
                UseFallback("Gemini returned invalid world JSON.", onComplete);
                yield break;
            }

            Finish(parsed, onComplete);
        }
    }

    public static IEnumerator RequestText(string systemInstruction, string prompt, Action<string> onComplete)
    {
        ApiKeys.Load();
        if (!ApiKeys.HasGeminiCredentials || !ApiKeys.IsModelId(ApiKeys.GeminiModel))
        {
            Debug.LogWarning("Gemini text request skipped because credentials are unavailable.");
            if (onComplete != null)
                onComplete(null);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(systemInstruction))
            systemInstruction = "Reply with one short sentence.";
        if (string.IsNullOrWhiteSpace(prompt))
            prompt = "Say hello.";

        string url = Endpoint + ApiKeys.GeminiModel + ":generateContent";
        string payload =
            "{" +
            "\"systemInstruction\":{\"parts\":[{\"text\":" + JsonString(systemInstruction) + "}]}," +
            "\"contents\":[{\"role\":\"user\",\"parts\":[{\"text\":" + JsonString(prompt) + "}]}]," +
            "\"generationConfig\":{" +
                "\"thinkingConfig\":{\"thinkingLevel\":\"LOW\"}," +
                "\"responseMimeType\":\"text/plain\"" +
            "}}";

        byte[] body = Encoding.UTF8.GetBytes(payload);
        using (var request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = TimeoutSeconds;
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("x-goog-api-key", ApiKeys.GeminiApiKey);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("Gemini text request failed. " + FailureReason(request));
                if (onComplete != null)
                    onComplete(null);
                yield break;
            }

            string text = PlainTextFromResponse(request.downloadHandler.text);
            if (onComplete != null)
                onComplete(string.IsNullOrWhiteSpace(text) ? null : text.Trim());
        }
    }

    public static WorldData LoadFallback()
    {
        TextAsset[] worlds = Resources.LoadAll<TextAsset>("Worlds");
        if (worlds == null || worlds.Length == 0)
        {
            Debug.LogError("No fallback worlds found in Assets/Resources/Worlds.");
            return null;
        }

        TextAsset chosen = worlds[UnityEngine.Random.Range(0, worlds.Length)];
        if (chosen == null || string.IsNullOrEmpty(chosen.text))
        {
            Debug.LogWarning("A fallback world file was empty.");
            return null;
        }

        WorldData data;
        try
        {
            data = JsonUtility.FromJson<WorldData>(chosen.text);
        }
        catch (Exception)
        {
            data = null;
        }

        if (data == null || data.islands == null)
        {
            Debug.LogWarning("Fallback world '" + chosen.name + "' could not be parsed.");
            return null;
        }

        return data;
    }

    static void UseFallback(string reason, Action<WorldData> onComplete)
    {
        WorldData fallback = LoadFallback();
        string name = fallback != null && !string.IsNullOrEmpty(fallback.worldName) ? fallback.worldName : "none";
        Debug.LogWarning(reason + " Loading fallback world \"" + name + "\".");
        Finish(fallback, onComplete);
    }

    static void Finish(WorldData data, Action<WorldData> onComplete)
    {
        if (data != null)
            data = WorldValidator.Validate(data);
        if (onComplete != null)
            onComplete(data);
    }

    static string FailureReason(UnityWebRequest request)
    {
        long code = request.responseCode;
        if (code == 429)
            return "Gemini rate limit reached (429).";
        if (request.result == UnityWebRequest.Result.ConnectionError)
            return "Gemini request failed to connect (timeout is " + TimeoutSeconds + " seconds).";
        if (code > 0)
            return "Gemini request failed (HTTP " + code + ").";
        return "Gemini request failed.";
    }

    static string BuildBody(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            description = "A peaceful sky over a quiet ocean.";

        return "{" +
            "\"systemInstruction\":{\"parts\":[{\"text\":" + JsonString(SystemInstruction) + "}]}," +
            "\"contents\":[{\"role\":\"user\",\"parts\":[{\"text\":" + JsonString(description.Trim()) + "}]}]," +
            "\"generationConfig\":{" +
                "\"thinkingConfig\":{\"thinkingLevel\":\"LOW\"}," +
                "\"responseMimeType\":\"application/json\"," +
                "\"responseSchema\":" + ResponseSchema +
            "}}";
    }

    static WorldData ParseWorld(string response)
    {
        string json = WorldJsonFromResponse(response);
        if (string.IsNullOrEmpty(json))
            return null;

        WorldData data;
        try
        {
            data = JsonUtility.FromJson<WorldData>(json);
        }
        catch (Exception)
        {
            return null;
        }

        if (data == null || data.islands == null || data.islands.Count == 0)
            return null;
        return data;
    }

    static string WorldJsonFromResponse(string response)
    {
        if (string.IsNullOrEmpty(response))
            return null;

        GeminiResponse envelope = null;
        try
        {
            envelope = JsonUtility.FromJson<GeminiResponse>(response);
        }
        catch (Exception)
        {
            envelope = null;
        }

        if (envelope != null && envelope.candidates != null)
        {
            for (int c = 0; c < envelope.candidates.Length; c++)
            {
                GeminiContent content = envelope.candidates[c] != null ? envelope.candidates[c].content : null;
                if (content == null || content.parts == null)
                    continue;

                var builder = new StringBuilder();
                for (int i = 0; i < content.parts.Length; i++)
                {
                    GeminiPart part = content.parts[i];
                    if (part == null || part.thought || string.IsNullOrEmpty(part.text))
                        continue;
                    builder.Append(part.text);
                }

                string extracted = IsolateObject(builder.ToString());
                if (LooksLikeWorld(extracted))
                    return extracted;
            }
        }

        string raw = IsolateObject(response);
        return LooksLikeWorld(raw) ? raw : null;
    }

    static string PlainTextFromResponse(string response)
    {
        if (string.IsNullOrEmpty(response))
            return null;

        GeminiResponse envelope = null;
        try
        {
            envelope = JsonUtility.FromJson<GeminiResponse>(response);
        }
        catch (Exception)
        {
            envelope = null;
        }

        if (envelope == null || envelope.candidates == null)
            return null;

        var builder = new StringBuilder();
        for (int c = 0; c < envelope.candidates.Length; c++)
        {
            GeminiContent content = envelope.candidates[c] != null ? envelope.candidates[c].content : null;
            if (content == null || content.parts == null)
                continue;

            for (int i = 0; i < content.parts.Length; i++)
            {
                GeminiPart part = content.parts[i];
                if (part == null || part.thought || string.IsNullOrEmpty(part.text))
                    continue;
                if (builder.Length > 0)
                    builder.Append('\n');
                builder.Append(part.text);
            }
        }

        return CleanReply(builder.ToString());
    }

    static string CleanReply(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        text = text.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            int firstNewline = text.IndexOf('\n');
            if (firstNewline >= 0)
                text = text.Substring(firstNewline + 1);
            if (text.EndsWith("```", StringComparison.Ordinal))
                text = text.Substring(0, text.Length - 3);
            text = text.Trim();
        }

        if (text.Length >= 2 && text[0] == '"' && text[text.Length - 1] == '"')
            text = text.Substring(1, text.Length - 2).Trim();

        int newline = text.IndexOf('\n');
        if (newline >= 0)
            text = text.Substring(0, newline).Trim();
        return text.Length == 0 ? null : text;
    }

    static bool LooksLikeWorld(string json)
    {
        return !string.IsNullOrEmpty(json) && json.Contains("\"worldName\"") && json.Contains("\"islands\"");
    }

    static string IsolateObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        text = text.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            int firstNewline = text.IndexOf('\n');
            if (firstNewline >= 0)
                text = text.Substring(firstNewline + 1);
            if (text.EndsWith("```", StringComparison.Ordinal))
                text = text.Substring(0, text.Length - 3);
            text = text.Trim();
        }

        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
            return null;
        return text.Substring(start, end - start + 1);
    }

    static string JsonString(string value)
    {
        if (value == null)
            value = "";

        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            switch (c)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (c < 32)
                        builder.Append("\\u").Append(((int)c).ToString("x4"));
                    else
                        builder.Append(c);
                    break;
            }
        }

        builder.Append('"');
        return builder.ToString();
    }
}
