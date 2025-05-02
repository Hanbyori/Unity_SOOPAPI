using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using VoltstroStudios.UnityWebBrowser.Core;

public class APIConnecter : MonoBehaviour
{
    [Header("General")]
    public BaseUwbClientManager UWB;

    public static event Action<string, string> OnMessage;
    public static event Action<string, int> OnDonation;

    [SerializeField] private string id;
    [SerializeField] private string key;
    [SerializeField] private string redirectURL;
    private string requestURL = "https://openapi.sooplive.co.kr/";

    private WebBrowserClient UWBClient;
    private HttpListener listener;

    [Header("Info")]
    [SerializeField, ReadOnly] private string code;
    [SerializeField, ReadOnly] private string token;
    [SerializeField, ReadOnly] private string refreshToken;

    private float tokenTime;
    private bool isReady = false, isCode = false, isToken = false;

    private void Update()
    {
        if (isCode && !isToken) GetToken();

        if (Input.GetKeyDown(KeyCode.Alpha0)) RequestAuth(); // 로그인 버튼으로 분리
        if (Input.GetKeyDown(KeyCode.Alpha1)) LoadChatSDK(); // 채팅연결 버튼으로 분리
    }

    private void Start()
    {
        UWBClient = UWB.browserClient;

        UWBClient.OnClientInitialized += OnUWBInitialized;
        UWBClient.OnClientConnected += OnUWBConnected;

        UWBClient.RegisterJsMethod<string>("OnChatConnected", OnChatConnected);
        UWBClient.RegisterJsMethod<string>("OnChatError", OnChatError);
        UWBClient.RegisterJsMethod<ChatData>("OnMessageReceived", OnMessageReceived);
        UWBClient.RegisterJsMethod<DonationData>("OnDonation", OnDonationReceived);
    }

    private void OnDestroy()
    {
        if (listener != null && listener.IsListening)
        {
            listener.Stop();
            listener.Close();
            Debug.Log("HTTP 서버 종료");
        }
    }

    private void OnUWBInitialized()
    {
        Debug.Log("UWB 초기화");
    }

    private void OnUWBConnected()
    {
        isReady = true;

        Debug.Log("UWB 연결");
    }

    private void OnChatConnected(string response)
    {
        Debug.Log("Chat 연결 성공: " + response);
    }

    private void OnChatError(string error)
    {
        Debug.LogError("Chat 연결 실패 : " + error);
    }

    private void OnMessageReceived(ChatData data)
    {
        try
        {
            byte[] msgBytes = Convert.FromBase64String(data.message);
            byte[] nameBytes = Convert.FromBase64String(data.userNickname);
            string message = Encoding.UTF8.GetString(msgBytes);
            string nickname = Encoding.UTF8.GetString(nameBytes);

            OnMessage?.Invoke(nickname, message);
        }
        catch (Exception e)
        {
            Debug.LogError("디코딩 실패 : " + e.Message);
        }
    }

    private void OnDonationReceived(DonationData data)
    {
        try
        {
            byte[] bytes = Convert.FromBase64String(data.userNickname);
            string nickname = Encoding.UTF8.GetString(bytes);

            OnDonation?.Invoke(nickname, data.count);
        }
        catch (Exception e)
        {
            Debug.LogError("디코딩 실패 : " + e.Message);
        }
    }

    public void RequestAuth()
    {
        if (!isReady) return;

        listener = new HttpListener();
        listener.Prefixes.Add(redirectURL);
        listener.Start();
        listener.BeginGetContext(GetCode, listener);

        string authUrl = $"https://login.sooplive.co.kr/afreeca/login.php?szFrom=oAuth&request_uri={requestURL}auth/code?client_id={id}";
        Application.OpenURL(authUrl);
    }

    private void GetCode(IAsyncResult result)
    {
        if (isCode || listener == null || !listener.IsListening) return;

        var context = listener.EndGetContext(result);
        var request = context.Request;

        string code = request.QueryString["code"];

        Debug.Log($"Code 발행 성공 : {code}");

        listener.Stop();

        this.code = code;
        isCode = true;

        GetToken();
    }

    public void GetToken()
    {
        if (isToken) return;
        StartCoroutine(GetTokenCoroutine());
    }

    private IEnumerator GetTokenCoroutine()
    {
        string url = $"{requestURL}auth/token";

        Dictionary<string, string> formFields = new Dictionary<string, string>()
        {
            { "grant_type", "authorization_code" },
            { "client_id", id },
            { "client_secret", key },
            { "redirect_uri", redirectURL },
            { "code", code }
        };

        List<string> formBodyList = new List<string>();
        foreach (var pair in formFields)
        {
            string encodedKey = UnityWebRequest.EscapeURL(pair.Key);
            string encodedValue = UnityWebRequest.EscapeURL(pair.Value);
            formBodyList.Add($"{encodedKey}={encodedValue}");
        }
        string formBody = string.Join("&", formBodyList);

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(formBody);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();

        request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
        request.SetRequestHeader("Accept", "*/*");

        isToken = true;

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            var tokenData = JsonUtility.FromJson<TokenData>(request.downloadHandler.text);
            token = tokenData.access_token;
            refreshToken = tokenData.refresh_token;
            tokenTime = Time.time + tokenData.expires_in;

            Debug.Log($"Token 발행 성공 : {token}");

            GetStationInfo();
        }
        else
        {
            isCode = false;
            isToken = false;

            Debug.LogError($"Token 발행 실패 : {request.error}");
        }
    }

    public bool IsAccessTokenExpired()
    {
        return Time.time >= tokenTime;
    }

    public void GetStationInfo()
    {
        StartCoroutine(GetStationInfoCoroutine());
    }

    private IEnumerator GetStationInfoCoroutine()
    {
        string url = $"{requestURL}user/stationinfo";

        Dictionary<string, string> formFields = new Dictionary<string, string>()
        {
            { "access_token", token }
        };

        List<string> formBodyList = new List<string>();
        foreach (var pair in formFields)
        {
            string encodedKey = UnityWebRequest.EscapeURL(pair.Key);
            string encodedValue = UnityWebRequest.EscapeURL(pair.Value);
            formBodyList.Add($"{encodedKey}={encodedValue}");
        }
        string formBody = string.Join("&", formBodyList);

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(formBody);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();

        request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
        request.SetRequestHeader("Accept", "*/*");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log($"방송국 정보 : {request.downloadHandler.text}");
        }
        else
        {
            Debug.LogError($"방송국 정보 요청 실패 : {request.error}");
        }
    }

    public void LoadChatSDK()
    {
        string jsCode = @"
        var script = document.createElement('script');
        script.src = 'https://static.sooplive.co.kr/asset/app/chat-sdk/sooplive-chat-sdk.js';
        script.onload = function() {

            console.log('Chat SDK script loaded');

            const chatSDK = new window.SOOP.ChatSDK(" + "\"" + id + "\", \"" + key + "\"" + @");
            chatSDK.setAuth(" + "\"" + token + "\"" + @");
            chatSDK.connect().then((res) => {
                console.log(res);
                let encodedMessage = btoa(unescape(encodeURIComponent(res)));
                uwb.ExecuteJsMethod('OnChatConnected', encodedMessage);

                chatSDK.handleMessageReceived((action, message) => {
                    switch (action) {
                        case 'MESSAGE':
                            console.log('Message received :', message);
                            let chatNickname = btoa(unescape(encodeURIComponent(message.userNickname)));
                            let chatMessage = btoa(unescape(encodeURIComponent(message.message)));
                            let chatData = {
                                userNickname: chatNickname,
                                message: chatMessage
                            };
                            uwb.ExecuteJsMethod('OnMessageReceived', chatData);
                            break;
                        case 'BALLOON_GIFTED':
                            console.log('Balloon gifted : ', message);
                            let donationNickname = btoa(unescape(encodeURIComponent(message.userNickname)));
                            let donationData = {
                                userNickname: donationNickname,
                                count: message.count
                            };
                            uwb.ExecuteJsMethod('OnDonation', donationData);
                            break;
                        default:
                            break;
                    }
                });
            }).catch((error) => {
                console.log(error);
                let encodedMessage = btoa(unescape(encodeURIComponent(error)));
                uwb.ExecuteJsMethod('OnChatError', encodedMessage);
            });
        };

        script.onerror = function() {
            console.error('ChatSDK Load Fail');
        };

        document.head.appendChild(script);
    ";

        UWBClient.ExecuteJs(jsCode);
    }
}
