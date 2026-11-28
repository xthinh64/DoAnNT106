namespace OnlineChessGame.Server;

/// <summary>Internal app policy; neither FIDE nor RIF rating compliance.</summary>
public static class EloPolicy
{
    public const string Version = "internal-elo-k32-v1";
    public static int Delta(int own, int opponent, double score) =>
        checked((int)Math.Round(32 * (score - 1 / (1 + Math.Pow(10, ((double)opponent - own) / 400))),
            MidpointRounding.AwayFromZero));
}
