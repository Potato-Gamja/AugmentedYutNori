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
        if (!string.IsNullOrWhiteSpace(iconId))
        {
            Sprite resourceIcon = Resources.Load<Sprite>(ProfileIconCatalog.SpritePath(iconId));
            if (resourceIcon != null) return resourceIcon;
            string normalized = Normalize(iconId);
            if (icons != null)
            {
                foreach (Sprite icon in icons)
                {
                    if (icon != null && Normalize(icon.name) == normalized) return icon;
                }
            }
        }

        // 미설정·찾을 수 없는 아이콘은 팀과 무관하게 같은 물음표 이미지로 표시한다.
        return Resources.Load<Sprite>("ProfileIcons/profile_px_locked");
    }

    private static string Normalize(string value)
    {
        string normalized = value.Trim().ToLowerInvariant();
        if (normalized.StartsWith("profile_px_")) normalized = normalized.Substring("profile_px_".Length);
        if (normalized.StartsWith("profile_")) normalized = normalized.Substring("profile_".Length);
        return normalized.Replace("-", "_");
    }
}
