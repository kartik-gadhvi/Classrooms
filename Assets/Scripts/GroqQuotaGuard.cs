public static class GroqQuotaGuard
{
    public static bool IsQuotaOrRateLimit(string error)
    {
        if (string.IsNullOrEmpty(error))
            return false;

        string text = error.ToLowerInvariant();
        return text.Contains("insufficient_quota")
            || text.Contains("exceeded your current quota")
            || text.Contains("rate_limit")
            || text.Contains("tokens per day")
            || text.Contains("tokens per minute")
            || text.Contains("tpm")
            || text.Contains("429");
    }
}
