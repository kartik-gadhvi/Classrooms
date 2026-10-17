using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rule-based guide condition: answers a transcribed question with predefined, pre-recorded lines.
/// No language model is involved. Clips are named after the line ids in RuleBasedGuideStrings.
/// </summary>
public class RuleBasedGuide : MonoBehaviour
{
    [SerializeField] private AudioClip[] clipsGerman;
    [SerializeField] private AudioClip[] clipsEnglish;
    [SerializeField] private float pauseBetweenLines = 0.3f;

    private AudioClip lastJoined;

    public struct Reply
    {
        public string topic;
        public string text;
        public AudioClip clip;
    }

    public Reply Respond(string transcript, int step)
    {
        AppLanguage language = LanguageManager.Current;
        string topic;
        string[] ids = RuleBasedGuideStrings.Reply(transcript, step, out topic);

        var texts = new List<string>();
        var clips = new List<AudioClip>();
        bool complete = true;

        foreach (string id in ids)
        {
            texts.Add(RuleBasedGuideStrings.Text(id, language));
            AudioClip clip = FindClip(id, language);
            if (clip == null)
                complete = false;
            clips.Add(clip);
        }

        return new Reply
        {
            topic = topic,
            text = string.Join(" ", texts),
            clip = complete ? Join(clips) : null
        };
    }

    public void SetClips(AppLanguage language, AudioClip[] clips)
    {
        if (language == AppLanguage.English)
            clipsEnglish = clips;
        else
            clipsGerman = clips;
    }

    private AudioClip FindClip(string id, AppLanguage language)
    {
        AudioClip[] clips = language == AppLanguage.English ? clipsEnglish : clipsGerman;
        if (clips == null)
            return null;

        foreach (AudioClip clip in clips)
        {
            if (clip != null && clip.name == id)
                return clip;
        }

        return null;
    }

    private AudioClip Join(List<AudioClip> clips)
    {
        if (clips.Count == 1)
            return clips[0];

        AudioClip first = clips[0];
        int gap = Mathf.RoundToInt(pauseBetweenLines * first.frequency) * first.channels;
        int total = 0;

        foreach (AudioClip clip in clips)
        {
            if (clip.frequency != first.frequency || clip.channels != first.channels)
                return first;
            total += clip.samples * clip.channels;
        }

        total += gap * (clips.Count - 1);
        var data = new float[total];
        int offset = 0;

        foreach (AudioClip clip in clips)
        {
            var part = new float[clip.samples * clip.channels];
            clip.GetData(part, 0);
            part.CopyTo(data, offset);
            offset += part.Length + gap;
        }

        if (lastJoined != null)
            Destroy(lastJoined);

        lastJoined = AudioClip.Create("RuleBasedReply", total / first.channels, first.channels, first.frequency, false);
        lastJoined.SetData(data, 0);
        return lastJoined;
    }

    private void OnDestroy()
    {
        if (lastJoined != null)
            Destroy(lastJoined);
    }
}
