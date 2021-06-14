using System;
// Networking libs
using System.Net;
using System.Net.Sockets;
// For parsing the client websocket requests
using System.Text; 
using System.Text.RegularExpressions;
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
            // Translate bytes of request to string
            Byte[] bytes = new Byte[connection.client.Available];
            connection.stream.Read(bytes, 0, bytes.Length);
            String data = Encoding.UTF8.GetString(bytes);

            // Check if the input has a "GET" header. If so, initiate the connection.
            if (Regex.IsMatch(data, "^GET")) {
                const string eol = "\r\n"; // HTTP/1.1 defines the sequence CR LF as the end-of-line marker

                Byte[] response = Encoding.UTF8.GetBytes("HTTP/1.1 101 Switching Protocols" + eol
                    + "Connection: Upgrade" + eol
                    + "Upgrade: websocket" + eol
                    + "Sec-WebSocket-Accept: " + Convert.ToBase64String(
                        System.Security.Cryptography.SHA1.Create().ComputeHash(
                            Encoding.UTF8.GetBytes(
                                new System.Text.RegularExpressions.Regex("Sec-WebSocket-Key: (.*)").Match(data).Groups[1].Value.Trim() + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
                            )
                        )
                    ) + eol
                    + eol);

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

            bool fin = (bytes[0] & 0b10000000) != 0,
                mask = (bytes[1] & 0b10000000) != 0; // must be true, "All messages from the client to the server have this bit set"

            int opcode = bytes[0] & 0b00001111, // expecting 1 - text message
                msglen = bytes[1] - 128, // & 0111 1111
                offset = 2;

            if (msglen == 126) {
                // was ToUInt16(bytes, offset) but the result is incorrect
                msglen = BitConverter.ToUInt16(new byte[] { bytes[3], bytes[2] }, 0);
                offset = 4;
            } else if (msglen == 127) {
                Debug.Log("TODO: msglen == 127, needs qword to store msglen");
                // i don't really know the byte order, please edit this
                // msglen = BitConverter.ToUInt64(new byte[] { bytes[5], bytes[4], bytes[3], bytes[2], bytes[9], bytes[8], bytes[7], bytes[6] }, 0);
                // offset = 10;
            }

            if (msglen == 0)
                Debug.Log("msglen == 0");
            else if (mask) {
                byte[] decoded = new byte[msglen];
                byte[] masks = new byte[4] { bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3] };
                offset += 4;

                for (int i = 0; i < msglen; ++i)
                    decoded[i] = (byte)(bytes[offset + i] ^ masks[i % 4]);

                string text = Encoding.UTF8.GetString(decoded);
                return text;
            } else {
                Debug.Log("mask bit not set");
            }
            return "";
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

