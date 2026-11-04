using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class SosedSmokeCheck
{
    static double began;
    static bool tested;
    static SosedSmokeCheck() { EditorApplication.update += Tick; }
    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!SessionState.GetBool("SosedSmokePending", false))
        {
            if (!File.Exists("sosed-smoke.request")) return;
            File.Move("sosed-smoke.request", "sosed-smoke.running");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.isDirty) { File.WriteAllText("sosed-smoke.result", "SKIPPED: current scene has unsaved edits"); return; }
            EditorSceneManager.OpenScene("Assets/1_Scenes/World/World.unity");
            SessionState.SetBool("SosedSmokePending", true);
            EditorApplication.isPlaying = true;
            return;
        }
        if (!EditorApplication.isPlaying) return;
        if (began == 0) began = EditorApplication.timeSinceStartup;
        if (EditorApplication.timeSinceStartup - began < 4 || tested) return;
        tested = true;
        try
        {
            var neighbor = Object.FindObjectOfType<node_AIMovement>();
            if (!neighbor) throw new System.Exception("Neighbor not found in World");
            if (!Resources.Load<AudioClip>("SosedVoice/chase-ru") || !Resources.Load<AudioClip>("SosedVoice/chase-en"))
                throw new System.Exception("RU/EN chase clip missing");
            neighbor.chase = false;
            neighbor.pursuePlayer(true);
            bool heard = false;
            foreach (var source in neighbor.GetComponents<AudioSource>())
                if (source.isPlaying && source.clip && source.clip.name == "chase-ru") heard = true;
            File.WriteAllText("sosed-smoke.result", heard ? "PASS: pursuePlayer(true) triggered chase-ru playback; RU+EN clips loaded" : "FAIL: chase voice not playing");
        }
        catch (System.Exception e) { File.WriteAllText("sosed-smoke.result", "FAIL: " + e); }
        SessionState.SetBool("SosedSmokePending", false);
        EditorApplication.isPlaying = false;
    }
}
