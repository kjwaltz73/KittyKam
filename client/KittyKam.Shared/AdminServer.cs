using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using KittyKamHost;

namespace KittyKam.Shared
{
    public sealed class AdminServer
    {
        private Socket _listener;
        private bool _running;
        private readonly object _sync = new object();
        private readonly IStatusProvider _statusProvider;
        private readonly string _pageTitle;

        public AdminServer(IStatusProvider statusProvider, string pageTitle = "Kitty Kam Admin")
        {
            _statusProvider = statusProvider ?? throw new ArgumentNullException(nameof(statusProvider));
            _pageTitle = pageTitle;
        }

        public void Start(int port)
        {
            _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listener.Bind(new IPEndPoint(IPAddress.Any, port));
            _listener.Listen(4);

            lock (_sync) _running = true;
            new Thread(ListenLoop).Start();

            Console.WriteLine($"AdminServer listening on port {port}");
        }

        public void Stop()
        {
            lock (_sync) _running = false;
            try { _listener?.Close(); } catch { /* ignore */ }
        }

        private void ListenLoop()
        {
            while (true)
            {
                bool running;
                lock (_sync) running = _running;
                if (!running) break;

                Socket client = null;
                try
                {
                    client = _listener.Accept();
                    client.ReceiveTimeout = 5000;
                    client.SendTimeout = 5000;

                    var buf = new byte[2048];
                    int read = 0;
                    
                    try
                    {
                        read = client.Receive(buf, SocketFlags.None);
                    }
                    catch (SocketException sockEx)
                    {
                        try { client.Close(); } catch { /* ignore */ }
                        continue;
                    }

                    if (read <= 0) 
                    { 
                        try { client.Close(); } catch { /* ignore */ }
                        continue; 
                    }

                    var req = Encoding.UTF8.GetString(buf, 0, read);
                    var firstLineEnd = req.IndexOf("\r\n");
                    var firstLine = firstLineEnd > 0 ? req.Substring(0, firstLineEnd) : req;
                    var parts = firstLine.Split(' ');
                    var method = parts.Length > 0 ? parts[0] : "GET";
                    var path = parts.Length > 1 ? parts[1] : "/";

                    if (method == "GET" && path == "/")
                    {
                        var body = Encoding.UTF8.GetBytes(AdminPageHtml());
                        WriteHttp(client, 200, "OK", "text/html; charset=utf-8", body);
                    }
                    else if (method == "GET" && path == "/status")
                    {
                        try
                        {
                            var ip = NetworkInfo.GetIpAddress();
                            var version = _statusProvider.GetVersion();
                            var extraJson = _statusProvider.GetStatusJson();
                            var json = $"{{\"version\":\"{version}\",\"ip\":\"{ip}\",{extraJson}}}";
                            var body = Encoding.UTF8.GetBytes(json);
                            WriteHttp(client, 200, "OK", "application/json", body);
                        }
                        catch (Exception statusEx)
                        {
                            Console.WriteLine("Status endpoint error: " + statusEx.Message);
                            var errorMsg = statusEx.Message;
                            var sb = new StringBuilder();
                            for (int i = 0; i < errorMsg.Length; i++)
                            {
                                var c = errorMsg[i];
                                if (c == '"') sb.Append("\\\"");
                                else sb.Append(c);
                            }
                            var errorJson = "{\"error\":\"" + sb.ToString() + "\"}";
                            var body = Encoding.UTF8.GetBytes(errorJson);
                            WriteHttp(client, 500, "Internal Server Error", "application/json", body);
                        }
                    }
                    else if (method == "OPTIONS")
                    {
                        WriteHttp(client, 204, "No Content", "text/plain; charset=utf-8", new byte[0]);
                    }
                    else
                    {
                        var body = Encoding.UTF8.GetBytes("Not Found");
                        WriteHttp(client, 404, "Not Found", "text/plain; charset=utf-8", body);
                    }

                    try { client.Close(); } catch { /* ignore */ }
                }
                catch (SocketException sockEx)
                {
                    if (client != null)
                    {
                        try { client.Close(); } catch { /* ignore */ }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("AdminServer error: " + ex.Message);
                    if (client != null)
                    {
                        try { client.Close(); } catch { /* ignore */ }
                    }
                }
            }
        }

        private void WriteHttp(Socket client, int statusCode, string statusText, string contentType, byte[] body)
        {
            var date = DateTime.UtcNow.ToString("R");
            var header = $"HTTP/1.1 {statusCode} {statusText}\r\n" +
                         $"Date: {date}\r\n" +
                         $"Server: KittyKamAdmin/1.0\r\n" +
                         $"Content-Type: {contentType}\r\n" +
                         $"Content-Length: {body.Length}\r\n" +
                         $"Connection: close\r\n" +
                         $"Access-Control-Allow-Origin: *\r\n" +
                         $"Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n" +
                         $"Access-Control-Allow-Headers: Content-Type\r\n\r\n";

            var headerBytes = Encoding.UTF8.GetBytes(header);
            client.Send(headerBytes, SocketFlags.None);
            if (body != null && body.Length > 0) client.Send(body, SocketFlags.None);
        }

        private string AdminPageHtml() => @"<!doctype html>
<html>
<head>
  <meta charset=""utf-8"">
  <title>" + _pageTitle + @"</title>
  <style>
    body { font-family: system-ui, sans-serif; margin: 2rem; }
    .card { border: 1px solid #ddd; border-radius: 6px; padding: 1rem; margin-bottom: 1rem; }
    button { padding: .6rem 1rem; border-radius: 4px; border: 1px solid #888; cursor: pointer; }
    button.primary { background: #2d7ef7; color: white; border: none; }
    .muted { color: #666; font-size: .9rem; }
  </style>
</head>
<body>
  <h1>" + _pageTitle + @"</h1>
  <p class=""muted"">Device status and information</p>

  <section class=""card"">
    <h2>Status</h2>
    <div id=""status"">Loading…</div>
  </section>

  <script>
  async function loadStatus() {
    try {
      const res = await fetch('/status');
      const s = await res.json();
      let html = `<p><b>Version:</b> ${s.version}</p><p><b>IP:</b> ${s.ip}</p>`;
      
      // Display additional status fields dynamically
      for (const key in s) {
        if (key !== 'version' && key !== 'ip') {
          html += `<p><b>${key}:</b> ${s[key]}</p>`;
        }
      }
      
      document.getElementById('status').innerHTML = html;
    } catch(e) {
      document.getElementById('status').innerHTML = `<span style='color:#B00020'>Status error: ${e}</span>`;
    }
  }
  loadStatus();
  setInterval(loadStatus, 5000); // Refresh every 5 seconds
  </script>
</body>
</html>";
    }
}
