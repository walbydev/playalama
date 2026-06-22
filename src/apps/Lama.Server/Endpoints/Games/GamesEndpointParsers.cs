using System.Text.Json;
using Lama.Contracts;
using Lama.Domain.Board;
using Lama.Domain.Engine;
using Lama.Server.Contracts.Api;
using Npgsql;

namespace Lama.Server.Endpoints;

internal static class GamesEndpointParsers
{
    internal static RankingQueue ResolveQueue(GameLevel level) => level switch
    {
        GameLevel.Casual => RankingQueue.CasualUnranked,
        GameLevel.Tournament => RankingQueue.Tournament,
        _ => RankingQueue.OpenRanked
    };

    internal static GameLevel ParseGameLevelToken(string token)
    {
        if (Enum.TryParse<GameLevel>(token, ignoreCase: true, out var parsed))
            return parsed;

        return GameLevel.Standard;
    }

    internal static RankingQueue ParseRankingQueueToken(string token)
    {
        return token.Trim().ToLowerInvariant() switch
        {
            "open" => RankingQueue.OpenRanked,
            "tournament" => RankingQueue.Tournament,
            "global" => RankingQueue.GlobalPrestige,
            "casual" => RankingQueue.CasualUnranked,
            _ => RankingQueue.OpenRanked
        };
    }

    internal static string NormalizeStatusToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return "unknown";

        return token.Trim().ToLowerInvariant();
    }

    internal static string ToOnlineCommand(string? actionType)
    {
        if (string.IsNullOrWhiteSpace(actionType))
            return "play.move";

        return actionType.Trim().ToLowerInvariant() switch
        {
            "move" => "play.move",
            "pass" => "play.pass",
            "swap" => "play.swap",
            "challenge" => "play.challenge",
            "check" => "play.check",
            "suggest" => "play.suggest",
            var other => $"play.{other}"
        };
    }

    internal static JsonElement? ParseActionPayload(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return null;

        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    internal static int ExtractScoreFromPayload(JsonElement? payload)
    {
        if (payload is null || payload.Value.ValueKind != JsonValueKind.Object)
            return 0;

        if (!payload.Value.TryGetProperty("score", out var scoreElement))
            return 0;

        return scoreElement.ValueKind switch
        {
            JsonValueKind.Number when scoreElement.TryGetInt32(out var score) => score,
            JsonValueKind.String when int.TryParse(scoreElement.GetString(), out var score) => score,
            _ => 0
        };
    }

    internal static IReadOnlyList<OnlineMovePlacement> ExtractPlacementsFromPayload(JsonElement? payload)
    {
        if (payload is null || payload.Value.ValueKind != JsonValueKind.Object)
            return [];

        if (!payload.Value.TryGetProperty("placements", out var placementsElement) ||
            placementsElement.ValueKind != JsonValueKind.Array)
            return [];

        var placements = new List<OnlineMovePlacement>();

        foreach (var item in placementsElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            if (!item.TryGetProperty("row", out var rowElement) || !rowElement.TryGetInt32(out var row))
                continue;
            if (!item.TryGetProperty("column", out var columnElement) || !columnElement.TryGetInt32(out var column))
                continue;
            if (!item.TryGetProperty("letter", out var letterElement))
                continue;

            var letterRaw = letterElement.ValueKind switch
            {
                JsonValueKind.String => letterElement.GetString(),
                _ => letterElement.ToString()
            };

            if (string.IsNullOrWhiteSpace(letterRaw))
                continue;

            placements.Add(new OnlineMovePlacement(row, column, letterRaw[0]));
        }

        return placements;
    }

    internal static IReadOnlyList<OnlineBoardTile> ParseBoardTilesFromJson(string? boardJson)
    {
        if (string.IsNullOrWhiteSpace(boardJson))
            return [];

        try
        {
            using var document = JsonDocument.Parse(boardJson);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
                return ParseBoardTilesFromArray(root);

            if (root.ValueKind != JsonValueKind.Object)
                return [];

            if (root.TryGetProperty("tiles", out var tilesElement) && tilesElement.ValueKind == JsonValueKind.Array)
                return ParseBoardTilesFromArray(tilesElement);

            if (root.TryGetProperty("grid", out var gridElement) && gridElement.ValueKind == JsonValueKind.Array)
                return ParseBoardTilesFromGrid(gridElement);

            return [];
        }
        catch
        {
            return [];
        }
    }

    internal static bool IsMissingDatabaseObject(PostgresException ex) =>
        ex.SqlState is PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.UndefinedColumn;

    internal static IReadOnlyList<OnlineBoardTile> CaptureBoard(BoardState board)
    {
        var tiles = new List<OnlineBoardTile>();
        for (var row = 0; row < 15; row++)
        for (var col = 0; col < 15; col++)
        {
            var tile = board.Grid[row, col];
            if (tile is not null)
                tiles.Add(new OnlineBoardTile(row, col, tile.Letter, tile.IsWildcard));
        }

        return tiles;
    }

    internal static async Task WriteEventAsync(HttpContext context, ServerEvent evt)
    {
        var payloadJson = JsonSerializer.Serialize(evt.Payload);
        await context.Response.WriteAsync($"event: {evt.Type}\\n");
        await context.Response.WriteAsync($"data: {payloadJson}\\n\\n");
        await context.Response.Body.FlushAsync();
    }

    internal static Dictionary<Position, char> BuildLetterPlacementsFromPayload(JsonElement? payload)
    {
        if (payload is null || payload.Value.ValueKind != JsonValueKind.Object)
            throw new GameException("Payload play.move invalide.");

        if (!payload.Value.TryGetProperty("position", out var positionProperty) ||
            !payload.Value.TryGetProperty("word", out var wordProperty) ||
            !payload.Value.TryGetProperty("direction", out var directionProperty))
            throw new GameException("Payload play.move incomplet.");

        var positionRaw = positionProperty.GetString();
        var word = wordProperty.GetString();
        var direction = directionProperty.GetString()?.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(positionRaw) || string.IsNullOrWhiteSpace(word) || (direction is not "H" and not "V"))
            throw new GameException("Payload play.move invalide.");

        if (!TryParsePosition(positionRaw, out var start))
            throw new GameException($"Position invalide: {positionRaw}");

        var placements = new Dictionary<Position, char>();
        for (var i = 0; i < word.Length; i++)
        {
            var pos = direction == "H"
                ? new Position(start.Row, start.Column + i)
                : new Position(start.Row + i, start.Column);
            placements[pos] = word[i];
        }

        return placements;
    }

    internal static IReadOnlyList<char> BuildSwapLettersFromPayload(JsonElement? payload, IReadOnlyList<char> currentRack)
    {
        if (payload is null || payload.Value.ValueKind != JsonValueKind.Object)
            throw new GameException("Payload play.swap invalide.");

        var swapAll = false;
        if (payload.Value.TryGetProperty("swapAll", out var swapAllProperty))
        {
            swapAll = swapAllProperty.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(swapAllProperty.GetString(), out var parsed) => parsed,
                _ => false
            };
        }

        if (swapAll)
            return currentRack.ToList();

        if (!payload.Value.TryGetProperty("letters", out var lettersProperty))
            throw new GameException("Payload play.swap incomplet (letters requis sans swapAll).");

        var lettersRaw = lettersProperty.ValueKind switch
        {
            JsonValueKind.String => lettersProperty.GetString(),
            _ => lettersProperty.ToString()
        };

        if (string.IsNullOrWhiteSpace(lettersRaw))
            throw new GameException("Aucune lettre fournie pour play.swap.");

        return lettersRaw
            .Trim()
            .ToUpperInvariant()
            .ToCharArray();
    }

    private static IReadOnlyList<OnlineBoardTile> ParseBoardTilesFromArray(JsonElement tilesElement)
    {
        var tiles = new List<OnlineBoardTile>();

        foreach (var tileElement in tilesElement.EnumerateArray())
        {
            if (tileElement.ValueKind != JsonValueKind.Object)
                continue;

            if (!TryGetIntProperty(tileElement, "row", out var row))
                continue;
            if (!TryGetIntProperty(tileElement, "column", out var column))
                continue;
            if (!TryGetLetterProperty(tileElement, "letter", out var letter))
                continue;

            var isWildcard = TryGetBoolProperty(tileElement, "isWildcard", out var wildcard) && wildcard;
            tiles.Add(new OnlineBoardTile(row, column, letter, isWildcard));
        }

        return tiles;
    }

    private static IReadOnlyList<OnlineBoardTile> ParseBoardTilesFromGrid(JsonElement gridElement)
    {
        var tiles = new List<OnlineBoardTile>();
        var rowIndex = 0;

        foreach (var rowElement in gridElement.EnumerateArray())
        {
            if (rowElement.ValueKind != JsonValueKind.Array)
            {
                rowIndex++;
                continue;
            }

            var columnIndex = 0;
            foreach (var cellElement in rowElement.EnumerateArray())
            {
                if (cellElement.ValueKind == JsonValueKind.Object &&
                    TryGetLetterProperty(cellElement, "letter", out var letter))
                {
                    var isWildcard = TryGetBoolProperty(cellElement, "isWildcard", out var wildcard) && wildcard;
                    tiles.Add(new OnlineBoardTile(rowIndex, columnIndex, letter, isWildcard));
                }

                columnIndex++;
            }

            rowIndex++;
        }

        return tiles;
    }

    private static bool TryGetIntProperty(JsonElement element, string name, out int value)
    {
        value = 0;
        if (!element.TryGetProperty(name, out var property))
            return false;

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt32(out value) => true,
            JsonValueKind.String when int.TryParse(property.GetString(), out value) => true,
            _ => false
        };
    }

    private static bool TryGetBoolProperty(JsonElement element, string name, out bool value)
    {
        value = false;
        if (!element.TryGetProperty(name, out var property))
            return false;

        if (property.ValueKind == JsonValueKind.True)
        {
            value = true;
            return true;
        }

        if (property.ValueKind == JsonValueKind.False)
        {
            value = false;
            return true;
        }

        return property.ValueKind == JsonValueKind.String && bool.TryParse(property.GetString(), out value);
    }

    private static bool TryGetLetterProperty(JsonElement element, string name, out char value)
    {
        value = default;
        if (!element.TryGetProperty(name, out var property))
            return false;

        var raw = property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            _ => property.ToString()
        };

        if (string.IsNullOrWhiteSpace(raw))
            return false;

        value = raw[0];
        return true;
    }

    private static bool TryParsePosition(string input, out Position position)
    {
        position = new Position(0, 0);
        input = input.Trim().ToUpperInvariant();

        if (input.Length < 2)
            return false;

        var colChar = input[0];
        if (colChar < 'A' || colChar > 'O')
            return false;

        if (!int.TryParse(input[1..], out var row) || row < 1 || row > 15)
            return false;

        position = new Position(row - 1, colChar - 'A');
        return true;
    }
}

