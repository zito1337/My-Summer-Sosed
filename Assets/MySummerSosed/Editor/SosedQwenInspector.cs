using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SosedQwen))]
public class SosedQwenInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.LabelField("Thinking token budget", ((SosedQwen)target).ReasoningTokenBudget.ToString());
        EditorGUILayout.HelpBox("Generation starts ONLY on chase entry. Context Window requires a server restart. Other settings apply to the next request.", MessageType.Info);
        if (GUILayout.Button("Start / restart local Qwen (apply context)"))
        {
            var qwen=(SosedQwen)target;
            string script=Path.GetFullPath(Path.Combine(Application.dataPath,"../Services/start_services.py"));
            var info=new ProcessStartInfo("python", "\""+script+"\" --context "+qwen.contextWindow)
            {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            var process=new Process {StartInfo=info,EnableRaisingEvents=true};
            process.OutputDataReceived+=(sender,args)=>{if(args.Data!=null) UnityEngine.Debug.Log("[Sosed services] "+args.Data);};
            process.ErrorDataReceived+=(sender,args)=>{if(args.Data!=null) UnityEngine.Debug.LogWarning("[Sosed services] "+args.Data);};
            try {process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();}
            catch(System.Exception e){UnityEngine.Debug.LogError(e.Message);}
        }
        if (Application.isPlaying && GUILayout.Button("Generate / play a new chase line")) ((SosedQwen)target).Chase();
        if (Application.isPlaying && GUILayout.Button("Cancel pending line / clear memory")) ((SosedQwen)target).ClearPrepared();
    }
}
