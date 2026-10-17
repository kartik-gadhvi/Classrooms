using UnityEngine;
using VRChat;

/// <summary>
/// A small glowing orb with sparkles that floats beside the tutor while the learner
/// holds the talk button. Its brightness and the sparkles follow the voice level.
/// </summary>
[DefaultExecutionOrder(200)]
public class VoiceLevelOrb : MonoBehaviour
{
    [SerializeField] private Transform anchor;
    [SerializeField] private float sideOffset = 0.24f;
    [SerializeField] private float heightOffset = 0.04f;
    [SerializeField] private float towardPlayer = 0.08f;
    [SerializeField] private float followSpeed = 8f;

    private VRSpeechToText speech;
    private Transform head;
    private Transform root;
    private Transform core;
    private Transform shell;
    private Material coreMaterial;
    private Material shellMaterial;
    private ParticleSystem sparkles;
    private ParticleSystem.EmissionModule sparkleEmission;
    private float level;
    private float shown;
    private bool placed;
    private static readonly int LevelID = Shader.PropertyToID("_Level");

    private void Start()
    {
        speech = FindFirstObjectByType<VRSpeechToText>();
        ResolveAnchor();
        BuildVisuals();
        Apply(0f, 0f);
    }

    private void ResolveAnchor()
    {
        if (head != null)
            return;

        if (anchor == null)
        {
            MillikanAvatarDriver[] avatars = FindObjectsByType<MillikanAvatarDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (avatars.Length > 0)
                anchor = avatars[0].transform;
        }

        if (anchor == null)
            return;

        root = anchor;
        foreach (Transform bone in anchor.GetComponentsInChildren<Transform>(true))
        {
            if (bone.name == "Head")
            {
                head = bone;
                break;
            }
        }
    }

    private void LateUpdate()
    {
        ResolveAnchor();
        FollowAnchor();

        float target = speech != null && speech.IsRecording ? speech.CurrentLevel : 0f;
        level = Mathf.Lerp(level, target, 1f - Mathf.Exp(-14f * Time.deltaTime));

        float wantShown = speech != null && speech.IsRecording ? 1f : 0f;
        shown = Mathf.MoveTowards(shown, wantShown, Time.deltaTime * (wantShown > 0f ? 6f : 4f));
        Apply(level, shown);
    }

    private void FollowAnchor()
    {
        if (head == null)
            return;

        Camera camera = Camera.main;
        Vector3 toPlayer = camera != null ? camera.transform.position - head.position : root.forward;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.001f)
            toPlayer = root.forward;
        toPlayer.Normalize();

        Vector3 side = Vector3.Cross(Vector3.up, toPlayer);
        Vector3 target = head.position + side * sideOffset + Vector3.up * heightOffset + toPlayer * towardPlayer;
        if (!placed)
        {
            transform.position = target;
            placed = true;
            return;
        }

        transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
    }

    private void Apply(float voice, float visibility)
    {
        if (core == null)
            return;

        bool visible = visibility > 0.01f;
        if (core.gameObject.activeSelf != visible)
        {
            core.gameObject.SetActive(visible);
            shell.gameObject.SetActive(visible);
            if (!visible && sparkles.isPlaying)
                sparkles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (!visible)
            return;

        if (!sparkles.isPlaying)
            sparkles.Play();

        float breathe = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.2f);
        float energy = Mathf.Max(voice, breathe * 0.18f);
        coreMaterial.SetFloat(LevelID, energy);
        shellMaterial.SetFloat(LevelID, energy * 0.65f);

        float scale = visibility * Mathf.Lerp(0.85f, 1.35f, voice);
        core.localScale = Vector3.one * (0.055f * scale);
        shell.localScale = Vector3.one * (0.1f * scale);
        sparkleEmission.rateOverTime = visibility * (6f + voice * 55f);
    }

    private void BuildVisuals()
    {
        Shader orbShader = Shader.Find("Millikan/VoiceOrb");
        Shader sparkleShader = Shader.Find("Millikan/VoiceSparkle");
        if (orbShader == null || sparkleShader == null)
        {
            Debug.LogError("[VoiceLevelOrb] Voice shaders are missing.");
            return;
        }

        Color gold = new Color(1f, 0.78f, 0.38f, 1f);
        core = CreateSphere("VoiceOrbCore", orbShader, gold, 0.85f, out coreMaterial);
        shell = CreateSphere("VoiceOrbShell", orbShader, new Color(1f, 0.9f, 0.62f, 1f), 0.25f, out shellMaterial);

        var sparkleObject = new GameObject("VoiceSparkles");
        sparkleObject.transform.SetParent(transform, false);
        sparkles = sparkleObject.AddComponent<ParticleSystem>();
        var renderer = sparkleObject.GetComponent<ParticleSystemRenderer>();
        renderer.material = SparkleMaterial(sparkleShader);
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        var main = sparkles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.06f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.012f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.75f), gold);
        main.maxParticles = 60;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        sparkleEmission = sparkles.emission;
        sparkleEmission.rateOverTime = 0f;

        var shape = sparkles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.055f;

        var velocity = sparkles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.orbitalY = 0.7f;
        velocity.radial = 0.04f;

        var size = sparkles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0f)));

        var color = sparkles.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(gold, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
    }

    private Transform CreateSphere(string sphereName, Shader shader, Color color, float coreAmount, out Material material)
    {
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = sphereName;
        sphere.transform.SetParent(transform, false);
        Destroy(sphere.GetComponent<Collider>());

        material = new Material(shader);
        material.SetColor("_Color", color);
        material.SetFloat("_Core", coreAmount);
        sphere.GetComponent<MeshRenderer>().sharedMaterial = material;
        return sphere.transform;
    }

    private static Material SparkleMaterial(Shader shader)
    {
        var material = new Material(shader);
        material.SetTexture("_BaseMap", SoftCircle());
        material.SetColor("_BaseColor", Color.white);
        return material;
    }

    private static Texture2D SoftCircle()
    {
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[size * size];
        float radius = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(radius, radius)) / radius;
                float alpha = Mathf.Clamp01(1f - distance);
                alpha *= alpha;
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }
}
