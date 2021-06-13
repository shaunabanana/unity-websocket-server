using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SomeDemoScript : MonoBehaviour
{
    public void onMessageReceived (string message) {
        Debug.Log("Received new message: " + message);
    }
}
