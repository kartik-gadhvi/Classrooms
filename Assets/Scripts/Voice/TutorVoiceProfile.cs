using UnityEngine;

/// <summary>
/// The one voice used everywhere: pre-generated narration and live AI replies
/// read the same profile, so they cannot drift apart.
/// </summary>
[CreateAssetMenu(menuName = "Millikan/Tutor Voice Profile", fileName = "TutorVoiceProfile")]
public class TutorVoiceProfile : ScriptableObject
{
    public const string ResourcePath = "TutorVoiceProfile";

    [Header("ElevenLabs voice")]
    public string voiceName = "Bill";
    public string voiceId = "pqHfZKP75CvOlQylNhV4";

    [Header("Models")]
    [Tooltip("Pre-generated narration clips: quality over latency.")]
    public string narrationModel = "eleven_multilingual_v2";
    [Tooltip("Live AI replies: latency over quality.")]
    public string liveModel = "eleven_flash_v2_5";

    [Header("Delivery")]
    [Range(0f, 1f)] public float stability = 0.55f;
    [Range(0f, 1f)] public float similarityBoost = 0.8f;
    [Range(0f, 1f)] public float style = 0f;
    public bool useSpeakerBoost = true;
    [Range(0.7f, 1.2f)] public float speed = 1f;
    [Tooltip("Narration seed; the same text + seed gives the same take.")]
    public int seed = 42;

    [Header("Audio")]
    public int sampleRate = 24000;
    [Tooltip("Active-speech RMS that narration and live replies are both normalized to (the original narration sat around -15 dBFS).")]
    public float targetRmsDb = -16f;
    public float peakLimitDb = -1f;

    public static TutorVoiceProfile Load()
    {
        return Resources.Load<TutorVoiceProfile>(ResourcePath);
    }
}
