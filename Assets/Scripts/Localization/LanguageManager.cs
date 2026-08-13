using System;
using UnityEngine;

public class LanguageManager : MonoBehaviour
{
    public static LanguageManager Instance { get; private set; }
    public static AppLanguage Current { get; private set; } = AppLanguage.German;
    public static event Action<AppLanguage> Changed;

    public static bool IsEnglish => Current == AppLanguage.English;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    public static void Set(AppLanguage language)
    {
        Current = language;

        if (SessionLogger.Instance != null)
            SessionLogger.Instance.SetLanguage(language);

        Changed?.Invoke(language);
    }
}
