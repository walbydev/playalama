using System.Text.RegularExpressions;
using Lama.Contracts.Lexicon;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Lama.Infrastructure.Lexicon;

/// <summary>
/// Lecteur du lexique sur PostgreSQL (schéma lexicon). Charge les dictionnaires
/// en mémoire et fournit les définitions/synonymes à la demande.
/// </summary>
public sealed partial class PostgresLexiconReader(string connectionString, ILogger<PostgresLexiconReader>? logger = null)
    : ILexiconReader
{
    private readonly string _connectionString = connectionString;

    [GeneratedRegex("^[A-Z]+$")]
    private static partial Regex ScrabbleWord();

    public IReadOnlySet<string> LoadDictionary(string languageCode)
    {
        var words = new HashSet<string>();
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(
            "SELECT lemma_normalized FROM lexicon.words WHERE language_code = @lang", connection);
        command.Parameters.AddWithValue("lang", languageCode);
        command.CommandTimeout = 0;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var word = reader.GetString(0).ToUpperInvariant();
            if (ScrabbleWord().IsMatch(word))
                words.Add(word);
        }

        logger?.LogInformation("Lexicon {Lang}: {Count} mots chargés depuis Postgres.", languageCode, words.Count);
        return words;
    }

    public async Task<WordInfo?> GetWordInfoAsync(string languageCode, string word, CancellationToken cancellationToken = default)
    {
        var normalized = word.Trim().ToUpperInvariant();
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var wordCmd = new NpgsqlCommand(
            "SELECT word_id, lemma, wiktionary_url FROM lexicon.words " +
            "WHERE language_code = @lang AND lemma_normalized = @w LIMIT 1", connection);
        wordCmd.Parameters.AddWithValue("lang", languageCode);
        wordCmd.Parameters.AddWithValue("w", normalized);

        Guid wordId;
        string lemma;
        string? url;
        await using (var r = await wordCmd.ExecuteReaderAsync(cancellationToken))
        {
            if (!await r.ReadAsync(cancellationToken))
                return null;
            wordId = r.GetGuid(0);
            lemma = r.GetString(1);
            url = r.IsDBNull(2) ? null : r.GetString(2);
        }

        var defs = new List<WordDefinition>();
        await using (var defCmd = new NpgsqlCommand(
            "SELECT sense_index, part_of_speech, definition_text FROM lexicon.definitions " +
            "WHERE word_id = @id ORDER BY sense_index", connection))
        {
            defCmd.Parameters.AddWithValue("id", wordId);
            await using var r = await defCmd.ExecuteReaderAsync(cancellationToken);
            while (await r.ReadAsync(cancellationToken))
                defs.Add(new WordDefinition(r.GetInt32(0), r.IsDBNull(1) ? null : r.GetString(1), r.GetString(2)));
        }

        var syns = new List<string>();
        await using (var synCmd = new NpgsqlCommand(
            "SELECT DISTINCT synonym FROM lexicon.synonyms WHERE word_id = @id ORDER BY synonym", connection))
        {
            synCmd.Parameters.AddWithValue("id", wordId);
            await using var r = await synCmd.ExecuteReaderAsync(cancellationToken);
            while (await r.ReadAsync(cancellationToken))
                syns.Add(r.GetString(0));
        }

        return new WordInfo(lemma, languageCode, url, defs, syns);
    }
}
