using OnlineChessGame.Auth;
using Xunit;

public class PasswordHasherTests
{
    [Theory]
    [InlineData("plaintext")]
    [InlineData("pbkdf2-sha256$v1$600000$bad$bad")]
    [InlineData("pbkdf2-sha256$v2$600000$AA==$AA==")]
    [InlineData("pbkdf2-sha256$v1$999999999$AA==$AA==")]
    public void RejectsUnrecognizedOrMalformedHashes(string encoded)
        => Assert.False(PasswordHasher.Verify("password", encoded));

    [Fact]
    public void HashIsSaltedVersionedAndVerifiesWithoutTrimming()
    {
        var hash = PasswordHasher.Hash(" password ");
        Assert.StartsWith("pbkdf2-sha256$v1$600000$", hash);
        Assert.InRange(hash.Length, 1, 255);
        Assert.NotEqual(hash, PasswordHasher.Hash(" password "));
        Assert.True(PasswordHasher.Verify(" password ", hash));
        Assert.False(PasswordHasher.Verify("password", hash));
        Assert.False(PasswordHasher.Verify("incorrect", hash));
    }
}
