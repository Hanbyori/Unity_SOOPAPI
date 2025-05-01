using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChatController : MonoBehaviour
{
    private void OnEnable()
    {
        APIConnecter.OnMessage += ChatHandler;
    }

    private void OnDisable()
    {
        APIConnecter.OnMessage -= ChatHandler;
    }

    private void ChatHandler(string nickname, string message)
    {
        Debug.Log($"[채팅] {nickname} : {message}");
    }
}
