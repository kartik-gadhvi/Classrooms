using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Puts the ElevenLabs key into the player build without it ever being committed:
/// writes Assets/Resources/ElevenLabsKey.txt from the local key (Tools > Millikan > API Keys) right before a
/// build and deletes it afterwards (the path is also git-ignored as a safety net).
/// </summary>
public class ElevenLabsKeyBuildStep : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    private const string AssetPath = "Assets/Resources/" + ElevenLabsKey.ResourceName + ".txt";

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        string key = ElevenLabsKey.Get();
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogWarning("[ElevenLabsKeyBuildStep] No ElevenLabs key (Tools > Millikan > API Keys); the build will use the Groq voice for AI replies.");
            return;
        }

        File.WriteAllText(AssetPath, key);
        AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceSynchronousImport);
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        Remove();
    }

    [InitializeOnLoadMethod]
    private static void RemoveLeftoverAfterFailedBuild()
    {
        EditorApplication.delayCall += () =>
        {
            if (!BuildPipeline.isBuildingPlayer)
                Remove();
        };
    }

    private static void Remove()
    {
        if (File.Exists(AssetPath))
            AssetDatabase.DeleteAsset(AssetPath);
    }
}
