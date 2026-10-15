using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class InGameDescriptionLayoutTests
{
    private GameObject hud;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    [TearDown] public void TearDown() { if (hud != null) UnityEngine.Object.DestroyImmediate(hud); }

    [TestCase(YutResult.Do, YutResult.Do, false, "")]
    [TestCase(YutResult.Yut, YutResult.Yut, true, "한 번 더")]
    [TestCase(YutResult.Yut, YutResult.Yut, false, "사냥꾼\n추가 던지기 없음")]
    [TestCase(YutResult.Geol, YutResult.Do, false, "도박사\n추가 던지기 없음")]
    public void ResultDetailShowsEffectsWithoutMoveDistance(YutResult result, YutResult original, bool extra, string expected)
    {
        var outcome = new YutThrowOutcome(new bool[4], result, original);
        object text = RuntimeType("YutResultRevealView").GetMethod("EffectDescription", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { outcome, extra });
        Assert.That(text, Is.EqualTo(expected));
    }

    [Test]
    public void TooltipGrowsForFiveLinesAndShrinksForShortDescription()
    {
        hud = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/GameHudPanel_3Col.prefab"));
        Type type = RuntimeType("PlayerStatusView");
        Component view = hud.GetComponent(type) ?? hud.AddComponent(type);
        RectTransform tooltip = hud.GetComponentsInChildren<RectTransform>(true).First(rect => rect.name == "AugmentTooltip");
        Component name = tooltip.Find("Name").GetComponents<Component>().First(component => component != null && component.GetType().Name == "TextMeshProUGUI");
        Component description = tooltip.Find("Description").GetComponents<Component>().First(component => component != null && component.GetType().Name == "TextMeshProUGUI");
        type.GetField("augmentTooltip", Private).SetValue(view, tooltip);
        type.GetField("augmentTooltipName", Private).SetValue(view, name);
        type.GetField("augmentTooltipDescription", Private).SetValue(view, description);
        name.GetType().GetProperty("text").SetValue(name, "긴 설명 증강");
        description.GetType().GetProperty("text").SetValue(description, "첫째 줄\n둘째 줄\n셋째 줄\n넷째 줄\n다섯째 줄");
        type.GetMethod("ResizeAugmentTooltip", Private).Invoke(view, null);
        Assert.That(tooltip.rect.height, Is.GreaterThan(100f));
        RectTransform descRect = (RectTransform)description.transform;
        RectTransform nameRect = (RectTransform)name.transform;
        Assert.That(-descRect.anchoredPosition.y + descRect.rect.height + 12f, Is.LessThanOrEqualTo(tooltip.rect.height + 0.1f));
        Assert.That(-descRect.anchoredPosition.y, Is.GreaterThanOrEqualTo(-nameRect.anchoredPosition.y + nameRect.rect.height + 6f));
        description.GetType().GetProperty("text").SetValue(description, "짧은 설명");
        type.GetMethod("ResizeAugmentTooltip", Private).Invoke(view, null);
        Assert.That(tooltip.rect.height, Is.EqualTo(100f).Within(0.1f));
    }
}
