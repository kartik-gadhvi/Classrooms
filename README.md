# Classrooms

VR Millikan oil-drop experiment with a bilingual AI voice tutor.

This Unity project is the implementation for Kartik Suru’s master’s thesis:

**Use of an AI-based virtual guide to support physics experiments in VR learning applications**

The thesis asks whether an LLM-powered guide adds measurable value over a scripted, rule-based guide when both use the same scene, apparatus, and learning content. The primary case study is a VR reproduction of the Millikan oil-drop experiment on Meta Quest 3.

## What this build does

Learners sit at a virtual Millikan apparatus, follow a 67-step tutorial (steps 0–66), measure droplet motion, and interpret charge quantization. Before the experiment they choose a language and take a pre-quiz. After the experiment they take a post-quiz and Likert questionnaires. Session data is written to a CSV for the study.

A voice-only AI tutor is available during the tutorial. It knows the current step, answers in the chosen language, and refuses off-topic questions.

## Session flow

1. **Language** — on the quiz wall, press **A** for Deutsch or **B** for English
2. **Pre-quiz** — knowledge items before any instruction
3. **Welcome and teleport** — enter the lab and sit at Seat 01
4. **Tutorial** — scripted Millikan steps with localized text, hints, and voice clips
5. **Voice tutor** — hold **left Y** to ask a question about the current step
6. **Post-quiz** — same knowledge items after the experiment
7. **Questionnaires** — Likert items for the evaluation

## Voice tutor

| Control | Action |
| --- | --- |
| Hold **left Y** | Record a question (red sphere on the left hand) |
| Release **left Y** | Transcribe with Whisper and get a spoken reply |

The tutor pauses scripted narration while you speak. Replies stay on the Millikan experiment and the current step. Jokes, news, and other subjects are refused and redirected.

One voice is used everywhere: ElevenLabs "Bill", defined in `Assets/Resources/TutorVoiceProfile.asset`. Scripted step audio is pre-generated from the on-screen text with the high-quality model (`eleven_multilingual_v2`) via **Tools > Millikan > Narration Generator**; live AI replies use the same voice with the low-latency model (`eleven_flash_v2_5`). If ElevenLabs is unavailable, replies fall back to Groq TTS (Orpheus).

There is no chat canvas. The tutor is voice-only.

## Requirements

- **Unity** 6000.0.58f2
- **Headset** Meta Quest 3 (Meta XR / OpenXR)
- **Groq API key** on the `ExperimentAI` object in `Classroom_Low` (`VRChatSetup`)
- **ElevenLabs API key**: paste it once under **Tools > Millikan > API Keys** (the window opens by itself when no key is set). It is saved only on your computer in `UserSettings/` (git-ignored) and added to headset builds automatically by `ElevenLabsKeyBuildStep`. The `ELEVENLABS_API_KEY` environment variable also works. Without a key, AI replies use the Groq fallback voice.

Open `Assets/Scenes/Classroom_Low.unity` and press Play, or build for Android and deploy to the headset.

## Project layout

```
Assets/Scenes/Classroom_Low.unity     Main study scene
Assets/Prefabs/millikan_optimized_v2.prefab
Assets/Material/MillikanWav/          German step voice clips (generated)
Assets/Material/MillikanWav_en/       English step voice clips (generated)
Assets/Resources/TutorVoiceProfile.asset  Shared voice settings (narration + live replies)
Narration/                            Spoken script and generation manifest
Assets/Scripts/
  Localization/                       DE/EN strings and language manager
  ExperimentAITutor.cs                Bridges tutorial state to the voice tutor
  BottomTutorialController.cs         Step sequence, hints, and WAV playback
  PrePostQuizController.cs
  QuestionnaireController.cs
  SessionLogger.cs                    One CSV per run
  Voice/                              ElevenLabs voice profile, key provider, TTS helpers
  VRChat/                             STT, TTS, and Groq chat
  GroqApiLibrary-master/              Groq client
```

Language is stored as `de` or `en` in the session CSV.

## Thesis context

Physics labs connect theory to observable phenomena, but access is limited by cost, safety, equipment, and supervision. VR can reproduce those labs on a standalone headset. A virtual lab still needs guidance. Scripted guides are reliable but cannot answer open questions. This project implements the AI condition: a context-aware voice guide grounded in the learner’s current Millikan step.

The planned comparison keeps avatar, scene, and content constant and changes only the dialogue mechanism (LLM vs. pre-recorded scripts). Evaluation goes beyond knowledge and usability to identification with the experimenter role, narrative engagement, interaction fidelity, and perceived realism.

## Author

Kartik Suru  
Master’s thesis project, VR physics learning
