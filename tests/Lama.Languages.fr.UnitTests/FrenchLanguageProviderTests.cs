using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Lama.Languages.fr;

namespace Lama.Languages.fr.UnitTests
{
    /// <summary>
    /// Suite de tests unitaires pour FrenchLanguageProvider.
    /// Format: Arrange-Act-Assert (AAA)
    /// </summary>
    public class FrenchLanguageProviderTests
    {
        #region Helper Methods

        /// <summary>
        /// Crée un répertoire temporaire avec les fichiers assets nécessaires.
        /// </summary>
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

        /// <summary>
        /// Supprime le répertoire temporaire de manière sûre.
        /// </summary>
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
                // Ignorer les erreurs lors du nettoyage
            }
        }

        #endregion

        #region Language Metadata Tests

        [Fact]
        public void GetLanguageName_ReturnsFrench()
        {
            // Arrange
            var tmp = CreateTempBasePath("BONJOUR\nSALUT", "{ \"scores\": { \"A\": 1 } }");
            try
            {
                var provider = new FrenchLanguageProvider(tmp);

                // Act
                var name = provider.GetLanguageName();

                // Assert
                Assert.Equal("Français", name);
            }
            finally
            {
                SafeDeleteDirectory(tmp);
            }
        }

        [Fact]
        public void GetLocale_ReturnsFrenchFranceLocale()
        {
            // Arrange
            var tmp = CreateTempBasePath("BONJOUR\nSALUT", "{ \"scores\": { \"A\": 1 } }");
            try
            {
                var provider = new FrenchLanguageProvider(tmp);

                // Act
                var locale = provider.GetLocale();

                // Assert
                Assert.Equal("fr-FR", locale);
            }
            finally
            {
                SafeDeleteDirectory(tmp);
            }
        }

        #endregion

        #region Dictionary Loading Tests

        [Fact]
        public void GetDictionary_ReturnsImmutableSet()
        {
            // Arrange
            var tmp = CreateTempBasePath("BONJOUR\nSALUT", "{ \"scores\": { \"A\": 1 } }");
            try
            {
                var provider = new FrenchLanguageProvider(tmp);

                // Act
                var dictionary = provider.GetDictionary();

                // Assert
                Assert.NotNull(dictionary);
                Assert.IsAssignableFrom<IReadOnlySet<string>>(dictionary);
            }
            finally
            {
                SafeDeleteDirectory(tmp);
            }
        }

        [Fact]
        public void GetDictionary_ContainsValidWords()
        {
            // Arrange
            var tmp = CreateTempBasePath(
                "BONJOUR\nSALUT\nAMI",
                "{ \"scores\": { \"A\": 1 } }");
            try
            {
                var provider = new FrenchLanguageProvider(tmp);

                // Act
                var dictionary = provider.GetDictionary();

                // Assert
                Assert.Contains("BONJOUR", dictionary);
                Assert.Contains("SALUT", dictionary);
                Assert.Contains("AMI", dictionary);
                Assert.Equal(3, dictionary.Count);
            }
            finally
            {
                SafeDeleteDirectory(tmp);
            }
        }

        [Fact]
        public void LoadDictionary_ConvertsToUppercase()
        {
            // Arrange
            var tmp = CreateTempBasePath(
                "bonjour\nsAlUt\nAMI",
                "{ \"scores\": { \"A\": 1 } }");
            try
            {
                var provider = new FrenchLanguageProvider(tmp);

                // Act
                var dictionary = provider.GetDictionary();

                // Assert
                Assert.Contains("BONJOUR", dictionary);
                Assert.Contains("SALUT", dictionary);
                Assert.Contains("AMI", dictionary);
                Assert.DoesNotContain("bonjour", dictionary);
                Assert.DoesNotContain("sAlUt", dictionary);
            }
            finally
            {
                SafeDeleteDirectory(tmp);
            }
        }

        [Fact]
        public void LoadDictionary_TrimsWhitespace()
        {
            // Arrange
            var tmp = CreateTempBasePath(
                "  BONJOUR  \n\t SALUT \t\nAMI  ",
                "{ \"scores\": { \"A\": 1 } }");
            try
            {
                var provider = new FrenchLanguageProvider(tmp);

                // Act
                var dictionary = provider.GetDictionary();

                // Assert
                Assert.Contains("BONJOUR", dictionary);
                Assert.Contains("SALUT", dictionary);
                Assert.Contains("AMI", dictionary);
                Assert.Equal(3, dictionary.Count);
            }
            finally
            {
                SafeDeleteDirectory(tmp);
            }
        }

        [Fact]
        public void LoadDictionary_IgnoresEmptyLines()
        {
            // Arrange
            var tmp = CreateTempBasePath(
                "BONJOUR\n\n\nSALUT\n\n",
                "{ \"scores\": { \"A\": 1 } }");
            try
            {
                var provider = new FrenchLanguageProvider(tmp);

                // Act
                var dictionary = provider.GetDictionary();

                // Assert
                Assert.Contains("BONJOUR", dictionary);
                Assert.Contains("SALUT", dictionary);
                Assert.Equal(2, dictionary.Count);
            }
            finally
            {
                SafeDeleteDirectory(tmp);
            }
        }

        [Fact]
        public void LoadDictionary_IgnoresNonAlphabeticCharacters()
        {
            // Arrange
            var tmp = CreateTempBasePath(
                "BONJOUR\nABC123\n12345\nSALUT-AMIS\nÉCLAIR",
                "{ \"scores\": { \"A\": 1 } }");
            try
            {
                var provider = new FrenchLanguageProvider(tmp);

                // Act
                var dictionary = provider.GetDictionary();

                // Assert
                Assert.Contains("BONJOUR", dictionary);
                Assert.DoesNotContain("ABC123", dictionary);
                Assert.DoesNotContain("12345", dictionary);
                Assert.DoesNotContain("SALUT-AMIS", dictionary);
                Assert.DoesNotContain("ÉCLAIR", dictionary);
                Assert.Equal(1, dictionary.Count);
            }
            finally
            {
                SafeDeleteDirectory(tmp);
            }
        }

        [Fact]
        public void LoadDictionary_RemovesDuplicates()
        {
            // Arrange
            var tmp = CreateTempBasePath(
                "BONJOUR\nBONJOUR\nSALUT\nBONJOUR\nSALUT",
                "{ \"scores\": { \"A\": 1 } }");
            try
            {
                var provider = new FrenchLanguageProvider(tmp);

                // Act
                var dictionary = provider.GetDictionary();

                // Assert
                Assert.Equal(2, dictionary.Count);
                Assert.Contains("BONJOUR", dictionary);
                Assert.Contains("SALUT", dictionary);
            }
            finally
            {
                SafeDeleteDirectory(tmp);
            }
        }

        [Fact]
        public void LoadDictionary_HandlesEmptyDictionary()
        {
            // Arrange
            var tmp = CreateTempBasePath(
                "",
                "{ \"scores\": { \"A\": 1 } }");
            try
            {
                var provider = new FrenchLanguageProvider(tmp);

                // Act
                var dictionary = provider.GetDictionary();

                // Assert
                Assert.NotNull(dictionary);
                Assert.Empty(dictionary);
            }
            finally
            {
                SafeDeleteDirectory(tmp);
            }
        }

        #endregion

        #region Letter Scores Tests

        [Fact]
        public void GetLetterScores_ReturnsImmutableDictionary()
        {
            // Arrange
            var tmp = CreateTempBasePath(
                "BONJOUR",
                "{ \"scores\": { \"A\": 1 } }");
            try
            {
                var provider = new FrenchLanguageProvider(tmp);

                // Act
                var scores = provider.GetLetterScores();

                // Assert
                Assert.NotNull(scores);
                Assert.IsAssignableFrom<IReadOnlyDictionary<char, int>>(scores);
            }
            finally
            {
                SafeDeleteDirectory(tmp);
            }
        }

        [Fact]
        public void GetLetterScores_ContainsCorrectScores()
        {
            // Arrange
            var tmp = CreateTempBasePath(
                "BONJOUR",
                "{ \"scores\": { \"A\": 1, \"B\": 3, \"Z\": 10 } }");
            try
            {
                var provider = new FrenchLanguageProvider(tmp);

                // Act
                var scores = provider.GetLetterScores();

                // Assert
                Assert.Equal(1, scores['A']);
                Assert.Equal(3, scores['B']);
                Assert.Equal(10, scores['Z']);
            }
            finally
            {
                SafeDeleteDirectory(tmp);
            }
        }

        [Fact]
        public void LoadLetterScores_HandlesSingleCharacterKeys()
        {
            // Arrange
            var tmp = CreateTempBasePath(
                "BONJOUR",
                "{ \"scores\": { \"A\": 1, \"B\": 3, \"C\": 3, \"Z\": 10 } }");
            try
            {
                var provider = new FrenchLanguageProvider(tmp);

                // Act
                var scores = provider.GetLetterScores();

                // Assert
                Assert.Equal(4, scores.Count);
                Assert.Contains('A', scores.Keys);
                Assert.Contains('B', scores.Keys);
                Assert.Contains('C', scores.Keys);
                Assert.Contains('Z', scores.Keys);
            }
            finally
            {
                SafeDeleteDirectory(tmp);
            }
        }

        [Fact]
        public void LoadLetterScores_IgnoresMultiCharacterKeys()
        {
            // Arrange
            var tmp = CreateTempBasePath(
                "BONJOUR",
                "{ \"scores\": { \"A\": 1, \"B\": 3, \"AA\": 5, \"AB\": 7, \"Z\": 10 } }");
            try
            {
                var provider = new FrenchLanguageProvider(tmp);

                // Act
                var scores = provider.GetLetterScores();

                // Assert
                Assert.Equal(3, scores.Count);
                Assert.Equal(1, scores['A']);
                Assert.Equal(3, scores['B']);
                Assert.Equal(10, scores['Z']);
                Assert.False(scores.ContainsKey('A') && scores['A'] == 5); // Verify AA didn't override A
            }
            finally
            {
                SafeDeleteDirectory(tmp);
            }
        }

        [Fact]
        public void LoadLetterScores_HandlesCaseSensitivity()
        {
            // Arrange
            var tmp = CreateTempBasePath(
                "BONJOUR",
                "{ \"scores\": { \"A\": 1, \"a\": 2, \"B\": 3, \"b\": 4 } }");
            try
            {
                var provider = new FrenchLanguageProvider(tmp);

                // Act
                var scores = provider.GetLetterScores();

                // Assert
                Assert.Equal(4, scores.Count);
                Assert.Equal(1, scores['A']);
                Assert.Equal(2, scores['a']);
                Assert.Equal(3, scores['B']);
                Assert.Equal(4, scores['b']);
            }
            finally
            {
                SafeDeleteDirectory(tmp);
            }
        }

        [Fact]
        public void LoadLetterScores_HandlesFrenchCharacters()
        {
            // Arrange
            var tmp = CreateTempBasePath(
                "BONJOUR",
                "{ \"scores\": { \"É\": 5, \"È\": 4, \"Ê\": 3, \"Ç\": 6 } }");
            try
            {
                var provider = new FrenchLanguageProvider(tmp);

                // Act
                var scores = provider.GetLetterScores();

                // Assert
                Assert.Equal(4, scores.Count);
                Assert.Equal(5, scores['É']);
                Assert.Equal(4, scores['È']);
                Assert.Equal(3, scores['Ê']);
                Assert.Equal(6, scores['Ç']);
            }
            finally
            {
                SafeDeleteDirectory(tmp);
            }
        }

        [Fact]
        public void LoadLetterScores_HandlesEmptyScores()
        {
            // Arrange
            var tmp = CreateTempBasePath(
                "BONJOUR",
                "{ \"scores\": { } }");
            try
            {
                var provider = new FrenchLanguageProvider(tmp);

                // Act
                var scores = provider.GetLetterScores();

                // Assert
                Assert.NotNull(scores);
                Assert.Empty(scores);
            }
            finally
            {
                SafeDeleteDirectory(tmp);
            }
        }

        [Fact]
        public void LoadLetterScores_HandlesZeroScores()
        {
            // Arrange
            var tmp = CreateTempBasePath(
                "BONJOUR",
                "{ \"scores\": { \"A\": 0, \"B\": 0, \"Z\": 10 } }");
            try
            {
                var provider = new FrenchLanguageProvider(tmp);

                // Act
                var scores = provider.GetLetterScores();

                // Assert
                Assert.Equal(0, scores['A']);
                Assert.Equal(0, scores['B']);
                Assert.Equal(10, scores['Z']);
            }
            finally
            {
                SafeDeleteDirectory(tmp);
            }
        }

        [Fact]
        public void LoadLetterScores_HandlesHighScores()
        {
            // Arrange
            var tmp = CreateTempBasePath(
                "BONJOUR",
                "{ \"scores\": { \"A\": 1, \"Z\": 100, \"X\": 500 } }");
            try
            {
                var provider = new FrenchLanguageProvider(tmp);

                // Act
                var scores = provider.GetLetterScores();

                // Assert
                Assert.Equal(1, scores['A']);
                Assert.Equal(100, scores['Z']);
                Assert.Equal(500, scores['X']);
            }
            finally
            {
                SafeDeleteDirectory(tmp);
            }
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public void Constructor_ThrowsFileNotFoundExceptionWhenDictionaryMissing()
        {
            // Arrange
            var tmp = CreateTempBasePath(null, "{ \"scores\": { \"A\": 1 } }");

            try
            {
                // Act & Assert
                var exception = Assert.Throws<FileNotFoundException>(() => new FrenchLanguageProvider(tmp));
                Assert.Contains("dictionary", exception.Message, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                SafeDeleteDirectory(tmp);
            }
        }

        [Fact]
        public void Constructor_ThrowsFileNotFoundExceptionWhenScoresMissing()
        {
            // Arrange
            var tmp = CreateTempBasePath("BONJOUR\nSALUT", null);

            try
            {
                // Act & Assert
                var exception = Assert.Throws<FileNotFoundException>(() => new FrenchLanguageProvider(tmp));
                Assert.Contains("scores", exception.Message, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                SafeDeleteDirectory(tmp);
            }
        }

        [Fact]
        public void Constructor_ThrowsExceptionWhenJsonMalformed()
        {
            // Arrange
            var tmp = CreateTempBasePath(
                "BONJOUR",
                "{ \"scores\": { \"A\": 1, \"B\" } }"); // Missing value

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

        [Fact]
        public void Constructor_ThrowsExceptionWhenScoreValueIsNotInteger()
        {
            // Arrange
            var tmp = CreateTempBasePath(
                "BONJOUR",
                "{ \"scores\": { \"A\": 1, \"B\": \"three\" } }");

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

        [Fact]
        public void Constructor_ThrowsExceptionWhenScoreValueIsDecimal()
        {
            // Arrange
            var tmp = CreateTempBasePath(
                "BONJOUR",
                "{ \"scores\": { \"A\": 1.5, \"B\": 3 } }");

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

        [Fact]
        public void Constructor_ThrowsExceptionWhenScoreValueIsBoolean()
        {
            // Arrange
            var tmp = CreateTempBasePath(
                "BONJOUR",
                "{ \"scores\": { \"A\": true, \"B\": 3 } }");

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

        [Fact]
        public void Constructor_ThrowsExceptionWhenJsonMissingScoresProperty()
        {
            // Arrange
            var tmp = CreateTempBasePath(
                "BONJOUR",
                "{ \"letters\": { \"A\": 1, \"B\": 3 } }"); // Wrong property name

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

        #endregion

        #region Integration Tests

        [Fact]
        public void Constructor_WithCustomBasePath_LoadsFilesFromCorrectLocation()
        {
            // Arrange
            var tmp = CreateTempBasePath("BONJOUR\nSALUT", "{ \"scores\": { \"A\": 1, \"B\": 3 } }");
            try
            {
                var customPath = tmp;

                // Act
                var provider = new FrenchLanguageProvider(customPath);

                // Assert
                var dictionary = provider.GetDictionary();
                var scores = provider.GetLetterScores();
                Assert.Contains("BONJOUR", dictionary);
                Assert.Contains("SALUT", dictionary);
                Assert.Equal(1, scores['A']);
                Assert.Equal(3, scores['B']);
            }
            finally
            {
                SafeDeleteDirectory(tmp);
            }
        }

        [Fact]
        public void MultipleProviderInstances_LoadDataIndependently()
        {
            // Arrange
            var tmp1 = CreateTempBasePath("MOT1\nMOT2", "{ \"scores\": { \"A\": 1, \"B\": 3 } }");
            var tmp2 = CreateTempBasePath("MOT3\nMOT4", "{ \"scores\": { \"A\": 2, \"B\": 4 } }");

            try
            {
                // Act
                var provider1 = new FrenchLanguageProvider(tmp1);
                var provider2 = new FrenchLanguageProvider(tmp2);

                // Assert
                var dict1 = provider1.GetDictionary();
                var dict2 = provider2.GetDictionary();
                var scores1 = provider1.GetLetterScores();
                var scores2 = provider2.GetLetterScores();

                Assert.Contains("MOT1", dict1);
                Assert.DoesNotContain("MOT3", dict1);
                Assert.Contains("MOT3", dict2);
                Assert.DoesNotContain("MOT1", dict2);

                Assert.Equal(1, scores1['A']);
                Assert.Equal(2, scores2['A']);
            }
            finally
            {
                SafeDeleteDirectory(tmp1);
                SafeDeleteDirectory(tmp2);
            }
        }

        [Fact]
        public void AllPublicMethods_AreConsistent_WithInterface()
        {
            // Arrange
            var tmp = CreateTempBasePath("BONJOUR\nSALUT", "{ \"scores\": { \"A\": 1, \"B\": 3 } }");
            try
            {
                var provider = new FrenchLanguageProvider(tmp);

                // Act
                var dictionary = provider.GetDictionary();
                var scores = provider.GetLetterScores();
                var languageName = provider.GetLanguageName();
                var locale = provider.GetLocale();

                // Assert
                Assert.NotNull(dictionary);
                Assert.NotNull(scores);
                Assert.NotEmpty(languageName);
                Assert.NotEmpty(locale);
                Assert.IsAssignableFrom<IReadOnlySet<string>>(dictionary);
                Assert.IsAssignableFrom<IReadOnlyDictionary<char, int>>(scores);
            }
            finally
            {
                SafeDeleteDirectory(tmp);
            }
        }

        #endregion
    }
}

