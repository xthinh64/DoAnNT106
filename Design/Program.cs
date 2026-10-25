namespace OnlineChessGame;

public class ChessApplicationContext : ApplicationContext
{
    public static ChessApplicationContext Current { get; private set; } = null!;

    public ChessApplicationContext()
    {
        Current = this;
        SwitchTo(new LoginForm());
    }

    public void SwitchTo(Form newForm)
    {
        var prev = MainForm;
        MainForm = newForm;
        newForm.FormClosed += (s, e) =>
        {
            if (MainForm == newForm)
            {
                ExitThread();
            }
        };
        newForm.Show();
        prev?.Hide();
        prev?.Dispose();
    }
}

static class Program
{
    public static ChessApplicationContext Context => ChessApplicationContext.Current;

    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new ChessApplicationContext());
    }    
}