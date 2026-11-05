using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public static class SosedServiceStartup
{
    [Serializable] class TtsHealth { public bool model_ready; public bool voice_ready; }
    static IEnumerator Check(string url, bool tts, Action<bool> result)
    {
        using(var request=UnityWebRequest.Get(url.TrimEnd('/')+(tts?"/status":"/health")))
        {
            request.timeout=2;
            yield return request.SendWebRequest();
            bool ready=request.result==UnityWebRequest.Result.Success;
            if(ready && tts)
            {
                TtsHealth health=null;
                try {health=JsonUtility.FromJson<TtsHealth>(request.downloadHandler.text);} catch(Exception) {}
                ready=health!=null && (health.model_ready || health.voice_ready);
            }
            result(ready);
        }
    }

    public static IEnumerator EnsureServers(int context, string qwenUrl, string ttsUrl,
        Action<string> status, Action<bool> completed)
    {
        bool qwen=false,tts=false;
        yield return Check(qwenUrl,false,ready=>qwen=ready);
        yield return Check(ttsUrl,true,ready=>tts=ready);
        if(qwen && tts) {status("Qwen + TTS ready");completed(true);yield break;}
        Uri qwenUri,ttsUri;
        if(!Uri.TryCreate(qwenUrl,UriKind.Absolute,out qwenUri) || !qwenUri.IsLoopback || qwenUri.Port!=8080 ||
           !Uri.TryCreate(ttsUrl,UriKind.Absolute,out ttsUri) || !ttsUri.IsLoopback || ttsUri.Port!=8081)
        {status("Autostart requires local ports 8080 / 8081");completed(false);yield break;}
        string script=Path.GetFullPath(Path.Combine(Application.dataPath,"../Services/start_services.py"));
        Process process=null;
        string error=null;
        try
        {
            if(!File.Exists(script)) throw new FileNotFoundException("Service launcher missing",script);
            var info=new ProcessStartInfo("python", "\""+script+"\" --ensure-running --context "+Mathf.Clamp(context,1024,32768))
            {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            process=new Process {StartInfo=info};
            process.OutputDataReceived+=(sender,args)=>{if(args.Data!=null) UnityEngine.Debug.Log("[Sosed services] "+args.Data);};
            process.ErrorDataReceived+=(sender,args)=>{if(args.Data!=null) UnityEngine.Debug.LogWarning("[Sosed services] "+args.Data);};
            process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();
        }
        catch(Exception exception) {error=exception.Message;}
        if(error!=null) {status(error);UnityEngine.Debug.LogWarning("[Sosed services] "+error);completed(false);yield break;}
        status("Starting missing servers / warming TTS...");
        float deadline=Time.realtimeSinceStartup+150f;
        while(Time.realtimeSinceStartup<deadline)
        {
            yield return Check(qwenUrl,false,ready=>qwen=ready);
            yield return Check(ttsUrl,true,ready=>tts=ready);
            if(qwen && tts) {process.Dispose();status("Qwen + TTS ready");completed(true);yield break;}
            if(process.HasExited && process.ExitCode!=0) break;
            yield return new WaitForSecondsRealtime(1f);
        }
        process.Dispose();status("Server startup failed; check service logs");completed(false);
    }
}
