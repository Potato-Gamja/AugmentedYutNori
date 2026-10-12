using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class PlayerDisplayInfo
{
    public string playerId;
    public string nickname;
    public int team;
    public string iconId;
}

public static class PlayerProfileIconResolver
{
    public static Sprite Resolve(IReadOnlyList<Sprite> icons, string iconId, int team)
    {
        if (icons == null || icons.Count == 0) return null;

        if (!string.IsNullOrWhiteSpace(iconId))
        {
            string normalized = Normalize(iconId);
            foreach (Sprite icon in icons)
            {
                if (icon != null && Normalize(icon.name) == normalized) return icon;
            }
        }

        return icons[Mathf.Abs(team) % icons.Count];
    }

    private static string Normalize(string value)
    {
        string normalized = value.Trim().ToLowerInvariant();
        if (normalized.StartsWith("profile_px_")) normalized = normalized.Substring("profile_px_".Length);
        if (normalized.StartsWith("profile_")) normalized = normalized.Substring("profile_".Length);
        return normalized.Replace("-", "_");
    }
}
