using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;

public class SoundStartupTests
{
    private const string Backup = "Yutnori.SoundStartupTests.";
    private static readonly string[] Keys = { "audio.master", "audio.bgm", "audio.sfx" };
    private static Type Runtime(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(name)).First(t => t != null);

    private static void BackupPreferences()
    {
        // SessionState survives EnterPlayMode's domain reload; never overwrite the original values.
        if (SessionState.GetBool(Backup + "exists", false)) return;
        for (int i = 0; i < Keys.Length; i++)
        {
            SessionState.SetBool(Backup + Keys[i], PlayerPrefs.HasKey(Keys[i]));
            SessionState.SetFloat(Backup + Keys[i] + ".value", PlayerPrefs.GetFloat(Keys[i]));
        }
        SessionState.SetFloat(Backup + "listener", AudioListener.volume);
        SessionState.SetBool(Backup + "background", Application.runInBackground);
        SessionState.SetBool(Backup + "exists", true);
    }

    [UnityTearDown]
    public IEnumerator RestorePreferences()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        if (!SessionState.GetBool(Backup + "exists", false)) yield break;
        for (int i = 0; i < Keys.Length; i++)
        {
            if (SessionState.GetBool(Backup + Keys[i], false))
                PlayerPrefs.SetFloat(Keys[i], SessionState.GetFloat(Backup + Keys[i] + ".value", 1f));
            else PlayerPrefs.DeleteKey(Keys[i]);
        }
        PlayerPrefs.Save();
        Runtime("AudioSettings").GetMethod("Load", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        AudioListener.volume = SessionState.GetFloat(Backup + "listener", 1f);
        Application.runInBackground = SessionState.GetBool(Backup + "background", false);
        SessionState.SetBool(Backup + "exists", false);
    }

    private static void AssertVolumes(AudioMixer mixer, float master, float bgm, float sfx)
    {
        Assert.That(AudioListener.volume, Is.EqualTo(master).Within(0.001f), "saved Master");
        Assert.That(mixer.GetFloat("BGM", out float actualBgm), Is.True);
        Assert.That(mixer.GetFloat("SFX", out float actualSfx), Is.True);
        Assert.That(actualBgm, Is.EqualTo(bgm == 0 ? -80f : Mathf.Log10(bgm) * 20f).Within(0.01f), "saved BGM");
        Assert.That(actualSfx, Is.EqualTo(sfx == 0 ? -80f : Mathf.Log10(sfx) * 20f).Within(0.01f), "saved SFX");
    }

    [UnityTest]
    public IEnumerator SavedVolumesApplyAtStartupWithoutOpeningSettingsAndRemainAdjustable()
    {
        BackupPreferences();
        PlayerPrefs.SetFloat(Keys[0], 0.7f);
        PlayerPrefs.SetFloat(Keys[1], 0.25f);
        PlayerPrefs.SetFloat(Keys[2], 0.4f);
        PlayerPrefs.Save();
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        yield return null;

        var managerType = Runtime("SoundManager");
        var manager = (Component)managerType.GetProperty("Instance").GetValue(null);
        Assert.That(manager, Is.Not.Null, "automatic SoundManager bootstrap");
        var library = (ScriptableObject)managerType.GetField("library", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
        var mixer = (AudioMixer)library.GetType().GetProperty("Mixer").GetValue(library);
        AssertVolumes(mixer, 0.7f, 0.25f, 0.4f);

        var set = Runtime("AudioSettings").GetMethod("Set");
        set.Invoke(null, new object[] { 0f, 0f, 0f });
        yield return null;
        AssertVolumes(mixer, 0f, 0f, 0f);
        set.Invoke(null, new object[] { 1f, 1f, 1f });
        yield return null;
        AssertVolumes(mixer, 1f, 1f, 1f);
    }
}
