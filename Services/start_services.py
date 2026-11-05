"""Local-only launcher. Keeps weights/venv outside the Unity Assets directory."""
import argparse, ctypes, json, os, subprocess, time, urllib.request, socket, msvcrt
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
STATE=ROOT/'UserSettings/SosedServices'
STATE.mkdir(parents=True,exist_ok=True)
home=Path.home()
p=argparse.ArgumentParser()
p.add_argument('--context',type=int,default=12288)
p.add_argument('--tts-only',action='store_true')
p.add_argument('--restart-tts',action='store_true')
p.add_argument('--ensure-running',action='store_true',help='Start only missing services; never restart existing ones')
a=p.parse_args()
if not 1024 <= a.context <= 32768: p.error('Context must be 1024..32768')
if a.ensure_running:
 # Serializes simultaneous launches (e.g. domain reload / multiple neighbors).
 startup_lock=open(STATE/'startup.lock','a+b')
 startup_lock.seek(0);startup_lock.write(b'0');startup_lock.flush()
 for attempt in range(150):
  try:
   startup_lock.seek(0);msvcrt.locking(startup_lock.fileno(),msvcrt.LK_NBLCK,1);break
  except OSError:time.sleep(1)
 else:raise RuntimeError('Another service launch did not finish in time')
config_path=STATE/'paths.json'
defaults={
 'llama':str(home/'llm/llama.cpp/llama-server.exe'),
 'model':str(home/'Documents/Codex/2026-11-04/new-chat/work/models/Qwen3.5-4B-Q4_K_M.gguf'),
 'tts_home':str(home/'Documents/Codex/2026-11-04/new-chat/outputs/qwen-tts'),
}
if not config_path.exists(): config_path.write_text(json.dumps(defaults,indent=2))
c=json.loads(config_path.read_text())
def health(port):
 try:
  with urllib.request.urlopen(f'http://127.0.0.1:{port}/'+('health' if port==8080 else 'status'),timeout=2) as r:return json.load(r)
 except Exception:return None

def listening(port):
 try:
  with socket.create_connection(('127.0.0.1',port),timeout=1):return True
 except OSError:return False
def stop_owned(pid_file, expected):
 if not pid_file.exists():return
 pid=int(pid_file.read_text())
 kernel=ctypes.WinDLL('kernel32',use_last_error=True)
 kernel.OpenProcess.restype=ctypes.c_void_p
 handle=kernel.OpenProcess(0x1000,False,pid)
 if not handle:return
 buf=ctypes.create_unicode_buffer(32768);length=ctypes.c_ulong(len(buf))
 try:
  if kernel.QueryFullProcessImageNameW(ctypes.c_void_p(handle),0,buf,ctypes.byref(length)):
   if Path(buf.value).resolve()==Path(expected).resolve():
    subprocess.run(['taskkill','/PID',str(pid),'/T','/F'],capture_output=True)
 finally:kernel.CloseHandle(ctypes.c_void_p(handle))
def start(command,name,env=None):
 with open(STATE/(name+'.log'),'w',encoding='utf-8') as log:
  child=subprocess.Popen(command,stdout=log,stderr=subprocess.STDOUT,cwd=ROOT,env=env,creationflags=subprocess.CREATE_NO_WINDOW)
 (STATE/(name+'.pid')).write_text(str(child.pid))
 return child
for path in [c['llama'],c['model'],str(Path(c['tts_home'])/'.venv/Scripts/python.exe')]:
 if not Path(path).exists():raise FileNotFoundError(f'{path}. Edit {config_path}')
if not a.tts_only and not (a.ensure_running and listening(8080)):
 stop_owned(STATE/'qwen.pid',c['llama'])
 if health(8080):raise RuntimeError('Port 8080 belongs to an existing server. Stop it first to apply context settings.')
 print('Starting Qwen with context',a.context,flush=True)
 start([c['llama'],'-m',c['model'],'-c',str(a.context),'-np','1','-ngl','99','--jinja','--host','127.0.0.1','--port','8080','--alias','Qwen3.5-4B'],'qwen')
gpu_ready=Path('D:/Codex/MySummerSosed-TTS-GPU/GPU_READY.txt')
tts_python=c.get('tts_python',gpu_ready.read_text().strip() if gpu_ready.exists() else str(Path(c['tts_home'])/'.venv/Scripts/python.exe'))
if a.restart_tts and not a.ensure_running:
 for exe in [str(Path(c['tts_home'])/'.venv/Scripts/python.exe'),tts_python,str(gpu_ready.parent/'venv/Scripts/python.exe')]: stop_owned(STATE/'tts.pid',exe)
if not health(8081) and not (a.ensure_running and listening(8081)):
 env=os.environ.copy();env['SOSED_TTS_HOME']=c['tts_home'];env['PYTHONUTF8']='1';env['SOSED_TTS_DEVICE']=c.get('tts_device','auto')
 start([tts_python,'-u',str(ROOT/'Services/speech_server.py')],'tts',env)
for _ in range(120):
 tts_status=health(8081)
 if health(8080) and tts_status and tts_status.get('model_ready',True):
  print('READY: Qwen :8080 / TTS :8081. Context:',a.context,flush=True);break
 time.sleep(1)
else:raise RuntimeError('Startup timed out; see logs in '+str(STATE))
