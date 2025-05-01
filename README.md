# Unity_SOOPAPI

해당 프로젝트는 UWB에 의존하고 있습니다.<br>
https://projects.voltstro.dev/UnityWebBrowser/latest/articles/user/setup/

(SOOP API 채팅(후원) 접속 방식이 Socket 통신에서 JavaScript 기반으로 변경되어, 미들웨어 없이 Unity 단일로 작동하기 위해 UWB를 사용합니다.)

### 연결 방법
```Assets\SOOPAPI\Prefabs``` 폴더에서 ```SOOPAPI.prefab```을 꺼내 배치<br>
인스펙터 창에 발급한 API 정보 ```ID```, ```Key(Secret)```, ```redirectURL``` 정보를 입력

절차를 시도하는 방법은 현재 다음과 같습니다.
```C#
private void Update()
{
    ...

    if (Input.GetKeyDown(KeyCode.Alpha0)) RequestAuth(); // 로그인 버튼으로 분리
    if (Input.GetKeyDown(KeyCode.Alpha1)) LoadChatSDK(); // 채팅연결 버튼으로 분리

    ...
}
```
<br>

### 이벤트
채팅 메세지와 별풍선 처리는 각각 ```ChatController```, ```DonationController```에서 관리됩니다.<br>
현재 채팅은 닉네임과 채팅메세지, 별풍선은 닉네임과 별풍선 갯수를 받습니다.

정보를 추가하거나, 새로운 이벤트를 구독하려면 [공식문서](https://developers.sooplive.co.kr/?szWork=chat_sdk&sub=documentation&part=events)를 참고하여 ```APIConnecter.cs```를 수정하거나, 새로운 스크립트를 작성하세요.

이벤트는 UWB를 통해 구독하며, 다음과 같이 동작합니다.
```Javascript
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
```
Unity에서 JS측의 메세지를 받으려면 ```RegisterJsMethod(string, Action)``` 메서드를 사용하세요.<br>
JS측에서 Unity로 메세지를 보내려면 ```uwb.ExecuteJsMethod(method, arguments)``` 메서드를 호출하면 됩니다.
```C#
UWBClient.RegisterJsMethod<ChatData>("OnMessageReceived", OnMessageReceived);
UWBClient.RegisterJsMethod<DonationData>("OnDonation", OnDonationReceived);

private void OnMessageReceived(ChatData data)
{
    OnMessage?.Invoke(data.nickname, data.message);
}

private void OnDonationReceived(DonationData data)
{
    OnDonation?.Invoke(data.nickname, data.count);
}
```
