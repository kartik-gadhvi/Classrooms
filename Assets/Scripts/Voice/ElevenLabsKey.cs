using System;
using UnityEngine;

/// <summary>
/// The ElevenLabs key never lives in the repo. In the editor it comes from the key entered in
/// Tools > Millikan > API Keys (stored in the git-ignored UserSettings folder), or else from the
/// ELEVENLABS_API_KEY environment variable. Player builds get it from a Resources text file that
/// ElevenLabsKeyBuildStep writes only for the duration of a build.
/// </summary>
public static class ElevenLabsKey
{
    public const string EnvironmentVariable = "ELEVENLABS_API_KEY";
    public const string ResourceName = "ElevenLabsKey";
    public const string LocalKeyPath = "UserSettings/ElevenLabsApiKey.txt";

    public static string Get()
    {
#if UNITY_EDITOR
        string key = null;
        try
        {
            if (System.IO.File.Exists(LocalKeyPath))
                key = System.IO.File.ReadAllText(LocalKeyPath);
        }
        catch (Exception) { key = null; }

        if (string.IsNullOrWhiteSpace(key))
            key = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(key))
        {
            // Unity may have been started before the variable was set; read the stored user value.
            try { key = Environment.GetEnvironmentVariable(EnvironmentVariable, EnvironmentVariableTarget.User); }
            catch (Exception) { key = null; }
        }
        return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
#else
        var asset = Resources.Load<TextAsset>(ResourceName);
        return asset != null && !string.IsNullOrWhiteSpace(asset.text) ? asset.text.Trim() : null;
#endif
    }
}
