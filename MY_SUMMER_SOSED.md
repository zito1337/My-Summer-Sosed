# My Summer Sosed

Based on filiposin/AngryNeighbor. Unity 2021.3.45f2.

## Qwen dialogue

Open World and enter Play. `NeighborBody > Sosed Qwen` contains all settings.
A new complete phrase is generated only when entering a chase. The reply is
displayed immediately as a subtitle and in Console / Last Line. Nothing is
generated while idle or during startup. Enable English to switch languages.

Context is at least 12,288 tokens. Thinking presets: None / Low / Mid / High /
Max (0 / 128 / 384 / 768 / 1536 reasoning tokens). Sampling, memory, reply
length, profanity and character instructions are configurable. Incomplete
replies are retried once without reasoning.

## Model server

Auto Start Servers starts Qwen at http://127.0.0.1:8080 if missing. Existing
processes are preserved. Use the inspector restart button to apply context.
The model and llama.cpp remain outside the project. Machine-specific paths
are stored in UserSettings/SosedServices/paths.json.

Manual launch: `python Services/start_services.py --ensure-running --context 12288`.
Windows builds copy the Python launcher beside the executable. The target
machine still needs Python, llama.cpp and model weights installed.
