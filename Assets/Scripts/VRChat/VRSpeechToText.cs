using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;

namespace VRChat
{
    public enum STTTriggerMode
    {
        Grip = 0,
        LeftYButton = 1
    }

    /// <summary>
    /// VR Speech-to-Text using Groq's Whisper API
    /// Hold the configured button to record, release to send to AI
    /// </summary>
    public class VRSpeechToText : MonoBehaviour
    {
        [Header("API Configuration")]
        [SerializeField] private string apiKey = "YOUR_GROQ_API_KEY";
        
        [Header("Input - Right Controller Grip")]
        [SerializeField] private InputActionReference gripAction;
        
        [Header("Meta Quest Input (OVRInput)")]
        [Tooltip("Use OVRInput for Meta Quest controllers instead of Input System")]
        [SerializeField] private bool useOVRInput = true;
        [SerializeField] private STTTriggerMode triggerMode = STTTriggerMode.Grip;
        [Tooltip("Which hand's grip button to use when triggerMode is Grip")]
        [SerializeField] private OVRInput.Controller gripController = OVRInput.Controller.LTouch;
        [Tooltip("Grip button threshold (0-1). Lower = more sensitive")]
        [Range(0.1f, 0.9f)]
        [SerializeField] private float gripThreshold = 0.5f;
        [Tooltip("Whisper language code. Empty = auto-detect.")]
        [SerializeField] private string whisperLanguage = "en";
        
        [Header("Recording Settings")]
        [SerializeField] private int sampleRate = 16000;
        [SerializeField] private int maxRecordingSeconds = 30;
        [SerializeField] private float minRecordingSeconds = 0.5f;
        
        [Header("Microphone Selection")]
        [Tooltip("Leave empty to auto-select VR headset microphone. Or type exact microphone name.")]
        [SerializeField] private string preferredMicrophone = "";
        [Tooltip("If true, always prefer Oculus/Quest headset microphone")]
        [SerializeField] private bool preferVRHeadsetMic = true;
        
        [Header("Visual Feedback (Optional)")]
        [SerializeField] private GameObject recordingIndicator;
        [SerializeField] private AudioSource feedbackAudio;
        [SerializeField] private AudioClip startRecordingSound;
        [SerializeField] private AudioClip stopRecordingSound;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        // Events
        public event Action OnRecordingStarted;
        public event Action OnRecordingStopped;
        public event Action<string> OnTranscriptionReceived;
        public event Action<string> OnError;

        // State
        public bool IsRecording => isRecording;
        public bool IsProcessing => isProcessing;
        public bool ListeningEnabled => listeningEnabled;

        /// <summary>Loudness of the voice being recorded right now, from 0 (silence) to 1.</summary>
        public float CurrentLevel => SampleLevel();

        private const string WHISPER_API_URL = "https://api.groq.com/openai/v1/audio/transcriptions";
        private const string WHISPER_MODEL = "whisper-large-v3-turbo";

        private AudioClip recordingClip;
        private bool isRecording = false;
        private bool isProcessing = false;
        private float recordingStartTime;
        private int recordingStartSample;
        private string microphoneDevice;
        
        // OVRInput state tracking
        private bool wasGripPressed = false;
        private bool listeningEnabled = true;
        private readonly float[] levelSamples = new float[512];

        // Reference to chat system
        private VRChatSetup chatSetup;

        void Awake()
        {
            // Find chat setup to send messages
            chatSetup = FindFirstObjectByType<VRChatSetup>();
        }

        void Start()
        {
            // Log available microphones first
            if (showDebugLogs)
            {
                Debug.Log($"[VRSpeechToText] Available microphones: {string.Join(", ", Microphone.devices)}");
            }

            // Use preferred microphone if specified
            if (!string.IsNullOrEmpty(preferredMicrophone))
            {
                if (System.Array.IndexOf(Microphone.devices, preferredMicrophone) >= 0)
                {
                    microphoneDevice = preferredMicrophone;
                }
                else
                {
                    Debug.LogWarning($"[VRSpeechToText] Preferred microphone '{preferredMicrophone}' not found!");
                    microphoneDevice = FindBestMicrophone();
                }
            }
            else
            {
                // Auto-select best microphone (prefer VR headset)
                microphoneDevice = FindBestMicrophone();
            }
            
            if (string.IsNullOrEmpty(microphoneDevice))
            {
                Debug.LogError("[VRSpeechToText] No microphone found!");
            }
            else if (showDebugLogs)
            {
                Debug.Log($"[VRSpeechToText] SELECTED microphone: {microphoneDevice}");
            }

            // Microphone.Start/End block the main thread for about a second on Windows, so the device stays open for the whole session
            EnsureMicrophoneRunning();

            // Setup input action
            SetupInputAction();

            // Hide recording indicator
            if (recordingIndicator != null)
            {
                recordingIndicator.SetActive(false);
            }

            if (showDebugLogs)
            {
                string trigger = triggerMode == STTTriggerMode.LeftYButton ? "LEFT Y" : "GRIP";
                Debug.Log($"[VRSpeechToText] Ready! Hold {trigger} to record, release to send.");
            }
        }

        void SetupInputAction()
        {
            // If using OVRInput, skip Input System setup
            if (useOVRInput)
            {
                if (showDebugLogs)
                {
                    Debug.Log($"[VRSpeechToText] Using OVRInput for {gripController} grip button");
                }
                return;
            }

            if (gripAction != null && gripAction.action != null)
            {
                gripAction.action.Enable();
                gripAction.action.started += OnGripPressed;
                gripAction.action.canceled += OnGripReleased;
                
                if (showDebugLogs)
                {
                    Debug.Log($"[VRSpeechToText] Grip action bound: {gripAction.action.name}");
                }
            }
            else
            {
                Debug.LogWarning("[VRSpeechToText] No grip action assigned! Falling back to keyboard 'V' key for testing.");
            }
        }

        void Update()
        {
            if (isRecording && Time.time - recordingStartTime >= maxRecordingSeconds)
                StopRecordingAndProcess();

            if (useOVRInput)
            {
                UpdateOVRInput();
                return;
            }

            if (!listeningEnabled)
                return;

            if (gripAction != null && gripAction.action != null)
                return;

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.vKey.wasPressedThisFrame && !isRecording)
                StartRecording();
            else if (keyboard.vKey.wasReleasedThisFrame && isRecording)
                StopRecordingAndProcess();
        }

        void UpdateOVRInput()
        {
            if (!listeningEnabled)
            {
                if (isRecording)
                    CancelRecording();
                wasGripPressed = false;
                return;
            }

            bool isPressed = IsTalkButtonHeld();

            if (isPressed && !wasGripPressed)
            {
                if (showDebugLogs) Debug.Log("[VRSpeechToText] Talk button PRESSED");
                StartRecording();
            }
            else if (!isPressed && wasGripPressed)
            {
                if (showDebugLogs) Debug.Log("[VRSpeechToText] Talk button RELEASED");
                StopRecordingAndProcess();
            }

            wasGripPressed = isPressed;
        }

        bool IsTalkButtonHeld()
        {
            if (triggerMode == STTTriggerMode.LeftYButton)
                return OVRInput.Get(OVRInput.Button.Four);

            float gripValue = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, gripController);
            return gripValue >= gripThreshold;
        }

        void OnGripPressed(InputAction.CallbackContext context)
        {
            if (showDebugLogs) Debug.Log("[VRSpeechToText] Grip PRESSED");
            StartRecording();
        }

        void OnGripReleased(InputAction.CallbackContext context)
        {
            if (showDebugLogs) Debug.Log("[VRSpeechToText] Grip RELEASED");
            StopRecordingAndProcess();
        }

        public void StartRecording()
        {
            if (!listeningEnabled || isRecording || isProcessing) return;
            if (string.IsNullOrEmpty(microphoneDevice))
            {
                Debug.LogError("[VRSpeechToText] No microphone available!");
                OnError?.Invoke("No microphone available");
                return;
            }

            if (!EnsureMicrophoneRunning())
            {
                OnError?.Invoke("Microphone could not be started");
                return;
            }

            isRecording = true;
            recordingStartTime = Time.time;
            recordingStartSample = Microphone.GetPosition(microphoneDevice);

            // Visual/audio feedback
            if (recordingIndicator != null) SetRecordingVisual(true);
            if (feedbackAudio != null && startRecordingSound != null)
            {
                feedbackAudio.PlayOneShot(startRecordingSound);
            }

            OnRecordingStarted?.Invoke();

            if (showDebugLogs)
            {
                Debug.Log("[VRSpeechToText] Recording started... Speak now!");
            }
        }

        public void StopRecordingAndProcess()
        {
            if (!isRecording) return;

            float recordingDuration = Time.time - recordingStartTime;
            isRecording = false;

            // Visual/audio feedback
            if (recordingIndicator != null) SetRecordingVisual(false);
            if (feedbackAudio != null && stopRecordingSound != null)
            {
                feedbackAudio.PlayOneShot(stopRecordingSound);
            }

            OnRecordingStopped?.Invoke();

            // Check minimum duration
            if (recordingDuration < minRecordingSeconds)
            {
                if (showDebugLogs)
                {
                    Debug.Log($"[VRSpeechToText] Recording too short ({recordingDuration:F1}s), ignoring.");
                }
                return;
            }

            int clipSamples = recordingClip != null ? recordingClip.samples : 0;
            int recordingLength = clipSamples > 0
                ? (Microphone.GetPosition(microphoneDevice) - recordingStartSample + clipSamples) % clipSamples
                : 0;

            if (recordingLength <= 0)
            {
                Debug.LogWarning("[VRSpeechToText] No audio recorded");
                OnError?.Invoke("No audio recorded");
                return;
            }

            if (showDebugLogs)
            {
                Debug.Log($"[VRSpeechToText] Recording stopped. Duration: {recordingDuration:F1}s, Samples: {recordingLength}");
            }

            float[] audioData = new float[recordingLength];
            recordingClip.GetData(audioData, recordingStartSample);

            StartCoroutine(ProcessRecording(audioData, recordingLength, recordingClip.frequency));
        }

        private IEnumerator ProcessRecording(float[] audioData, int sampleCount, int clipSampleRate)
        {
            isProcessing = true;

            if (showDebugLogs)
            {
                Debug.Log("[VRSpeechToText] Processing audio...");
            }

            // Convert to WAV bytes
            byte[] wavData = ConvertToWav(audioData, sampleCount, clipSampleRate);

            Debug.Log($"[VRSpeechToText] Sending {wavData.Length} bytes to Whisper API...");

            // Create multipart form data for Whisper API
            List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
            formData.Add(new MultipartFormFileSection("file", wavData, "recording.wav", "audio/wav"));
            formData.Add(new MultipartFormDataSection("model", WHISPER_MODEL));
            if (!string.IsNullOrWhiteSpace(whisperLanguage))
                formData.Add(new MultipartFormDataSection("language", whisperLanguage));

            // Use UnityWebRequest for Android/Quest compatibility
            using (UnityWebRequest request = UnityWebRequest.Post(WHISPER_API_URL, formData))
            {
                request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                request.timeout = 30;

                yield return request.SendWebRequest();

                isProcessing = false;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    string error = $"Whisper API error: {request.error}";
                    if (request.downloadHandler != null && request.downloadHandler.text != null)
                    {
                        error += $" - {request.downloadHandler.text}";
                    }
                    Debug.LogError($"[VRSpeechToText] {error}");
                    OnError?.Invoke(error);
                    if (SessionLogger.Instance != null)
                    {
                        if (GroqQuotaGuard.IsQuotaOrRateLimit(error))
                            SessionLogger.Instance.RegisterAiQuotaExceeded(error);
                        else
                            SessionLogger.Instance.RegisterAiError(error);
                    }
                    yield break;
                }

                string jsonResponse = request.downloadHandler.text;
                Debug.Log($"[VRSpeechToText] API Response: {jsonResponse}");

                // Parse JSON response
                var result = JsonUtility.FromJson<WhisperResponse>(jsonResponse);
                string transcription = result?.text ?? "";

                if (string.IsNullOrWhiteSpace(transcription))
                {
                    if (showDebugLogs)
                    {
                        Debug.Log("[VRSpeechToText] No speech detected");
                    }
                    yield break;
                }

                if (showDebugLogs)
                {
                    Debug.Log($"[VRSpeechToText] Transcription: \"{transcription}\"");
                }

                OnTranscriptionReceived?.Invoke(transcription);

                // Send to chat system
                SendToChat(transcription);
            }
        }

        private void SendToChat(string message)
        {
            if (chatSetup != null)
            {
                chatSetup.SendVoiceMessage(message);
                if (showDebugLogs)
                {
                    Debug.Log($"[VRSpeechToText] Sent to chat: \"{message}\"");
                }
            }
            else
            {
                Debug.LogWarning("[VRSpeechToText] VRChatSetup not found, cannot send message");
            }
        }

        private byte[] ConvertToWav(float[] audioData, int sampleCount, int sampleRate)
        {
            // WAV header + data
            int headerSize = 44;
            int dataSize = sampleCount * 2; // 16-bit samples
            byte[] wav = new byte[headerSize + dataSize];

            // RIFF header
            System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(wav, 0);
            BitConverter.GetBytes(wav.Length - 8).CopyTo(wav, 4);
            System.Text.Encoding.ASCII.GetBytes("WAVE").CopyTo(wav, 8);

            // fmt chunk
            System.Text.Encoding.ASCII.GetBytes("fmt ").CopyTo(wav, 12);
            BitConverter.GetBytes(16).CopyTo(wav, 16); // chunk size
            BitConverter.GetBytes((short)1).CopyTo(wav, 20); // PCM format
            BitConverter.GetBytes((short)1).CopyTo(wav, 22); // mono
            BitConverter.GetBytes(sampleRate).CopyTo(wav, 24); // sample rate
            BitConverter.GetBytes(sampleRate * 2).CopyTo(wav, 28); // byte rate
            BitConverter.GetBytes((short)2).CopyTo(wav, 32); // block align
            BitConverter.GetBytes((short)16).CopyTo(wav, 34); // bits per sample

            // data chunk
            System.Text.Encoding.ASCII.GetBytes("data").CopyTo(wav, 36);
            BitConverter.GetBytes(dataSize).CopyTo(wav, 40);

            // Convert float samples to 16-bit PCM
            int offset = 44;
            for (int i = 0; i < sampleCount; i++)
            {
                float sample = Mathf.Clamp(audioData[i], -1f, 1f);
                short pcmSample = (short)(sample * 32767f);
                wav[offset] = (byte)pcmSample;
                wav[offset + 1] = (byte)(pcmSample >> 8);
                offset += 2;
            }

            return wav;
        }

        private void SetRecordingVisual(bool recording)
        {
            if (recordingIndicator == null)
                return;

            recordingIndicator.SetActive(recording);
            var indicator = recordingIndicator.GetComponent<RecordingIndicator>();
            if (indicator != null)
                indicator.SetRecording(recording);
        }

        public void SetApiKey(string key)
        {
            apiKey = key;
        }

        public void SetListeningEnabled(bool enabled)
        {
            listeningEnabled = enabled;
            if (!enabled && isRecording)
                CancelRecording();
        }

        public void SetWhisperLanguage(string language)
        {
            whisperLanguage = language ?? "";
        }

        public void CancelRecording()
        {
            if (!isRecording)
                return;

            isRecording = false;
            wasGripPressed = false;

            if (recordingIndicator != null)
                SetRecordingVisual(false);

            OnRecordingStopped?.Invoke();
        }

        private float SampleLevel()
        {
            if (!isRecording || recordingClip == null || string.IsNullOrEmpty(microphoneDevice))
                return 0f;
            if (!Microphone.IsRecording(microphoneDevice))
                return 0f;

            int channels = Mathf.Max(1, recordingClip.channels);
            int window = levelSamples.Length / channels;
            int position = Microphone.GetPosition(microphoneDevice);
            if (position < window)
                return 0f;

            if (!recordingClip.GetData(levelSamples, position - window))
                return 0f;

            double sum = 0;
            int count = window * channels;
            for (int i = 0; i < count; i++)
                sum += levelSamples[i] * levelSamples[i];

            return Mathf.Clamp01((float)System.Math.Sqrt(sum / count) * 14f);
        }

        private bool EnsureMicrophoneRunning()
        {
            if (string.IsNullOrEmpty(microphoneDevice))
                return false;

            if (recordingClip != null && Microphone.IsRecording(microphoneDevice))
                return true;

            recordingClip = Microphone.Start(microphoneDevice, true, maxRecordingSeconds + 1, sampleRate);
            return recordingClip != null;
        }

        private void StopMicrophone()
        {
            if (!string.IsNullOrEmpty(microphoneDevice) && Microphone.IsRecording(microphoneDevice))
                Microphone.End(microphoneDevice);
            recordingClip = null;
        }

        /// <summary>
        /// Find the best microphone, preferring VR headset microphones
        /// </summary>
        private string FindBestMicrophone()
        {
            if (Microphone.devices.Length == 0)
                return null;

            // Keywords that indicate VR headset microphones (in priority order)
            string[] vrMicKeywords = new string[]
            {
                "Oculus",
                "Quest",
                "Meta",
                "Headset",
                "VR",
                "Virtual Audio",
                "Rift"
            };

            // Keywords to avoid (webcams, laptop mics, etc.)
            string[] avoidKeywords = new string[]
            {
                "Camo",
                "Webcam",
                "Camera",
                "Intel",
                "Realtek"
            };

            string bestMic = null;
            int bestPriority = -1;

            foreach (string device in Microphone.devices)
            {
                string deviceLower = device.ToLower();
                
                // Check if this is a VR microphone
                int priority = 0;
                for (int i = 0; i < vrMicKeywords.Length; i++)
                {
                    if (deviceLower.Contains(vrMicKeywords[i].ToLower()))
                    {
                        priority = vrMicKeywords.Length - i + 10; // Higher priority for earlier keywords
                        break;
                    }
                }

                // Check if we should avoid this mic
                bool shouldAvoid = false;
                foreach (string avoid in avoidKeywords)
                {
                    if (deviceLower.Contains(avoid.ToLower()))
                    {
                        shouldAvoid = true;
                        break;
                    }
                }

                // If not a VR mic but not avoided, give it low priority
                if (priority == 0 && !shouldAvoid)
                {
                    priority = 1;
                }
                else if (shouldAvoid)
                {
                    priority = 0;
                }

                if (priority > bestPriority)
                {
                    bestPriority = priority;
                    bestMic = device;
                }
            }

            // Fallback to first device if nothing good found
            if (bestMic == null)
            {
                bestMic = Microphone.devices[0];
            }

            return bestMic;
        }

        /// <summary>
        /// Manually set which microphone to use
        /// </summary>
        public void SetMicrophone(string deviceName)
        {
            if (System.Array.IndexOf(Microphone.devices, deviceName) >= 0)
            {
                CancelRecording();
                StopMicrophone();
                microphoneDevice = deviceName;
                EnsureMicrophoneRunning();
                Debug.Log($"[VRSpeechToText] Microphone changed to: {deviceName}");
            }
            else
            {
                Debug.LogWarning($"[VRSpeechToText] Microphone not found: {deviceName}");
            }
        }

        void OnDestroy()
        {
            if (gripAction != null && gripAction.action != null)
            {
                gripAction.action.started -= OnGripPressed;
                gripAction.action.canceled -= OnGripReleased;
            }

            StopMicrophone();
        }

        [Serializable]
        private class WhisperResponse
        {
            public string text;
        }
    }
}
