import os
import json
import threading
import urllib.request
import uuid
import time
from types import SimpleNamespace
from http.server import ThreadingHTTPServer, BaseHTTPRequestHandler
from pathlib import Path

ROOT = Path(os.environ.get('SOSED_TTS_HOME', str(Path(__file__).resolve().parent)))
LOCK = threading.Lock()
MODEL = None
PROMPT = {}
WARM_READY = False

def load_model():
    global MODEL
    if MODEL is not None: return
    import torch
    from qwen_tts import Qwen3TTSModel
    torch.set_num_threads(8)
    device='cpu'
    if os.environ.get('SOSED_TTS_DEVICE','auto')!='cpu' and torch.cuda.is_available():
        index=next((i for i in range(torch.cuda.device_count()) if '9070' in torch.cuda.get_device_name(i)),0)
        device='cuda:'+str(index)
    print('Loading TTS on',device,flush=True)
    MODEL=Qwen3TTSModel.from_pretrained(str(ROOT/'models/base-0.6b'), device_map=device,
        dtype=torch.bfloat16 if device!='cpu' else torch.float32,
        attn_implementation='sdpa')
    if device!='cpu':
        # Convolution decoding has high HIP overhead on Windows.
        tokenizer=MODEL.model.speech_tokenizer
        tokenizer.model.to(device='cpu',dtype=torch.float32)
        tokenizer.device=torch.device('cpu')
        predictor=MODEL.model.talker.code_predictor
        predictor._sosed_reference_generate=predictor.generate
        @torch.no_grad()
        def greedy_residual(inputs_embeds, max_new_tokens, **kwargs):
            # Exactly 15 residual codes, no EOS/search. Avoid HF's generic
            # sampling/stopping machinery and its per-code CPU/GPU barriers.
            cache=None
            step=None
            token=None
            tokens=[]
            for i in range(max_new_tokens):
                output=predictor(inputs_embeds=inputs_embeds if i==0 else None,
                    input_ids=token,past_key_values=cache,generation_steps=step,
                    use_cache=True,output_hidden_states=False,return_dict=True)
                token=output.logits[:,-1,:].argmax(dim=-1,keepdim=True)
                tokens.append(token)
                cache=output.past_key_values
                step=output.generation_steps
            return SimpleNamespace(sequences=torch.cat(tokens,dim=-1))
        predictor.generate=greedy_residual
        print('Hybrid: talker GPU, codec CPU',flush=True)

def warm():
    global WARM_READY
    try:
        with LOCK:
            load_model()
            for language, name in [('Russian','grandpa-ru'),('English','reference-en')]:
                reference=ROOT/'voices/grandpa'/(name+'.wav')
                PROMPT[language]=MODEL.create_voice_clone_prompt(ref_audio=str(reference),ref_text=reference.with_suffix('.txt').read_text(encoding='utf-8'))
            WARM_READY=True
        print('TTS model warm (no speech generated)',flush=True)
    except Exception as error: print('TTS warmup failed:',error,flush=True)

def speak(text):
    global MODEL, PROMPT
    with LOCK:
        started=time.perf_counter()
        import torch
        import soundfile as sf
        from qwen_tts import Qwen3TTSModel
        voice = ROOT/'voices/grandpa'
        russian = any('\u0400' <= c <= '\u04ff' for c in text)
        language = 'Russian' if russian else 'English'
        reference = voice/('grandpa-ru.wav' if russian else 'reference-en.wav')
        transcript = reference.with_suffix('.txt')
        if not reference.exists():
            raise RuntimeError('Neighbor voice is still being generated. Please wait.')
        load_model()
        if language not in PROMPT:
            PROMPT[language] = MODEL.create_voice_clone_prompt(ref_audio=str(reference), ref_text=transcript.read_text(encoding='utf-8'))
        waves, sr = MODEL.generate_voice_clone(text=text[:1000], language=language, voice_clone_prompt=PROMPT[language], max_new_tokens=300, subtalker_dosample=False)
        name = 'speech-'+uuid.uuid4().hex+'.wav'
        (ROOT/'audio').mkdir(exist_ok=True)
        sf.write(str(ROOT/'audio'/name), waves[0], sr)
        print('TTS seconds:',round(time.perf_counter()-started,3),'audio seconds:',round(len(waves[0])/sr,3),flush=True)
        return '/audio/'+name

class Handler(BaseHTTPRequestHandler):
    def reply(self, data, content_type='application/json', status=200):
        if not isinstance(data, bytes): data = json.dumps(data, ensure_ascii=False).encode('utf-8')
        self.send_response(status)
        self.send_header('Content-Type',content_type)
        self.send_header('Content-Length',str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        if self.path == '/': return self.reply((ROOT/'voice_chat.html').read_bytes(),'text/html; charset=utf-8')
        if self.path == '/status': return self.reply({'voice_ready':WARM_READY,'model_ready':WARM_READY})
        if self.path.startswith('/audio/'):
            path = ROOT/'audio'/Path(self.path).name
            if path.exists() and path.suffix=='.wav': return self.reply(path.read_bytes(),'audio/wav')
        self.reply({'error':'Not found'},status=404)

    def do_POST(self):
        try:
            size = int(self.headers.get('Content-Length','0'))
            if size > 200000: raise ValueError('Request too large')
            body = json.loads(self.rfile.read(size))
            if self.path == '/speak': return self.reply({'url':speak(body['text'])})
            if self.path != '/chat': return self.reply({'error':'Not found'},status=404)
            system = 'Ты пожилой ворчливый сосед из деревни. Говоришь естественно, с сухим юмором. Помнишь о своей канистре и инструментах. Отвечай на языке собеседника, кратко: одно-два предложения. Не используй Markdown и сценические ремарки.'
            messages = [{'role':'system','content':system}] + body['messages'][-16:]
            data = json.dumps({'messages':messages,'stream':True,'max_tokens':180,'chat_template_kwargs':{'enable_thinking':False}}).encode()
            request = urllib.request.Request('http://127.0.0.1:8080/v1/chat/completions',data=data,headers={'Content-Type':'application/json'})
            with urllib.request.urlopen(request,timeout=120) as response:
                self.send_response(200)
                self.send_header('Content-Type','application/x-ndjson; charset=utf-8')
                self.end_headers()
                for line in response:
                    if not line.startswith(b'data: '): continue
                    raw = line[6:].strip()
                    if raw == b'[DONE]': break
                    event = json.loads(raw)
                    token = event.get('choices',[{}])[0].get('delta',{}).get('content','')
                    if token:
                        self.wfile.write((json.dumps({'text':token})+'\n').encode())
                        self.wfile.flush()
        except Exception as e:
            print('ERROR:', e, flush=True)
            try: self.reply({'error':str(e)},status=500)
            except Exception: pass

print('Voice chat: http://localhost:8081',flush=True)
threading.Thread(target=warm,daemon=True).start()
ThreadingHTTPServer(('127.0.0.1',8081),Handler).serve_forever()
