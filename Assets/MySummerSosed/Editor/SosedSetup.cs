using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class SosedSetup
{
    static SosedSetup()
    {
        EditorApplication.delayCall += Configure;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Configure;
        };
    }
    static void Configure()
    {
        PlayerSettings.productName = "My Summer Sosed";
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var neighbor = Object.FindObjectOfType<node_AIMovement>();
        if (neighbor == null || neighbor.GetComponent<SosedVoice>() != null) return;
        var scene = neighbor.gameObject.scene;
        bool wasDirty = scene.isDirty;
        Undo.AddComponent<SosedVoice>(neighbor.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!wasDirty && !string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
        Debug.Log("[My Summer Sosed] Voice settings attached to " + neighbor.name, neighbor);
    }

    [MenuItem("My Summer Sosed/Open game world")]
    public static void OpenWorld()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene("Assets/1_Scenes/World/World.unity");
    }

    [MenuItem("My Summer Sosed/Test chase voice (Play mode)")]
    public static void TestChase()
    {
        var neighbor = Object.FindObjectOfType<node_AIMovement>();
        if (!Application.isPlaying || neighbor == null)
        { Debug.LogWarning("Enter Play mode in World first."); return; }
        SosedVoice.For(neighbor.gameObject).OnChaseStarted();
    }

    [MenuItem("My Summer Sosed/Test live TTS (Play mode)")]
    public static void TestLiveTts()
    {
        var neighbor = Object.FindObjectOfType<node_AIMovement>();
        if (!Application.isPlaying || neighbor == null)
        { Debug.LogWarning("Enter Play mode in World first."); return; }
        SosedVoice.For(neighbor.gameObject).Speak("Эй, сосед! Ты опять за моей канистрой пришёл?");
    }
}
