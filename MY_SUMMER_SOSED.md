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

`SosedVoice.For(neighbor.gameObject).Speak(text)` requests arbitrary speech
from the local Qwen3-TTS service at http://127.0.0.1:8081. Calls are asynchronous
and do not stop the game. The service currently runs on CPU: expect tens of
seconds of synthesis delay. This is not yet suitable for instant chase reactions.
The menu `Test live TTS (Play mode)` sends a sample request.

On this machine the installed service is started with:
`C:\Users\kleii\Documents\Codex\2026-11-04\new-chat\outputs\qwen-tts\start-voice-chat.cmd`

The Python service and installed model weights remain outside the Unity project.
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
