using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

namespace VRChat
{
    /// <summary>
    /// Easy Setup Script - Attach to any GameObject and it will automatically find
    /// and configure the VR Chat system with your existing UI elements.
    /// 
    /// USAGE:
    /// 1. Add this script to an empty GameObject in your scene (e.g., "VRChatManager")
    /// 2. Enter your Groq API key in the Inspector
    /// 3. Press Play - it will auto-find ChatScrollView, MessageInput, SendButton, and Content
    /// </summary>
    public class VRChatSetup : MonoBehaviour
    {
        [Header("API Configuration (Required)")]
        [SerializeField] private string groqApiKey = "YOUR_GROQ_API_KEY";
        [SerializeField] private string model = "openai/gpt-oss-20b";
        [SerializeField] [TextArea(2, 4)] private string systemPrompt = "You are a helpful AI assistant in a VR environment. Keep responses concise and friendly.";

        [Header("Auto-Detection (Optional - Leave empty to auto-find)")]
        [SerializeField] private ScrollRect chatScrollRect;
        [SerializeField] private RectTransform contentContainer;
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private Button sendButton;

        [Header("Theme")]
        [SerializeField] private Color userBubbleColor = new Color(0.2f, 0.5f, 0.85f, 1f);
        [SerializeField] private Color aiBubbleColor = new Color(0.22f, 0.22f, 0.28f, 1f);
        [SerializeField] private Color textColor = new Color(0.95f, 0.95f, 0.95f, 1f);

        [Header("Text-to-Speech")]
        [SerializeField] private bool enableTTS = true;
        [SerializeField] private TTSVoice aiVoice = TTSVoice.Troy;
        [SerializeField] private AudioSource ttsAudioSource; // Optional - will create if null

        [Header("Speech-to-Text (Voice Input)")]
        [SerializeField] private bool enableSTT = true;
        [Tooltip("Use Meta Quest OVRInput for grip button (recommended for Quest)")]
        [SerializeField] private bool useOVRInput = true;
        [Tooltip("Which controller's grip to use (LTouch = Left, RTouch = Right)")]
        [SerializeField] private OVRInput.Controller sttGripController = OVRInput.Controller.LTouch;
        [Tooltip("Only needed if useOVRInput is false")]
        [SerializeField] private InputActionReference rightGripAction;
        [SerializeField] private GameObject recordingIndicator;

        [Header("Speaking Indicator (Optional)")]
        [SerializeField] private GameObject speakingIndicator; // Show when AI is speaking

        [Header("Voice-Only Mode")]
        [Tooltip("If false, chat canvas is optional. Voice STT/TTS still works.")]
        [SerializeField] private bool requireChatUI = true;
        [SerializeField] private STTTriggerMode sttTrigger = STTTriggerMode.Grip;
        [Tooltip("Whisper language code, e.g. de or en. Leave empty to auto-detect.")]
        [SerializeField] private string whisperLanguage = "en";

        private VRChatController chatController;
        private VRChatTTS ttsManager;
        private VRSpeechToText sttManager;

        private void Awake()
        {
            Setup();
        }

        [ContextMenu("Setup VR Chat System")]
        public void Setup()
        {
            Debug.Log("[VRChatSetup] Starting auto-setup...");

            if (requireChatUI)
                AutoFindUIElements();

            if (!ValidateSetup())
            {
                Debug.LogError("[VRChatSetup] Setup failed - missing required configuration!");
                return;
            }

            // Add/Get VRChatController
            chatController = gameObject.GetComponent<VRChatController>();
            if (chatController == null)
            {
                chatController = gameObject.AddComponent<VRChatController>();
            }

            ConfigureChatController();

            // Setup TTS if enabled
            if (enableTTS)
            {
                SetupTTS();
            }

            // Setup STT (voice input) if enabled
            if (enableSTT)
            {
                SetupSTT();
            }

            Debug.Log("[VRChatSetup] Setup complete! Ready to chat.");
            if (enableSTT)
            {
                string triggerHint = sttTrigger == STTTriggerMode.LeftYButton
                    ? "Hold LEFT Y to speak, or press V key for testing."
                    : "Hold GRIP to speak, or press V key for testing.";
                Debug.Log("[VRChatSetup] Voice input enabled - " + triggerHint);
            }
        }

        private void AutoFindUIElements()
        {
            // Find ChatScrollView
            if (chatScrollRect == null)
            {
                var scrollViews = FindObjectsByType<ScrollRect>(FindObjectsSortMode.None);
                foreach (var sv in scrollViews)
                {
                    if (sv.name.Contains("Chat") || sv.name.Contains("Scroll") || sv.name.Contains("Message"))
                    {
                        chatScrollRect = sv;
                        Debug.Log($"[VRChatSetup] Auto-found ScrollRect: {sv.name}");
                        break;
                    }
                }
            }

            // Find Content container (usually child of ScrollRect viewport)
            if (contentContainer == null && chatScrollRect != null)
            {
                contentContainer = chatScrollRect.content;
                if (contentContainer != null)
                {
                    Debug.Log($"[VRChatSetup] Auto-found Content: {contentContainer.name}");
                }
            }

            // Find MessageInput
            if (inputField == null)
            {
                var inputs = FindObjectsByType<TMP_InputField>(FindObjectsSortMode.None);
                foreach (var input in inputs)
                {
                    if (input.name.Contains("Message") || input.name.Contains("Input") || input.name.Contains("Chat"))
                    {
                        inputField = input;
                        Debug.Log($"[VRChatSetup] Auto-found InputField: {input.name}");
                        break;
                    }
                }
            }

            // Find SendButton
            if (sendButton == null)
            {
                var buttons = FindObjectsByType<Button>(FindObjectsSortMode.None);
                foreach (var btn in buttons)
                {
                    if (btn.name.Contains("Send") || btn.name.Contains("Submit"))
                    {
                        sendButton = btn;
                        Debug.Log($"[VRChatSetup] Auto-found SendButton: {btn.name}");
                        break;
                    }
                }
            }

            // Try to find AudioSource for TTS
            if (ttsAudioSource == null)
            {
                // Look for existing audio source on this object or children
                ttsAudioSource = GetComponentInChildren<AudioSource>();
                if (ttsAudioSource != null)
                {
                    Debug.Log($"[VRChatSetup] Auto-found AudioSource: {ttsAudioSource.gameObject.name}");
                }
            }
        }

        private bool ValidateSetup()
        {
            bool valid = true;

            if (string.IsNullOrEmpty(groqApiKey) || groqApiKey == "YOUR_GROQ_API_KEY")
            {
                Debug.LogWarning("[VRChatSetup] Groq API Key not set! Please enter your API key.");
                valid = false;
            }

            if (!requireChatUI)
                return valid;

            if (chatScrollRect == null)
            {
                Debug.LogWarning("[VRChatSetup] ChatScrollRect not found! Please assign manually.");
                valid = false;
            }

            if (contentContainer == null)
            {
                Debug.LogWarning("[VRChatSetup] Content container not found! Please assign manually.");
                valid = false;
            }

            if (inputField == null)
            {
                Debug.LogWarning("[VRChatSetup] InputField not found! Please assign manually.");
                valid = false;
            }

            if (sendButton == null)
            {
                Debug.LogWarning("[VRChatSetup] SendButton not found! Please assign manually.");
                valid = false;
            }

            return valid;
        }

        private void ConfigureChatController()
        {
            var type = typeof(VRChatController);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

            // Set API config
            var apiKeyField = type.GetField("apiKey", flags);
            apiKeyField?.SetValue(chatController, groqApiKey);

            var modelField = type.GetField("model", flags);
            modelField?.SetValue(chatController, GroqChatModel.Resolve(model));

            var promptField = type.GetField("systemPrompt", flags);
            promptField?.SetValue(chatController, systemPrompt);

            var maxTokensField = type.GetField("maxTokens", flags);
            maxTokensField?.SetValue(chatController, 256);

            var historyField = type.GetField("maxHistoryTurns", flags);
            historyField?.SetValue(chatController, 6);

            // Set UI references
            var scrollField = type.GetField("chatScrollRect", flags);
            scrollField?.SetValue(chatController, chatScrollRect);

            var contentField = type.GetField("contentContainer", flags);
            contentField?.SetValue(chatController, contentContainer);

            var inputFieldRef = type.GetField("inputField", flags);
            inputFieldRef?.SetValue(chatController, inputField);

            var buttonField = type.GetField("sendButton", flags);
            buttonField?.SetValue(chatController, sendButton);

            // Set colors
            var userBubbleField = type.GetField("userBubbleColor", flags);
            userBubbleField?.SetValue(chatController, userBubbleColor);

            var aiBubbleField = type.GetField("aiBubbleColor", flags);
            aiBubbleField?.SetValue(chatController, aiBubbleColor);
        }

        private void SetupTTS()
        {
            // Add/Get VRChatTTS component
            ttsManager = gameObject.GetComponent<VRChatTTS>();
            if (ttsManager == null)
            {
                ttsManager = gameObject.AddComponent<VRChatTTS>();
            }

            // Configure TTS
            ttsManager.SetApiKey(groqApiKey);
            
            // Validate voice enum (handle old PlayAI values saved in scene)
            int voiceIndex = (int)aiVoice;
            if (voiceIndex < 0 || voiceIndex > 5)
            {
                Debug.LogWarning($"[VRChatSetup] Invalid voice enum value {voiceIndex} (old PlayAI?), defaulting to Troy");
                aiVoice = TTSVoice.Troy;
            }
            ttsManager.SetVoice(aiVoice);

            // Set audio source if provided
            if (ttsAudioSource != null)
            {
                var audioField = typeof(VRChatTTS).GetField("audioSource", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                audioField?.SetValue(ttsManager, ttsAudioSource);
            }

            // Subscribe to AI responses
            if (chatController != null)
            {
                chatController.OnAIResponseReceived += OnAIResponse;
            }

            // Setup speaking indicator
            if (speakingIndicator != null)
            {
                speakingIndicator.SetActive(false);
                
                ttsManager.OnSpeakingStarted += () => speakingIndicator.SetActive(true);
                ttsManager.OnSpeakingFinished += () => speakingIndicator.SetActive(false);
            }

            // Log errors
            ttsManager.OnError += (error) => Debug.LogError($"[VRChatTTS] {error}");

            Debug.Log($"[VRChatSetup] TTS enabled with voice: {aiVoice}");
        }

        private void SetupSTT()
        {
            // Add/Get VRSpeechToText component
            sttManager = gameObject.GetComponent<VRSpeechToText>();
            if (sttManager == null)
            {
                sttManager = gameObject.AddComponent<VRSpeechToText>();
            }

            // Configure STT via reflection (to set serialized fields)
            var sttType = typeof(VRSpeechToText);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

            // Set API key
            sttManager.SetApiKey(groqApiKey);

            // Set OVRInput settings
            var useOVRField = sttType.GetField("useOVRInput", flags);
            useOVRField?.SetValue(sttManager, useOVRInput);

            var controllerField = sttType.GetField("gripController", flags);
            controllerField?.SetValue(sttManager, sttGripController);

            var triggerField = sttType.GetField("triggerMode", flags);
            triggerField?.SetValue(sttManager, sttTrigger);

            var languageField = sttType.GetField("whisperLanguage", flags);
            languageField?.SetValue(sttManager, whisperLanguage);

            // Set grip action if provided (for non-OVR input)
            if (rightGripAction != null)
            {
                var gripField = sttType.GetField("gripAction", flags);
                gripField?.SetValue(sttManager, rightGripAction);
            }

            // Set recording indicator if provided
            if (recordingIndicator != null)
            {
                var indicatorField = sttType.GetField("recordingIndicator", flags);
                indicatorField?.SetValue(sttManager, recordingIndicator);
            }

            // Subscribe to events for feedback
            sttManager.OnRecordingStarted += () => Debug.Log("[VRChatSetup] Voice recording started...");
            sttManager.OnRecordingStopped += () => Debug.Log("[VRChatSetup] Voice recording stopped, processing...");
            sttManager.OnTranscriptionReceived += (text) => Debug.Log($"[VRChatSetup] You said: \"{text}\"");
            sttManager.OnError += (error) => Debug.LogError($"[VRChatSetup] STT Error: {error}");

            string hand = sttGripController == OVRInput.Controller.LTouch ? "LEFT" : "RIGHT";
            string inputMethod = useOVRInput ? $"OVRInput ({hand} grip)" : "Input System";
            Debug.Log($"[VRChatSetup] Speech-to-Text enabled using {inputMethod}");
        }

        private void OnAIResponse(string text)
        {
            if (ttsManager != null && enableTTS)
            {
                Debug.Log("[VRChatSetup] Speaking AI response...");
                ttsManager.Speak(text);
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (chatController != null)
            {
                chatController.OnAIResponseReceived -= OnAIResponse;
            }
        }

        /// <summary>
        /// Get the chat controller for external use (e.g., voice input)
        /// </summary>
        public VRChatController GetChatController()
        {
            return chatController;
        }

        /// <summary>
        /// Get the TTS manager for external control
        /// </summary>
        public VRChatTTS GetTTSManager()
        {
            return ttsManager;
        }

        /// <summary>
        /// Get the STT (Speech-to-Text) manager for external control
        /// </summary>
        public VRSpeechToText GetSTTManager()
        {
            return sttManager;
        }

        public void SetSystemPrompt(string prompt)
        {
            systemPrompt = prompt;
            chatController?.SetSystemPrompt(prompt);
        }

        public void SetWhisperLanguage(string language)
        {
            whisperLanguage = language ?? "";
            sttManager?.SetWhisperLanguage(whisperLanguage);
        }

        /// <summary>
        /// Send a message directly (useful for voice-to-text integration)
        /// </summary>
        public void SendVoiceMessage(string message)
        {
            if (chatController != null && !string.IsNullOrWhiteSpace(message))
            {
                chatController.SendMessage(message);
            }
        }

        /// <summary>
        /// Toggle TTS on/off at runtime
        /// </summary>
        public void SetTTSEnabled(bool enabled)
        {
            enableTTS = enabled;
            Debug.Log($"[VRChatSetup] TTS {(enabled ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// Change AI voice at runtime
        /// </summary>
        public void SetAIVoice(TTSVoice voice)
        {
            aiVoice = voice;
            if (ttsManager != null)
            {
                ttsManager.SetVoice(voice);
            }
        }

        /// <summary>
        /// Stop AI from speaking
        /// </summary>
        public void StopSpeaking()
        {
            if (ttsManager != null)
            {
                ttsManager.Stop();
            }
        }

        /// <summary>
        /// Check if AI is currently speaking
        /// </summary>
        public bool IsSpeaking()
        {
            return ttsManager != null && ttsManager.IsSpeaking;
        }
    }
}

