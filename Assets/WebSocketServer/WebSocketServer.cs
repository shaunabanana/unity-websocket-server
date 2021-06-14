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

// For parsing the client websocket requests
using System.Text; 
using System.Text.RegularExpressions;

namespace WebSocketServer {
    [System.Serializable]
    public class StringEvent : UnityEvent<string> {}

    public struct WebSocketConnection {
        public WebSocketConnection(TcpClient client, NetworkStream stream, ConcurrentQueue<string> queue)
        {
            this.client = client;
            this.stream = stream;
            this.queue = queue;
        }

        public TcpClient client { get; }
        public NetworkStream stream { get; }
        public ConcurrentQueue<string> queue { get; }
    }

    public class WebSocketServer : MonoBehaviour
    {
        // The tcpListenerThread listens for incoming WebSocket connections, then assigns the client to handler threads;
        private TcpListener tcpListener;
        private Thread tcpListenerThread;
        private List<Thread> workerThreads;
        private TcpClient connectedTcpClient;

        private ConcurrentQueue<string> messages;

        public string address;
        public int port;
        public StringEvent onMessage;

        void Awake() {
            if (onMessage == null) onMessage = new StringEvent();
        }

        void Start()
        {
            messages = new ConcurrentQueue<string>();
            workerThreads = new List<Thread>();

            tcpListenerThread = new Thread (new ThreadStart(ListenForTcpConnection));
            tcpListenerThread.IsBackground = true;
            tcpListenerThread.Start();
        }

        void Update()
        {
            string message;
            while (messages.TryDequeue(out message)) {
                onMessage.Invoke(message);
            }
        }

        private void ListenForTcpConnection () { 		
            try {
                // Create listener on <address>:<port>.
                tcpListener = new TcpListener(IPAddress.Parse(address), port);
                tcpListener.Start();
                Debug.Log("WebSocket server is listening for incoming connections.");
                while (true) {
                    connectedTcpClient = tcpListener.AcceptTcpClient();
                    NetworkStream stream = connectedTcpClient.GetStream();
                    WebSocketConnection connection = new WebSocketConnection(connectedTcpClient, stream, messages);
                    EstablishConnection(connection);
                    Thread worker = new Thread (new ParameterizedThreadStart(HandleConnection));
                    worker.IsBackground = true;
                    worker.Start(connection);
                    workerThreads.Add(worker);
                }
            }
            catch (SocketException socketException) {
                Debug.Log("SocketException " + socketException.ToString());
            }
        }

        private void EstablishConnection (WebSocketConnection connection) {
            // Wait for enough bytes to be available
            while (!connection.stream.DataAvailable);
            while(connection.client.Available < 3);
            // Translate bytes of request to a RequestHeader object
            Byte[] bytes = new Byte[connection.client.Available];
            connection.stream.Read(bytes, 0, bytes.Length);
            RequestHeader request = new RequestHeader(Encoding.UTF8.GetString(bytes));

            // Check if the request complies with WebSocket protocol.
            if (WebSocketProtocol.CheckConnectionHandshake(request)) {
                // If so, initiate the connection by sending a reply according to protocol.
                Byte[] response = WebSocketProtocol.CreateHandshakeReply(request);
                connection.stream.Write(response, 0, response.Length);
                Debug.Log("WebSocket client connected.");
            }
        }

        private void HandleConnection (object parameter) {
            WebSocketConnection connection = (WebSocketConnection)parameter;
            while (true) {
                string message = ReceiveMessage(connection.client, connection.stream);
                connection.queue.Enqueue(message);
            }
        }

        private string ReceiveMessage(TcpClient client, NetworkStream stream) {
            // Wait for data to be available, then read the data.
            while (!stream.DataAvailable);
            Byte[] bytes = new Byte[client.Available];
            stream.Read(bytes, 0, bytes.Length);

            return WebSocketProtocol.DecodeMessage(bytes);
        }

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

