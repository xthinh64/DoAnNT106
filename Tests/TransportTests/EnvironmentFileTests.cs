using OnlineChessGame.Network;
using Xunit;

public sealed class EnvironmentFileTests
{
    [Fact]
    public void LoadsQuotedConnectionStringWithoutChangingAnExistingEnvironmentValue()
    {
        string newKey = "CHESS_ENV_TEST_" + Guid.NewGuid().ToString("N");
        string existingKey = "CHESS_ENV_TEST_" + Guid.NewGuid().ToString("N");
        string file = Path.GetTempFileName();
        try
        {
            Environment.SetEnvironmentVariable(existingKey, "configured-outside-file");
            File.WriteAllText(file,
                $"# local configuration\n{newKey}=\"Server=127.0.0.1;Password=some@secret;\"\n" +
                $"{existingKey}=ignored\n");

            EnvironmentFile.Load(file);

            Assert.Equal("Server=127.0.0.1;Password=some@secret;", Environment.GetEnvironmentVariable(newKey));
            Assert.Equal("configured-outside-file", Environment.GetEnvironmentVariable(existingKey));
        }
        finally
        {
            Environment.SetEnvironmentVariable(newKey, null);
            Environment.SetEnvironmentVariable(existingKey, null);
            File.Delete(file);
        }
    }

}
