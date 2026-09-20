using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Replaces the static "oldman" tutor mesh with the MetaPerson avatar and
/// wires the tutor voice + AI TTS through AudioSources on the avatar's head.
/// Re-runnable; everything touched on prefab instances is recorded as an override.
/// </summary>
public static class MillikanAvatarInstaller
{
    private const string AvatarAssetPath = "Assets/Avatars/MetaPerson/metaperson_avatar.glb";
    private const string NpcName = "oldman";
    private const string AvatarObjectName = "MillikanAvatar";
    private const string VoiceObjectName = "TutorVoice";
    private const string TtsObjectName = "TutorTTS";

    /// <summary>Target standing height in metres (the previous mesh was ~1.5 m; the dialogue panel sits just above).</summary>
    private const float TargetHeightMeters = 1.55f;

    /// <summary>Yaw applied on first install only (matches the orientation tuned by hand in the scene); re-runs keep the scene value.</summary>
    private const float InitialYawDegrees = -40f;

    [MenuItem("Tools/Millikan/Install MetaPerson Tutor Avatar")]
    public static void Install()
    {
        GameObject npc = GameObject.Find(NpcName);
        if (npc == null)
        {
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (go.name == NpcName && go.transform.parent == null) { npc = go; break; }
        }

        if (npc == null)
        {
            Debug.LogError("[MillikanAvatarInstaller] '" + NpcName + "' not found in the open scene.");
            return;
        }

        GameObject avatarAsset = AssetDatabase.LoadAssetAtPath<GameObject>(AvatarAssetPath);
        if (avatarAsset == null)
        {
            Debug.LogError("[MillikanAvatarInstaller] Avatar asset missing at " + AvatarAssetPath);
            return;
        }

        // 1. Hide the old static mesh (keep it for rollback).
        var oldRenderer = npc.GetComponent<MeshRenderer>();
        if (oldRenderer != null)
            oldRenderer.enabled = false;

        // 2. Place the avatar under the NPC anchor so its UI children keep working.
        Transform existing = npc.transform.Find(AvatarObjectName);
        GameObject avatar = existing != null ? existing.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(avatarAsset, npc.transform);
        avatar.name = AvatarObjectName;
        if (existing == null)
            Undo.RegisterCreatedObjectUndo(avatar, "Install tutor avatar");

        Bounds bounds = ComputeAssetBounds(avatarAsset);
        float parentScale = npc.transform.lossyScale.y;
        float scale = TargetHeightMeters / Mathf.Max(0.01f, bounds.size.y) / Mathf.Max(0.01f, parentScale);
        avatar.transform.localScale = Vector3.one * scale;
        if (existing == null)
            avatar.transform.localRotation = Quaternion.Euler(0f, InitialYawDegrees, 0f);
        avatar.transform.localPosition = new Vector3(0f, -bounds.min.y * scale, 0f);

        // 2b. No sunglasses on a physics tutor.
        Transform glasses = avatar.transform.Find("glasses");
        if (glasses != null)
            glasses.gameObject.SetActive(false);

        // 3. Life: procedural driver.
        var driver = avatar.GetComponent<MillikanAvatarDriver>();
        if (driver == null)
            driver = avatar.AddComponent<MillikanAvatarDriver>();

        // 4. Voice comes out of the head.
        Transform headBone = FindOutermost(avatar.transform, "Head");
        Transform audioParent = headBone != null ? headBone : avatar.transform;
        AudioSource voice = EnsureAudioSource(audioParent, VoiceObjectName);
        AudioSource tts = EnsureAudioSource(audioParent, TtsObjectName);

        driver.voiceSources.Clear();
        driver.voiceSources.Add(voice);
        driver.voiceSources.Add(tts);

        var chatSetup = Object.FindFirstObjectByType<VRChat.VRChatSetup>(FindObjectsInactive.Include);
        driver.chatSetup = chatSetup;

        // 5. Route the tutorial narration through the head voice source.
        var tutorial = Object.FindFirstObjectByType<BottomTutorialController>(FindObjectsInactive.Include);
        if (tutorial != null)
        {
            if (tutorial.voiceSource != null && tutorial.voiceSource != voice)
            {
                voice.volume = tutorial.voiceSource.volume;
                voice.outputAudioMixerGroup = tutorial.voiceSource.outputAudioMixerGroup;
            }
            tutorial.voiceSource = voice;
            EditorUtility.SetDirty(tutorial);
            RecordIfPrefab(tutorial);
        }

        // 6. Route the AI voice (TTS) through the head as well.
        if (chatSetup != null)
        {
            var so = new SerializedObject(chatSetup);
            SerializedProperty prop = so.FindProperty("ttsAudioSource");
            if (prop != null)
            {
                prop.objectReferenceValue = tts;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(chatSetup);
            RecordIfPrefab(chatSetup);
        }

        // 7. Persist overrides on the NPC prefab instance and save.
        foreach (Component c in npc.GetComponentsInChildren<Component>(true))
        {
            if (c == null) continue;
            EditorUtility.SetDirty(c);
            RecordIfPrefab(c);
        }
        foreach (Transform t in npc.GetComponentsInChildren<Transform>(true))
        {
            EditorUtility.SetDirty(t.gameObject);
            RecordIfPrefab(t.gameObject);
        }

        var scene = npc.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[MillikanAvatarInstaller] Installed avatar (height " + TargetHeightMeters + " m, scale " + scale.ToString("F3") + "), voice sources on '" + audioParent.name + "', scene saved.");
    }

    private static AudioSource EnsureAudioSource(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name);
        if (existing == null)
        {
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        }

        var src = go.GetComponent<AudioSource>();
        if (src == null)
            src = go.AddComponent<AudioSource>();

        src.playOnAwake = false;
        src.loop = false;
        src.spatialBlend = 0.9f;
        src.rolloffMode = AudioRolloffMode.Logarithmic;
        src.minDistance = 1.2f;
        src.maxDistance = 15f;
        src.dopplerLevel = 0f;
        src.spread = 20f;
        return src;
    }

    private static Bounds ComputeAssetBounds(GameObject asset)
    {
        var renderers = asset.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(Vector3.zero, Vector3.one * 1.8f);

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        return b;
    }

    private static Transform FindOutermost(Transform root, string name)
    {
        var queue = new System.Collections.Generic.Queue<Transform>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            Transform t = queue.Dequeue();
            if (t != root && t.name == name)
                return t;
            for (int i = 0; i < t.childCount; i++)
                queue.Enqueue(t.GetChild(i));
        }
        return null;
    }

    private static void RecordIfPrefab(Object o)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(o))
            PrefabUtility.RecordPrefabInstancePropertyModifications(o);
    }
}
