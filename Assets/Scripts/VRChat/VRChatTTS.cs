using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace VRChat
{
    /// <summary>
    /// Available voices for Groq Orpheus TTS (Canopy Labs)
    /// </summary>
    public enum TTSVoice
    {
        // Female voices
        Autumn,
        Diana,
        Hannah,
        
        // Male voices
        Austin,
        Daniel,
        Troy
    }

    /// <summary>
    /// Vocal direction presets for Orpheus TTS expressiveness
    /// </summary>
    public enum TTSTone
    {
        None,           // Natural conversational cadence
        Friendly,       // Warm and approachable
        Cheerful,       // Upbeat and positive
        FriendlyCheerful, // Combination of friendly and cheerful
        Professional,   // Business-like and formal
        Calm,           // Relaxed and soothing
        Excited,        // Energetic and enthusiastic
        Warm            // Comforting and welcoming
    }

    public enum TTSProvider
    {
        ElevenLabs, // same voice as the narration (TutorVoiceProfile)
        Groq        // Orpheus, English only; fallback
    }

    /// <summary>
    /// VR Chat TTS Manager - Handles text-to-speech for AI responses
    /// Features: Audio queuing, speaking indicator, escape handling
    /// </summary>
    public class VRChatTTS : MonoBehaviour
    {
        [Header("Provider")]
        [Tooltip("ElevenLabs speaks with the narration voice. Groq Orpheus is used if ElevenLabs is unavailable.")]
        [SerializeField] private TTSProvider provider = TTSProvider.ElevenLabs;
        [SerializeField] private TutorVoiceProfile voiceProfile;

        [Header("API Configuration (Groq fallback)")]
        [SerializeField] private string apiKey = "YOUR_GROQ_API_KEY";
        
        [Header("Voice Settings")]
        [SerializeField] private TTSVoice selectedVoice = TTSVoice.Troy;
        [SerializeField] private TTSTone tone = TTSTone.FriendlyCheerful;
        [SerializeField] [Range(0.5f, 2f)] private float volume = 1f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;

        [Header("Behavior")]
        [SerializeField] private bool autoCreateAudioSource = true;
        [SerializeField] private bool queueMessages = true; // Queue if already speaking
        [SerializeField] private int maxQueueSize = 5;

        // Events
        public event Action OnSpeakingStarted;
        public event Action OnSpeakingFinished;
        public event Action<string> OnSpeakingText;
        public event Action<string> OnError;

        // State
        public bool IsSpeaking => isSpeaking;
        public bool IsProcessing => isProcessing;
        public int QueueCount => speechQueue.Count;
        
        /// <summary>
        /// Get the AudioSource used for TTS playback (for lip sync integration)
        /// </summary>
        public AudioSource AudioSource => audioSource;

        private const string API_URL = "https://api.groq.com/openai/v1/audio/speech";
        private const string MODEL = "canopylabs/orpheus-v1-english";
        private const string RESPONSE_FORMAT = "wav";
        private const int MAX_INPUT_LENGTH = 200; // Orpheus has 200 character limit
        private const int ELEVENLABS_MAX_INPUT_LENGTH = 2000;

        private Queue<string> speechQueue = new Queue<string>();
        private bool isSpeaking = false;
        private bool isProcessing = false;
        private Coroutine currentSpeechCoroutine;
        private string elevenLabsKey;
        private bool elevenLabsDisabledForSession;

        private bool HasGroqKey => !string.IsNullOrEmpty(apiKey) && apiKey != "YOUR_GROQ_API_KEY";
        private bool UsingElevenLabs => provider == TTSProvider.ElevenLabs && !elevenLabsDisabledForSession
                                        && voiceProfile != null && !string.IsNullOrEmpty(elevenLabsKey);

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeAudioSource();

            if (voiceProfile == null)
                voiceProfile = TutorVoiceProfile.Load();
            elevenLabsKey = ElevenLabsKey.Get();

            if (provider == TTSProvider.ElevenLabs && !UsingElevenLabs)
                Debug.LogWarning("[VRChatTTS] ElevenLabs " + (voiceProfile == null ? "voice profile" : "API key") + " missing; using the Groq voice. Add the key under Tools > Millikan > API Keys.");
            else if (UsingElevenLabs)
                Debug.Log("[VRChatTTS] Using ElevenLabs voice '" + voiceProfile.voiceName + "' (" + voiceProfile.liveModel + ").");
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
        }

        private void Update()
        {
            // Check if current audio finished playing
            if (isSpeaking && audioSource != null && !audioSource.isPlaying && !isProcessing)
            {
                isSpeaking = false;
                OnSpeakingFinished?.Invoke();
                
                // Process next in queue
                ProcessQueue();
            }
        }

        #endregion

        #region Initialization

        private void InitializeAudioSource()
        {
            if (audioSource == null && autoCreateAudioSource)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f; // 2D sound for UI
                Debug.Log("[VRChatTTS] Created AudioSource");
            }

            if (audioSource != null)
            {
                audioSource.volume = volume;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Speak the given text. Will queue if already speaking (when queueMessages is true)
        /// </summary>
        public void Speak(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                Debug.LogWarning("[VRChatTTS] Cannot speak empty text");
                return;
            }

            if (!UsingElevenLabs && !HasGroqKey)
            {
                Debug.LogError("[VRChatTTS] API key not set!");
                OnError?.Invoke("API key not configured");
                return;
            }

            // Clean the text
            string cleanedText = CleanTextForTTS(text);

            // Orpheus accepts 200 characters per request; ElevenLabs takes a whole reply at once
            int maxLength = UsingElevenLabs ? ELEVENLABS_MAX_INPUT_LENGTH : MAX_INPUT_LENGTH;
            List<string> chunks = SplitTextIntoChunks(cleanedText, maxLength);

            foreach (string chunk in chunks)
            {
                if (isSpeaking || isProcessing)
                {
                    if (queueMessages && speechQueue.Count < maxQueueSize)
                    {
                        speechQueue.Enqueue(chunk);
                        Debug.Log($"[VRChatTTS] Queued speech ({speechQueue.Count} in queue)");
                    }
                    else if (!queueMessages)
                    {
                        // Stop current and speak new
                        Stop();
                        StartSpeech(chunk);
                    }
                    else
                    {
                        Debug.LogWarning("[VRChatTTS] Queue full, dropping message");
                    }
                }
                else
                {
                    StartSpeech(chunk);
                }
            }
        }

        /// <summary>
        /// Speak text as a coroutine (useful for waiting until done)
        /// </summary>
        public IEnumerator SpeakAndWait(string text)
        {
            Speak(text);
            
            // Wait for processing to start
            yield return new WaitUntil(() => isProcessing || isSpeaking);
            
            // Wait for speaking to finish
            yield return new WaitWhile(() => isProcessing || isSpeaking);
        }

        /// <summary>
        /// Stop current speech and clear queue
        /// </summary>
        public void Stop()
        {
            if (currentSpeechCoroutine != null)
            {
                StopCoroutine(currentSpeechCoroutine);
                currentSpeechCoroutine = null;
            }

            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }

            speechQueue.Clear();
            isSpeaking = false;
            isProcessing = false;

            OnSpeakingFinished?.Invoke();
        }

        /// <summary>
        /// Change voice at runtime
        /// </summary>
        public void SetVoice(TTSVoice voice)
        {
            selectedVoice = voice;
            Debug.Log($"[VRChatTTS] Voice changed to: {voice}");
        }

        /// <summary>
        /// Set API key at runtime
        /// </summary>
        public void SetApiKey(string key)
        {
            apiKey = key;
        }

        #endregion

        #region Private Methods

        private void StartSpeech(string text)
        {
            currentSpeechCoroutine = StartCoroutine(GenerateAndPlaySpeech(text));
        }

        private void ProcessQueue()
        {
            if (speechQueue.Count > 0 && !isSpeaking && !isProcessing)
            {
                string nextText = speechQueue.Dequeue();
                StartSpeech(nextText);
            }
        }

        private IEnumerator GenerateAndPlaySpeech(string text)
        {
            isProcessing = true;
            OnSpeakingText?.Invoke(text);

            if (UsingElevenLabs)
            {
                bool spoke = false;
                yield return ElevenLabsSpeech(text, ok => spoke = ok);
                if (spoke)
                    yield break;

                if (!HasGroqKey)
                {
                    isProcessing = false;
                    OnError?.Invoke("TTS unavailable");
                    ProcessQueue();
                    yield break;
                }

                // Groq needs short chunks; speak the first now and queue the rest ahead of anything waiting
                List<string> fallbackChunks = SplitTextIntoChunks(text, MAX_INPUT_LENGTH);
                if (fallbackChunks.Count == 0)
                {
                    isProcessing = false;
                    ProcessQueue();
                    yield break;
                }

                if (fallbackChunks.Count > 1)
                    speechQueue = new Queue<string>(fallbackChunks.Skip(1).Concat(speechQueue));
                text = fallbackChunks[0];
            }

            yield return GroqSpeech(text);
        }

        private IEnumerator ElevenLabsSpeech(string text, Action<bool> done)
        {
            string language = ElevenLabsTts.LanguageCode(LanguageManager.Current);

            using (UnityWebRequest request = ElevenLabsTts.CreateRequest(voiceProfile, voiceProfile.liveModel, text, language, -1, elevenLabsKey))
            {
                request.timeout = 30;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    string body = request.downloadHandler != null ? request.downloadHandler.text : "";
                    string error = "ElevenLabs TTS error " + request.responseCode + ": " + request.error + " " + body;
                    bool permanent = request.responseCode == 401 || request.responseCode == 402 || request.responseCode == 403
                                     || body.Contains("quota");
                    if (permanent)
                        elevenLabsDisabledForSession = true;

                    Debug.LogWarning("[VRChatTTS] " + error + (permanent ? " Using the Groq voice for the rest of this session." : " Using the Groq voice for this reply."));
                    if (SessionLogger.Instance != null)
                        SessionLogger.Instance.RegisterAiError(error);
                    done(false);
                    yield break;
                }

                AudioClip clip = null;
                try
                {
                    clip = ElevenLabsTts.CreateClip(request.downloadHandler.data, voiceProfile, "TTS_Audio");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[VRChatTTS] ElevenLabs audio decode error: {e.Message}");
                }

                if (clip == null || audioSource == null)
                {
                    done(false);
                    yield break;
                }

                isProcessing = false;
                PlayClip(clip);
                done(true);
            }
        }

        private void PlayClip(AudioClip clip)
        {
            audioSource.clip = clip;
            audioSource.volume = volume;
            audioSource.Play();

            isSpeaking = true;
            OnSpeakingStarted?.Invoke();

            Debug.Log($"[VRChatTTS] Playing audio ({clip.length:F1}s)");
        }

        private IEnumerator GroqSpeech(string text)
        {
            isProcessing = true;
            Debug.Log($"[VRChatTTS] Generating speech for: {(text.Length > 50 ? text.Substring(0, 50) + "..." : text)}");
            Debug.Log($"[VRChatTTS] API Key set: {!string.IsNullOrEmpty(apiKey) && apiKey != "YOUR_GROQ_API_KEY"}");

            // Build request JSON
            string voiceName = GetVoiceName(selectedVoice);
            string toneDirection = GetToneDirection(tone);
            string inputWithTone = toneDirection + text;

            string json = JsonUtility.ToJson(new TTSRequest
            {
                model = MODEL,
                voice = voiceName,
                input = inputWithTone,
                response_format = RESPONSE_FORMAT
            });

            Debug.Log($"[VRChatTTS] Sending request to API...");
            Debug.Log($"[VRChatTTS] JSON: {json}");

            // Use UnityWebRequest for Android/Quest compatibility
            using (UnityWebRequest request = new UnityWebRequest(API_URL, "POST"))
            {
                byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(jsonBytes);
                request.uploadHandler.contentType = "application/json";
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                request.timeout = 30;

                Debug.Log($"[VRChatTTS] Sending web request...");

                // Send request
                yield return request.SendWebRequest();
                
                Debug.Log($"[VRChatTTS] Request completed. Result: {request.result}");

                isProcessing = false;

                // Check for errors
                if (request.result != UnityWebRequest.Result.Success)
                {
                    string error = $"TTS API error: {request.error}";
                    if (request.downloadHandler != null && request.downloadHandler.data != null)
                    {
                        error += $" - {Encoding.UTF8.GetString(request.downloadHandler.data)}";
                    }
                    Debug.LogError($"[VRChatTTS] {error}");
                    OnError?.Invoke(error);
                    if (SessionLogger.Instance != null)
                    {
                        if (GroqQuotaGuard.IsQuotaOrRateLimit(error))
                            SessionLogger.Instance.RegisterAiQuotaExceeded(error);
                        else
                            SessionLogger.Instance.RegisterAiError(error);
                    }
                    ProcessQueue();
                    yield break;
                }

                byte[] audioData = request.downloadHandler.data;

                if (audioData == null || audioData.Length == 0)
                {
                    Debug.LogError("[VRChatTTS] No audio data received");
                    OnError?.Invoke("No audio data received");
                    ProcessQueue();
                    yield break;
                }

                Debug.Log($"[VRChatTTS] Received {audioData.Length} bytes of audio data");

                // Create and play audio clip
                try
                {
                    AudioClip clip = CreateClipFromWav(audioData);
                    
                    if (clip != null && audioSource != null)
                    {
                        PlayClip(clip);
                    }
                    else
                    {
                        Debug.LogError("[VRChatTTS] Failed to create AudioClip or AudioSource is null");
                        OnError?.Invoke("Failed to create audio clip");
                        ProcessQueue();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[VRChatTTS] Error creating audio clip: {e.Message}");
                    OnError?.Invoke($"Audio decode error: {e.Message}");
                    ProcessQueue();
                }
            }
        }

        private string GetVoiceName(TTSVoice voice)
        {
            // Orpheus uses lowercase voice names
            // Handle invalid enum values (from old PlayAI enum saved in scenes)
            int voiceIndex = (int)voice;
            if (voiceIndex < 0 || voiceIndex > 5)
            {
                Debug.LogWarning($"[VRChatTTS] Invalid voice enum value {voiceIndex}, defaulting to 'troy'");
                return "troy";
            }
            return voice.ToString().ToLower();
        }

        private string GetToneDirection(TTSTone selectedTone)
        {
            return selectedTone switch
            {
                TTSTone.Friendly => "[friendly] ",
                TTSTone.Cheerful => "[cheerful] ",
                TTSTone.FriendlyCheerful => "[friendly] [cheerful] ",
                TTSTone.Professional => "[professionally] ",
                TTSTone.Calm => "[calm] ",
                TTSTone.Excited => "[excited] ",
                TTSTone.Warm => "[warm] ",
                _ => "" // None = natural conversational cadence
            };
        }

        /// <summary>
        /// Set tone at runtime
        /// </summary>
        public void SetTone(TTSTone newTone)
        {
            tone = newTone;
            Debug.Log($"[VRChatTTS] Tone changed to: {newTone}");
        }

        private string CleanTextForTTS(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Remove markdown formatting
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\*\*(.+?)\*\*", "$1"); // Bold
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\*(.+?)\*", "$1"); // Italic
            text = System.Text.RegularExpressions.Regex.Replace(text, @"`(.+?)`", "$1"); // Code
            text = System.Text.RegularExpressions.Regex.Replace(text, @"```[\s\S]*?```", ""); // Code blocks
            
            // Remove URLs
            text = System.Text.RegularExpressions.Regex.Replace(text, @"https?://\S+", "");

            // Remove special characters that might cause issues
            text = text.Replace("\n", " ");
            text = text.Replace("\r", " ");
            text = text.Replace("\t", " ");
            text = text.Replace("\"", "'");
            text = text.Replace("\\", "");

            // Collapse multiple spaces
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");

            // Trim
            text = text.Trim();

            return text;
        }

        /// <summary>
        /// Split text into chunks that fit within the Orpheus character limit.
        /// Tries to split at sentence boundaries for natural speech.
        /// </summary>
        private List<string> SplitTextIntoChunks(string text, int maxLength)
        {
            var chunks = new List<string>();
            
            if (string.IsNullOrEmpty(text))
                return chunks;

            if (text.Length <= maxLength)
            {
                chunks.Add(text);
                return chunks;
            }

            // Split by sentences first
            var sentenceEnders = new[] { ". ", "! ", "? ", ".\n", "!\n", "?\n" };
            int currentStart = 0;
            string currentChunk = "";

            while (currentStart < text.Length)
            {
                // Find the next sentence boundary
                int nextEnd = -1;
                foreach (var ender in sentenceEnders)
                {
                    int pos = text.IndexOf(ender, currentStart);
                    if (pos >= 0 && (nextEnd < 0 || pos < nextEnd))
                    {
                        nextEnd = pos + ender.Length;
                    }
                }

                string segment;
                if (nextEnd < 0 || nextEnd > text.Length)
                {
                    // No more sentence boundaries, take the rest
                    segment = text.Substring(currentStart);
                    currentStart = text.Length;
                }
                else
                {
                    segment = text.Substring(currentStart, nextEnd - currentStart);
                    currentStart = nextEnd;
                }

                // Check if adding this segment would exceed the limit
                if ((currentChunk + segment).Length <= maxLength)
                {
                    currentChunk += segment;
                }
                else
                {
                    // Save current chunk if not empty
                    if (!string.IsNullOrWhiteSpace(currentChunk))
                    {
                        chunks.Add(currentChunk.Trim());
                    }

                    // If segment itself is too long, split it by words
                    if (segment.Length > maxLength)
                    {
                        var words = segment.Split(' ');
                        currentChunk = "";
                        foreach (var word in words)
                        {
                            if ((currentChunk + " " + word).Trim().Length <= maxLength)
                            {
                                currentChunk = (currentChunk + " " + word).Trim();
                            }
                            else
                            {
                                if (!string.IsNullOrWhiteSpace(currentChunk))
                                {
                                    chunks.Add(currentChunk.Trim());
                                }
                                // If single word is too long, truncate it
                                currentChunk = word.Length > maxLength ? word.Substring(0, maxLength) : word;
                            }
                        }
                    }
                    else
                    {
                        currentChunk = segment;
                    }
                }
            }

            // Don't forget the last chunk
            if (!string.IsNullOrWhiteSpace(currentChunk))
            {
                chunks.Add(currentChunk.Trim());
            }

            return chunks;
        }

        private AudioClip CreateClipFromWav(byte[] wav)
        {
            if (wav.Length < 44)
            {
                throw new Exception("WAV data too short");
            }

            // Parse WAV header
            int channels = BitConverter.ToInt16(wav, 22);
            int sampleRate = BitConverter.ToInt32(wav, 24);
            int bitsPerSample = BitConverter.ToInt16(wav, 34);

            if (channels < 1 || channels > 2)
            {
                throw new Exception($"Unsupported channel count: {channels}");
            }

            if (bitsPerSample != 16)
            {
                throw new Exception($"Unsupported bits per sample: {bitsPerSample}");
            }

            // Find data chunk
            int dataStart = FindDataChunk(wav) + 8;
            int dataLength = wav.Length - dataStart;
            int sampleCount = dataLength / (bitsPerSample / 8);

            if (sampleCount <= 0)
            {
                throw new Exception("No audio samples found");
            }

            // Convert to float samples
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                int sampleIndex = dataStart + i * 2;
                if (sampleIndex + 1 < wav.Length)
                {
                    short sample = BitConverter.ToInt16(wav, sampleIndex);
                    samples[i] = sample / 32768f;
                }
            }

            // Create AudioClip
            int samplesPerChannel = sampleCount / channels;
            AudioClip clip = AudioClip.Create("TTS_Audio", samplesPerChannel, channels, sampleRate, false);
            clip.SetData(samples, 0);

            return clip;
        }

        private int FindDataChunk(byte[] wav)
        {
            for (int i = 12; i < wav.Length - 8; i++)
            {
                if (wav[i] == 'd' && wav[i + 1] == 'a' && wav[i + 2] == 't' && wav[i + 3] == 'a')
                {
                    return i;
                }
            }
            throw new Exception("DATA chunk not found in WAV file");
        }

        #endregion

        /// <summary>
        /// JSON serializable request object
        /// </summary>
        [Serializable]
        private class TTSRequest
        {
            public string model;
            public string voice;
            public string input;
            public string response_format;
        }
    }
}

