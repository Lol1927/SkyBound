using System;
using System.IO;
using UnityEngine;

public static class ApiKeys
{
    public static string GeminiApiKey { get; private set; }
    public static string GeminiModel { get; private set; }
    public static string FishAudioApiKey { get; private set; }
    public static string FishAudioVoiceId { get; private set; }
    public static string FishAudioModel { get; private set; }
    public static bool HasGeminiCredentials { get; private set; }
    public static bool HasFishAudioCredentials { get; private set; }

    static string loggedError;
    static bool loggedLegacyKey;

    [Serializable]
    class KeyFile
    {
        public string geminiApiKey;
        public string geminiModel;
        public string fishAudioApiKey;
        public string fishAudioVoiceId;
        public string fishAudioModel;
    }

    public static void Load()
    {
        GeminiApiKey = null;
        GeminiModel = null;
        FishAudioApiKey = "";
        FishAudioVoiceId = "";
        FishAudioModel = "s1";
        HasGeminiCredentials = false;
        HasFishAudioCredentials = false;

        string path = Application.dataPath + "/Secrets/api_keys.json";
        if (!File.Exists(path))
        {
            LogErrorOnce("Missing API keys file at Assets/Secrets/api_keys.json. Add geminiApiKey, geminiModel, and fishAudioApiKey.");
            return;
        }

        string json;
        try
        {
            json = File.ReadAllText(Application.dataPath + "/Secrets/api_keys.json");
        }
        catch (Exception)
        {
            LogErrorOnce("Could not read Assets/Secrets/api_keys.json.");
            return;
        }

        KeyFile keys;
        try
        {
            keys = JsonUtility.FromJson<KeyFile>(json);
        }
        catch (Exception)
        {
            LogErrorOnce("Could not parse Assets/Secrets/api_keys.json.");
            return;
        }

        ApplyFishAudio(keys);

        if (keys == null || string.IsNullOrWhiteSpace(keys.geminiModel))
        {
            LogErrorOnce("Assets/Secrets/api_keys.json must include geminiApiKey and geminiModel.");
            return;
        }

        string geminiKey = keys.geminiApiKey;
        if (string.IsNullOrWhiteSpace(geminiKey))
            geminiKey = ReadLegacyKey();
        if (string.IsNullOrWhiteSpace(geminiKey))
        {
            LogErrorOnce("Assets/Secrets/api_keys.json must include geminiApiKey and geminiModel.");
            return;
        }

        string model = keys.geminiModel.Trim();
        if (!IsModelId(model))
        {
            LogErrorOnce("geminiModel in Assets/Secrets/api_keys.json is not a usable model id.");
            return;
        }

        GeminiApiKey = geminiKey.Trim();
        GeminiModel = model;
        HasGeminiCredentials = true;
        loggedError = null;
    }

    static void ApplyFishAudio(KeyFile keys)
    {
        FishAudioApiKey = "";
        FishAudioVoiceId = "";
        FishAudioModel = "s1";
        HasFishAudioCredentials = false;
        if (keys == null)
            return;

        if (!string.IsNullOrWhiteSpace(keys.fishAudioApiKey))
            FishAudioApiKey = keys.fishAudioApiKey.Trim();
        if (!string.IsNullOrWhiteSpace(keys.fishAudioVoiceId))
            FishAudioVoiceId = keys.fishAudioVoiceId.Trim();
        if (!string.IsNullOrWhiteSpace(keys.fishAudioModel) && IsModelId(keys.fishAudioModel.Trim()))
            FishAudioModel = keys.fishAudioModel.Trim();

        HasFishAudioCredentials = FishAudioApiKey.Length > 0 && FishAudioVoiceId.Length > 0;
    }

    static string ReadLegacyKey()
    {
        string path = Application.dataPath + "/Scret/api_keys";
        if (!File.Exists(path))
            return null;

        try
        {
            string raw = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            if (!loggedLegacyKey)
            {
                loggedLegacyKey = true;
                Debug.LogWarning("geminiApiKey is empty in Assets/Secrets/api_keys.json. Using Assets/Scret/api_keys instead.");
            }

            return raw.Trim();
        }
        catch (Exception)
        {
            LogErrorOnce("Could not read Assets/Scret/api_keys.");
            return null;
        }
    }

    public static bool IsModelId(string model)
    {
        if (string.IsNullOrEmpty(model))
            return false;

        for (int i = 0; i < model.Length; i++)
        {
            char c = model[i];
            bool ok = (c >= 'a' && c <= 'z')
                || (c >= 'A' && c <= 'Z')
                || (c >= '0' && c <= '9')
                || c == '.' || c == '_' || c == '-';
            if (!ok)
                return false;
        }

        return true;
    }

    static void LogErrorOnce(string message)
    {
        if (loggedError == message)
            return;

        loggedError = message;
        Debug.LogError(message);
    }
}
