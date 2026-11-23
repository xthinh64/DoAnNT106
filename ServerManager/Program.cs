namespace OnlineChessGame.ServerManager;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args is ["--game-worker", var workerPort, var workerCapacity])
        {
            OnlineChessGame.Server.GameWorker.RunAsync(int.Parse(workerPort, System.Globalization.CultureInfo.InvariantCulture),
                int.Parse(workerCapacity, System.Globalization.CultureInfo.InvariantCulture))
                .GetAwaiter().GetResult();
            return;
        }
        OnlineChessGame.Network.EnvironmentFile.LoadForServer();
        ApplicationConfiguration.Initialize();
        Application.Run(new ServerManagerForm());
    }
}
