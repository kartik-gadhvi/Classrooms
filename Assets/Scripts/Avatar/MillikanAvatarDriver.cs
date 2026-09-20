using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedural life for the tutor avatar: relaxed rest pose, breathing, idle
/// sway, head look-at, blinking, eye saccades, and an audio-driven mouth.
/// Drives the skeleton bones directly — no Animator or humanoid retargeting —
/// so it can never stretch or sink the mesh. Works with any AudioSource:
/// the runtime TTS voice and the pre-recorded narration behave identically.
/// </summary>
public class MillikanAvatarDriver : MonoBehaviour
{
    [Header("Voice sources (mouth + talking gestures follow whichever is playing)")]
    public List<AudioSource> voiceSources = new List<AudioSource>();

    [Header("Tutor state (optional, for listening / thinking poses)")]
    public VRChat.VRChatSetup chatSetup;

    [Header("Look At")]
    [Tooltip("Defaults to the main camera (the player's head).")]
    public Transform lookTarget;
    public float maxHeadYaw = 40f;
    public float maxHeadPitch = 22f;
    public float headTurnSpeed = 3.5f;
    [Range(0f, 1f)] public float neckShare = 0.3f;

    [Header("Rest Pose (applied once to the T-pose)")]
    public float armDownAngle = 70f;
    public float forearmBendForward = 14f;
    public float shoulderDrop = 4f;

    [Header("Idle")]
    public float breathAmplitude = 1.3f;
    public float breathRate = 0.22f;
    public float swayAmplitude = 1.1f;
    public float weightShiftAmplitude = 0.9f;

    [Header("Speaking")]
    public float speakingNod = 2.2f;
    public float speakingSway = 2.6f;
    public float gestureAmplitude = 7f;
    public float speakingBlendSpeed = 3f;

    [Header("Mouth")]
    [Tooltip("RMS loudness that counts as full speech level.")]
    public float mouthSensitivity = 4.5f;
    [Tooltip("Loudness below this is treated as silence (mouth closes between words).")]
    public float mouthNoiseGate = 0.025f;
    [Tooltip("Jaw opening (0-100) at full speech level. Keep low for subtle talking.")]
    public float maxJawOpen = 24f;
    public float mouthAttack = 28f;
    public float mouthRelease = 14f;
    public float restingSmile = 7f;

    [Header("Blink")]
    public Vector2 blinkInterval = new Vector2(2.5f, 6f);
    public float blinkDuration = 0.13f;

    public bool IsSpeaking { get; private set; }
    public bool IsListening { get; set; }
    public bool IsThinking { get; set; }

    private const string HeadMeshName = "AvatarHead";
    private const string EyelashMeshName = "AvatarEyelashes";
    private const string LowerTeethMeshName = "AvatarTeethLower";

    private Transform root;
    private Transform hips, spine, spine1, spine2, neck, head, leftEye, rightEye;
    private Transform leftShoulder, rightShoulder, leftArm, rightArm, leftForeArm, rightForeArm;

    private readonly Dictionary<Transform, Quaternion> restLocal = new Dictionary<Transform, Quaternion>();

    private SkinnedMeshRenderer headMesh, eyelashMesh, lowerTeethMesh;
    private readonly Dictionary<string, int> headShapes = new Dictionary<string, int>();
    private readonly Dictionary<string, int> eyelashShapes = new Dictionary<string, int>();
    private readonly Dictionary<string, int> teethShapes = new Dictionary<string, int>();

    // glTF morph targets import with a frame weight of 1 (not Unity's 100), so
    // weights in this class are authored on a 0-100 scale and multiplied by this.
    private readonly Dictionary<SkinnedMeshRenderer, float> shapeScale = new Dictionary<SkinnedMeshRenderer, float>();

    private readonly float[] samples = new float[512];
    private readonly float[] spectrum = new float[64];
    private float outputPeak = 0.05f;

    private float speakBlend;
    private float jaw, funnel, stretch;
    private float blinkTimer, blinkPhase = -1f;
    private float saccadeTimer;
    private Vector2 saccade, saccadeTarget;
    private Quaternion headLookLocal = Quaternion.identity;
    private Quaternion neckLookLocal = Quaternion.identity;
    private float listenBlend, thinkBlend;
    private float noiseSeed;

    private void Awake()
    {
        root = transform;
        noiseSeed = Random.Range(0f, 100f);

        hips = FindBone("Hips");
        spine = FindBone("Spine");
        spine1 = FindBone("Spine1");
        spine2 = FindBone("Spine2");
        neck = FindBone("Neck");
        head = FindBone("Head");
        leftEye = FindBone("LeftEye");
        rightEye = FindBone("RightEye");
        leftShoulder = FindBone("LeftShoulder");
        rightShoulder = FindBone("RightShoulder");
        leftArm = FindBone("LeftArm");
        rightArm = FindBone("RightArm");
        leftForeArm = FindBone("LeftForeArm");
        rightForeArm = FindBone("RightForeArm");

        headMesh = FindMesh(HeadMeshName);
        eyelashMesh = FindMesh(EyelashMeshName);
        lowerTeethMesh = FindMesh(LowerTeethMeshName);
        IndexShapes(headMesh, headShapes);
        IndexShapes(eyelashMesh, eyelashShapes);
        IndexShapes(lowerTeethMesh, teethShapes);

        foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            r.updateWhenOffscreen = true;

        ApplyRestPose();
        CacheRest();

        blinkTimer = Random.Range(blinkInterval.x, blinkInterval.y);
        saccadeTimer = Random.Range(0.8f, 2.5f);
    }

    private void Start()
    {
        if (lookTarget == null)
        {
            Camera cam = Camera.main;
            if (cam != null)
                lookTarget = cam.transform;
        }
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        float t = Time.time;

        UpdateSpeakingState(dt);
        UpdateTutorState(dt);
        UpdateBody(t, dt);
        UpdateHead(t, dt);
        UpdateEyes(dt);
        UpdateMouth(dt);
        UpdateBlink(dt);
        UpdateExpression();
    }

    // ---------------------------------------------------------------- state

    private void UpdateSpeakingState(float dt)
    {
        bool speaking = false;
        foreach (var src in voiceSources)
        {
            if (src != null && src.isPlaying && !src.mute)
            {
                speaking = true;
                break;
            }
        }

        IsSpeaking = speaking;
        speakBlend = Mathf.MoveTowards(speakBlend, speaking ? 1f : 0f, speakingBlendSpeed * dt);
    }

    private void UpdateTutorState(float dt)
    {
        if (chatSetup != null)
        {
            var stt = chatSetup.GetSTTManager();
            var chat = chatSetup.GetChatController();
            var tts = chatSetup.GetTTSManager();

            IsListening = stt != null && stt.IsRecording;
            IsThinking = !IsListening && !IsSpeaking &&
                         ((stt != null && stt.IsProcessing) ||
                          (chat != null && chat.IsProcessing) ||
                          (tts != null && tts.IsProcessing));
        }

        listenBlend = Mathf.MoveTowards(listenBlend, IsListening ? 1f : 0f, 2.5f * dt);
        thinkBlend = Mathf.MoveTowards(thinkBlend, IsThinking ? 1f : 0f, 2.5f * dt);
    }

    // ---------------------------------------------------------------- body

    private void UpdateBody(float t, float dt)
    {
        float breath = Mathf.Sin(t * breathRate * Mathf.PI * 2f);
        float n1 = Noise(0.07f, 0f);
        float n2 = Noise(0.05f, 10f);
        float n3 = Noise(0.09f, 20f);

        SetOffset(hips, new Vector3(0f, 0f, weightShiftAmplitude * n2));
        SetOffset(spine, new Vector3(0f, swayAmplitude * n1, -weightShiftAmplitude * 0.6f * n2));
        SetOffset(spine1, new Vector3(breath * breathAmplitude * 0.6f, swayAmplitude * 0.5f * n3, 0f));
        SetOffset(spine2, new Vector3(breath * breathAmplitude + speakBlend * speakingSway * 0.3f * Noise(0.6f, 30f), speakBlend * speakingSway * Noise(0.35f, 40f), 0f));

        // Talking micro-gestures: gentle forearm/upper-arm drift, mirrored asymmetrically.
        float g = gestureAmplitude * speakBlend;
        SetOffset(leftArm, new Vector3(g * Noise(0.5f, 50f), 0f, g * 0.5f * Noise(0.4f, 60f)));
        SetOffset(rightArm, new Vector3(g * Noise(0.45f, 70f), 0f, -g * 0.5f * Noise(0.4f, 80f)));
        SetOffset(leftForeArm, new Vector3(g * 1.4f * Mathf.Max(0f, Noise(0.55f, 90f)), 0f, 0f));
        SetOffset(rightForeArm, new Vector3(g * 1.4f * Mathf.Max(0f, Noise(0.5f, 100f)), 0f, 0f));

        // Listening: small attentive tilt. Thinking: slight look-away handled in head/eyes.
        SetOffset(neck, new Vector3(0f, 0f, 4f * listenBlend));
    }

    private void UpdateHead(float t, float dt)
    {
        if (head == null)
            return;

        // Desired look direction in the head parent's space, clamped.
        Vector3 yawPitch = Vector3.zero;
        if (lookTarget != null)
        {
            Vector3 toTarget = lookTarget.position - head.position;
            Vector3 local = head.parent.InverseTransformDirection(toTarget.normalized);
            // Parent frame of the head is world-aligned when the avatar faces +Z locally.
            float yaw = Mathf.Atan2(local.x, Mathf.Max(0.001f, local.z)) * Mathf.Rad2Deg;
            float pitch = -Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg;
            yaw = Mathf.Clamp(yaw, -maxHeadYaw, maxHeadYaw);
            pitch = Mathf.Clamp(pitch, -maxHeadPitch, maxHeadPitch);
            yawPitch = new Vector3(pitch, yaw, 0f);
        }

        // Thinking: glance up and to the side; speaking: nod and sway.
        yawPitch += new Vector3(-9f, 12f, 0f) * thinkBlend;
        yawPitch += new Vector3(
            Mathf.Sin(t * 2.9f) * speakingNod * speakBlend + speakingNod * 0.6f * speakBlend * Noise(0.8f, 110f),
            speakingSway * speakBlend * Noise(0.5f, 120f),
            speakingSway * 0.4f * speakBlend * Noise(0.45f, 130f));

        // Idle micro-movement so the head is never frozen.
        yawPitch += new Vector3(Noise(0.12f, 140f) * 1.2f, Noise(0.1f, 150f) * 1.6f, Noise(0.08f, 160f) * 0.8f);

        Quaternion headTarget = Quaternion.Euler(yawPitch * (1f - neckShare));
        Quaternion neckTarget = Quaternion.Euler(yawPitch * neckShare);
        float k = 1f - Mathf.Exp(-headTurnSpeed * dt);
        headLookLocal = Quaternion.Slerp(headLookLocal, headTarget, k);
        neckLookLocal = Quaternion.Slerp(neckLookLocal, neckTarget, k);

        head.localRotation = restLocal[head] * headLookLocal;
        if (neck != null)
            neck.localRotation = neck.localRotation * neckLookLocal;
    }

    private void UpdateEyes(float dt)
    {
        saccadeTimer -= dt;
        if (saccadeTimer <= 0f)
        {
            saccadeTimer = Random.Range(0.7f, 2.6f);
            saccadeTarget = new Vector2(Random.Range(-4f, 4f), Random.Range(-7f, 7f));
            if (IsThinking)
                saccadeTarget += new Vector2(-8f, 10f);
        }

        saccade = Vector2.Lerp(saccade, saccadeTarget, 1f - Mathf.Exp(-12f * dt));

        // Eyes lead the head toward the look target a little.
        Vector2 converge = Vector2.zero;
        if (lookTarget != null && head != null)
        {
            Vector3 local = head.InverseTransformDirection((lookTarget.position - head.position).normalized);
            converge = new Vector2(
                Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg, -10f, 10f),
                Mathf.Clamp(Mathf.Atan2(local.x, Mathf.Max(0.001f, local.z)) * Mathf.Rad2Deg, -14f, 14f)) * 0.35f;
        }

        Quaternion eyeRot = Quaternion.Euler(saccade.x + converge.x, saccade.y + converge.y, 0f);
        if (leftEye != null) leftEye.localRotation = restLocal[leftEye] * eyeRot;
        if (rightEye != null) rightEye.localRotation = restLocal[rightEye] * eyeRot;
    }

    // ---------------------------------------------------------------- face

    private void UpdateMouth(float dt)
    {
        float loud = 0f, low = 0f, high = 0f;

        foreach (var src in voiceSources)
        {
            if (src == null || !src.isPlaying || src.mute)
                continue;

            loud = Mathf.Max(loud, SampleLoudness(src, dt));

            src.GetSpectrumData(spectrum, 0, FFTWindow.Hamming);
            float lo = 0f, hi = 0f;
            for (int i = 1; i < 8; i++) lo += spectrum[i];
            for (int i = 8; i < 40; i++) hi += spectrum[i];
            low = Mathf.Max(low, lo);
            high = Mathf.Max(high, hi);
        }

        // Gate out silence/noise, then map loudness to a gentle curve so quiet
        // syllables still move the lips while loud ones don't fly open.
        float gated = Mathf.Max(0f, loud - mouthNoiseGate);
        float level = Mathf.Clamp01(gated * mouthSensitivity);
        level = Mathf.Pow(level, 0.75f);

        float targetJaw = level * maxJawOpen;
        float tonal = low + high > 1e-5f ? low / (low + high) : 0.5f;
        float targetFunnel = level * 14f * Mathf.Clamp01((tonal - 0.35f) * 2f);
        float targetStretch = level * 10f * Mathf.Clamp01((0.55f - tonal) * 2f);

        float rate = targetJaw > jaw ? mouthAttack : mouthRelease;
        jaw = Mathf.Lerp(jaw, targetJaw, 1f - Mathf.Exp(-rate * dt));
        funnel = Mathf.Lerp(funnel, targetFunnel, 1f - Mathf.Exp(-rate * dt));
        stretch = Mathf.Lerp(stretch, targetStretch, 1f - Mathf.Exp(-rate * dt));

        SetShape(headMesh, headShapes, "jawOpen", jaw);
        SetShape(lowerTeethMesh, teethShapes, "jawOpen", jaw);
        SetShape(headMesh, headShapes, "mouthFunnel", funnel);
        SetShape(headMesh, headShapes, "mouthPucker", funnel * 0.4f);
        SetShape(headMesh, headShapes, "mouthStretchLeft", stretch);
        SetShape(headMesh, headShapes, "mouthStretchRight", stretch);
        SetShape(headMesh, headShapes, "mouthClose", 0f);
    }

    /// <summary>
    /// RMS loudness of what the source is saying right now, independent of the
    /// listener's distance: read straight from the clip at the playhead when the
    /// clip is decompressed in memory (narration + generated TTS clips), otherwise
    /// fall back to the spatialized output normalized by a slowly adapting peak.
    /// </summary>
    private float SampleLoudness(AudioSource src, float dt)
    {
        AudioClip clip = src.clip;
        float sum = 0f;

        if (clip != null && clip.loadType == AudioClipLoadType.DecompressOnLoad && clip.loadState == AudioDataLoadState.Loaded && clip.channels > 0)
        {
            int frames = samples.Length / clip.channels;
            int pos = src.timeSamples;
            if (pos >= 0 && pos + frames < clip.samples && clip.GetData(samples, pos))
            {
                for (int i = 0; i < samples.Length; i++)
                    sum += samples[i] * samples[i];
                return Mathf.Sqrt(sum / samples.Length);
            }
        }

        src.GetOutputData(samples, 0);
        for (int i = 0; i < samples.Length; i++)
            sum += samples[i] * samples[i];
        float rms = Mathf.Sqrt(sum / samples.Length);

        // Auto-gain: the output level depends on distance/volume, so scale by a
        // running peak to recover a 0..~0.25 speech-like range.
        outputPeak = Mathf.Max(rms, outputPeak * Mathf.Exp(-dt / 2.5f), 0.005f);
        return rms / outputPeak * 0.2f;
    }

    private void UpdateBlink(float dt)
    {
        float weight;

        if (blinkPhase >= 0f)
        {
            blinkPhase += dt / Mathf.Max(0.03f, blinkDuration);
            weight = Mathf.Sin(Mathf.Clamp01(blinkPhase) * Mathf.PI) * 100f;
            if (blinkPhase >= 1f)
            {
                blinkPhase = -1f;
                blinkTimer = Random.Range(blinkInterval.x, blinkInterval.y);
                if (Random.value < 0.15f)
                    blinkTimer = 0.25f; // occasional double blink
            }
        }
        else
        {
            weight = 0f;
            blinkTimer -= dt;
            if (blinkTimer <= 0f)
                blinkPhase = 0f;
        }

        SetShape(headMesh, headShapes, "eyeBlinkLeft", weight);
        SetShape(headMesh, headShapes, "eyeBlinkRight", weight);
        SetShape(eyelashMesh, eyelashShapes, "eyeBlinkLeft", weight);
        SetShape(eyelashMesh, eyelashShapes, "eyeBlinkRight", weight);
    }

    private void UpdateExpression()
    {
        float smile = restingSmile + 6f * speakBlend;
        float brows = 30f * listenBlend + 18f * thinkBlend;

        SetShape(headMesh, headShapes, "mouthSmileLeft", smile);
        SetShape(headMesh, headShapes, "mouthSmileRight", smile);
        SetShape(eyelashMesh, eyelashShapes, "mouthSmileLeft", smile);
        SetShape(eyelashMesh, eyelashShapes, "mouthSmileRight", smile);
        SetShape(headMesh, headShapes, "browInnerUp", brows);
        SetShape(eyelashMesh, eyelashShapes, "browInnerUp", brows);
        SetShape(headMesh, headShapes, "browOuterUpLeft", brows * 0.5f);
        SetShape(headMesh, headShapes, "browOuterUpRight", brows * 0.5f);
        SetShape(eyelashMesh, eyelashShapes, "browOuterUpLeft", brows * 0.5f);
        SetShape(eyelashMesh, eyelashShapes, "browOuterUpRight", brows * 0.5f);
    }

    // ---------------------------------------------------------------- rest pose

    /// <summary>Bring the exported T-pose into a relaxed standing pose using world-space targets.</summary>
    private void ApplyRestPose()
    {
        PoseArm(leftArm, leftForeArm, FindBone("LeftHand"), +1f);
        PoseArm(rightArm, rightForeArm, FindBone("RightHand"), -1f);

        if (leftShoulder != null)
            leftShoulder.rotation = Quaternion.AngleAxis(shoulderDrop, root.forward) * leftShoulder.rotation;
        if (rightShoulder != null)
            rightShoulder.rotation = Quaternion.AngleAxis(-shoulderDrop, root.forward) * rightShoulder.rotation;
    }

    private void PoseArm(Transform upper, Transform lower, Transform hand, float side)
    {
        if (upper == null || lower == null)
            return;

        Vector3 upperDir = (lower.position - upper.position).normalized;
        Vector3 targetUpper = Quaternion.AngleAxis(armDownAngle * side, root.forward) * upperDir;
        upper.rotation = Quaternion.FromToRotation(upperDir, targetUpper) * upper.rotation;

        if (hand == null)
            return;

        Vector3 lowerDir = (hand.position - lower.position).normalized;
        Vector3 targetLower = Vector3.RotateTowards(lowerDir, root.forward, forearmBendForward * Mathf.Deg2Rad, 0f);
        lower.rotation = Quaternion.FromToRotation(lowerDir, targetLower) * lower.rotation;
    }

    private void CacheRest()
    {
        restLocal.Clear();
        foreach (var b in new[] { hips, spine, spine1, spine2, neck, head, leftEye, rightEye, leftShoulder, rightShoulder, leftArm, rightArm, leftForeArm, rightForeArm })
        {
            if (b != null && !restLocal.ContainsKey(b))
                restLocal[b] = b.localRotation;
        }
    }

    // ---------------------------------------------------------------- helpers

    private void SetOffset(Transform bone, Vector3 eulerOffset)
    {
        if (bone == null || !restLocal.ContainsKey(bone))
            return;

        bone.localRotation = restLocal[bone] * Quaternion.Euler(eulerOffset);
    }

    private float Noise(float frequency, float offset)
    {
        return Mathf.PerlinNoise(Time.time * frequency + noiseSeed, offset) * 2f - 1f;
    }

    private void IndexShapes(SkinnedMeshRenderer smr, Dictionary<string, int> map)
    {
        map.Clear();
        if (smr == null || smr.sharedMesh == null)
            return;

        Mesh mesh = smr.sharedMesh;
        for (int i = 0; i < mesh.blendShapeCount; i++)
            map[mesh.GetBlendShapeName(i)] = i;

        float scale = 1f;
        if (mesh.blendShapeCount > 0)
        {
            int frames = mesh.GetBlendShapeFrameCount(0);
            if (frames > 0)
                scale = mesh.GetBlendShapeFrameWeight(0, frames - 1) / 100f;
        }
        shapeScale[smr] = scale;
    }

    private void SetShape(SkinnedMeshRenderer smr, Dictionary<string, int> map, string name, float weight)
    {
        if (smr == null)
            return;

        int index;
        if (!map.TryGetValue(name, out index))
            return;

        float scale;
        if (!shapeScale.TryGetValue(smr, out scale))
            scale = 1f;

        smr.SetBlendShapeWeight(index, Mathf.Clamp(weight, 0f, 100f) * scale);
    }

    private SkinnedMeshRenderer FindMesh(string name)
    {
        foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (r.name == name)
                return r;
        }

        return null;
    }

    /// <summary>The importer nests duplicate joint nodes (Head/Head/…); the outermost one drives them all.</summary>
    private Transform FindBone(string name)
    {
        var queue = new Queue<Transform>();
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
}
