using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// The chase cue is cached, so it is instantaneous and works without Python.
// Speak() can synthesize arbitrary lines through the local Qwen TTS service.
public sealed class SosedVoice : MonoBehaviour
{
    public bool english;
    public float cooldown = 15f;
    public string serverUrl = "http://127.0.0.1:8081";
    private AudioSource source;
    private AudioClip ru, en, generated;
    private float nextChase;
    private bool busy;

    public static SosedVoice For(GameObject neighbor)
    {
        var voice = neighbor.GetComponent<SosedVoice>();
        return voice != null ? voice : neighbor.AddComponent<SosedVoice>();
    }

    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.minDistance = 3f;
        source.maxDistance = 35f;
        source.rolloffMode = AudioRolloffMode.Linear;
        ru = Resources.Load<AudioClip>("SosedVoice/chase-ru");
        en = Resources.Load<AudioClip>("SosedVoice/chase-en");
    }

    public void OnChaseStarted()
    {
        if (Time.time < nextChase) return;
        var clip = english ? en : ru;
        if (clip == null) { Debug.LogWarning("Sosed chase voice is missing.", this); return; }
        nextChase = Time.time + cooldown;
        source.Stop();
        source.clip = clip;
        source.Play();
        Debug.Log("[My Summer Sosed] Chase voice: " + (english ? "EN" : "RU"), this);
    }

    [Serializable] private class SpeechRequest { public string text; }
    [Serializable] private class SpeechReply { public string url; }

    public void Speak(string text)
    {
        if (!busy && !string.IsNullOrWhiteSpace(text)) StartCoroutine(Synthesize(text));
    }

    private IEnumerator Synthesize(string text)
    {
        busy = true;
        using (var request = new UnityWebRequest(serverUrl + "/speak", "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new SpeechRequest { text = text })));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 180;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            { Debug.LogWarning("Sosed TTS: " + request.error, this); busy = false; yield break; }
            var reply = JsonUtility.FromJson<SpeechReply>(request.downloadHandler.text);
            if (reply == null || string.IsNullOrEmpty(reply.url) || !reply.url.StartsWith("/audio/"))
            { busy = false; yield break; }
            using (var audio = UnityWebRequestMultimedia.GetAudioClip(serverUrl + reply.url, AudioType.WAV))
            {
                audio.timeout = 30;
                yield return audio.SendWebRequest();
                if (audio.result == UnityWebRequest.Result.Success)
                {
                    source.Stop();
                    if (generated != null) Destroy(generated);
                    generated = DownloadHandlerAudioClip.GetContent(audio);
                    source.clip = generated;
                    source.Play();
                }
                else Debug.LogWarning("Sosed audio: " + audio.error, this);
            }
        }
        busy = false;
    }

    void OnDestroy() { if (generated != null) Destroy(generated); }
}
