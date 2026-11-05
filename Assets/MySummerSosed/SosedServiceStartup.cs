using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public static class SosedServiceStartup
{
    static IEnumerator Check(string url, Action<bool> result)
    {
        using(var request=UnityWebRequest.Get(url.TrimEnd('/')+"/health"))
        {
            request.timeout=2;
            yield return request.SendWebRequest();
            bool ready=request.result==UnityWebRequest.Result.Success;
            result(ready);
        }
    }

    public static IEnumerator EnsureServers(int context, string qwenUrl,
        Action<string> status, Action<bool> completed)
    {
        bool qwen=false;
        yield return Check(qwenUrl,ready=>qwen=ready);
        if(qwen) {status("Qwen ready");completed(true);yield break;}
        Uri qwenUri;
        if(!Uri.TryCreate(qwenUrl,UriKind.Absolute,out qwenUri) || !qwenUri.IsLoopback || qwenUri.Port!=8080)
        {status("Autostart requires local port 8080");completed(false);yield break;}
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
        status("Starting Qwen server...");
        float deadline=Time.realtimeSinceStartup+150f;
        while(Time.realtimeSinceStartup<deadline)
        {
            yield return Check(qwenUrl,ready=>qwen=ready);
            if(qwen) {process.Dispose();status("Qwen ready");completed(true);yield break;}
            if(process.HasExited && process.ExitCode!=0) break;
            yield return new WaitForSecondsRealtime(1f);
        }
        process.Dispose();status("Server startup failed; check service logs");completed(false);
    }
}
