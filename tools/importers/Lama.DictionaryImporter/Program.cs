using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

var options = ImportOptions.Parse(args);

if (!File.Exists(options.InputPath))
{
    Console.Error.WriteLine($"Input file not found: {options.InputPath}");
    return 2;
}

var fileSha256 = ComputeSha256(options.InputPath);
var importOptionsJson = JsonSerializer.Serialize(new
{
    includeDefinitions = options.IncludeDefinitions,
    includeSynonyms = options.IncludeSynonyms,
    scrabbleOnly = options.ScrabbleOnly,
    clearLanguageBeforeImport = options.ClearLanguageBeforeImport
});

await using var connection = new NpgsqlConnection(options.ConnectionString);
await connection.OpenAsync();

if (!options.DryRun)
{
    if (options.ClearLanguageBeforeImport)
    {
        await ClearLanguageDataAsync(connection, options);
    }
    else
    {
        await EnsureNoCompletedRunForSameFingerprintAsync(connection, options, fileSha256, importOptionsJson);
    }
}

var runId = Guid.NewGuid();
if (!options.DryRun)
{
    await InsertRunAsync(connection, runId, options, fileSha256, importOptionsJson);
}

var stopwatch = System.Diagnostics.Stopwatch.StartNew();
var wordsCount = 0L;
var definitionsCount = 0L;
var synonymsCount = 0L;
var skippedCount = 0L;
var parseErrorCount = 0L;
var lineNumber = 0;
var processedSinceLastCommit = 0;
var lastHeartbeatLine = 0;

NpgsqlTransaction? transaction = null;
var wordCommand = default(NpgsqlCommand);
var definitionCommand = default(NpgsqlCommand);
var synonymCommand = default(NpgsqlCommand);

if (!options.DryRun)
{
    transaction = await connection.BeginTransactionAsync();
    (wordCommand, definitionCommand, synonymCommand) = BuildCommands(connection, transaction);
}

try
{
    using var stream = File.OpenRead(options.InputPath);
    using var reader = new StreamReader(stream, Encoding.UTF8);

    while (await reader.ReadLineAsync() is { } line)
    {
        lineNumber++;
        if (options.ProgressEveryLines > 0 &&
            lineNumber - lastHeartbeatLine >= options.ProgressEveryLines)
        {
            var elapsedSeconds = Math.Max(1.0, stopwatch.Elapsed.TotalSeconds);
            var linesPerSecond = lineNumber / elapsedSeconds;
            Console.WriteLine(
                $"Heartbeat line={lineNumber} words={wordsCount} definitions={definitionsCount} synonyms={synonymsCount} skipped={skippedCount} rate={linesPerSecond:F0} lines/s");
            lastHeartbeatLine = lineNumber;
        }

        if (string.IsNullOrWhiteSpace(line))
        {
            skippedCount++;
            continue;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException ex)
        {
            parseErrorCount++;
            if (options.FailOnError)
            {
                throw new InvalidOperationException($"JSON parse error at line {lineNumber}: {ex.Message}", ex);
            }

            Console.Error.WriteLine($"[WARN] Skipping malformed JSON line {lineNumber}: {ex.Message}");
            continue;
        }

        using (document)
        {
            var root = document.RootElement;
            var word = ExtractWord(root);
            if (string.IsNullOrWhiteSpace(word))
            {
                skippedCount++;
                continue;
            }

            if (!MatchesRequestedLanguage(root, options.LanguageCode))
            {
                skippedCount++;
                continue;
            }

            if (options.ScrabbleOnly && !IsScrabblePlayableWord(word))
            {
                skippedCount++;
                continue;
            }

            var normalizedLemma = NormalizeForLookup(word);
            if (string.IsNullOrWhiteSpace(normalizedLemma))
            {
                skippedCount++;
                continue;
            }

            var wiktionaryUrl = ExtractWiktionaryUrl(root);
            var sourceEntryId = ExtractSourceEntryId(root);
            var partOfSpeech = TryGetString(root, "pos");

            Guid? wordId = null;
            if (!options.DryRun)
            {
                if (wordCommand is null)
                {
                    throw new InvalidOperationException("Word command is not initialized.");
                }

                wordCommand.Parameters["language_code"].Value = options.LanguageCode;
                wordCommand.Parameters["lemma"].Value = word;
                wordCommand.Parameters["lemma_normalized"].Value = normalizedLemma;
                wordCommand.Parameters["length"].Value = word.Length;
                wordCommand.Parameters["wiktionary_url"].Value = (object?)wiktionaryUrl ?? DBNull.Value;
                wordCommand.Parameters["source"].Value = options.Source;
                wordCommand.Parameters["source_entry_id"].Value = (object?)sourceEntryId ?? DBNull.Value;
                var insertedWordId = await wordCommand.ExecuteScalarAsync();
                wordId = insertedWordId is Guid g ? g : throw new InvalidOperationException("Upsert did not return word_id.");
            }

            wordsCount++;

            if (options.IncludeDefinitions)
            {
                var definitions = ExtractDefinitions(root);
                for (var i = 0; i < definitions.Count; i++)
                {
                    var definition = definitions[i];
                    if (string.IsNullOrWhiteSpace(definition))
                    {
                        continue;
                    }

                    if (!options.DryRun)
                    {
                        if (definitionCommand is null || wordId is null)
                        {
                            throw new InvalidOperationException("Definition command is not initialized.");
                        }

                        definitionCommand.Parameters["word_id"].Value = wordId.Value;
                        definitionCommand.Parameters["sense_index"].Value = i;
                        definitionCommand.Parameters["part_of_speech"].Value = (object?)partOfSpeech ?? DBNull.Value;
                        definitionCommand.Parameters["definition_text"].Value = definition;
                        await definitionCommand.ExecuteNonQueryAsync();
                    }

                    definitionsCount++;
                }
            }

            if (options.IncludeSynonyms)
            {
                var synonymsBySense = ExtractSynonymsBySense(root);
                foreach (var (senseIndex, synonyms) in synonymsBySense)
                {
                    foreach (var synonym in synonyms)
                    {
                        if (string.IsNullOrWhiteSpace(synonym))
                        {
                            continue;
                        }

                        var normalizedSynonym = NormalizeForLookup(synonym);
                        if (string.IsNullOrWhiteSpace(normalizedSynonym))
                        {
                            continue;
                        }

                        if (!options.DryRun)
                        {
                            if (synonymCommand is null || wordId is null)
                            {
                                throw new InvalidOperationException("Synonym command is not initialized.");
                            }

                            synonymCommand.Parameters["word_id"].Value = wordId.Value;
                            synonymCommand.Parameters["sense_index"].Value = senseIndex;
                            synonymCommand.Parameters["synonym"].Value = synonym;
                            synonymCommand.Parameters["synonym_normalized"].Value = normalizedSynonym;
                            await synonymCommand.ExecuteNonQueryAsync();
                        }

                        synonymsCount++;
                    }
                }
            }

            processedSinceLastCommit++;
            if (!options.DryRun && processedSinceLastCommit >= options.BatchSize)
            {
                if (transaction is null)
                {
                    throw new InvalidOperationException("Transaction is not initialized.");
                }

                await CommitBatchAsync(transaction);
                transaction = await connection.BeginTransactionAsync();
                AttachTransaction(wordCommand, definitionCommand, synonymCommand, transaction);
                processedSinceLastCommit = 0;

                Console.WriteLine(
                    $"Progress line={lineNumber} words={wordsCount} definitions={definitionsCount} synonyms={synonymsCount} skipped={skippedCount}");
            }
        }
    }

    if (!options.DryRun && transaction is not null)
    {
        await CommitBatchAsync(transaction);
    }

    if (!options.DryRun)
    {
        await UpdateRunSuccessAsync(connection, runId, wordsCount, definitionsCount, synonymsCount);
    }
}
catch (Exception ex)
{
    if (!options.DryRun)
    {
        await UpdateRunFailureAsync(connection, runId, ex.Message);
    }

    throw;
}
finally
{
    stopwatch.Stop();
}

Console.WriteLine(
    $"Done dryRun={options.DryRun} language={options.LanguageCode} words={wordsCount} definitions={definitionsCount} synonyms={synonymsCount} skipped={skippedCount} parseErrors={parseErrorCount} sha256={fileSha256} elapsed={stopwatch.Elapsed}");
return 0;

static async Task EnsureNoCompletedRunForSameFingerprintAsync(
    NpgsqlConnection connection,
    ImportOptions options,
    string sha256,
    string optionsJson)
{
    const string sql = """
        SELECT run_id
        FROM lexicon.import_runs
        WHERE environment = @environment
          AND language_code = @language_code
          AND sha256 = @sha256
          AND options = @options::jsonb
          AND status = 'completed'
        LIMIT 1;
        """;

    await using var command = new NpgsqlCommand(sql, connection);
    command.Parameters.AddWithValue("environment", options.Environment);
    command.Parameters.AddWithValue("language_code", options.LanguageCode);
    command.Parameters.AddWithValue("sha256", sha256);
    command.Parameters.AddWithValue("options", optionsJson);
    var existing = await command.ExecuteScalarAsync();
    if (existing is not null)
    {
        throw new InvalidOperationException(
            $"Import already completed for env={options.Environment} lang={options.LanguageCode} sha256={sha256}.");
    }
}

static async Task ClearLanguageDataAsync(NpgsqlConnection connection, ImportOptions options)
{
    const string sql = """
        DELETE FROM lexicon.import_runs
        WHERE environment = @environment AND language_code = @language_code;

        DELETE FROM lexicon.words
        WHERE language_code = @language_code;
        """;

    await using var command = new NpgsqlCommand(sql, connection);
    command.CommandTimeout = 0;
    command.Parameters.AddWithValue("environment", options.Environment);
    command.Parameters.AddWithValue("language_code", options.LanguageCode);
    await command.ExecuteNonQueryAsync();
}

static async Task InsertRunAsync(
    NpgsqlConnection connection,
    Guid runId,
    ImportOptions options,
    string sha256,
    string optionsJson)
{
    const string sql = """
        INSERT INTO lexicon.import_runs
            (run_id, environment, language_code, source_file, sha256, options, status, started_at)
        VALUES
            (@run_id, @environment, @language_code, @source_file, @sha256, @options::jsonb, 'running', NOW());
        """;
    await using var command = new NpgsqlCommand(sql, connection);
    command.Parameters.AddWithValue("run_id", runId);
    command.Parameters.AddWithValue("environment", options.Environment);
    command.Parameters.AddWithValue("language_code", options.LanguageCode);
    command.Parameters.AddWithValue("source_file", options.InputPath);
    command.Parameters.AddWithValue("sha256", sha256);
    command.Parameters.AddWithValue("options", optionsJson);
    await command.ExecuteNonQueryAsync();
}

static async Task UpdateRunSuccessAsync(
    NpgsqlConnection connection,
    Guid runId,
    long wordsCount,
    long definitionsCount,
    long synonymsCount)
{
    const string sql = """
        UPDATE lexicon.import_runs
        SET status = 'completed',
            completed_at = NOW(),
            words_count = @words_count,
            definitions_count = @definitions_count,
            synonyms_count = @synonyms_count,
            error_message = NULL
        WHERE run_id = @run_id;
        """;
    await using var command = new NpgsqlCommand(sql, connection);
    command.Parameters.AddWithValue("run_id", runId);
    command.Parameters.AddWithValue("words_count", wordsCount);
    command.Parameters.AddWithValue("definitions_count", definitionsCount);
    command.Parameters.AddWithValue("synonyms_count", synonymsCount);
    await command.ExecuteNonQueryAsync();
}

static async Task UpdateRunFailureAsync(NpgsqlConnection connection, Guid runId, string errorMessage)
{
    const string sql = """
        UPDATE lexicon.import_runs
        SET status = 'failed',
            completed_at = NOW(),
            error_message = LEFT(@error_message, 2000)
        WHERE run_id = @run_id;
        """;
    await using var command = new NpgsqlCommand(sql, connection);
    command.Parameters.AddWithValue("run_id", runId);
    command.Parameters.AddWithValue("error_message", errorMessage);
    await command.ExecuteNonQueryAsync();
}

static (NpgsqlCommand Word, NpgsqlCommand Definition, NpgsqlCommand Synonym) BuildCommands(
    NpgsqlConnection connection,
    NpgsqlTransaction transaction)
{
    var wordSql = """
        INSERT INTO lexicon.words
            (language_code, lemma, lemma_normalized, length, wiktionary_url, source, source_entry_id, created_at, updated_at)
        VALUES
            (@language_code, @lemma, @lemma_normalized, @length, @wiktionary_url, @source, @source_entry_id, NOW(), NOW())
        ON CONFLICT (language_code, lemma_normalized)
        DO UPDATE SET
            lemma = EXCLUDED.lemma,
            length = EXCLUDED.length,
            wiktionary_url = COALESCE(EXCLUDED.wiktionary_url, lexicon.words.wiktionary_url),
            source = EXCLUDED.source,
            source_entry_id = COALESCE(EXCLUDED.source_entry_id, lexicon.words.source_entry_id),
            updated_at = NOW()
        RETURNING word_id;
        """;
    var word = new NpgsqlCommand(wordSql, connection, transaction);
    word.Parameters.Add(new NpgsqlParameter("language_code", NpgsqlTypes.NpgsqlDbType.Varchar));
    word.Parameters.Add(new NpgsqlParameter("lemma", NpgsqlTypes.NpgsqlDbType.Text));
    word.Parameters.Add(new NpgsqlParameter("lemma_normalized", NpgsqlTypes.NpgsqlDbType.Text));
    word.Parameters.Add(new NpgsqlParameter("length", NpgsqlTypes.NpgsqlDbType.Integer));
    word.Parameters.Add(new NpgsqlParameter("wiktionary_url", NpgsqlTypes.NpgsqlDbType.Text));
    word.Parameters.Add(new NpgsqlParameter("source", NpgsqlTypes.NpgsqlDbType.Varchar));
    word.Parameters.Add(new NpgsqlParameter("source_entry_id", NpgsqlTypes.NpgsqlDbType.Text));

    var definitionSql = """
        INSERT INTO lexicon.definitions
            (word_id, sense_index, part_of_speech, definition_text, created_at)
        VALUES
            (@word_id, @sense_index, @part_of_speech, @definition_text, NOW())
        ON CONFLICT (word_id, sense_index, definition_text) DO NOTHING;
        """;
    var definition = new NpgsqlCommand(definitionSql, connection, transaction);
    definition.Parameters.Add(new NpgsqlParameter("word_id", NpgsqlTypes.NpgsqlDbType.Uuid));
    definition.Parameters.Add(new NpgsqlParameter("sense_index", NpgsqlTypes.NpgsqlDbType.Integer));
    definition.Parameters.Add(new NpgsqlParameter("part_of_speech", NpgsqlTypes.NpgsqlDbType.Varchar));
    definition.Parameters.Add(new NpgsqlParameter("definition_text", NpgsqlTypes.NpgsqlDbType.Text));

    var synonymSql = """
        INSERT INTO lexicon.synonyms
            (word_id, sense_index, synonym, synonym_normalized, created_at)
        VALUES
            (@word_id, @sense_index, @synonym, @synonym_normalized, NOW())
        ON CONFLICT (word_id, sense_index, synonym_normalized) DO NOTHING;
        """;
    var synonym = new NpgsqlCommand(synonymSql, connection, transaction);
    synonym.Parameters.Add(new NpgsqlParameter("word_id", NpgsqlTypes.NpgsqlDbType.Uuid));
    synonym.Parameters.Add(new NpgsqlParameter("sense_index", NpgsqlTypes.NpgsqlDbType.Integer));
    synonym.Parameters.Add(new NpgsqlParameter("synonym", NpgsqlTypes.NpgsqlDbType.Text));
    synonym.Parameters.Add(new NpgsqlParameter("synonym_normalized", NpgsqlTypes.NpgsqlDbType.Text));

    return (word, definition, synonym);
}

static void AttachTransaction(
    NpgsqlCommand? wordCommand,
    NpgsqlCommand? definitionCommand,
    NpgsqlCommand? synonymCommand,
    NpgsqlTransaction transaction)
{
    if (wordCommand is not null)
    {
        wordCommand.Transaction = transaction;
    }

    if (definitionCommand is not null)
    {
        definitionCommand.Transaction = transaction;
    }

    if (synonymCommand is not null)
    {
        synonymCommand.Transaction = transaction;
    }
}

static async Task CommitBatchAsync(NpgsqlTransaction transaction)
{
    await transaction.CommitAsync();
}

static string ComputeSha256(string path)
{
    using var sha = SHA256.Create();
    using var stream = File.OpenRead(path);
    var hash = sha.ComputeHash(stream);
    return Convert.ToHexString(hash).ToLowerInvariant();
}

static bool MatchesRequestedLanguage(JsonElement root, string requestedLanguage)
{
    var langCode = TryGetString(root, "lang_code");
    if (string.IsNullOrWhiteSpace(langCode))
    {
        return true;
    }

    return string.Equals(langCode, requestedLanguage, StringComparison.OrdinalIgnoreCase);
}

static string? ExtractWord(JsonElement root)
{
    return TryGetString(root, "word")
           ?? TryGetString(root, "title")
           ?? TryGetString(root, "lemma");
}

static string? ExtractWiktionaryUrl(JsonElement root)
{
    return TryGetString(root, "url")
           ?? TryGetString(root, "wiktionary_url")
           ?? TryGetString(root, "page_url");
}

static string? ExtractSourceEntryId(JsonElement root)
{
    return TryGetString(root, "id")
           ?? TryGetString(root, "entry_id");
}

static string? TryGetString(JsonElement root, string propertyName)
{
    if (!root.TryGetProperty(propertyName, out var value))
    {
        return null;
    }

    return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

static List<string> ExtractDefinitions(JsonElement root)
{
    var definitions = new List<string>();
    var dedupe = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    if (!root.TryGetProperty("senses", out var senses) || senses.ValueKind != JsonValueKind.Array)
    {
        return definitions;
    }

    foreach (var sense in senses.EnumerateArray())
    {
        ExtractStringsFromSense(sense, "glosses", definitions, dedupe);
        ExtractStringsFromSense(sense, "raw_glosses", definitions, dedupe);
    }

    return definitions;
}

static List<(int SenseIndex, List<string> Synonyms)> ExtractSynonymsBySense(JsonElement root)
{
    var result = new Dictionary<int, List<string>>();
    var dedupeBySense = new Dictionary<int, HashSet<string>>();

    if (root.TryGetProperty("senses", out var senses) && senses.ValueKind == JsonValueKind.Array)
    {
        var senseIndex = 0;
        foreach (var sense in senses.EnumerateArray())
        {
            if (sense.TryGetProperty("synonyms", out var synonymsElement) && synonymsElement.ValueKind == JsonValueKind.Array)
            {
                AddSynonymsFromArray(synonymsElement, senseIndex, result, dedupeBySense);
            }

            senseIndex++;
        }
    }

    // Kaikki FR often provides synonyms at the top-level field "synonyms".
    if (root.TryGetProperty("synonyms", out var topLevelSynonyms) && topLevelSynonyms.ValueKind == JsonValueKind.Array)
    {
        AddSynonymsFromArray(topLevelSynonyms, null, result, dedupeBySense);
    }

    return result
        .OrderBy(kv => kv.Key)
        .Select(kv => (kv.Key, kv.Value))
        .ToList();
}

static void AddSynonymsFromArray(
    JsonElement synonymsArray,
    int? fallbackSenseIndex,
    Dictionary<int, List<string>> result,
    Dictionary<int, HashSet<string>> dedupeBySense)
{
    foreach (var synonymElement in synonymsArray.EnumerateArray())
    {
        var resolvedSenseIndex = fallbackSenseIndex ?? 0;
        string? synonymValue = null;

        switch (synonymElement.ValueKind)
        {
            case JsonValueKind.String:
                synonymValue = synonymElement.GetString();
                break;
            case JsonValueKind.Object:
                synonymValue = TryGetString(synonymElement, "word");
                if (synonymElement.TryGetProperty("sense_index", out var senseIndexElement) &&
                    senseIndexElement.ValueKind == JsonValueKind.Number &&
                    senseIndexElement.TryGetInt32(out var parsedSenseIndex))
                {
                    resolvedSenseIndex = parsedSenseIndex;
                }
                break;
        }

        if (string.IsNullOrWhiteSpace(synonymValue))
        {
            continue;
        }

        if (!result.TryGetValue(resolvedSenseIndex, out var synonyms))
        {
            synonyms = [];
            result[resolvedSenseIndex] = synonyms;
            dedupeBySense[resolvedSenseIndex] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        if (dedupeBySense[resolvedSenseIndex].Add(synonymValue))
        {
            synonyms.Add(synonymValue);
        }
    }
}

static void ExtractStringsFromSense(
    JsonElement sense,
    string key,
    List<string> target,
    HashSet<string> dedupe)
{
    if (!sense.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.Array)
    {
        return;
    }

    foreach (var entry in value.EnumerateArray())
    {
        if (entry.ValueKind != JsonValueKind.String)
        {
            continue;
        }

        var text = entry.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            continue;
        }

        if (dedupe.Add(text))
        {
            target.Add(text);
        }
    }
}

static bool IsScrabblePlayableWord(string value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return false;
    }

    if (value.Length < 2)
    {
        return false;
    }

    foreach (var c in value)
    {
        if (!char.IsLetter(c))
        {
            return false;
        }
    }

    return true;
}

static string NormalizeForLookup(string value)
{
    var normalized = value.Trim().Normalize(NormalizationForm.FormD);
    var sb = new StringBuilder(normalized.Length);
    foreach (var c in normalized)
    {
        var uc = CharUnicodeInfo.GetUnicodeCategory(c);
        if (uc == UnicodeCategory.NonSpacingMark)
        {
            continue;
        }

        if (char.IsLetter(c))
        {
            sb.Append(char.ToUpperInvariant(c));
        }
    }

    return sb.ToString().Normalize(NormalizationForm.FormC);
}

sealed class ImportOptions
{
    public required string ConnectionString { get; init; }
    public required string InputPath { get; init; }
    public required string LanguageCode { get; init; }
    public required string Environment { get; init; }
    public required string Source { get; init; }
    public bool IncludeDefinitions { get; init; }
    public bool IncludeSynonyms { get; init; }
    public bool ScrabbleOnly { get; init; }
    public bool ClearLanguageBeforeImport { get; init; }
    public bool DryRun { get; init; }
    public bool FailOnError { get; init; }
    public int BatchSize { get; init; }
    public int ProgressEveryLines { get; init; }

    public static ImportOptions Parse(string[] args)
    {
        string? connectionString = null;
        string? inputPath = null;
        string? languageCode = null;
        string? environment = null;
        var source = "kaikki";
        var includeDefinitions = false;
        var includeSynonyms = false;
        var scrabbleOnly = true;
        var clearLanguageBeforeImport = true;
        var dryRun = false;
        var failOnError = true;
        var batchSize = 5000;
        var progressEveryLines = 50_000;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--connection-string":
                    connectionString = ReadValue(args, ref i, arg);
                    break;
                case "--input":
                    inputPath = ReadValue(args, ref i, arg);
                    break;
                case "--language":
                    languageCode = ReadValue(args, ref i, arg).ToLowerInvariant();
                    break;
                case "--environment":
                    environment = ReadValue(args, ref i, arg).ToLowerInvariant();
                    break;
                case "--source":
                    source = ReadValue(args, ref i, arg);
                    break;
                case "--include-definitions":
                    includeDefinitions = true;
                    break;
                case "--include-synonyms":
                    includeSynonyms = true;
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--keep-language-data":
                    clearLanguageBeforeImport = false;
                    break;
                case "--allow-non-scrabble":
                    scrabbleOnly = false;
                    break;
                case "--continue-on-error":
                    failOnError = false;
                    break;
                case "--batch-size":
                {
                    var value = ReadValue(args, ref i, arg);
                    if (!int.TryParse(value, out batchSize) || batchSize <= 0)
                    {
                        throw new ArgumentException($"Invalid --batch-size value: {value}");
                    }

                    break;
                }
                case "--progress-every-lines":
                {
                    var value = ReadValue(args, ref i, arg);
                    if (!int.TryParse(value, out progressEveryLines) || progressEveryLines <= 0)
                    {
                        throw new ArgumentException($"Invalid --progress-every-lines value: {value}");
                    }

                    break;
                }
                case "--help":
                case "-h":
                    PrintHelpAndExit();
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {arg}");
            }
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("--connection-string is required.");
        }

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ArgumentException("--input is required.");
        }

        if (string.IsNullOrWhiteSpace(languageCode) || languageCode is not ("fr" or "en" or "de"))
        {
            throw new ArgumentException("--language must be one of: fr, en, de.");
        }

        if (string.IsNullOrWhiteSpace(environment) || environment is not ("dev" or "staging" or "prod"))
        {
            throw new ArgumentException("--environment must be one of: dev, staging, prod.");
        }

        return new ImportOptions
        {
            ConnectionString = connectionString,
            InputPath = Path.GetFullPath(inputPath),
            LanguageCode = languageCode,
            Environment = environment,
            Source = source,
            IncludeDefinitions = includeDefinitions,
            IncludeSynonyms = includeSynonyms,
            ScrabbleOnly = scrabbleOnly,
            ClearLanguageBeforeImport = clearLanguageBeforeImport,
            DryRun = dryRun,
            FailOnError = failOnError,
            BatchSize = batchSize,
            ProgressEveryLines = progressEveryLines
        };
    }

    private static string ReadValue(string[] args, ref int i, string key)
    {
        if (i + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {key}");
        }

        i++;
        return args[i];
    }

    private static void PrintHelpAndExit()
    {
        Console.WriteLine(
            """
            Usage:
              dotnet run --project tools/importers/Lama.DictionaryImporter -- \
                --connection-string "<postgres-connection-string>" \
                --environment dev|staging|prod \
                --language fr|en|de \
                --input /path/to/kaikki.jsonl \
                [--source kaikki] \
                [--include-definitions] \
                [--include-synonyms] \
                [--keep-language-data] \
                [--allow-non-scrabble] \
                [--batch-size 5000] \
                [--progress-every-lines 50000] \
                [--continue-on-error] \
                [--dry-run]
            """);
        System.Environment.Exit(0);
    }
}
