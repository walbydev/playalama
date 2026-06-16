using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using Lama.Languages.fr;

namespace Lama.Languages.fr.UnitTests
{
    public class FrenchLanguageProviderTests
    {
        // Helper to create a temporary base path with assets/dictionary.txt and assets/scores.json
        private static string CreateTempBasePath(string dictionaryContent, string scoresJsonContent)
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "LamaTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            var assetsDir = Path.Combine(tempRoot, "assets");
            Directory.CreateDirectory(assetsDir);

            if (dictionaryContent != null)
            {
                var dictPath = Path.Combine(assetsDir, "dictionary.txt");
                File.WriteAllText(dictPath, dictionaryContent);
            }

            if (scoresJsonContent != null)
            {
                var scoresPath = Path.Combine(assetsDir, "scores.json");
                File.WriteAllText(scoresPath, scoresJsonContent);
            }

            return tempRoot;
        }

        private static void SafeDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch
            {
                // ignore cleanup errors in tests
            }
        }

        [Fact]
        public void GetLanguageNameAndLocale_ReturnsExpected()
        {
            // Arrange
            var tmp = CreateTempBasePath("BONJOUR\nSALUT", "{ \"scores\": { \"A\": 1 } }");
            try
            {
                var provider = new FrenchLanguageProvider(tmp);

                // Act
                var name = provider.GetLanguageName();
                var locale = provider.GetLocale();

                // Assert
                Assert.Equal("Français", name);
                Assert.Equal("fr-FR", locale);
            }
            finally
            {
                SafeDeleteDirectory(tmp);
            }
        }

        [Fact]
        public void LoadDictionary_ParsesAndFiltersWords_ReturnsUppercaseUniqueWords()
        {
            // Arrange
            var dictionaryContent = string.Join(
                Environment.NewLine,
                new[]
                {
                    "bonjour",
                    "  salut  ",
                    "",
                    "abc123",
                    "éclair",
                    "BONJOUR"
                });
            var tmp = CreateTempBasePath(dictionaryContent, "{ \"scores\": { \"A\": 1 } }");

            try
            {
                var provider = new FrenchLanguageProvider(tmp);

                // Act
                var dict = provider.GetDictionary();

                // Assert
                Assert.Contains("BONJOUR", dict);
                Assert.Contains("SALUT", dict);

                Assert.DoesNotContain("ABC123", dict);
                Assert.DoesNotContain("ÉCLAIR", dict);

                Assert.Equal(2, dict.Count);
            }
            finally
            {
                SafeDeleteDirectory(tmp);
            }
        }

        [Fact]
        public void LoadLetterScores_ReadsSingleCharKeys_IgnoresMultiCharKeys()
        {
            // Arrange
            var scoresJson = "{ \"scores\": { \"A\": 1, \"B\": 3, \"Z\": 10, \"a\": 2, \"AA\": 5 } }";
            var tmp = CreateTempBasePath("BONJOUR", scoresJson);

            try
            {
                var provider = new FrenchLanguageProvider(tmp);

                // Act
                var scores = provider.GetLetterScores();

                // Assert
                Assert.True(scores.ContainsKey('A'));
                Assert.Equal(1, scores['A']);

                Assert.True(scores.ContainsKey('B'));
                Assert.Equal(3, scores['B']);

                Assert.True(scores.ContainsKey('Z'));
                Assert.Equal(10, scores['Z']);

                Assert.True(scores.ContainsKey('a'));
                Assert.Equal(2, scores['a']);

                // Ensure multi-char key didn't override 'A'
                Assert.Equal(1, scores['A']);
            }
            finally
            {
                SafeDeleteDirectory(tmp);
            }
        }

        [Fact]
        public void Constructor_ThrowsWhenDictionaryMissing()
        {
            // Arrange
            var tmp = CreateTempBasePath(null, "{ \"scores\": { \"A\": 1 } }");

            try
            {
                // Act & Assert
                Assert.Throws<FileNotFoundException>(() => new FrenchLanguageProvider(tmp));
            }
            finally
            {
                SafeDeleteDirectory(tmp);
            }
        }

        [Fact]
        public void Constructor_ThrowsWhenScoresMissing()
        {
            // Arrange
            var tmp = CreateTempBasePath("BONJOUR\nSALUT", null);

            try
            {
                // Act & Assert
                Assert.Throws<FileNotFoundException>(() => new FrenchLanguageProvider(tmp));
            }
            finally
            {
                SafeDeleteDirectory(tmp);
            }
        }

        [Fact]
        public void LoadLetterScores_ThrowsIfScoreValueNotInt()
        {
            // Arrange
            var scoresJson = "{ \"scores\": { \"A\": 1, \"B\": \"three\" } }";
            var tmp = CreateTempBasePath("BONJOUR", scoresJson);

            try
            {
                // Act & Assert
                Assert.ThrowsAny<Exception>(() => new FrenchLanguageProvider(tmp));
            }
            finally
            {
                SafeDeleteDirectory(tmp);
            }
        }
    }
}

