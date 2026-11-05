"""Install official AMD ROCm PyTorch separately from the working CPU runtime."""
import os, subprocess, time, venv
from pathlib import Path
root=Path(__file__).resolve().parents[1]
runtime=Path('D:/Codex/MySummerSosed-TTS-GPU')
runtime.mkdir(parents=True,exist_ok=True)
env=runtime/'venv'
if not (env/'Scripts/python.exe').exists():venv.create(env,with_pip=True)
python=str(env/'Scripts/python.exe')
base='https://repo.radeon.com/rocm/windows/rocm-rel-7.2.1/'
wheels=['rocm_sdk_core-7.2.1-py3-none-win_amd64.whl','rocm_sdk_devel-7.2.1-py3-none-win_amd64.whl','rocm_sdk_libraries_custom-7.2.1-py3-none-win_amd64.whl','rocm-7.2.1.tar.gz','torch-2.9.1%2Brocm7.2.1-cp312-cp312-win_amd64.whl','torchaudio-2.9.1%2Brocm7.2.1-cp312-cp312-win_amd64.whl']
downloads=runtime/'downloads';downloads.mkdir(exist_ok=True)
for wheel in wheels:
 dest=downloads/wheel.replace('%2B','+')
 done=dest.with_suffix(dest.suffix+'.complete')
 if not done.exists():
  while True:
   result=subprocess.run(['curl.exe','-L','--fail','--retry','5','--retry-all-errors','-C','-','-o',str(dest),base+wheel])
   if result.returncode==0:done.write_text('download complete');break
   print('Download interrupted; retrying in 5 seconds',flush=True);time.sleep(5)
subprocess.run([python,'-m','pip','install','--retries','20','--timeout','120',*[str(downloads/w.replace('%2B','+')) for w in wheels]],check=True)
config=root/'UserSettings/SosedServices/paths.json'
import json
c=json.loads(config.read_text())
cpu_site=Path(c['tts_home'])/'.venv/Lib/site-packages'
(env/'Lib/site-packages/sosed_cpu_dependencies.pth').write_text(str(cpu_site)+'\n')
subprocess.run([python,'-c','import torch; print(torch.__version__); assert torch.cuda.is_available(), "AMD GPU unavailable"; print(torch.cuda.get_device_name(0)); print((torch.ones(8,device="cuda")*2).cpu())'],check=True)
(runtime/'GPU_READY.txt').write_text(python)
print('GPU RUNTIME READY:',python,flush=True)
