using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>Request building and PCM handling shared by live replies and the narration generator.</summary>
public static class ElevenLabsTts
{
    private const string Endpoint = "https://api.elevenlabs.io/v1/text-to-speech/";

    public static bool SupportsLanguageCode(string model)
    {
        return model != null && (model.Contains("flash_v2_5") || model.Contains("turbo_v2_5"));
    }

    public static string LanguageCode(AppLanguage language)
    {
        return language == AppLanguage.English ? "en" : "de";
    }

    /// <param name="seed">Negative to omit.</param>
    public static UnityWebRequest CreateRequest(TutorVoiceProfile profile, string model, string text, string languageCode, int seed, string apiKey)
    {
        var json = new StringBuilder(256);
        json.Append("{\"text\":").Append(Quote(text));
        json.Append(",\"model_id\":").Append(Quote(model));
        if (!string.IsNullOrEmpty(languageCode) && SupportsLanguageCode(model))
            json.Append(",\"language_code\":").Append(Quote(languageCode));
        if (seed >= 0)
            json.Append(",\"seed\":").Append(seed.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"voice_settings\":{");
        json.Append("\"stability\":").Append(Num(profile.stability));
        json.Append(",\"similarity_boost\":").Append(Num(profile.similarityBoost));
        json.Append(",\"style\":").Append(Num(profile.style));
        json.Append(",\"use_speaker_boost\":").Append(profile.useSpeakerBoost ? "true" : "false");
        json.Append(",\"speed\":").Append(Num(profile.speed));
        json.Append("}}");

        string url = Endpoint + profile.voiceId + "?output_format=pcm_" + profile.sampleRate.ToString(CultureInfo.InvariantCulture);
        var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json.ToString()));
        request.uploadHandler.contentType = "application/json";
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("xi-api-key", apiKey);
        return request;
    }

    public static float[] Pcm16ToFloat(byte[] pcm)
    {
        int count = pcm.Length / 2;
        var samples = new float[count];
        for (int i = 0; i < count; i++)
            samples[i] = (short)(pcm[2 * i] | (pcm[2 * i + 1] << 8)) / 32768f;
        return samples;
    }

    /// <summary>Scale to a target active-speech RMS without letting peaks exceed the ceiling.</summary>
    public static void Normalize(float[] samples, float targetRmsDb, float peakLimitDb)
    {
        double sum = 0;
        int active = 0;
        float peak = 0f;

        for (int i = 0; i < samples.Length; i++)
        {
            float a = Math.Abs(samples[i]);
            if (a > peak) peak = a;
            if (a > 0.003f)
            {
                sum += samples[i] * samples[i];
                active++;
            }
        }

        if (active == 0 || peak <= 0f)
            return;

        float rms = (float)Math.Sqrt(sum / active);
        float gain = Mathf.Pow(10f, targetRmsDb / 20f) / rms;
        float ceiling = Mathf.Pow(10f, peakLimitDb / 20f);
        if (peak * gain > ceiling)
            gain = ceiling / peak;

        for (int i = 0; i < samples.Length; i++)
            samples[i] *= gain;
    }

    public static AudioClip CreateClip(byte[] pcm, TutorVoiceProfile profile, string name)
    {
        float[] samples = Pcm16ToFloat(pcm);
        if (samples.Length == 0)
            return null;

        Normalize(samples, profile.targetRmsDb, profile.peakLimitDb);
        AudioClip clip = AudioClip.Create(name, samples.Length, 1, profile.sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static string Num(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string Quote(string s)
    {
        var sb = new StringBuilder(s.Length + 2).Append('"');
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.Append('"').ToString();
    }
}
