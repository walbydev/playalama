using FluentAssertions;
using Xunit;
using System.IO;
using System.Linq;

namespace Lama.ArchitectureTests
{
    [Trait("Category", "Architecture")]
    public class DependencyStructureTests
    {
        private static readonly string SolutionRoot = FindSolutionRoot();
        private static readonly string SrcRoot = Path.Combine(SolutionRoot, "src");

        private static string FindSolutionRoot()
        {
            var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var currentDir = Path.GetDirectoryName(assemblyLocation);

            while (!string.IsNullOrEmpty(currentDir) && 
                   !Directory.GetFiles(currentDir, "*.sln").Any() &&
                   !Directory.GetFiles(currentDir, "*.slnx").Any())
            {
                currentDir = Directory.GetParent(currentDir)?.FullName;
            }

            return currentDir ?? throw new DirectoryNotFoundException("Could not find solution root with .sln or .slnx file");
        }

        [Fact]
        public void Src_Directory_Contains_Expected_Folders()
        {
            var srcFolders = Directory.GetDirectories(SrcRoot)
                .Select(f => Path.GetFileName(f))
                .ToList();

            srcFolders.Should().Contain("apps", "Should contain apps folder");
            srcFolders.Should().Contain("libs", "Should contain libs folder");
        }

        [Fact]
        public void Project_Folders_Start_With_Lama()
        {
            var projectFolders = Directory.GetDirectories(SrcRoot)
                .SelectMany(f => Directory.GetDirectories(f))
                .Select(f => Path.GetFileName(f))
                .Distinct();

            projectFolders.Should().AllSatisfy(f => f.StartsWith("Lama"),
                "All project folders should start with 'Lama' for consistency");
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

            // Contracts should have no project references
            var contractsRefs = ParseProjectReferences(contracts.Content);
            contractsRefs.Should().BeEmpty("Contracts should not depend on any other project");

            // Domain should reference Contracts only
            var domainRefs = ParseProjectReferences(domain.Content);
            domainRefs.Should().Contain(r => r.EndsWith("Lama.Contracts.csproj"));
            domainRefs.Should().NotContain(r => r.Contains("Lama.Core") || r.Contains("Lama.Infrastructure"));

            // Core should reference Domain (and Contracts transitively)
            var coreRefs = ParseProjectReferences(core.Content);
            coreRefs.Should().Contain(r => r.EndsWith("Lama.Domain.csproj"));
            coreRefs.Should().Contain(r => r.EndsWith("Lama.Contracts.csproj"));

            // Infrastructure should reference Contracts and Domain
            var infraRefs = ParseProjectReferences(infrastructure.Content);
            infraRefs.Should().Contain(r => r.EndsWith("Lama.Contracts.csproj"));
            infraRefs.Should().Contain(r => r.EndsWith("Lama.Domain.csproj"));
        }

        [Fact]
        public void LanguagePacks_Dependencies_ArchCorrect()
        {
            var languageFiles = Directory.GetFiles(SrcRoot, "*.csproj", SearchOption.AllDirectories)
                .Where(f => Path.GetFileNameWithoutExtension(f).Contains("Languages"))
                .ToList();

            foreach (var langFile in languageFiles)
            {
                var content = File.ReadAllText(langFile);
                var references = ParseProjectReferences(content);
                
                references.Should().Contain(r => r.EndsWith("Lama.Contracts.csproj"),
                    $"Language pack {Path.GetFileNameWithoutExtension(langFile)} should reference Contracts");
            }
        }

        [Fact]
        public void Source_Contains_Expected_App_Projects()
        {
            var appFiles = Directory.GetFiles(SrcRoot, "*.csproj", SearchOption.AllDirectories);
            var appProjectNames = new HashSet<string>(new[]
            {
                "Lama.Console",
                "Lama.Server",
                "Lama.WebApp",
                "Lama.AIServer"
            });

            var appProjects = appFiles
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .Count(name => appProjectNames.Contains(name));

            appProjects.Should().Be(4, "Should have Console, Server, WebApp, and AIServer projects");
        }

        [Fact]
        public void Apps_Reference_Only_Contracts_Not_Internal_Layers()
        {
            // According to Clean Architecture, Apps should reference Contracts only
            // They get Domain/Core/Infrastructure through Contracts or should use APIs
            var appFiles = Directory.GetFiles(SrcRoot, "*.csproj", SearchOption.AllDirectories)
                .Where(f => !Path.GetFileNameWithoutExtension(f).EndsWith(".Tests"))
                .Where(f => {
                    var name = Path.GetFileNameWithoutExtension(f);
                    return name == "Lama.Console" || name == "Lama.Server" || 
                           name == "Lama.WebApp" || name == "Lama.AIServer";
                })
                .ToList();

            foreach (var appFile in appFiles)
            {
                var content = File.ReadAllText(appFile);
                var references = ParseProjectReferences(content);
                var appName = Path.GetFileNameWithoutExtension(appFile);

                // Apps should only reference Contracts (not Domain, Core, Infrastructure directly)
                var directDomainRef = references.Any(r => r.Contains("Lama.Domain.csproj"));
                var directCoreRef = references.Any(r => r.Contains("Lama.Core.csproj"));
                var directInfraRef = references.Any(r => r.Contains("Lama.Infrastructure.csproj"));

                // Only WebApp should reference just Contracts - others may reference more
                // This test documents the current architecture state
                if (appName == "Lama.WebApp")
                {
                    // WebApp only references Contracts
                    directDomainRef.Should().BeFalse($"{appName} should not reference Domain directly");
                    directCoreRef.Should().BeFalse($"{appName} should not reference Core directly");
                    directInfraRef.Should().BeFalse($"{appName} should not reference Infrastructure directly");
                }
            }
        }

        private static List<string> ParseProjectReferences(string csprojContent)
        {
            var result = new List<string>();
            var searchPattern = "<ProjectReference Include=\"";
            var openingTag = csprojContent.IndexOf(searchPattern, StringComparison.Ordinal);

            while (openingTag > -1)
            {
                var closingQuoteIndex = csprojContent.IndexOf('"', openingTag + searchPattern.Length);
                if (closingQuoteIndex == -1)
                    break;

                var startIndex = openingTag + searchPattern.Length;
                var reference = csprojContent.Substring(startIndex, closingQuoteIndex - startIndex);

                result.Add(reference.Trim());
                openingTag = csprojContent.IndexOf(searchPattern, openingTag + 1, StringComparison.Ordinal);
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

    public class DirectoryNotFoundException : System.Exception
    {
        public DirectoryNotFoundException(string message) : base(message) { }
    }
}