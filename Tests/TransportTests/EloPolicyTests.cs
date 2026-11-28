using OnlineChessGame.Server;
using Xunit;

public class EloPolicyTests
{
    [Theory]
    [InlineData(1200,1200,1,16)]
    [InlineData(1200,1200,0.5,0)]
    [InlineData(1200,1200,0,-16)]
    [InlineData(1400,1200,1,8)]
    [InlineData(1400,1200,0.5,-8)]
    [InlineData(1400,1200,0,-24)]
    public void InternalPolicyComputesSingleZeroSumDelta(int own,int opponent,double score,int expected)
        => Assert.Equal(expected, EloPolicy.Delta(own,opponent,score));
}
