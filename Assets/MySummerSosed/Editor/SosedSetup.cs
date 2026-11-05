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
        if (neighbor == null) return;
        var scene = neighbor.gameObject.scene;
        bool wasDirty = scene.isDirty;
        bool changed = false;
        if (!neighbor.GetComponent<SosedQwen>()) { Undo.AddComponent<SosedQwen>(neighbor.gameObject); changed=true; }
        if (!changed) return;
        EditorSceneManager.MarkSceneDirty(scene);
        if (!wasDirty && !string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
        Debug.Log("[My Summer Sosed] Qwen settings attached to " + neighbor.name, neighbor);
    }

    [MenuItem("My Summer Sosed/Open game world")]
    public static void OpenWorld()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene("Assets/1_Scenes/World/World.unity");
    }

    [MenuItem("My Summer Sosed/Test Qwen reply (Play mode)")]
    public static void TestChase()
    {
        var neighbor = Object.FindObjectOfType<node_AIMovement>();
        if (!Application.isPlaying || neighbor == null)
        { Debug.LogWarning("Enter Play mode in World first."); return; }
        SosedQwen.For(neighbor.gameObject).Chase();
    }

}
