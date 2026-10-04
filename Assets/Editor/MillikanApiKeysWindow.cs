using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Lets each person paste the ElevenLabs key once. It is saved to UserSettings/ (git-ignored),
/// so it never reaches the repository; builds include it automatically.
/// </summary>
public class MillikanApiKeysWindow : EditorWindow
{
    private const string PromptedThisSession = "Millikan.ApiKeysPrompted";

    private string keyInput = "";
    private bool showKey;
    private string message = "";
    private MessageType messageType = MessageType.None;
    private UnityWebRequest testRequest;

    [MenuItem("Tools/Millikan/API Keys")]
    public static void Open()
    {
        var window = GetWindow<MillikanApiKeysWindow>(true, "Millikan API Keys");
        window.minSize = new Vector2(460f, 250f);
    }

    /// <summary>Opens this window once per editor session when no key is available.</summary>
    [InitializeOnLoadMethod]
    private static void PromptIfMissing()
    {
        EditorApplication.delayCall += () =>
        {
            if (Application.isBatchMode || SessionState.GetBool(PromptedThisSession, false))
                return;

            SessionState.SetBool(PromptedThisSession, true);
            if (string.IsNullOrEmpty(ElevenLabsKey.Get()))
                Open();
        };
    }

    private void OnEnable()
    {
        keyInput = File.Exists(ElevenLabsKey.LocalKeyPath) ? File.ReadAllText(ElevenLabsKey.LocalKeyPath).Trim() : "";
    }

    private void OnDisable()
    {
        if (testRequest != null)
        {
            testRequest.Dispose();
            testRequest = null;
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("ElevenLabs (tutor voice)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Paste the ElevenLabs API key you received. It is stored only on this computer " +
            "(UserSettings folder, ignored by git) and added to headset builds automatically.\n" +
            "Without a key, the AI tutor answers with the Groq fallback voice.",
            MessageType.Info);

        EditorGUILayout.Space(4);
        using (new EditorGUILayout.HorizontalScope())
        {
            keyInput = showKey
                ? EditorGUILayout.TextField("API key", keyInput)
                : EditorGUILayout.PasswordField("API key", keyInput);
            showKey = GUILayout.Toggle(showKey, "Show", "Button", GUILayout.Width(50f));
        }

        EditorGUILayout.LabelField("Status", DescribeCurrentKey(), EditorStyles.miniLabel);

        EditorGUILayout.Space(6);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Save"))
                Save();

            using (new EditorGUI.DisabledScope(testRequest != null || string.IsNullOrEmpty(ElevenLabsKey.Get())))
            {
                if (GUILayout.Button(testRequest != null ? "Testing..." : "Test voice"))
                    StartTest();
            }

            using (new EditorGUI.DisabledScope(!File.Exists(ElevenLabsKey.LocalKeyPath)))
            {
                if (GUILayout.Button("Remove"))
                    Remove();
            }
        }

        if (!string.IsNullOrEmpty(message))
            EditorGUILayout.HelpBox(message, messageType);
    }

    private static string DescribeCurrentKey()
    {
        if (File.Exists(ElevenLabsKey.LocalKeyPath) && File.ReadAllText(ElevenLabsKey.LocalKeyPath).Trim().Length > 0)
            return "Key saved on this computer.";
        if (!string.IsNullOrEmpty(ElevenLabsKey.Get()))
            return "Using the " + ElevenLabsKey.EnvironmentVariable + " environment variable.";
        return "No key set.";
    }

    private void Save()
    {
        string key = (keyInput ?? "").Trim();
        if (key.Length == 0)
        {
            SetMessage("Paste a key first.", MessageType.Warning);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(ElevenLabsKey.LocalKeyPath));
        File.WriteAllText(ElevenLabsKey.LocalKeyPath, key, new UTF8Encoding(false));
        keyInput = key;
        SetMessage("Saved. Press Test voice to check it, then press Play.", MessageType.Info);
    }

    private void Remove()
    {
        File.Delete(ElevenLabsKey.LocalKeyPath);
        keyInput = "";
        SetMessage("Removed the key from this computer.", MessageType.Info);
    }

    private void StartTest()
    {
        TutorVoiceProfile profile = TutorVoiceProfile.Load();
        if (profile == null)
        {
            SetMessage("Voice profile not found (Assets/Resources/TutorVoiceProfile.asset).", MessageType.Error);
            return;
        }

        testRequest = ElevenLabsTts.CreateRequest(profile, profile.liveModel, "Test.", "en", -1, ElevenLabsKey.Get());
        testRequest.timeout = 30;
        testRequest.SendWebRequest().completed += _ => FinishTest();
        SetMessage("Testing...", MessageType.None);
    }

    private void FinishTest()
    {
        if (testRequest == null)
            return;

        if (testRequest.result == UnityWebRequest.Result.Success && testRequest.downloadHandler.data.Length > 0)
        {
            SetMessage("The key works. The tutor will speak with the ElevenLabs voice.", MessageType.Info);
        }
        else
        {
            string body = testRequest.downloadHandler != null ? testRequest.downloadHandler.text : "";
            string reason = testRequest.responseCode == 401 || testRequest.responseCode == 403
                ? "The key was rejected (wrong key, or it lacks the Text to Speech permission)."
                : "Request failed (" + testRequest.responseCode + " " + testRequest.error + ").";
            SetMessage(reason + (string.IsNullOrEmpty(body) ? "" : "\n" + body), MessageType.Error);
        }

        testRequest.Dispose();
        testRequest = null;
        Repaint();
    }

    private void SetMessage(string text, MessageType type)
    {
        message = text;
        messageType = type;
        Repaint();
    }
}
