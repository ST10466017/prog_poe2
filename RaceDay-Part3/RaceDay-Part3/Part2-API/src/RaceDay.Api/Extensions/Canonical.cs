namespace RaceDay.Api.Extensions;

public static class Canonical
{
    /// <summary>Case-insensitive match against an allowed list; returns the canonical spelling.</summary>
    public static bool TryMatch(string? value, IEnumerable<string> allowed, out string canonical)
    {
        canonical = string.Empty;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var match = allowed.FirstOrDefault(a => string.Equals(a, value.Trim(), StringComparison.OrdinalIgnoreCase));
        if (match is null) return false;
        canonical = match;
        return true;
    }
}
