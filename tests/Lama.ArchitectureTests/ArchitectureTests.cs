using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Lama.ArchitectureTests
{
    [Trait("Category", "Architecture")]
    public class DependencyStructureTests
    {
        private static readonly string SrcRoot = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "src");

        [Fact]
        public void Project_Folders_Start_With_Lama()
        {
            var srcDir = Path.Combine(SrcRoot, "src");
            var projectFolders = Directory.GetDirectories(srcDir)
                .Select(f => Path.GetFileName(f))
                .Distinct();

            projectFolders.Should().AllSatisfy(f => f.StartsWith("Lama"), "All project folders should start with 'Lama'");
        }

        [Fact]
        public void Root_Directory_Contains_Expected_Folders()
        {
            var srcParent = Path.GetDirectoryName(SrcRoot);
            if (string.IsNullOrEmpty(srcParent))
                return;

            var srcParentFolders = Directory.GetDirectories(srcParent);

            srcParentFolders.Should().Contain("apps", "Should contain apps folder");
            srcParentFolders.Should().Contain("libs", "Should contain libs folder");
            srcParentFolders.Should().Contain("tools", "Should contain tools folder");
        }

        [Fact]
        public void CleanArchitecture_Center_Dependencies_Directed_Inwards()
        {
            var projectFiles = Directory.GetFiles(SrcRoot, "*.csproj", SearchOption.AllDirectories)
                .Where(f => !Path.GetFileNameWithoutExtension(f).EndsWith(".Tests"))
                .Select(f => new TestProjectInfo
                {
                    FilePath = f,
                    Name = Path.GetFileNameWithoutExtension(f),
                    Content = File.ReadAllText(f)
                })
                .ToList();

            var contracts = projectFiles.Single(p => p.Name == "Lama.Contracts");
            var domain = projectFiles.Single(p => p.Name == "Lama.Domain");
            var core = projectFiles.Single(p => p.Name == "Lama.Core");
            var infrastructure = projectFiles.Single(p => p.Name == "Lama.Infrastructure");

            ValidateProjectStructure(contracts, new[] { "Lama.Domain", "Lama.Core", "Lama.Infrastructure" },
                "Contracts should not depend on any other project");

            ValidateProjectStructure(domain, Array.Empty<string>(), "Domain should only reference Contracts",
                new[] { "Lama.Contracts" });

            ValidateProjectStructure(core, new[] { "Lama.Infrastructure" },
                "Core should only reference Domain", new[] { "Lama.Domain" });

            ValidateProjectStructure(infrastructure, Array.Empty<string>(), "Infrastructure should reference Contracts and Domain",
                new[] { "Lama.Contracts", "Lama.Domain" });
        }

        [Fact]
        public void LanguagePacks_Dependencies_ArchCorrect()
        {
            var languageFiles = Directory.GetFiles(SrcRoot, "*.csproj", SearchOption.AllDirectories)
                .Where(f => Path.GetFileNameWithoutExtension(f).Contains("Languages"))
                .ToList();

            foreach (var langFile in languageFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(langFile);
                var projectInfo = new TestProjectInfo
                {
                    FilePath = langFile,
                    Name = fileName,
                    Content = File.ReadAllText(langFile)
                };

                var references = ParseProjectReferences(projectInfo.Content);
                references.Should().Contain(r => r.Contains("Lama.Contracts"),
                    $"Language pack {fileName} should reference Contracts");
            }
        }

        [Fact]
        public void Source_Contains_Expected_App_Projects()
        {
            var appProjects = Directory.GetFiles(SrcRoot, "*.csproj", SearchOption.AllDirectories)
                .Count(f =>
                    new[] {
                        "Lama.Console.csproj",
                        "Lama.Server.csproj",
                        "Lama.WebApp.csproj",
                        "Lama.AIServer.csproj"
                    }.Contains(Path.GetFileNameWithoutExtension(f)));

            appProjects.Should().BeGreaterThan(0, "Should have at least one App project");
        }

        [Fact]
        public void All_Apps_Should_Not_Reference_Domain_Core_Or_Infrastructure_Directly()
        {
            var appFiles = Directory.GetFiles(SrcRoot, "*.csproj", SearchOption.AllDirectories)
                .Where(f => !Path.GetFileNameWithoutExtension(f).EndsWith(".Tests"))
                .Where(f =>
                    new[] {
                        "Lama.Console",
                        "Lama.Server",
                        "Lama.WebApp",
                        "Lama.AIServer"
                    }.Any(prefix => Path.GetFileNameWithoutExtension(f).Contains(prefix)))
                .ToList();

            foreach (var appFile in appFiles)
            {
                var appContent = File.ReadAllText(appFile);
                var appName = Path.GetFileNameWithoutExtension(appFile);

                var references = ParseProjectReferences(appContent);

                var validReferences = references.Where(r =>
                    r.Contains("Lama.Domain") ||
                    r.Contains("Lama.Core") ||
                    r.Contains("Lama.Infrastructure") ||
                    r.Contains("Lama.Contracts"));

                validReferences.Should().BeEmpty(
                    $"App {appName} should not reference Domain/Core/Infrastructure directly");
            }
        }

        private static void ValidateProjectStructure(TestProjectInfo project, string[] forbiddenDeps, string failureMessage, string[]? shouldReference = null)
        {
            var references = ParseProjectReferences(project.Content);

            var forbiddenFound = references.Where(r => forbiddenDeps.Any(d => r.Contains(d)));
            forbiddenFound.Should().BeEmpty(failureMessage);

            if (shouldReference != null && shouldReference.Length > 0)
            {
                var requiredFound = references.Where(r => shouldReference.Any(d => r.Contains(d)));
                requiredFound.Should().NotBeEmpty(
                    $"Project {project.Name} should reference: [{string.Join(", ", shouldReference)}]");
            }
        }

        private static List<string> ParseProjectReferences(string csprojContent)
        {
            var result = new List<string>();
            var quote = '"';
            var openingTag = csprojContent.IndexOf("<ProjectReference Include=", StringComparison.Ordinal);

            while (openingTag > -1)
            {
                var closingQuoteIndex = csprojContent.IndexOf(quote, openingTag + 25);
                if (closingQuoteIndex == -1)
                    break;

                var startIndex = openingTag + 25;
                var reference = csprojContent.Substring(startIndex, closingQuoteIndex - startIndex);

                reference = reference.Replace(quote, "");
                result.Add(reference.Trim());
                openingTag = csprojContent.IndexOf("<ProjectReference Include=", openingTag + 1, StringComparison.Ordinal);
            }

            return result;
        }
    }

    public class TestProjectInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}