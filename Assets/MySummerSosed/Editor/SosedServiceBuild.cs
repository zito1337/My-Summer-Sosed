using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Ship only service source; installed models/venvs remain machine-specific.
public sealed class SosedServiceBuild : IPostprocessBuildWithReport
{
    public int callbackOrder {get{return 0;}}
    public void OnPostprocessBuild(BuildReport report)
    {
        if(report.summary.platform!=UnityEditor.BuildTarget.StandaloneWindows64 &&
           report.summary.platform!=UnityEditor.BuildTarget.StandaloneWindows) return;
        string source=Path.GetFullPath(Path.Combine(Application.dataPath,"../Services"));
        string destination=Path.Combine(Path.GetDirectoryName(report.summary.outputPath),"Services");
        Directory.CreateDirectory(destination);
        foreach(string file in Directory.GetFiles(source,"*.py"))
            File.Copy(file,Path.Combine(destination,Path.GetFileName(file)),true);
    }
}
