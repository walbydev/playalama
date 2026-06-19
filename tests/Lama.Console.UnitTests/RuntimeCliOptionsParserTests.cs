using FluentAssertions;
using Lama.Console.Services;

namespace Lama.Console.UnitTests;

public sealed class RuntimeCliOptionsParserTests
{
    [Fact]
    public void Parse_ExtractsServerUrl_AndFiltersRuntimeOption()
    {
        var result = RuntimeCliOptionsParser.Parse([
            "game", "create", "Alice", "--server-url", "http://127.0.0.1:5055"
        ]);

        result.ErrorMessage.Should().BeNull();
        result.ServerUrl.Should().Be("http://127.0.0.1:5055");
        result.FilteredArgs.Should().BeEquivalentTo(["game", "create", "Alice"]);
    }

    [Fact]
    public void Parse_AcceptsServerIpAlias()
    {
        var result = RuntimeCliOptionsParser.Parse([
            "--server-ip", "https://game.playalama.online", "game", "list"
        ]);

        result.ErrorMessage.Should().BeNull();
        result.ServerUrl.Should().Be("https://game.playalama.online");
        result.FilteredArgs.Should().BeEquivalentTo(["game", "list"]);
    }

    [Fact]
    public void Parse_ReturnsError_WhenServerOptionHasNoValue()
    {
        var result = RuntimeCliOptionsParser.Parse(["game", "list", "--server-url"]);

        result.ErrorMessage.Should().NotBeNull();
        result.FilteredArgs.Should().BeEquivalentTo(["game", "list", "--server-url"]);
    }
}

