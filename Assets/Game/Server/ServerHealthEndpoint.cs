using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Game.Core;
using UnityEngine;

namespace Game.Server
{
    public static class ServerHealthEndpoint
    {
        private static TcpListener _listener;
        private static Thread _thread;
        private static DateTime _startUtc;
        private static bool _running;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeOnLoad()
        {
            Start();
        }

        public static void StartForSmoke()
        {
            Start();
        }

        private static void Start()
        {
            if (_running || !IsEnabled())
            {
                return;
            }

            var port = GetPort();
            _startUtc = DateTime.UtcNow;
            _listener = new TcpListener(IPAddress.Loopback, port);
            _listener.Start();
            _running = true;

            _thread = new Thread(RunLoop)
            {
                IsBackground = true,
                Name = "ServerHealthEndpoint"
            };
            _thread.Start();

            Application.quitting += Stop;
            Debug.Log($"ServerHealthEndpoint listening on 127.0.0.1:{port}");
        }

        private static void RunLoop()
        {
            while (_running)
            {
                try
                {
                    if (!_listener.Pending())
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    using (var client = _listener.AcceptTcpClient())
                    using (var stream = client.GetStream())
                    {
                        var buffer = new byte[512];
                        stream.Read(buffer, 0, buffer.Length);
                        var response = BuildResponse();
                        var bytes = Encoding.UTF8.GetBytes(response);
                        stream.Write(bytes, 0, bytes.Length);
                    }
                }
                catch
                {
                    Thread.Sleep(50);
                }
            }
        }

        private static string BuildResponse()
        {
            var uptime = (DateTime.UtcNow - _startUtc).TotalSeconds;
            var body = $"{{\"status\":\"ok\",\"uptime_s\":{uptime:0.0},\"build_version\":\"{BuildInfo.BuildVersion}\"}}";
            var headers = "HTTP/1.1 200 OK\r\n" +
                          "Content-Type: application/json\r\n" +
                          $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n" +
                          "Connection: close\r\n\r\n";
            return headers + body;
        }

        private static void Stop()
        {
            _running = false;
            try
            {
                _listener?.Stop();
            }
            catch
            {
            }
        }

        private static bool IsEnabled()
        {
            var enabled = Environment.GetEnvironmentVariable("SERVER_HEALTH_ENABLE");
            if (string.IsNullOrWhiteSpace(enabled))
            {
                return false;
            }

            return enabled == "1" || enabled.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private static int GetPort()
        {
            var value = Environment.GetEnvironmentVariable("SERVER_HEALTH_PORT");
            if (int.TryParse(value, out var port) && port > 0)
            {
                return port;
            }

            return 18080;
        }
    }
}
