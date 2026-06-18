using FluentAssertions;
using Lama.Contracts;
using Lama.Infrastructure.Profile;
using Lama.Infrastructure.UnitTests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lama.Infrastructure.UnitTests;

[Collection(SequentialTestCollection.Name)]
public class PlayerProfileServiceTests : IDisposable
{
    private readonly TempDirectory _tempDir;
    private readonly JsonPlayerProfileService _sut;

    public PlayerProfileServiceTests()
    {
        _tempDir = new TempDirectory();
        Environment.SetEnvironmentVariable("LAMA_SESSION_DIR", _tempDir.Path);
        _sut = new JsonPlayerProfileService(NullLogger<JsonPlayerProfileService>.Instance);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("LAMA_SESSION_DIR", null);
        _tempDir.Dispose();
    }

    [Fact]
    public async Task SaveAndGetById_PersistsAllFields()
    {
        var profile = new PlayerProfile(
            PlayerId: "p1",
            DisplayName: "Alice",
            Pseudo: "LamaQueen",
            Country: "FR",
            Region: "Occitanie",
            BirthYear: 1997,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow);

        await _sut.SaveAsync(profile);
        var loaded = await _sut.GetByIdAsync("p1");

        loaded.Should().NotBeNull();
        loaded!.DisplayName.Should().Be("Alice");
        loaded.Pseudo.Should().Be("LamaQueen");
        loaded.Country.Should().Be("FR");
        loaded.Region.Should().Be("Occitanie");
        loaded.BirthYear.Should().Be(1997);
    }

    [Fact]
    public async Task SaveAsync_Update_KeptCreatedAt_AndRefreshUpdatedAt()
    {
        var first = await _sut.SaveAsync(new PlayerProfile(
            PlayerId: "p2",
            DisplayName: "Bob",
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow));

        await Task.Delay(5);

        var second = await _sut.SaveAsync(first with
        {
            DisplayName = "Bob Updated",
            Country = "CA"
        });

        second.CreatedAt.Should().Be(first.CreatedAt);
        second.UpdatedAt.Should().BeAfter(first.UpdatedAt);
        second.DisplayName.Should().Be("Bob Updated");
        second.Country.Should().Be("CA");
    }

    [Fact]
    public async Task SaveAsync_InvalidBirthYear_Throws()
    {
        var act = async () => await _sut.SaveAsync(new PlayerProfile(
            PlayerId: "p3",
            DisplayName: "Bad",
            BirthYear: 1800,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow));

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task ListAsync_ReturnsOrderedByDisplayName()
    {
        await _sut.SaveAsync(new PlayerProfile("p3", "Zoe", CreatedAt: DateTimeOffset.UtcNow, UpdatedAt: DateTimeOffset.UtcNow));
        await _sut.SaveAsync(new PlayerProfile("p4", "Alice", CreatedAt: DateTimeOffset.UtcNow, UpdatedAt: DateTimeOffset.UtcNow));

        var list = await _sut.ListAsync();

        list.Should().HaveCount(2);
        list[0].DisplayName.Should().Be("Alice");
        list[1].DisplayName.Should().Be("Zoe");
    }
}

