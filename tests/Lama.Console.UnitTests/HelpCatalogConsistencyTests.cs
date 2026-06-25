using System.Text.RegularExpressions;
using FluentAssertions;
using Lama.Console.Services;

namespace Lama.Console.UnitTests;

public sealed class HelpCatalogConsistencyTests
{
    [Fact]
    public void HelpCatalog_CoversAllCommandsRegisteredInProgram()
    {
        var repoRoot = FindRepoRoot();
        var programPath = Path.Combine(repoRoot, "src", "apps", "Lama.Console", "Program.cs");
        var commandsPath = Path.Combine(repoRoot, "src", "apps", "Lama.Console", "Commands");

        var registeredTypeNames = ParseRegisteredCommandTypes(programPath);
        var commandIdByTypeName = ParseCommandIdsByType(commandsPath);

        var missingTypeDefinitions = registeredTypeNames
            .Where(typeName => !commandIdByTypeName.ContainsKey(typeName))
            .ToList();

        missingTypeDefinitions.Should().BeEmpty(
            "chaque commande enregistree dans Program.cs doit exposer un CommandId lisible");

        var registeredCommandIds = registeredTypeNames
            .Select(typeName => commandIdByTypeName[typeName])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var helpCommandIds = HelpCatalog.Commands
            .Select(c => c.CommandId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        helpCommandIds.Should().OnlyHaveUniqueItems();
        registeredCommandIds.Should().BeEquivalentTo(helpCommandIds,
            "HelpCatalog doit rester synchronise avec les commandes enregistrees dans Program.cs");
    }

    [Fact]
    public void HelpCatalog_CommandResolvers_AreConsistent()
    {
        foreach (var command in HelpCatalog.Commands)
        {
            if (command.CommandId.Contains('.'))
            {
                var resolved = HelpCatalog.TryGetCommand(command.Group, command.ActionPath, out var byPath);
                resolved.Should().BeTrue();
                byPath!.CommandId.Should().Be(command.CommandId);
            }
            else
            {
                var resolved = HelpCatalog.TryGetSingleLevelCommand(command.CommandId, out var byId);
                resolved.Should().BeTrue();
                byId!.CommandId.Should().Be(command.CommandId);
            }
        }
    }

    private static List<string> ParseRegisteredCommandTypes(string programPath)
    {
        var content = File.ReadAllText(programPath);
        var matches = Regex.Matches(content, @"AddSingleton<ICommand,\s*(?<type>[\w\.]+)>");

        return matches
            .Select(m => m.Groups["type"].Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Split('.').Last())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static Dictionary<string, string> ParseCommandIdsByType(string commandsRoot)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var files = Directory.GetFiles(commandsRoot, "*.cs", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            var classMatch = Regex.Match(content, @"class\s+(?<class>\w+)\s*:\s*ICommand");
            var commandIdMatch = Regex.Match(content, @"CommandId\s*=>\s*""(?<id>[^""]+)""");

            if (!classMatch.Success || !commandIdMatch.Success)
                continue;

            var className = classMatch.Groups["class"].Value;
            var commandId = commandIdMatch.Groups["id"].Value;
            result[className] = commandId;
        }

        return result;
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Lama.slnx")))
                return current.FullName;
            current = current.Parent;
        }

        throw new InvalidOperationException("Impossible de localiser la racine du repository.");
    }
}


