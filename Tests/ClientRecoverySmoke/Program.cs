using System.Reflection;
using OnlineChessGame;
using OnlineChessGame.Network;

internal static class Program
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern IntPtr GetLastActivePopup(IntPtr window);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object target, string field, object? value) => target.GetType().GetField(field, Hidden)!.SetValue(target, value);
    static object? Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Hidden)!.Invoke(target, args);
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    [STAThread]
    static int Main()
    {
        Application.EnableVisualStyles();
        var client = GameClient.Instance;
        using var context = new ChessApplicationContext();
        int passed = 0;
        try
        {
            client.Disconnect();
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            var retry = (Task)Call(client, "ReconnectAsync", cancelled.Token)!;
            retry.GetAwaiter().GetResult();
            Check(!client.IsConnected, "Cancelled retry opened a connection");
            Console.WriteLine("PASS cancelled recovery does not open transport"); passed++;

            var gate = (SemaphoreSlim)typeof(GameClient).GetField("_connectLock", Hidden)!.GetValue(client)!;
            gate.Wait();
            var connecting = client.ConnectAsync("cancelled");
            client.Disconnect();
            gate.Release();
            while (!connecting.IsCompleted) { Application.DoEvents(); Thread.Sleep(1); }
            try { connecting.GetAwaiter().GetResult(); throw new Exception("Queued connection survived logout"); }
            catch (OperationCanceledException) { }
            Console.WriteLine("PASS logout cancels connection waiting on serialized gate"); passed++;

            // Packet A reaches the real receive loop only AFTER cancel A and requeue B.
            var lateQueue = new DashboardForm("smoke", 1200);
            context.SwitchTo(lateQueue);
            Set(lateQueue, "queuedMode", "CHESS");
            Call(lateQueue, "ResetQueue");
            Set(lateQueue, "queuedMode", "CHESS");
            typeof(DashboardForm).GetField("queueAttemptId", Hidden)?.SetValue(lateQueue, "attempt-B");
            using (var key = System.Security.Cryptography.RSA.Create(2048))
            {
                var request = new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=localhost", key,
                    System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
                using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
                using var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(generated.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx), (string?)null, System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.UserKeySet);
                using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
                listener.Start();
                using var tcp = new System.Net.Sockets.TcpClient();
                var connect = Task.Run(() => tcp.ConnectAsync(System.Net.IPAddress.Loopback, ((System.Net.IPEndPoint)listener.LocalEndpoint).Port));

                using var peer = listener.AcceptTcpClient();
                connect.GetAwaiter().GetResult();
                using var serverTls = new System.Net.Security.SslStream(peer.GetStream());
                using var clientTls = new System.Net.Security.SslStream(tcp.GetStream(), false, (_, _, _, _) => true);
                var handshake = Task.Run(() => serverTls.AuthenticateAsServerAsync(cert, false, System.Security.Authentication.SslProtocols.Tls12, false));

                var clientHandshake = Task.Run(() => clientTls.AuthenticateAsClient("localhost", null, System.Security.Authentication.SslProtocols.Tls12, false));
                while (!clientHandshake.IsCompleted || !handshake.IsCompleted) { Application.DoEvents(); Thread.Sleep(1); }
                clientHandshake.GetAwaiter().GetResult();

                handshake.GetAwaiter().GetResult();
                Set(client, "_tcp", tcp); Set(client, "_stream", clientTls);
                var receive = (Task)Call(client, "ListenLoopAsync", tcp, clientTls)!;
                var packet = System.Text.Json.JsonSerializer.Deserialize<GamePacket>("""
                    {"Type":4,"RoomId":"late-A","GameMode":"CHESS","Payload":"FIRST|1200","QueueAttemptId":"attempt-A"}
                    """)!;

                Task.Run(() => GameWire.WriteAsync(serverTls, packet)).GetAwaiter().GetResult();
                var timeout = DateTime.UtcNow.AddSeconds(5);
                while (DateTime.UtcNow < timeout && string.IsNullOrEmpty(client.CurrentRoomId)) { Application.DoEvents(); Thread.Sleep(1); }
                // Drain the queued UI callback after the actual receive event.
                Application.DoEvents();
                Check(ReferenceEquals(context.MainForm, lateQueue), "Incoming A after same-mode requeue B opened a board");
                Check(string.IsNullOrEmpty(client.CurrentRoomId), "Late A was not left");
                using var readDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var left = Task.Run(() => GameWire.ReadAsync<GamePacket>(serverTls, readDeadline.Token)).GetAwaiter().GetResult();
                Check(left?.Type == PacketType.LEAVE_ROOM && left.RoomId == "late-A", "Late A leave was not sent");
                Check((string?)typeof(DashboardForm).GetField("queuedMode", Hidden)!.GetValue(lateQueue) == "CHESS", "Late A reset B queue");
                var valid = packet with { RoomId = "valid-B", QueueAttemptId = "attempt-B" };
                Task.Run(() => GameWire.WriteAsync(serverTls, valid)).GetAwaiter().GetResult();
                timeout = DateTime.UtcNow.AddSeconds(5);
                while (DateTime.UtcNow < timeout && ReferenceEquals(context.MainForm, lateQueue)) { Application.DoEvents(); Thread.Sleep(1); }
                Check(context.MainForm is ChessBoardForm, "Correlated B did not open board");
                client.Disconnect();
                while (!receive.IsCompleted) { Application.DoEvents(); Thread.Sleep(1); }
            }
            Console.WriteLine("PASS actual incoming A after cancel/requeue B is rejected and left without opening board"); passed++;
            context.SwitchTo(new LoginForm());

            foreach (bool requeue in new[] { false, true })
            {
            var cancelledQueue = new DashboardForm("smoke", 1200);
            context.SwitchTo(cancelledQueue);
            Set(cancelledQueue, "queuedMode", "CHESS");
            client.CurrentRoomId = "cancelled-new-room";
            Set(client, "<CurrentMatchState>k__BackingField", new MatchState(1, 0, "", 1, 600000, 600000, 0, 0));
            Call(cancelledQueue, "MatchFound", client.CurrentRoomId, "opponent", 1200, true, "CHESS");
            Call(cancelledQueue, "ResetQueue");
            if (requeue) Set(cancelledQueue, "queuedMode", "CHESS");
            Application.DoEvents();
            Check(ReferenceEquals(context.MainForm, cancelledQueue), "Cancelled ordinary MATCH_FOUND with State opened a board");
            Check(string.IsNullOrEmpty(client.CurrentRoomId), "Cancelled ordinary match was not left");
            Console.WriteLine($"PASS cancelled ordinary snapshot match is rejected and left (same-mode requeue={requeue})"); passed++;
            client.Disconnect();
            context.SwitchTo(new LoginForm());
            }

            foreach (string mode in new[] { "CHESS", "CARO" })
            {
                var dashboard = new DashboardForm("smoke", 1200);
                context.SwitchTo(dashboard);
                Set(client, "<IsResumedMatch>k__BackingField", true);
                client.CurrentRoomId = "recovered-" + mode;
                Set(client, "<CurrentMatchState>k__BackingField", new MatchState(2, 1, mode == "CHESS" ? "e2e4" : "7,7", 2, 600000, 600000, 0, 0));
                // Recovered CARO wins over a CHESS queue; recovered CHESS works from idle lobby.
                if (mode == "CARO") Set(dashboard, "queuedMode", "CHESS");
                Call(dashboard, "MatchFound", client.CurrentRoomId, "opponent", 1200, true, mode);
                Application.DoEvents();
                Check(mode == "CHESS" ? context.MainForm is ChessBoardForm : context.MainForm is CaroBoardForm, "Recovered mode did not open from lobby");
                var opened = context.MainForm;
                Call(dashboard, "MatchFound", client.CurrentRoomId, "opponent", 1200, true, mode);
                Application.DoEvents();
                Check(ReferenceEquals(opened, context.MainForm), "Duplicate match opened a second board");
                var state = (MatchState?)opened!.GetType().GetField("onlineState", Hidden)!.GetValue(opened);
                Check(state?.Revision == 1 && state.Moves == (mode == "CHESS" ? "e2e4" : "7,7"), "Board did not load retained authoritative snapshot");
                Call(opened, "OnlineChatReceived", "opponent", "must-not-appear");
                client.Disconnect();
                Application.DoEvents();
                var chat = (RichTextBox)opened.GetType().GetField("rtbChat", Hidden)!.GetValue(opened)!;
                Check(!chat.Text.Contains("must-not-appear"), "Stale board callback ran after connection invalidation");
                Console.WriteLine($"PASS {mode} recovered snapshot opens exactly once; stale board callback rejected"); passed++;
                client.Disconnect();
                context.SwitchTo(new LoginForm());
            }

            foreach (string callback in new[] { "GameDisconnected", "MatchUnavailable" })
            foreach (string invalidation in new[] { "connection", "attempt", "lifetime" })
            {
                var dashboard = new DashboardForm("smoke", 1200);
                context.SwitchTo(dashboard);
                Set(dashboard, "queuedMode", "CHESS");
                if (callback == "GameDisconnected") Call(dashboard, callback);
                else Call(dashboard, callback, "stale error must not show a modal");
                if (invalidation == "connection") client.Disconnect();
                else if (invalidation == "attempt") Call(dashboard, "ResetQueue");
                else ((CancellationTokenSource)typeof(DashboardForm).GetField("lifetime", Hidden)!.GetValue(dashboard)!).Cancel();
                Set(dashboard, "queuedMode", "CARO");
                // A stale modal would block the harness; close it, then assert it never appeared.
                bool modalShown = false;
                using var modalWatch = new System.Windows.Forms.Timer { Interval = 25 };
                modalWatch.Tick += (_, _) =>
                {
                    IntPtr popup = GetLastActivePopup(dashboard.Handle);
                    if (popup != IntPtr.Zero && popup != dashboard.Handle)
                    {
                        modalShown = true;
                        PostMessage(popup, 0x0010, IntPtr.Zero, IntPtr.Zero); // WM_CLOSE
                    }
                };
                modalWatch.Start();
                Application.DoEvents();
                modalWatch.Stop();
                Check((string?)typeof(DashboardForm).GetField("queuedMode", Hidden)!.GetValue(dashboard) == "CARO", $"Stale {callback}/{invalidation} reset newer queue");
                Check(!modalShown, $"Stale {callback}/{invalidation} showed a modal");
                Console.WriteLine($"PASS stale {callback}/{invalidation} cannot reset queue or show modal"); passed++;
                context.SwitchTo(new LoginForm());
            }

            var stale = new DashboardForm("smoke", 1200);
            context.SwitchTo(stale);
            client.CurrentRoomId = "stale";
            Set(client, "<CurrentMatchState>k__BackingField", new MatchState(2, 1, "e2e4", 2, 600000, 600000, 0, 0));
            Call(stale, "MatchFound", "stale", "opponent", 1200, true, "CHESS");
            client.Disconnect();
            Application.DoEvents();
            Check(ReferenceEquals(context.MainForm, stale), "Stale match callback opened board after disconnect");
            Console.WriteLine("PASS stale queued MATCH_FOUND cannot navigate after disconnect"); passed++;
            Call(stale, "RecoveryFailed", "Phiên đã hết hạn. Vui lòng đăng nhập lại.");
            Application.DoEvents();
            Check(context.MainForm is LoginForm, "Invalid session did not show re-login flow");
            var status = (Label)context.MainForm!.GetType().GetField("lblError", Hidden)!.GetValue(context.MainForm)!;
            Check(status.Text.Contains("đăng nhập lại"), "Re-login reason not visible");
            Console.WriteLine("PASS invalid session returns to login with visible reason and no modal"); passed++;
            Console.WriteLine($"RESULT {passed} passed");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { client.Disconnect(); context.MainForm?.Dispose(); }
    }
}
