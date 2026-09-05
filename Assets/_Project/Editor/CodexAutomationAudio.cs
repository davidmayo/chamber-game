using UnityEditor;

// Mute the Editor's output only for bridge-owned play sessions. SessionState
// survives Play Mode domain reloads; standalone players never include this file.
[InitializeOnLoad]
public static class CodexAutomationAudio
{
    private const string ActiveKey = "ChamberGame.AutomationAudio.Active";
    private const string OriginalKey = "ChamberGame.AutomationAudio.OriginalMute";
    private const string TestsKey = "ChamberGame.AutomationAudio.Tests";
    public static bool Active => SessionState.GetBool(ActiveKey, false);

    static CodexAutomationAudio()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.quitting += Restore;
        if (Active) EditorUtility.audioMasterMute = true;
    }

    public static void Begin(bool tests = false)
    {
        if (!Active) SessionState.SetBool(OriginalKey, EditorUtility.audioMasterMute);
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(TestsKey, tests);
        EditorUtility.audioMasterMute = true;
    }

    public static void EndTests()
    {
        SessionState.SetBool(TestsKey, false);
        if (!EditorApplication.isPlayingOrWillChangePlaymode) Restore();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!Active) return;
        if (state == PlayModeStateChange.EnteredEditMode && !SessionState.GetBool(TestsKey, false)) Restore();
        else EditorUtility.audioMasterMute = true;
    }

    private static void Restore()
    {
        if (!Active) return;
        EditorUtility.audioMasterMute = SessionState.GetBool(OriginalKey, false);
        SessionState.EraseBool(ActiveKey);
        SessionState.EraseBool(OriginalKey);
        SessionState.EraseBool(TestsKey);
    }
}
