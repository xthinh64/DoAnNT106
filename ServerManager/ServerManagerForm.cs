using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using MySqlConnector;
using OnlineChessGame.Auth;
using OnlineChessGame.Network;

namespace OnlineChessGame.ServerManager;

public sealed class ServerManagerForm : Form
{
    private static readonly Color Navy = Color.FromArgb(20, 36, 57);
    private static readonly Color Blue = Color.FromArgb(37, 99, 235);
    private readonly TextBox bindAddress = new() { Text = "127.0.0.1" };
    private readonly NumericUpDown gatewayPort = Port(8888), worker1Port = Port(9001), worker2Port = Port(9002);
    private readonly TextBox certPath = new();
    private readonly TextBox certPassword = new() { UseSystemPasswordChar = true };
    private readonly TextBox databaseConnection = new() { UseSystemPasswordChar = true };
    private readonly TextBox certificatePin = new() { ReadOnly = true };
    private readonly CheckBox legacyMigration = new() { Text = "Cho phép chuyển mật khẩu cũ (mặc định tắt)", AutoSize = true };
    private readonly Button startButton = ActionButton("Khởi động", Blue);
    private readonly Button stopButton = ActionButton("Dừng server", Color.FromArgb(185, 28, 28));
    private readonly Button testDatabaseButton = new() { Text = "Kiểm tra MySQL", AutoSize = true };
    private readonly Button browseButton = new() { Text = "Chọn PFX…", AutoSize = true };
    private readonly Button createCertificateButton = new() { Text = "Tạo PFX mới…", AutoSize = true };
    private readonly Button copyPinButton = new() { Text = "Sao chép pin", AutoSize = true };
    private readonly Label status = new() { Text = "ĐÃ DỪNG", AutoSize = true, ForeColor = Color.FromArgb(148, 163, 184) };
    private readonly Label databaseStatus = new() { Text = "MySQL: chưa kiểm tra", AutoSize = true };
    private readonly Label uptime = new() { Text = "Thời gian chạy: —", AutoSize = true };
    private readonly Label outcomes = new() { Text = "Thành công: 0   |   Thất bại: 0", AutoSize = true };
    private readonly DataGridView listeners = new();
    private readonly RichTextBox log = new() { ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Color.White, Font = new Font("Consolas", 10), Dock = DockStyle.Fill, WordWrap = false };
    private readonly System.Windows.Forms.Timer refreshTimer = new() { Interval = 500 };
    private readonly CancellationTokenSource lifetime = new();
    private readonly TabControl tabs = new() { Dock = DockStyle.Fill };
    private readonly TabPage configTab = new("Cấu hình");
    private readonly Queue<string> logLines = new();
    private AuthCluster? cluster;
    private X509Certificate2? certificate;
    private AuthMonitor monitor = new(new UnusedService());
    private Task operation = Task.CompletedTask;
    private bool busy, closing, allowClose;
    private DateTimeOffset startedAt;
    private int[] displayedPorts = [8888, 9001, 9002];
    private string displayedAddress = "127.0.0.1";

    public ServerManagerForm()
    {
        Text = "Online Chess & Caro — Quản trị server";
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(243, 246, 250);
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1120, 780);
        MinimumSize = new Size(960, 680);
        AutoScaleMode = AutoScaleMode.Dpi;
        BuildLayout();
        LoadEnvironment();
        startButton.Click += (_, _) => BeginOperation(StartServerAsync);
        stopButton.Click += (_, _) => BeginOperation(StopServerAsync);
        testDatabaseButton.Click += (_, _) => BeginOperation(TestDatabaseAsync);
        createCertificateButton.Click += (_, _) => BeginOperation(CreateCertificateAsync);
        browseButton.Click += (_, _) => BrowseCertificate();
        copyPinButton.Click += (_, _) => CopyPin();
        refreshTimer.Tick += (_, _) => RefreshDashboard();
        refreshTimer.Start();
        monitor.Log("Sẵn sàng. Cấu hình MySQL và chứng chỉ, sau đó khởi động server.");
        monitor.Log("Kết nối TCP đang xử lý không phải số người chơi online. Server hiện phục vụ xác thực.");
        RefreshControls();
        RefreshDashboard();
    }

    private static NumericUpDown Port(int value) => new() { Minimum = 1, Maximum = 65535, Value = value };
    private static Button ActionButton(string text, Color color) => new()
    {
        Text = text, AutoSize = true, BackColor = color, ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat, Padding = new Padding(12, 6, 12, 6)
    };
    private static Label Caption(string text) => new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 10, 8) };

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(20) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Navy, ColumnCount = 2, RowCount = 2, Padding = new Padding(18, 10, 18, 10) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        header.Controls.Add(new Label { Text = "QUẢN TRỊ SERVER", ForeColor = Color.White, Font = new Font("Segoe UI", 17, FontStyle.Bold), AutoSize = true }, 0, 0);
        header.Controls.Add(new Label { Text = "Gateway TCP  /  Hai worker TLS  /  Xác thực MySQL", ForeColor = Color.FromArgb(203, 213, 225), AutoSize = true }, 0, 1);
        status.Anchor = AnchorStyles.Right;
        header.Controls.Add(status, 1, 0);
        root.Controls.Add(header, 0, 0);
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 9, 0, 4) };
        toolbar.Controls.AddRange([startButton, stopButton, testDatabaseButton]);
        databaseStatus.Margin = new Padding(18, 12, 0, 0);
        toolbar.Controls.Add(databaseStatus);
        root.Controls.Add(toolbar, 0, 1);
        root.Controls.Add(tabs, 0, 2);

        var overview = new TabPage("Vận hành") { Padding = new Padding(14), BackColor = Color.White };
        tabs.TabPages.Add(overview);
        tabs.TabPages.Add(configTab);
        var dashboard = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1 };
        dashboard.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        dashboard.RowStyles.Add(new RowStyle(SizeType.Absolute, 152));
        dashboard.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        dashboard.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        dashboard.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        overview.Controls.Add(dashboard);
        dashboard.Controls.Add(uptime, 0, 0);
        ConfigureListenersGrid();
        dashboard.Controls.Add(listeners, 0, 1);
        dashboard.Controls.Add(outcomes, 0, 2);
        var logToolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        logToolbar.Controls.Add(Caption("NHẬT KÝ HOẠT ĐỘNG"));
        var clear = new Button { Text = "Xóa hiển thị", AutoSize = true };
        clear.Click += (_, _) => { logLines.Clear(); log.Clear(); };
        logToolbar.Controls.Add(clear);
        dashboard.Controls.Add(logToolbar, 0, 3);
        dashboard.Controls.Add(log, 0, 4);

        configTab.Padding = new Padding(18);
        configTab.BackColor = Color.White;
        configTab.AutoScroll = true;
        var config = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, RowCount = 10 };
        config.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175));
        config.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        config.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        configTab.Controls.Add(config);
        AddField(config, 0, "IP gateway", bindAddress);
        var ports = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        foreach (var (label, port) in new[] { ("Gateway", gatewayPort), ("Worker 1", worker1Port), ("Worker 2", worker2Port) })
        {
            port.Width = 90;
            ports.Controls.Add(Caption(label));
            ports.Controls.Add(port);
        }
        AddField(config, 1, "Cổng lắng nghe", ports);
        AddField(config, 2, "Chứng chỉ PFX", certPath, browseButton);
        AddField(config, 3, "Mật khẩu PFX", certPassword, createCertificateButton);
        AddField(config, 4, "Chuỗi kết nối MySQL", databaseConnection);
        AddField(config, 5, "SHA-256 certificate pin", certificatePin, copyPinButton);
        config.Controls.Add(legacyMigration, 1, 6);
        config.SetColumnSpan(legacyMigration, 2);
        var note = new Label
        {
            AutoSize = true, MaximumSize = new Size(780, 0), Margin = new Padding(0, 22, 0, 0),
            Text = "Worker chỉ mở trên 127.0.0.1. Để dùng LAN, đặt IP gateway thành IP máy chủ hoặc 0.0.0.0; client dùng IP thực của máy chủ.\r\n\r\nCấu hình được đọc từ biến môi trường và chỉ giữ trong bộ nhớ. Thay đổi cấu hình khi server đã dừng. Nhập mật khẩu PFX trước khi tạo chứng chỉ mới; công cụ không ghi đè tệp đã tồn tại.\r\n\r\nPin được hiển thị sau khi tạo PFX hoặc khởi động thành công. Cấp pin này cho CHESS_CERT_PIN ở client qua kênh tin cậy."
        };
        config.Controls.Add(note, 0, 7);
        config.SetColumnSpan(note, 3);
    }

    private static void AddField(TableLayoutPanel panel, int row, string label, Control field, Control? button = null)
    {
        panel.Controls.Add(Caption(label), 0, row);
        field.Dock = DockStyle.Fill;
        field.Margin = new Padding(0, 6, 10, 8);
        panel.Controls.Add(field, 1, row);
        if (button is not null) { button.Dock = DockStyle.Fill; panel.Controls.Add(button, 2, row); }
        else panel.SetColumnSpan(field, 2);
    }

    private void ConfigureListenersGrid()
    {
        listeners.Dock = DockStyle.Fill;
        listeners.ReadOnly = true;
        listeners.AllowUserToAddRows = false;
        listeners.AllowUserToDeleteRows = false;
        listeners.AllowUserToResizeRows = false;
        listeners.RowHeadersVisible = false;
        listeners.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        listeners.BackgroundColor = Color.White;
        listeners.BorderStyle = BorderStyle.None;
        listeners.RowTemplate.Height = 32;
        foreach (string title in new[] { "Thành phần", "Địa chỉ", "Kết nối đang xử lý", "Yêu cầu auth đã nhận" })
            listeners.Columns.Add(title, title);
        listeners.Rows.Add("Gateway TCP", "—", 0, "—");
        listeners.Rows.Add("Worker TLS 1", "—", 0, 0);
        listeners.Rows.Add("Worker TLS 2", "—", 0, 0);
    }

    private void LoadEnvironment()
    {
        bindAddress.Text = Environment.GetEnvironmentVariable("CHESS_BIND_ADDRESS") ?? "127.0.0.1";
        certPath.Text = Environment.GetEnvironmentVariable("CHESS_CERT_PATH") ?? "";
        certPassword.Text = Environment.GetEnvironmentVariable("CHESS_CERT_PASSWORD") ?? "";
        databaseConnection.Text = Environment.GetEnvironmentVariable("CHESS_DB_CONNECTION") ?? "";
        legacyMigration.Checked = string.Equals(Environment.GetEnvironmentVariable("AUTH_ALLOW_LEGACY_MIGRATION"), "true", StringComparison.OrdinalIgnoreCase);
        if (certPath.Text.Length == 0 || databaseConnection.Text.Length == 0) tabs.SelectedTab = configTab;
        certPath.TextChanged += (_, _) => certificatePin.Clear();
        databaseConnection.TextChanged += (_, _) => databaseStatus.Text = "MySQL: chưa kiểm tra";
    }

    private void BeginOperation(Func<Task> action)
    {
        if (busy || closing) return;
        busy = true;
        RefreshControls();
        operation = ExecuteOperationAsync(action);
    }

    private async Task ExecuteOperationAsync(Func<Task> action)
    {
        try { await action(); }
        catch (OperationCanceledException)
        {
            monitor.Log("Thao tác đã hủy hoặc hết thời gian.");
            if (!closing) tabs.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            monitor.Log(FriendlyError(ex));
            if (!closing) tabs.SelectedIndex = 0;
        }
        finally { busy = false; RefreshControls(); RefreshDashboard(); }
    }

    private async Task StartServerAsync()
    {
        if (cluster is not null) return;
        if (!IPAddress.TryParse(bindAddress.Text.Trim(), out var address)) throw new InvalidOperationException("IP gateway không hợp lệ.");
        int[] ports = [(int)gatewayPort.Value, (int)worker1Port.Value, (int)worker2Port.Value];
        if (ports.Distinct().Count() != 3) throw new InvalidOperationException("Ba cổng phải khác nhau.");
        if (string.IsNullOrWhiteSpace(certPath.Text) || string.IsNullOrEmpty(certPassword.Text))
            throw new InvalidOperationException("Cần đường dẫn PFX và mật khẩu chứng chỉ.");
        string connection = ReadConnectionString();
        status.Text = "ĐANG KHỞI ĐỘNG";
        monitor.Log("Đang kiểm tra MySQL và chứng chỉ…");
        databaseStatus.Text = "MySQL: đang kiểm tra…";
        try
        {
            await ProbeDatabaseAsync(connection, lifetime.Token);
            databaseStatus.Text = "MySQL: schema auth sẵn sàng";
        }
        catch { databaseStatus.Text = "MySQL: kiểm tra chưa đạt"; throw; }
        string path = certPath.Text.Trim(), password = certPassword.Text;
        var loaded = await Task.Run(() => new X509Certificate2(path, password, X509KeyStorageFlags.UserKeySet), lifetime.Token);
        AuthCluster? candidate = null;
        try
        {
            if (!loaded.HasPrivateKey || DateTime.UtcNow < loaded.NotBefore.ToUniversalTime() || DateTime.UtcNow > loaded.NotAfter.ToUniversalTime())
                throw new InvalidOperationException("Chứng chỉ phải có khóa riêng và còn hiệu lực.");
            var nextMonitor = new AuthMonitor(new AuthAdmissionService(new MySqlAuthService(connection, legacyMigration.Checked)));
            candidate = new AuthCluster(nextMonitor, loaded, ports[0], ports[1], ports[2], address);
            // Listener continuations and password hashing must not capture the WinForms context.
            await Task.Run(() => candidate.StartAsync(lifetime.Token), lifetime.Token);
            lifetime.Token.ThrowIfCancellationRequested();
            // Commit ownership only after all listeners have started successfully.
            foreach (string line in monitor.DrainEvents()) AppendLog(line);
            monitor = nextMonitor;
            cluster = candidate;
            certificate = loaded;
            displayedAddress = address.ToString();
            displayedPorts = ports;
            startedAt = DateTimeOffset.UtcNow;
            certificatePin.Text = loaded.GetCertHashString(HashAlgorithmName.SHA256);
            monitor.Log("Server đã khởi động. Gateway và hai worker chạy trong cùng tiến trình.");
            if (legacyMigration.Checked) monitor.Log("Chuyển mật khẩu cũ đang BẬT; chỉ dùng trong đợt chuyển đổi có kiểm soát.");
            tabs.SelectedIndex = 0;
        }
        catch
        {
            if (candidate is not null) await candidate.DisposeAsync();
            loaded.Dispose();
            throw;
        }
    }

    private async Task StopServerAsync()
    {
        if (cluster is null) return;
        status.Text = "ĐANG DỪNG";
        monitor.Log("Đang đóng listener và kết nối đang xử lý…");
        await cluster.DisposeAsync();
        cluster = null;
        certificate?.Dispose();
        certificate = null;
        monitor.Log("Server đã dừng. Có thể đổi cấu hình và khởi động lại.");
    }

    private string ReadConnectionString()
    {
        if (string.IsNullOrWhiteSpace(databaseConnection.Text)) throw new InvalidOperationException("Cần chuỗi kết nối MySQL.");
        try
        {
            var builder = new MySqlConnectionStringBuilder(databaseConnection.Text);
            if (string.IsNullOrWhiteSpace(builder.Database)) throw new ArgumentException();
            return builder.ConnectionString;
        }
        catch (ArgumentException) { throw new InvalidOperationException("Chuỗi kết nối MySQL không hợp lệ hoặc thiếu Database."); }
    }

    private static async Task ProbeDatabaseAsync(string connectionString, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(deadline.Token);
        // Read schema only. No account data is retrieved and no existing database is modified.
        await using var command = new MySqlCommand("SELECT u.user_id,u.username,u.display_name,u.password_hash,s.elo,s.game_type FROM Users u LEFT JOIN UserGameStats s ON u.user_id=s.user_id LIMIT 0", connection);
        await using var reader = await command.ExecuteReaderAsync(deadline.Token);
    }

    private async Task TestDatabaseAsync()
    {
        databaseStatus.Text = "MySQL: đang kiểm tra…";
        try
        {
            await ProbeDatabaseAsync(ReadConnectionString(), lifetime.Token);
            databaseStatus.Text = "MySQL: schema auth sẵn sàng";
            monitor.Log("Kiểm tra MySQL thành công: truy cập được schema xác thực.");
        }
        catch { databaseStatus.Text = "MySQL: kiểm tra chưa đạt"; throw; }
    }

    private async Task CreateCertificateAsync()
    {
        if (string.IsNullOrEmpty(certPassword.Text)) throw new InvalidOperationException("Nhập mật khẩu PFX trước khi tạo chứng chỉ.");
        using var dialog = new SaveFileDialog { Filter = "Chứng chỉ PFX (*.pfx)|*.pfx", FileName = "chess-auth.pfx", OverwritePrompt = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        string path = dialog.FileName, password = certPassword.Text;
        string pin = await Task.Run(() =>
        {
            using var rsa = RSA.Create(3072);
            var request = new CertificateRequest("CN=Chess Auth", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, false));
            using var created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(1));
            using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            output.Write(created.Export(X509ContentType.Pfx, password));
            return created.GetCertHashString(HashAlgorithmName.SHA256);
        }, lifetime.Token);
        certPath.Text = path;
        certificatePin.Text = pin;
        monitor.Log("Đã tạo chứng chỉ mới. Giữ PFX trên server và cấp pin cho client.");
    }

    private void BrowseCertificate()
    {
        using var dialog = new OpenFileDialog { Filter = "Chứng chỉ PFX (*.pfx;*.p12)|*.pfx;*.p12", CheckFileExists = true };
        if (dialog.ShowDialog(this) == DialogResult.OK) certPath.Text = dialog.FileName;
    }

    private void CopyPin()
    {
        if (certificatePin.Text.Length == 0) return;
        try { Clipboard.SetText(certificatePin.Text); monitor.Log("Đã sao chép pin chứng chỉ."); }
        catch (System.Runtime.InteropServices.ExternalException) { monitor.Log("Clipboard đang bận. Hãy thử sao chép lại."); }
    }

    private void RefreshControls()
    {
        bool running = cluster is not null;
        startButton.Enabled = !busy && !running && !closing;
        stopButton.Enabled = !busy && running && !closing;
        bool editable = !busy && !running && !closing;
        foreach (Control field in new Control[] { bindAddress, gatewayPort, worker1Port, worker2Port, certPath, certPassword, databaseConnection, legacyMigration, browseButton, createCertificateButton })
            field.Enabled = editable;
        copyPinButton.Enabled = !closing && certificatePin.Text.Length == 64;
        testDatabaseButton.Enabled = !busy && !running && !closing;
        if (!busy) status.Text = running ? "ĐANG CHẠY" : "ĐÃ DỪNG";
        status.ForeColor = running ? Color.FromArgb(74, 222, 128) : Color.FromArgb(148, 163, 184);
    }

    private void RefreshDashboard()
    {
        var current = cluster;
        string endpoint = displayedAddress.Contains(':') ? $"[{displayedAddress}]" : displayedAddress;
        listeners.Rows[0].Cells[1].Value = current is null ? "Đã dừng" : $"{endpoint}:{displayedPorts[0]}";
        listeners.Rows[1].Cells[1].Value = current is null ? "Đã dừng" : $"127.0.0.1:{displayedPorts[1]}";
        listeners.Rows[2].Cells[1].Value = current is null ? "Đã dừng" : $"127.0.0.1:{displayedPorts[2]}";
        listeners.Rows[0].Cells[2].Value = current?.ActiveGatewayConnections ?? 0;
        listeners.Rows[1].Cells[2].Value = current?.ActiveWorker1Connections ?? 0;
        listeners.Rows[2].Cells[2].Value = current?.ActiveWorker2Connections ?? 0;
        if (current is not null)
        {
            listeners.Rows[1].Cells[3].Value = current.Worker1Requests;
            listeners.Rows[2].Cells[3].Value = current.Worker2Requests;
        }
        uptime.Text = current is null ? "Thời gian chạy: —" : $"Thời gian chạy: {(DateTimeOffset.UtcNow - startedAt).ToString(@"d\.hh\:mm\:ss")}";
        outcomes.Text = $"Kết quả nghiệp vụ — Thành công: {monitor.Successful}   |   Thất bại: {monitor.Failed}   |   Đặt lại bộ đếm khi khởi động mới";
        foreach (string line in monitor.DrainEvents()) AppendLog(line);
    }

    private void AppendLog(string line)
    {
        logLines.Enqueue(line);
        if (logLines.Count > 1000)
        {
            logLines.Dequeue();
            log.Lines = logLines.ToArray();
        }
        else log.AppendText(line + Environment.NewLine);
        log.SelectionStart = log.TextLength;
        log.ScrollToCaret();
    }

    private static string FriendlyError(Exception error) => error switch
    {
        // Only messages authored in this form are shown; provider messages may contain secrets.
        InvalidOperationException e when e.Message is "IP gateway không hợp lệ." or "Ba cổng phải khác nhau." or "Cần đường dẫn PFX và mật khẩu chứng chỉ." or "Chứng chỉ phải có khóa riêng và còn hiệu lực." or "Cần chuỗi kết nối MySQL." or "Chuỗi kết nối MySQL không hợp lệ hoặc thiếu Database." or "Nhập mật khẩu PFX trước khi tạo chứng chỉ." => e.Message,
        MySqlException => "Không truy cập được MySQL/schema auth. Kiểm tra địa chỉ, tài khoản, quyền và schema.",
        CryptographicException => "Không nạp được chứng chỉ. Kiểm tra định dạng PFX và mật khẩu.",
        SocketException => "Không mở được cổng server. Kiểm tra IP, cổng bị chiếm hoặc server console đang chạy.",
        IOException => "Không đọc/ghi được PFX. Kiểm tra đường dẫn, quyền và tệp đã tồn tại.",
        UnauthorizedAccessException => "Không có quyền truy cập tệp chứng chỉ.",
        _ => "Thao tác không thành công. Kiểm tra cấu hình và trạng thái dịch vụ."
    };

    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (allowClose) return;
        e.Cancel = true;
        if (closing) return;
        closing = true;
        lifetime.Cancel();
        RefreshControls();
        await operation;
        try { await StopServerAsync(); }
        catch (Exception ex) { monitor.Log(FriendlyError(ex)); }
        allowClose = true;
        BeginInvoke(new Action(Close));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            refreshTimer.Dispose();
            lifetime.Dispose();
        }
        base.Dispose(disposing);
    }

    private sealed class UnusedService : IAuthService
    {
        public Task<AuthResponse> HandleAsync(AuthRequest request, CancellationToken ct) => throw new InvalidOperationException();
    }
}
