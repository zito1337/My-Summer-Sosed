# My Summer Sosed

Based on filiposin/AngryNeighbor. Unity 2021.3.45f2.

## Chase voice

Open `My Summer Sosed > Open game world` and enter Play mode. When the
neighbor starts pursuing the player, he speaks a cached Russian chase line.
It works offline without any model/server running. The dedicated AudioSource
uses 3D spatial audio and a 15-second cooldown. Set `SosedVoice.english` at
runtime to select English. Both clips are in `Assets/MySummerSosed/Resources/SosedVoice`.

The menu `My Summer Sosed > Test chase voice (Play mode)` previews the cue.

## Live generation

`NeighborBody > Sosed Qwen` generates one new line ONLY when the AI enters a
chase. No lines are generated at game startup or while idle. The response is
spoken through Qwen3-TTS using the approved Russian/English grandpa reference.
If the chase ends before synthesis finishes, the late audio is discarded.
Use short shouts (4–8 words) to reduce synthesis latency. A second chase requests
a different line; recent lines are supplied as context to discourage repetition.

The component exposes thinking mode/budget, context size, sampling parameters,
reply token limit, short memory, profanity and character instructions.
The inspector's Start / restart services button applies the actual llama.cpp
context size. Thinking is disabled by default because it adds latency.
`enabledInGame = false` restores the offline cached chase cue.

GPU TTS setup uses `Services/install_gpu_tts.py`. It installs official AMD ROCm
PyTorch separately under `D:/Codex/MySummerSosed-TTS-GPU`, reusing existing TTS
dependencies without replacing the working CPU environment. The launcher selects
the GPU environment only after its verification marker is written. Service logs
and machine-specific paths are stored under `UserSettings/SosedServices`.

The tested Windows Radeon path uses BF16 GPU inference for the talker and FP32
CPU decoding for the speech codec. Residual codes use a fixed-length greedy
decoder instead of the generic Hugging Face sampler (Qwen3-TTS 0.1.1,
Transformers 4.57.3). Warmup loads the model and voice reference embeddings,
without generating any utterance. A short end-to-end Qwen -> speech -> WAV
request measured 4.12 seconds on this machine; CPU synthesis alone previously
measured 10.24 seconds for the comparison line. Timings vary with text length;
this is not zero-latency streaming audio.

`SosedVoice.For(neighbor.gameObject).Speak(text)` requests arbitrary speech
from the local Qwen3-TTS service at http://127.0.0.1:8081. Calls are asynchronous
and do not stop the game. The tested GPU/CPU path still has several seconds
of synthesis delay; it is not instantaneous streaming playback.
The menu `Test live TTS (Play mode)` sends a sample request.

On this machine the installed service is started with:
`python Services/start_services.py --context 4096 --restart-tts`

The Python service source is in `Services`; installed model weights and runtimes remain outside the Unity project.
Do not commit the virtual environment or multi-GB model weights. No network
service is required for the cached chase cue. The existing LLM has not been
wired to gameplay decisions by this change.

## Voice references

`VoiceReferences/grandpa` preserves the approved English grandpa and its Russian
version, their exact transcripts, and both generated chase lines. The English
reference was created by Qwen3-TTS 1.7B VoiceDesign; Russian was cloned with
Qwen3-TTS 0.6B Base. The WAV files are the fixed references for future generation.

## Validation

Unity Editor Play-mode smoke check opened World, verified RU/EN resource clips,
called `node_AIMovement.pursuePlayer(true)`, and confirmed that `chase-ru` was
playing on the neighbor's AudioSource. No C# compilation errors were observed.
The check leaves Play mode after validation; it is triggered only by a local
`sosed-smoke.request` file (ignored by Git).
