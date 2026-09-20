using UnityEngine;

public static class GroqChatModel
{
    public const string Default = "openai/gpt-oss-20b";
    public const string Fallback = "openai/gpt-oss-120b";

    public static string Resolve(string requested)
    {
        if (string.IsNullOrWhiteSpace(requested) || IsUnavailableOnDeveloperPlan(requested))
        {
            if (!string.IsNullOrWhiteSpace(requested))
                Debug.LogWarning("[Groq] Model '" + requested + "' is not available on this API plan. Using " + Default + ".");
            return Default;
        }

        return requested.Trim();
    }

    public static string NextFallback(string current)
    {
        if (current != Default)
            return Default;
        if (current != Fallback)
            return Fallback;
        return Default;
    }

    public static bool IsModelNotFound(string error)
    {
        if (string.IsNullOrEmpty(error))
            return false;

        string text = error.ToLowerInvariant();
        return text.Contains("model_not_found")
            || text.Contains("does not exist")
            || text.Contains("do not have access");
    }

    private static bool IsUnavailableOnDeveloperPlan(string model)
    {
        string id = model.ToLowerInvariant();
        return id.Contains("llama-3.3-70b")
            || id.Contains("llama-3.1-8b")
            || id.Contains("llama3-70b")
            || id.Contains("llama3-8b")
            || id.Contains("mixtral");
    }
}
