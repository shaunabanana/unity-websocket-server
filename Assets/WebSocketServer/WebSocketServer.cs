using System;
// Networking libs
using System.Net;
using System.Net.Sockets;
// For creating a thread
using System.Threading;
// For List & ConcurrentQueue
using System.Collections.Generic;
using System.Collections.Concurrent;
// Unity & Unity events
using UnityEngine;
using UnityEngine.Events;

namespace WebSocketServer {
    [System.Serializable]
    public class WebSocketOpenEvent : UnityEvent<WebSocketConnection> {}

    [System.Serializable]
    public class WebSocketMessageEvent : UnityEvent<WebSocketMessage> {}

    [System.Serializable]
    public class WebSocketCloseEvent : UnityEvent<WebSocketConnection> {}

    public class WebSocketServer : MonoBehaviour
    {
        // The tcpListenerThread listens for incoming WebSocket connections, then assigns the client to handler threads;
        private TcpListener tcpListener;
        private Thread tcpListenerThread;
        private List<Thread> workerThreads;
        private TcpClient connectedTcpClient;

        public ConcurrentQueue<WebSocketMessage> messages;

        public string address;
        public int port;
        public WebSocketOpenEvent onOpen;
        public WebSocketMessageEvent onMessage;
        public WebSocketCloseEvent onClose;

        void Awake() {
            if (onMessage == null) onMessage = new WebSocketMessageEvent();
        }

        void Start() {
            messages = new ConcurrentQueue<WebSocketMessage>();
            workerThreads = new List<Thread>();

            tcpListenerThread = new Thread (new ThreadStart(ListenForTcpConnection));
            tcpListenerThread.IsBackground = true;
            tcpListenerThread.Start();
        }

        void Update() {
            WebSocketMessage message;
            while (messages.TryDequeue(out message)) {
                onMessage.Invoke(message);
                this.OnMessage(message);
            }
        }

        private void ListenForTcpConnection () { 		
            try {
                // Create listener on <address>:<port>.
                tcpListener = new TcpListener(IPAddress.Parse(address), port);
                tcpListener.Start();
                Debug.Log("WebSocket server is listening for incoming connections.");
                while (true) {
                    // Accept a new client, then open a stream for reading and writing.
                    connectedTcpClient = tcpListener.AcceptTcpClient();
                    // Create a new connection
                    WebSocketConnection connection = new WebSocketConnection(connectedTcpClient, this);
                    // Establish connection
                    connection.Establish();
                    // // Start a new thread to handle the connection.
                    // Thread worker = new Thread (new ParameterizedThreadStart(HandleConnection));
                    // worker.IsBackground = true;
                    // worker.Start(connection);
                    // // Add it to the thread list. TODO: delete thread when disconnecting.
                    // workerThreads.Add(worker);
                }
            }
            catch (SocketException socketException) {
                Debug.Log("SocketException " + socketException.ToString());
            }
        }

        // private void HandleConnection (object parameter) {
        //     WebSocketConnection connection = (WebSocketConnection)parameter;
        //     while (true) {
        //         string message = ReceiveMessage(connection.client, connection.stream);
        //         connection.queue.Enqueue(message);
        //     }
        // }

        // private string ReceiveMessage(TcpClient client, NetworkStream stream) {
        //     // Wait for data to be available, then read the data.
        //     while (!stream.DataAvailable);
        //     Byte[] bytes = new Byte[client.Available];
        //     stream.Read(bytes, 0, bytes.Length);

        //     return WebSocketProtocol.DecodeMessage(bytes);
        // }

        public void OnOpen(WebSocketConnection connection) {}

        public void OnMessage(WebSocketMessage message) {}

        public void OnClose(WebSocketConnection connection) {}

        public void OnError(WebSocketConnection connection) {}


        // private void SendMessage() {
        //     if (connectedTcpClient == null) {
        //         return;
        //     }

        //     try {
        //         // Get a stream object for writing.
        //         NetworkStream stream = connectedTcpClient.GetStream();
        //         if (stream.CanWrite) {
        //             string serverMessage = "This is a message from your server.";
        //             // Convert string message to byte array.
        //             byte[] serverMessageAsByteArray = Encoding.ASCII.GetBytes(serverMessage);
        //             // Write byte array to socketConnection stream.
        //             stream.Write(serverMessageAsByteArray, 0, serverMessageAsByteArray.Length);
        //             Debug.Log("Server sent his message - should be received by client");
        //         }
        //     }
        //     catch (SocketException socketException) {
        //         Debug.Log("Socket exception: " + socketException);
        //     }
        // }
    }
}

