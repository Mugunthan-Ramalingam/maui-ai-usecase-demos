using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataGrid_Duplicate_Sample;

/// <summary>
/// Reads/writes enums using snake_case tokens such as "likely_duplicate" /
/// "possible_duplicate" / "not_duplicate", which is the form the Azure
/// OpenAI system prompt instructs the model to emit. Falls back to a
/// case-insensitive exact match for PascalCase ("LikelyDuplicate") so the
/// class is robust to either naming style without code changes elsewhere.
/// </summary>
public sealed class SnakeCaseEnumConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
{
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                if (reader.TryGetInt32(out var numeric))
                {
                    if (Enum.IsDefined(typeToConvert, numeric))
                        return (TEnum)Enum.ToObject(typeToConvert, numeric);
                    throw new JsonException(
                        $"Numeric value {numeric} is not a valid {typeToConvert.Name}. " +
                        $"Allowed values: {string.Join(", ", Enum.GetNames<TEnum>())}.");
                }
                throw new JsonException(
                    $"Numeric token for {typeToConvert.Name} is not a whole number.");

            case JsonTokenType.String:
                {
                    var raw = reader.GetString();
                    if (string.IsNullOrWhiteSpace(raw))
                        throw new JsonException(
                            $"Empty string is not a valid {typeToConvert.Name} value.");

                    var pascal = ToPascalCase(raw);
                    if (Enum.TryParse<TEnum>(pascal, ignoreCase: true, out var parsed))
                        return parsed;

                    if (Enum.TryParse<TEnum>(raw, ignoreCase: true, out parsed))
                        return parsed;

                    if (int.TryParse(raw, out var asNumber) && Enum.IsDefined(typeToConvert, asNumber))
                        return (TEnum)Enum.ToObject(typeToConvert, asNumber);

                    throw new JsonException(
                        $"'{raw}' is not a valid {typeToConvert.Name}. " +
                        $"Allowed values: {string.Join(", ", Enum.GetNames<TEnum>())} " +
                        $"(snake_case is also accepted).");
                }

            case JsonTokenType.Null:
                return default;

            default:
                throw new JsonException(
                    $"Expected a string or number for enum {typeToConvert.Name}, got {reader.TokenType}.");
        }
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }

    private static string ToPascalCase(string snake)
    {
        var parts = snake.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return snake;
        return string.Concat(parts.Select(p =>
            p.Length == 0 ? string.Empty :
            char.ToUpperInvariant(p[0]) + (p.Length > 1 ? p[1..] : string.Empty)));
    }
}
