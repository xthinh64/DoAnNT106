using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using OnlineChessGame;
using OnlineChessGame.Network;
using Timer = System.Windows.Forms.Timer;

internal static class Program
{
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly Type Auth = typeof(LoginForm).Assembly.GetType("OnlineChessGame.AuthConnection", true)!;
    static readonly List<string> Dialogs = [];
    static readonly List<Exception> Errors = [];
    static ChessApplicationContext context = null!;
    static int passed, failed;
    static string current = "startup";
    static bool capture;

    [STAThread]
    static int Main(string[] args)
    {
        capture = args.Contains("--capture");
        Console.OutputEncoding = Encoding.UTF8;
        // Guard against any accidental valid submission reaching a socket.
        Environment.SetEnvironmentVariable("CHESS_AUTH_ENDPOINT", "invalid-endpoint");
        using var deadline = new System.Threading.Timer(_ =>
        {
            Console.Error.WriteLine($"RUNTIME TIMEOUT: {current}; 28-second suite deadline");
            Environment.Exit(2);
        }, null, 28000, Timeout.Infinite);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Errors.Add(e.Exception);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using var dialogs = new Timer { Interval = 25 };
        dialogs.Tick += (_, _) => CloseDialogs();
        dialogs.Start();
        using var start = new Timer { Interval = 30 };
        start.Tick += async (_, _) =>
        {
            start.Stop();
            try { await Suite(); }
            catch (Exception ex) { failed++; Console.Error.WriteLine($"RUNTIME suite: {ex}"); }
            finally
            {
                foreach (Form form in Application.OpenForms.Cast<Form>().ToArray()) form.Dispose();
                context.ExitThread();
            }
        };
        try
        {
            context = new ChessApplicationContext();
            start.Start();
            Application.Run(context);
        }
        catch (Exception ex) { failed++; Console.Error.WriteLine($"RUNTIME startup: {ex}"); }
        Console.WriteLine($"RESULT: {passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }

    static async Task Suite()
    {
        Check(Thread.CurrentThread.GetApartmentState() == ApartmentState.STA, "Harness must run STA");
        await Test("login empty, masked, AcceptButton, local validation", async () =>
        {
            var f = context.MainForm!;
            Check(f is LoginForm && f.Visible && f.IsHandleCreated, "Context did not show login");
            Capture(f, "login");
            Empty(f);
            Check(ReferenceEquals(f.AcceptButton, C<Button>(f, "btnLogin")), "Login AcceptButton not set");
            C<Button>(f, "btnLogin").PerformClick();
            await Validation(f);
        });
        await Test("connection settings collapsed, expandable and themeable", async () =>
        {
            context.SwitchTo(new LoginForm());
            var f = context.MainForm!;
            var server = C<TextBox>(f, "txtServer");
            Check(!server.Visible, "Server should be hidden on default sign-in");
            Check(f.ActiveControl == C<TextBox>(f,"txtUser"), "Initial focus is not username");
            C<Button>(f,"btnConnection").PerformClick();
            Check(server.Visible && server.Bottom < C<Panel>(f,"card").Height, "Expanded server input clipped");
            Capture(f,"login-connection");
            C<Button>(f,"btnThemeToggle").PerformClick();
            Check(server.ForeColor == ThemeColors.InputFg, "Expanded settings not themed");
            Capture(f,"login-dark");
            C<Button>(f,"btnThemeToggle").PerformClick();
            C<Button>(f,"btnConnection").PerformClick();
            Check(!server.Visible && f.AcceptButton == C<Button>(f,"btnLogin"), "Collapsing settings broke sign-in");
            await Task.Delay(25);
        });
        await Test("register empty, masked, AcceptButton, back navigation", async () =>
        {
            context.SwitchTo(new LoginForm());
            C<Button>(context.MainForm!, "btnRegister").PerformClick();
            var f = context.MainForm!;
            Check(f is RegisterForm, "Register navigation failed");
            Capture(f, "register");
            Empty(f);
            Check(C<TextBox>(f, "txtConfirm").Text == "", "Confirm prefilled");
            Check(ReferenceEquals(f.AcceptButton, C<Button>(f, "btnRegister")), "Register AcceptButton not set");
            C<Button>(f, "btnBack").PerformClick();
            await Task.Delay(40);
            Check(context.MainForm is LoginForm && f.IsDisposed, "Back did not dispose registration/show login");
        });
        await Test("password reveal and registration success handoff", async () =>
        {
            context.SwitchTo(new RegisterForm());
            var f = context.MainForm!;
            var show = C<CheckBox>(f,"chkShowPassword");
            show.Checked = true;
            Check(!C<TextBox>(f,"txtPass").UseSystemPasswordChar && !C<TextBox>(f,"txtConfirm").UseSystemPasswordChar,"Reveal did not update both fields");
            show.Checked = false;
            Check(C<TextBox>(f,"txtPass").UseSystemPasswordChar && C<TextBox>(f,"txtConfirm").UseSystemPasswordChar,"Passwords not masked again");
            context.SwitchTo(new LoginForm("new-user", "Tạo tài khoản thành công."));
            f = context.MainForm!;
            Check(C<TextBox>(f,"txtUser").Text == "new-user" && C<TextBox>(f,"txtPass").Text == "", "Success handoff lost username or preset password");
            Check(f.ActiveControl == C<TextBox>(f,"txtPass"),"Success handoff does not focus password");
            Check(C<Label>(f,"lblError").Text.Contains("thành công"),"Success status missing");
            await Task.Delay(25);
        });
        await Test("registration invalid inputs stay local", async () =>
        {
            context.SwitchTo(new RegisterForm());
            var f = context.MainForm!;
            var cases = new (string User, string Display, string Pass, string Confirm, string Error)[]
            {
                ("", "", "", "", "50"),
                (new string('u',51), "Name", "abcdefgh", "abcdefgh", "50"),
                ("user", new string('d',101), "abcdefgh", "abcdefgh", "100"),
                ("user", "Name", "short", "short", "8"),
                ("user", "Name", new string('p',129), new string('p',129), "128"),
                ("user", "Name", "abcdefgh", "different", "khớp")
            };
            foreach (var item in cases)
            {
                C<TextBox>(f,"txtUser").Text=item.User;
                C<TextBox>(f,"txtDisplay").Text=item.Display;
                C<TextBox>(f,"txtPass").Text=item.Pass;
                C<TextBox>(f,"txtConfirm").Text=item.Confirm;
                C<Label>(f,"lblError").Text="";
                C<Button>(f,"btnRegister").PerformClick();
                await Validation(f);
                Check(C<Label>(f,"lblError").Text.Contains(item.Error), $"Wrong validation: {C<Label>(f,"lblError").Text}");
            }
        });
        await Test("dashboard honest empty history", () =>
        {
            var f = Dashboard();
            Capture(f,"dashboard");
            string text = string.Join(" ", Children(f).Where(c=>c.Visible).Select(c=>c.Text));
            foreach (var fake in new[]{"GrandMaster_VN","Caro_King99","Tactician_07","PeaceMaker","Beginner_Pro"})
                Check(!text.Contains(fake), $"Fabricated history: {fake}");
            Check(!string.IsNullOrWhiteSpace(text), "Dashboard has no visible content");
            return Task.CompletedTask;
        });
        await Test("logout clears Player/SessionToken and returns to login", async () =>
        {
            SetAuth("Player",new PlayerProfile(42,"smoke-user","Smoke User",1234,1250));
            SetAuth("SessionToken","smoke-token-not-a-credential");
            var old = Dashboard();
            C<Button>(old,"btnLogout").PerformClick();
            await Task.Delay(50);
            Check(context.MainForm is LoginForm && context.MainForm.Visible, "Logout navigation failed");
            Check(old.IsDisposed,"Logout left dashboard alive");
            Check(GetAuth("SessionToken") is null,"Logout left SessionToken in memory");
            Check(GetAuth("Player") is null,"Logout left Player in memory");
            Empty(context.MainForm!);
        });
        await Test("solo center click receives deterministic local response", async () =>
        {
            var first=await CenterReply();
            var second=await CenterReply();
            Check(first==second,$"Identical openings differ: {first} vs {second}");
        });
        await Test("keyboard selection places a stone and dark board renders", async () =>
        {
            var f = Solo();
            var panel = C<Panel>(f,"boardPanel");
            Check(panel.TabStop && panel.Focus(), "Board cannot receive keyboard focus");
            typeof(Control).GetMethod("OnKeyDown",Private)!.Invoke(panel,[new KeyEventArgs(Keys.Right)]);
            typeof(Control).GetMethod("OnKeyDown",Private)!.Invoke(panel,[new KeyEventArgs(Keys.Space)]);
            Check(Field<int[,]>(f,"board")[7,8] == 1, "Keyboard move did not use selected cell");
            C<Button>(f,"btnThemeToggle").PerformClick();
            Capture(f,"caro-dark");
            C<Button>(f,"btnThemeToggle").PerformClick();
            await Task.Delay(400);
        });
        await Test("occupied click ignored without consuming turn", async () =>
        {
            var f=Solo();
            var b=Field<int[,]>(f,"board");
            b[7,7]=1;
            var before=(int[,])b.Clone();
            Click(f,7,7);
            await Task.Delay(550);
            Check(Same(b,before),"Occupied click changed board/scheduled bot");
            Check(Field<bool>(f,"isPlayerTurn"),"Occupied click consumed turn");
        });
        await Test("leave during bot delay disposes timer and prevents mutation", async () =>
        {
            var f=Solo();
            var b=Field<int[,]>(f,"board");
            Click(f,7,7);
            var before=(int[,])b.Clone();
            var timer=f.GetType().GetField("botDelay",Private)?.GetValue(f) as Timer;
            bool disposed=false;
            if(timer is not null) timer.Disposed+=(_,_)=>disposed=true;
            C<Button>(f,"btnLeave").PerformClick();
            await Task.Delay(650);
            Check(context.MainForm is DashboardForm && f.IsDisposed,"Leave failed to dispose board/show dashboard");
            Check(Same(b,before),"Pending bot mutated disposed board");
            Check(timer is not null,"Bot delay not owned by form: missing botDelay field");
            Check(disposed && !timer!.Enabled,"Bot delay timer was not disposed");
        });
        await Test("fifth player stone wins inline without modal", async () =>
        {
            var f=Solo();
            var b=Field<int[,]>(f,"board");
            for(int c=3;c<7;c++) b[7,c]=1;
            Click(f,7,7);
            await Task.Delay(80);
            Check(Field<bool>(f,"gameOver"),"Five stones did not end game");
            string text=C<Label>(f,"lblTurn").Text;
            Check(text.Contains("thang",StringComparison.OrdinalIgnoreCase)||text.Contains("thắng",StringComparison.OrdinalIgnoreCase),"Win banner missing");
            Capture(f,"caro-win");
            var before=(int[,])b.Clone();
            Click(f,0,0);
            await Task.Delay(450);
            Check(Same(b,before),"Game accepted a move after win");
        });
        await Test("full board draws immediately without modal", async () =>
        {
            var f=Solo();
            var b=Field<int[,]>(f,"board");
            // Two-by-two bands avoid five in all four directions.
            for(int r=0;r<15;r++) for(int c=0;c<15;c++) b[r,c]=(r+2*c)%4<2?1:2;
            Check(!HasFive(b),"Invalid draw fixture contains a five");
            b[14,13]=0;
            Click(f,14,13);
            await Task.Delay(100);
            Check(Field<bool>(f,"gameOver"),"Full board not ended immediately after final legal move");
            string text=C<Label>(f,"lblTurn").Text;
            Check(text.Contains("hoa",StringComparison.OrdinalIgnoreCase)||text.Contains("hòa",StringComparison.OrdinalIgnoreCase),$"Missing draw banner: {text}");
            Capture(f,"caro-draw");
        });
        await Test("theme retains gameOver banner", async () =>
        {
            var f=Solo();
            Invoke(f,"EndGame","Smoke finished game");
            string text=C<Label>(f,"lblTurn").Text;
            var original=ThemeColors.CurrentTheme;
            C<Button>(f,"btnThemeToggle").PerformClick();
            await Task.Delay(50);
            Check(ThemeColors.CurrentTheme!=original,"Theme did not toggle");
            Check(Field<bool>(f,"gameOver") && C<Label>(f,"lblTurn").Text==text,"Theme overwrote endgame banner");
        });
        await Test("solo replay clears board and terminal state", async () =>
        {
            var f=Solo();
            var b=Field<int[,]>(f,"board");
            for(int c=3;c<7;c++) b[7,c]=1;
            Click(f,7,7);
            Check(Field<bool>(f,"gameOver"),"Replay setup did not end game");
            C<Button>(f,"btnDraw").PerformClick();
            await Task.Delay(50);
            Check(!Field<bool>(f,"gameOver"),"Replay retained gameOver");
            Check(Field<int[,]>(f,"board").Cast<int>().All(p=>p==0),"Replay retained stones");
            Check(Field<bool>(f,"isPlayerTurn"),"Replay did not restore player's turn");
            Click(f,7,7);
            Check(Field<int[,]>(f,"board")[7,7]==1,"Replayed game rejected opening click");
        });
        SetAuth("Player",null);
        SetAuth("SessionToken",null);
    }

    static async Task Test(string name,Func<Task> body)
    {
        current=name;
        int dialogs=Dialogs.Count, errors=Errors.Count;
        var watch=Stopwatch.StartNew();
        Exception? failure=null;
        try { await body(); }
        catch(Exception ex) { failure=ex is TargetInvocationException { InnerException: {} inner }?inner:ex; }
        await Task.Delay(50);
        if(Dialogs.Count>dialogs) failure=new MissingFeature($"Unexpected modal: {string.Join("; ",Dialogs.Skip(dialogs))}. {failure?.Message}");
        if(Errors.Count>errors) failure=Errors[errors];
        if(failure is null) { passed++; Console.WriteLine($"PASS {name} ({watch.ElapsedMilliseconds} ms)"); }
        else
        {
            failed++;
            string kind=failure is MissingFeature or MissingMemberException?"MISSING FEATURE":"RUNTIME";
            Console.WriteLine($"FAIL [{kind}] {name}: {failure.GetType().Name}: {failure.Message}");
            if(kind=="RUNTIME") Console.WriteLine(failure.StackTrace);
        }
    }

    static void Empty(Form f)
    {
        Check(C<TextBox>(f,"txtUser").Text=="","Username prefilled");
        var pass=C<TextBox>(f,"txtPass");
        Check(pass.Text=="","Password prefilled");
        Check(pass.UseSystemPasswordChar||pass.PasswordChar!='\0',"Password not masked");
    }
    static async Task Validation(Form f)
    {
        await Task.Delay(30);
        var error=C<Label>(f,"lblError");
        Check(error.Visible&&!string.IsNullOrWhiteSpace(error.Text),"Validation not visible inline");
        Check(!error.Text.Contains("TLS",StringComparison.OrdinalIgnoreCase)&&!error.Text.Contains("kết nối",StringComparison.OrdinalIgnoreCase),$"Invalid input reached connection: {error.Text}");
        Check(C<Button>(f,f is LoginForm?"btnLogin":"btnRegister").Enabled,"Invalid submission disabled action");
    }
    static DashboardForm Dashboard()
    {
        var f=new DashboardForm("smoke-user",1234);
        context.SwitchTo(f);
        return f;
    }
    static CaroBoardForm Solo()
    {
        var f=Dashboard();
        C<Button>(f,"btnSoloCaro").PerformClick();
        Check(context.MainForm is CaroBoardForm,"Solo button navigation failed");
        return (CaroBoardForm)context.MainForm!;
    }
    static async Task<(int,int)> CenterReply()
    {
        var f=Solo();
        var b=Field<int[,]>(f,"board");
        Click(f,7,7);
        Check(b[7,7]==1,"Center click did not place X");
        var wait=Stopwatch.StartNew();
        while(b.Cast<int>().Count(p=>p==2)==0&&wait.ElapsedMilliseconds<2500) await Task.Delay(25);
        Check(b.Cast<int>().Count(p=>p==1)==1&&b.Cast<int>().Count(p=>p==2)==1,"Expected one X and one bot reply within 2.5s");
        Check(Field<bool>(f,"isPlayerTurn"),"Bot did not return player turn");
        Capture(f,"caro-moves");
        for(int r=0;r<15;r++) for(int c=0;c<15;c++) if(b[r,c]==2)
        {
            Check(Math.Max(Math.Abs(r-7),Math.Abs(c-7))<=2,$"Bot ignored local opening: ({r},{c})");
            return(r,c);
        }
        throw new MissingFeature("No bot stone");
    }
    static void Click(CaroBoardForm f,int row,int col)
    {
        // Raise real control event so broken Designer wiring also fails.
        typeof(Control).GetMethod("OnMouseClick",Private)!.Invoke(C<Panel>(f,"boardPanel"),
            [new MouseEventArgs(MouseButtons.Left,1,32+col*40,32+row*40,0)]);
    }
    static T C<T>(object f,string name) where T:Control => Field<T>(f,name);
    static T Field<T>(object f,string name)=>(T)(f.GetType().GetField(name,Private)?.GetValue(f)??throw new MissingFeature($"Missing {f.GetType().Name}.{name}"));
    static void Invoke(object f,string name,params object[] args)=>(f.GetType().GetMethod(name,Private)??throw new MissingFeature($"Missing {name}")).Invoke(f,args);
    static object? GetAuth(string name)=>Auth.GetProperty(name)!.GetValue(null);
    static void SetAuth(string name,object? value)=>Auth.GetProperty(name)!.SetValue(null,value);
    static void Check(bool condition,string message) { if(!condition) throw new MissingFeature(message); }
    sealed class MissingFeature(string message):Exception(message);
    static bool Same(int[,] a,int[,] b)=>a.Cast<int>().SequenceEqual(b.Cast<int>());
    static IEnumerable<Control> Children(Control f)=>f.Controls.Cast<Control>().SelectMany(c=>new[]{c}.Concat(Children(c)));
    static bool HasFive(int[,] b)
    {
        for(int r=0;r<15;r++) for(int c=0;c<15;c++)
            foreach(var(dr,dc) in new[]{(0,1),(1,0),(1,1),(1,-1)})
                if(Enumerable.Range(0,5).All(i=>r+i*dr is >=0 and <15 && c+i*dc is >=0 and <15 && b[r+i*dr,c+i*dc]==b[r,c])) return true;
        return false;
    }
    static void Capture(Form f,string name)
    {
        if(!capture) return;
        string directory=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../artifacts"));
        Directory.CreateDirectory(directory);
        f.Update();
        using var bitmap=new Bitmap(f.Width,f.Height);
        f.DrawToBitmap(bitmap,new Rectangle(Point.Empty,f.Size));
        string path=Path.Combine(directory,name+".png");
        bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine($"CAPTURE {path}");
    }
    static void CloseDialogs()
    {
        // Thread-local enumeration cannot touch another application's windows.
        EnumThreadWindows(GetCurrentThreadId(),(window,_)=>
        {
            var type=new StringBuilder(64);
            GetClassName(window,type,type.Capacity);
            if(type.ToString()=="#32770"&&IsWindowVisible(window))
            {
                var title=new StringBuilder(256);
                GetWindowText(window,title,title.Capacity);
                Dialogs.Add(title.ToString());
                PostMessage(window,0x0010,IntPtr.Zero,IntPtr.Zero);
            }
            return true;
        },IntPtr.Zero);
    }
    delegate bool WindowCallback(IntPtr window,IntPtr parameter);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern bool EnumThreadWindows(uint thread,WindowCallback callback,IntPtr parameter);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,StringBuilder value,int length);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr window,StringBuilder value,int length);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
}
