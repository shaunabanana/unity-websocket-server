using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using WebSocketServer;

public class SomeDemoScript : MonoBehaviour
{
    public void onMessageReceived (WebSocketMessage message) {
        Debug.Log("Received new message: " + message.data);
    }
}
