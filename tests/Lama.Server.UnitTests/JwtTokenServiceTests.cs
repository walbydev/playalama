using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Lama.Server.Security;

namespace Lama.Server.UnitTests;

public class JwtTokenServiceTests
{
    private const string ValidSecret = "this-is-a-very-long-secret-key-for-testing-purposes-32+";

    private static JwtTokenService CreateSut(TimeSpan? expirationTime = null) =>
        new(ValidSecret, expirationTime: expirationTime);

    [Fact]
    public void Constructor_WithShortSecret_ThrowsArgumentException()
    {
        var act = () => new JwtTokenService("short");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithEmptySecret_ThrowsArgumentException()
    {
        var act = () => new JwtTokenService("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNullSecret_ThrowsArgumentException()
    {
        var act = () => new JwtTokenService(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GenerateToken_ReturnsNonEmptyString()
    {
        var sut = CreateSut();
        var token = sut.GenerateToken("player-1", "Alice");
        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateToken_WithEmptyPlayerId_Throws()
    {
        var sut = CreateSut();
        var act = () => sut.GenerateToken("", "Alice");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GenerateToken_WithEmptyPlayerName_Throws()
    {
        var sut = CreateSut();
        var act = () => sut.GenerateToken("p1", "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidateToken_WithValidToken_ReturnsPrincipal()
    {
        var sut = CreateSut();
        var token = sut.GenerateToken("player-1", "Alice");

        var principal = sut.ValidateToken(token);

        principal.Should().NotBeNull();
        principal!.Identity!.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public void ValidateToken_WithNullToken_ReturnsNull()
    {
        var sut = CreateSut();
        sut.ValidateToken(null!).Should().BeNull();
    }

    [Fact]
    public void ValidateToken_WithEmptyToken_ReturnsNull()
    {
        var sut = CreateSut();
        sut.ValidateToken("").Should().BeNull();
    }

    [Fact]
    public void ValidateToken_WithGarbageToken_ReturnsNull()
    {
        var sut = CreateSut();
        sut.ValidateToken("not.a.valid.token").Should().BeNull();
    }

    [Fact]
    public void ValidateToken_WithDifferentSecret_ReturnsNull()
    {
        var sut1 = new JwtTokenService("first-secret-key-that-is-long-enough-32-chars");
        var sut2 = new JwtTokenService("second-secret-key-that-is-long-enough-32");

        var token = sut1.GenerateToken("p1", "Alice");
        sut2.ValidateToken(token).Should().BeNull();
    }

    [Fact]
    public void ValidateToken_WithExpiredToken_ReturnsNull()
    {
        var sut = new JwtTokenService(ValidSecret, expirationTime: TimeSpan.FromMilliseconds(1));
        var token = sut.GenerateToken("p1", "Alice");

        Thread.Sleep(100);
        sut.ValidateToken(token).Should().BeNull();
    }

    [Fact]
    public void ExtractPlayerId_WithValidToken_ReturnsId()
    {
        var sut = CreateSut();
        var token = sut.GenerateToken("player-42", "Alice");

        sut.ExtractPlayerId(token).Should().Be("player-42");
    }

    [Fact]
    public void ExtractPlayerId_WithInvalidToken_ReturnsNull()
    {
        var sut = CreateSut();
        sut.ExtractPlayerId("invalid").Should().BeNull();
    }

    [Fact]
    public void ExtractPlayerName_WithValidToken_ReturnsName()
    {
        var sut = CreateSut();
        var token = sut.GenerateToken("p1", "BobThePlayer");

        sut.ExtractPlayerName(token).Should().Be("BobThePlayer");
    }

    [Fact]
    public void ExtractPlayerName_WithInvalidToken_ReturnsNull()
    {
        var sut = CreateSut();
        sut.ExtractPlayerName("invalid").Should().BeNull();
    }

    [Fact]
    public void GenerateThenValidate_RoundTrip_PreservesBothClaims()
    {
        var sut = CreateSut();
        var token = sut.GenerateToken("player-99", "Charlie");

        sut.ExtractPlayerId(token).Should().Be("player-99");
        sut.ExtractPlayerName(token).Should().Be("Charlie");
    }
}
