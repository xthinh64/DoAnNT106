using System.Text.Json;
using Xunit;

public sealed class Phase3EvidenceTests
{
    [Fact]
    public void PercentilesUseNearestRankAndReportActualSampleCount()
    {
        using var stats = JsonDocument.Parse(JsonSerializer.Serialize(Phase3BenchmarkTests.Stats(Enumerable.Range(1, 100).Select(i => (double)i))));
        Assert.Equal(100, stats.RootElement.GetProperty("count").GetInt32());
        Assert.Equal(50, stats.RootElement.GetProperty("p50").GetDouble());
        Assert.Equal(95, stats.RootElement.GetProperty("p95").GetDouble());
        Assert.Equal(99, stats.RootElement.GetProperty("p99").GetDouble());
        Assert.Equal(100, stats.RootElement.GetProperty("max").GetDouble());
    }
}
