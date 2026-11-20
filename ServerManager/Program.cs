namespace OnlineChessGame.ServerManager;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        OnlineChessGame.Network.EnvironmentFile.LoadForServer();
        ApplicationConfiguration.Initialize();
        Application.Run(new ServerManagerForm());
    }
}
