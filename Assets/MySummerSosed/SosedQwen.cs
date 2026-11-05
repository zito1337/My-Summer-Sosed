using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;

// Requests run through UnityWebRequest coroutines; no model inference on the game thread.
[RequireComponent(typeof(SosedVoice))]
public sealed class SosedQwen : MonoBehaviour
{
    [Header("Qwen connection")]
    public bool enabledInGame = true;
    public string endpoint = "http://127.0.0.1:8080";
    public string model = "Qwen3.5-4B";
    [Tooltip("Actual llama.cpp context. Apply with the Start / restart services button.")]
    [Range(1024, 32768)] public int contextWindow = 4096;
    public enum ThinkingLevel { None, Low, Mid, High, Max }
    public ThinkingLevel thinkingLevel = ThinkingLevel.None;
    [FormerlySerializedAs("thinking"), SerializeField, HideInInspector] bool legacyThinking;
    [SerializeField, HideInInspector] int thinkingSettingsVersion;
    public int ReasoningTokenBudget
    {
        get
        {
            switch(thinkingLevel)
            {
                case ThinkingLevel.Low: return 128;
                case ThinkingLevel.Mid: return 384;
                case ThinkingLevel.High: return 768;
                case ThinkingLevel.Max: return 1536;
                default: return 0;
            }
        }
    }
    [Range(24, 256)] public int maxReplyTokens = 80;
    [Range(0.1f, 1.5f)] public float temperature = 0.95f;
    [Range(0.1f, 1f)] public float topP = 0.9f;
    [Range(1, 100)] public int topK = 40;
    [Range(1f, 1.5f)] public float repeatPenalty = 1.1f;
    [Range(0, 12)] public int rememberedLines = 6;
    [Range(5, 180)] public int requestTimeout = 60;
    [Header("Character")]
    public bool allowSwearing = true;
    [TextArea(2, 5)] public string personality = "An elderly, cantankerous village neighbor. Protective of his house, tools and fuel can. Irritable, suspicious, dry humor. Speak naturally, never like an assistant.";
    [Header("Live status (read only in practice)")]
    public string status = "Waiting";
    [TextArea] public string lastLine;
    public float lastGenerationSeconds;

    readonly List<string> history = new List<string>();
    SosedVoice voice;
    bool generating, waitingForChase, previousEnglish;
    float nextAttempt;
    int revision;
    [Serializable] class Message { public string role; public string content; public Message(string r, string c) { role=r; content=c; } }
    [Serializable] class Template { public bool enable_thinking; }
    [Serializable] class Request
    {
        public string model; public Message[] messages; public bool stream;
        public int max_tokens, top_k, reasoning_budget_tokens; public float temperature, top_p, repeat_penalty;
        public Template chat_template_kwargs;
    }
    [Serializable] class ReplyMessage { public string content; }
    [Serializable] class Choice { public ReplyMessage message; }
    [Serializable] class Reply { public Choice[] choices; }

    void MigrateThinking()
    {
        if(thinkingSettingsVersion!=0) return;
        if(legacyThinking && thinkingLevel==ThinkingLevel.None) thinkingLevel=ThinkingLevel.Mid;
        thinkingSettingsVersion=1;
    }
    void OnValidate() { MigrateThinking(); }
    void Awake() { MigrateThinking(); voice=GetComponent<SosedVoice>(); previousEnglish=voice.english; }
    void Update()
    {
        if (voice.english != previousEnglish) { previousEnglish=voice.english; ClearPrepared(); }
        var movement=GetComponent<node_AIMovement>();
        if (waitingForChase && movement && !movement.chase)
        { waitingForChase=false; revision++; status="Chase ended; discarded pending speech"; voice.HideSubtitle(); }
        if (enabledInGame && !generating && waitingForChase && Time.unscaledTime >= nextAttempt)
            StartCoroutine(Generate());
    }
    public void Chase()
    {
        if (!enabledInGame) { voice.PlayFallback(); return; }
        revision++;
        waitingForChase=true;
        nextAttempt=0;
        status="Generating chase line...";
    }
    public void ClearPrepared()
    {
        revision++;
        waitingForChase=false;
        history.Clear();
    }
    IEnumerator Generate()
    {
        generating=true;
        int version=revision;
        bool english=voice.english;
        float started=Time.realtimeSinceStartup;
        status="Qwen generating text";
        string system=personality + (english ? " Speak only native English." : " Говори только по-русски.") +
            (allowSwearing ? " Coarse profanity is allowed when angry, vary its use." : " Do not swear.") +
            " Return ONLY one brief angry shout of 4 to 8 words. No narration, stage directions, quotes, markdown or explanations. Each line must differ from previous lines.";
        var messages=new List<Message> {new Message("system",system)};
        int memory=Mathf.Min(rememberedLines,history.Count);
        for(int i=history.Count-memory;i<history.Count;i++) messages.Add(new Message("assistant",history[i]));
        var movement=GetComponent<node_AIMovement>();
        string context="The player has trespassed in your house. You have just spotted them and started chasing them. Shout a new angry reaction.";
        if (movement && movement.chase) context+=" The pursuit is currently ongoing.";
        messages.Add(new Message("user",context));
        int budget=ReasoningTokenBudget;
        var payload=new Request {model=model,messages=messages.ToArray(),stream=false,
            max_tokens=Mathf.Max(24,maxReplyTokens)+budget+128, temperature=temperature,top_p=topP,
            top_k=topK,repeat_penalty=repeatPenalty,reasoning_budget_tokens=budget,
            chat_template_kwargs=new Template {enable_thinking=budget>0}};
        string line=null;
        for(int attempt=0;attempt<(budget>0?2:1);attempt++)
        {
          if(attempt==1)
          {
            status="Thinking produced no final line; retrying without thinking";
            payload.reasoning_budget_tokens=0;
            payload.chat_template_kwargs.enable_thinking=false;
            payload.max_tokens=Mathf.Max(24,maxReplyTokens)+128;
          }
          using(var request=new UnityWebRequest(endpoint.TrimEnd('/')+"/v1/chat/completions","POST"))
          {
            request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload)));
            request.downloadHandler=new DownloadHandlerBuffer(); request.SetRequestHeader("Content-Type","application/json");
            request.timeout=requestTimeout;
            yield return request.SendWebRequest();
            if(request.result==UnityWebRequest.Result.Success)
            {
                Reply reply=null;
                try {reply=JsonUtility.FromJson<Reply>(request.downloadHandler.text);} catch(Exception e) {status=e.Message;}
                if(reply!=null && reply.choices!=null && reply.choices.Length>0 && reply.choices[0].message!=null)
                    line=reply.choices[0].message.content;
            }
            else { status="Qwen unavailable: "+request.error; break; }
          }
          if(version!=revision || !enabledInGame) {generating=false; yield break;}
          line=ExtractSpokenLine(line);
          if(!string.IsNullOrWhiteSpace(line)) break;
        }
        if(version!=revision || !enabledInGame) {generating=false; yield break;}
        if(string.IsNullOrWhiteSpace(line)) {Fail("No spoken reply. Check server or increase token budget.");yield break;}
        if(string.IsNullOrWhiteSpace(line) || line.Contains("<think>") || line.Length>400 || history.Contains(line))
        { Fail("Rejected empty, repeated or overly long line; retrying.");yield break; }
        lastLine=line; lastGenerationSeconds=Time.realtimeSinceStartup-started;
        history.Add(line); while(history.Count>12) history.RemoveAt(0);
        Debug.Log("[Sosed Qwen] "+line,this);
        status="TTS generating voice";
        if(waitingForChase) voice.ShowSubtitle(line);
        AudioClip clip=null;
        yield return voice.FetchSpeech(line, result=>clip=result);
        if(version!=revision || !enabledInGame) {if(clip) Destroy(clip); generating=false;yield break;}
        if(!clip) {Fail("TTS failed; text is visible in Last Line / Console.");yield break;}
        if(waitingForChase)
        {waitingForChase=false;voice.PlayGenerated(clip,line);status="Speaking new Qwen line";}
        else Destroy(clip);
        generating=false;
    }
    public static string ExtractSpokenLine(string content)
    {
        if(string.IsNullOrWhiteSpace(content)) return null;
        int end=content.LastIndexOf("</think>",StringComparison.Ordinal);
        if(end>=0) content=content.Substring(end+8);
        if(content.Contains("<think>")) return null;
        return string.IsNullOrWhiteSpace(content)?null:content.Trim();
    }
    void Fail(string message)
    {
        status=message; generating=false;nextAttempt=Time.unscaledTime+10;
        if(waitingForChase) {waitingForChase=false;voice.PlayFallback();}
        Debug.LogWarning("[Sosed Qwen] "+message,this);
    }
    void OnDestroy() {ClearPrepared();}
}
