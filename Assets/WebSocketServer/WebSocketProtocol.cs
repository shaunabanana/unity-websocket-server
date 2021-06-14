using UnityEngine;
// For String
using System;
// For dictionary
using System.Collections.Generic;
// For parsing the client websocket requests
using System.Text; 
using System.Text.RegularExpressions;

namespace WebSocketServer {

    class RequestHeader {

        static Regex head = new Regex("^(GET|POST|PUT|DELETE|OPTIONS) (.+) HTTP/([0-9.]+)", RegexOptions.Compiled);
        static Regex body = new Regex("([A-Za-z0-9-]+): ?([^\n^\r]+)", RegexOptions.Compiled);

        public string method = "";
        public string uri = "";
        public string version = "";
        public Dictionary<string, string> headers;
        
        public RequestHeader(string data) {
            headers = new Dictionary<string, string>();

            MatchCollection matches = head.Matches(data);
            foreach (Match match in matches) {
                method = match.Groups[1].Value.Trim();
                uri = match.Groups[2].Value.Trim();
                version = match.Groups[3].Value.Trim();
            }

            matches = body.Matches(data);
            foreach (Match match in matches) {
                headers.Add(match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim());
            }
        }
    }

    class WebSocketProtocol {

        public static bool CheckConnectionHandshake(RequestHeader request) {
            // The method must be GET.
            if (!String.Equals(request.method, "GET")) {
                Debug.Log("Request does not begin with GET.");
                return false;
            }
            // TODO: Version must be greater than "1.1".
            // Must have a Host.
            if (!request.headers.ContainsKey("Host")) {
                Debug.Log("Request does not have a Host.");
                return false;
            }
            // Must have a Upgrade: websocket
            if (!request.headers.ContainsKey("Upgrade") || !String.Equals(request.headers["Upgrade"], "websocket")) {
                Debug.Log("Request does not have Upgrade: websocket.");
                return false;
            }

            // Must have a Connection: Upgrade
            if (!request.headers.ContainsKey("Connection") || !String.Equals(request.headers["Connection"], "Upgrade")) {
                Debug.Log("Request does not have Connection: Upgrade.");
                return false;
            }

            // Must have a Sec-WebSocket-Key
            if (!request.headers.ContainsKey("Sec-WebSocket-Key")) {
                Debug.Log("Request does not have Sec-WebSocket-Key");
                return false;
            }

            // Must have a Sec-WebSocket-Key
            if (!request.headers.ContainsKey("Sec-WebSocket-Key")) {
                Debug.Log("Request does not have Sec-WebSocket-Key");
                return false;
            }

            // Must have a Sec-WebSocket-Version: 13
            if (!request.headers.ContainsKey("Sec-WebSocket-Version") || !String.Equals(request.headers["Sec-WebSocket-Version"], "13")) {
                Debug.Log("Request does not have Sec-WebSocket-Version: 13");
                return false;
            }

            return true;
        }

        public static Byte[] CreateHandshakeReply(RequestHeader request) {
            const string eol = "\r\n"; // HTTP/1.1 defines the sequence CR LF as the end-of-line marker

            Byte[] response = Encoding.UTF8.GetBytes("HTTP/1.1 101 Switching Protocols" + eol
                + "Connection: Upgrade" + eol
                + "Upgrade: websocket" + eol
                + "Sec-WebSocket-Accept: " + Convert.ToBase64String(
                    System.Security.Cryptography.SHA1.Create().ComputeHash(
                        Encoding.UTF8.GetBytes(
                            request.headers["Sec-WebSocket-Key"] + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
                        )
                    )
                ) + eol
                + eol);

            return response;
        }

        public static string DecodeMessage(byte[] bytes) {
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

    }

}