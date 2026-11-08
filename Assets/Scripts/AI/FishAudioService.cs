using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class FishAudioService : MonoBehaviour
{
    public static FishAudioService Instance { get; private set; }

    const string Endpoint = "https://api.fish.audio/v1/tts";
    const int TimeoutSeconds = 30;

    readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    readonly Dictionary<string, Pending> pending = new Dictionary<string, Pending>();
    readonly List<Pending> order = new List<Pending>();
    readonly List<UnityWebRequest> pinned = new List<UnityWebRequest>();

    bool draining;
    bool loggedMissing;

    class Pending
    {
        public string text;
        public bool priority;
        public readonly List<Action<AudioClip>> callbacks = new List<Action<AudioClip>>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        for (int i = 0; i < pinned.Count; i++)
        {
            if (pinned[i] != null)
                pinned[i].Dispose();
        }

        pinned.Clear();
    }

    public bool HasClip(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        return cache.ContainsKey(text.Trim());
    }

    public void Request(string text, Action<AudioClip> onComplete, bool priority)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            if (onComplete != null)
                onComplete(null);
            return;
        }

        text = text.Trim();
        AudioClip cached;
        if (cache.TryGetValue(text, out cached))
        {
            if (onComplete != null)
                onComplete(cached);
            return;
        }

        Pending job;
        if (pending.TryGetValue(text, out job))
        {
            if (onComplete != null)
                job.callbacks.Add(onComplete);
            if (priority && !job.priority)
                Promote(job);
            return;
        }

        job = new Pending { text = text, priority = priority };
        if (onComplete != null)
            job.callbacks.Add(onComplete);
        pending.Add(text, job);
        Insert(job);
        if (!draining)
            StartCoroutine(Drain());
    }

    void Promote(Pending job)
    {
        int index = order.IndexOf(job);
        if (index < 0)
            return;

        order.RemoveAt(index);
        job.priority = true;
        Insert(job);
    }

    void Insert(Pending job)
    {
        if (!job.priority)
        {
            order.Add(job);
            return;
        }

        int index = 0;
        while (index < order.Count && order[index].priority)
            index++;
        order.Insert(index, job);
    }

    IEnumerator Drain()
    {
        draining = true;
        while (order.Count > 0)
        {
            Pending job = order[0];
            order.RemoveAt(0);

            AudioClip clip = null;
            yield return Download(job.text, value => clip = value);

            pending.Remove(job.text);
            if (clip != null)
                cache[job.text] = clip;

            for (int i = 0; i < job.callbacks.Count; i++)
            {
                Action<AudioClip> callback = job.callbacks[i];
                if (callback == null)
                    continue;

                try
                {
                    callback(clip);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Fish Audio callback failed: " + exception.Message);
                }
            }
        }

        draining = false;
    }

    IEnumerator Download(string text, Action<AudioClip> onComplete)
    {
        ApiKeys.Load();
        if (!ApiKeys.HasFishAudioCredentials)
        {
            if (!loggedMissing)
            {
                loggedMissing = true;
                Debug.LogWarning("Fish Audio credentials are missing. Co-pilot lines will use subtitles only.");
            }

            onComplete(null);
            yield break;
        }

        byte[] payload = Encoding.UTF8.GetBytes(BuildBody(text));
        using (var request = new UnityWebRequest(Endpoint, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(payload);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = TimeoutSeconds;
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + ApiKeys.FishAudioApiKey);
            if (!string.IsNullOrEmpty(ApiKeys.FishAudioModel))
                request.SetRequestHeader("model", ApiKeys.FishAudioModel);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("Fish Audio TTS failed (" + Describe(request) + ").");
                onComplete(null);
                yield break;
            }

            byte[] audioBytes = request.downloadHandler != null ? request.downloadHandler.data : null;
            if (audioBytes == null || audioBytes.Length < 64 || audioBytes[0] == (byte)'{')
            {
                Debug.LogWarning("Fish Audio returned no audio.");
                onComplete(null);
                yield break;
            }

            string path = Path.Combine(Application.temporaryCachePath, "skybound-" + Guid.NewGuid().ToString("N") + ".mp3");
            bool wrote = false;
            try
            {
                File.WriteAllBytes(path, audioBytes);
                wrote = true;
            }
            catch (Exception)
            {
                Debug.LogWarning("Fish Audio could not store temporary audio.");
            }

            if (!wrote)
            {
                onComplete(null);
                yield break;
            }

            AudioClip clip = null;
            UnityWebRequest loader = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, AudioType.MPEG);
            var handler = loader.downloadHandler as DownloadHandlerAudioClip;
            if (handler != null)
                handler.streamAudio = false;
            loader.timeout = 15;
            yield return loader.SendWebRequest();

            if (loader.result == UnityWebRequest.Result.Success && handler != null && handler.audioClip != null)
            {
                clip = CopyClip(handler.audioClip);
                if (clip == null)
                {
                    clip = handler.audioClip;
                    pinned.Add(loader);
                    loader = null;
                }
            }
            else
            {
                Debug.LogWarning("Fish Audio audio could not be decoded.");
            }

            if (loader != null)
                loader.Dispose();

            DeleteTemp(path);
            onComplete(clip);
        }
    }

    static AudioClip CopyClip(AudioClip source)
    {
        if (source == null || source.samples <= 0 || source.channels <= 0 || source.frequency <= 0)
            return null;

        int length = source.samples * source.channels;
        var data = new float[length];
        try
        {
            if (!source.GetData(data, 0))
                return null;
        }
        catch (Exception)
        {
            return null;
        }

        AudioClip copy = AudioClip.Create("CoPilot", source.samples, source.channels, source.frequency, false);
        if (copy == null || !copy.SetData(data, 0))
            return null;
        return copy;
    }

    static string BuildBody(string text)
    {
        return "{" +
            "\"text\":" + JsonString(text) + "," +
            "\"reference_id\":" + JsonString(ApiKeys.FishAudioVoiceId) + "," +
            "\"format\":\"mp3\"" +
            "}";
    }

    static string Describe(UnityWebRequest request)
    {
        long code = request.responseCode;
        if (code == 429)
            return "HTTP 429";
        if (request.result == UnityWebRequest.Result.ConnectionError)
            return "connection error";
        if (code > 0)
            return "HTTP " + code;
        return "request failed";
    }

    static void DeleteTemp(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception)
        {
        }
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
