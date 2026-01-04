using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace KittyKamHost
{
    public sealed class AdminServer
    {
        private Socket _listener;
        private bool _running;
        private readonly object _sync = new object();

        public void Start(int port)
        {
            _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listener.Bind(new IPEndPoint(IPAddress.Any, port));
            _listener.Listen(4);

            lock (_sync) _running = true;
            new Thread(ListenLoop).Start();

            Console.WriteLine($"AdminServer listening on port {port}");
        }

        // Call this if you want to stop the server gracefully
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

                try
                {
                    using var client = _listener.Accept();
                    client.ReceiveTimeout = 5000;
                    client.SendTimeout = 5000;

                    var buf = new byte[2048];
                    var read = client.Receive(buf, SocketFlags.None);
                    if (read <= 0) { client.Close(); continue; }

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
                            var version = OtaHost.CurrentVersion ?? "unknown";
                            var lastCheck = OtaHost.LastCheckUtc.ToString("O");
                            var lastResult = OtaHost.LastResult ?? "none";
                            var json = $"{{\"version\":\"{version}\",\"ip\":\"{ip}\",\"lastCheck\":\"{lastCheck}\",\"lastResult\":\"{lastResult}\"}}";
                            var body = Encoding.UTF8.GetBytes(json);
                            WriteHttp(client, 200, "OK", "application/json", body);
                        }
                        catch (Exception statusEx)
                        {
                            Console.WriteLine("Status endpoint error: " + statusEx.Message);
                            var errorMsg = statusEx.Message;
                            // Manual string replace for nanoFramework compatibility
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
                    else if (method == "POST" && path == "/update")
                    {
                        // Sync call (nanoFramework doesn't expose Task/await on many targets)
                        OtaHost.CheckForUpdate(manual: true);
                        var msg = "Update check initiated: " + OtaHost.LastResult;
                        var body = Encoding.UTF8.GetBytes(msg);
                        WriteHttp(client, 200, "OK", "text/plain; charset=utf-8", body);
                    }

                    else if (method == "OPTIONS")
                    {
                        // Respond OK to any preflight; no body needed
                        WriteHttp(client, 204, "No Content", "text/plain; charset=utf-8", new byte[0]);
                    }
                    else
                    {
                        var body = Encoding.UTF8.GetBytes("Not Found");
                        WriteHttp(client, 404, "Not Found", "text/plain; charset=utf-8", body);
                    }

                    client.Close();
                }
                catch (Exception ex)
                {
                    // Keep the server alive; log if helpful
                    Console.WriteLine("AdminServer error: " + ex.Message);
                }
            }
        }


        private void WriteHttp(Socket client, int statusCode, string statusText, string contentType, byte[] body)
        {
            // Minimal standard headers for better browser compatibility
            var date = DateTime.UtcNow.ToString("R"); // RFC1123, e.g. "Sun, 04 Jan 2026 23:23:00 GMT"
            var header = $"HTTP/1.1 {statusCode} {statusText}\r\n" +
                         $"Date: {date}\r\n" +
                         $"Server: KittyKamAdmin/1.0\r\n" +
                         $"Content-Type: {contentType}\r\n" +
                         $"Content-Length: {body.Length}\r\n" +
                         $"Connection: close\r\n" +
                         // CORS: harmless on same-origin, necessary if you open the page from file:// or another host/port
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
  <title>Kitty Kam Admin</title>
  <style>
    body { font-family: system-ui, sans-serif; margin: 2rem; }
    .card { border: 1px solid #ddd; border-radius: 6px; padding: 1rem; margin-bottom: 1rem; }
    button { padding: .6rem 1rem; border-radius: 4px; border: 1px solid #888; cursor: pointer; }
    button.primary { background: #2d7ef7; color: white; border: none; }
    .muted { color: #666; font-size: .9rem; }
  </style>
</head>
<body>
  <h1>Kitty Kam Admin</h1>
  <p class=""muted"">Status and manual update trigger</p>

  <section class=""card"">
    <h2>Status</h2>
    <div id=""status"">Loading…</div>
    <button class=""primary"" id=""btnUpdate"">Check for update</button>
  </section>

  <script>
  async function loadStatus() {
    try {
      const res = await fetch('/status');
      const s = await res.json();
      document.getElementById('status').innerHTML =
        `<p><b>Version:</b> ${s.version}</p>
         <p><b>IP:</b> ${s.ip}</p>
         <p><b>Last check:</b> ${s.lastCheck} (${s.lastResult || 'n/a'})</p>`;
    } catch(e) {
      document.getElementById('status').innerHTML = `<span style='color:#B00020'>Status error: ${e}</span>`;
    }
  }
  document.getElementById('btnUpdate').onclick = async () => {
    const btn = document.getElementById('btnUpdate');
    btn.disabled = true; btn.textContent = 'Checking…';
    try {
      const res = await fetch('/update', { method:'POST' });
      const text = await res.text();
      await loadStatus();
      alert(text);
    } catch(e) {
      alert('Update trigger failed: ' + e);
    }
    btn.textContent = 'Check for update';
    btn.disabled = false;
  };
  loadStatus();
  </script>
</body>
</html>";
    }
}
