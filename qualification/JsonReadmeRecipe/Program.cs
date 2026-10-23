using System.Text.Json;
using System.Text.Json.Serialization;
using Rezultat;

var checks = new Checks();
var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

ProfilePatch Read(string json) => JsonSerializer.Deserialize<ProfilePatch>(json, options)!;
string Write(ProfilePatch patch) => JsonSerializer.Serialize(patch, options);

var absent = Read("{}");
checks.That("absent properties are Missing", absent.Nickname.IsMissing && absent.Age.IsMissing && absent.Newsletter.IsMissing);

var nulls = Read("""{"nickname":null,"age":null,"newsletter":null}""");
checks.That("null properties are Null", nulls.Nickname.IsNull && nulls.Age.IsNull && nulls.Newsletter.IsNull);

var ordinary = Read("""{"nickname":"","age":0,"newsletter":false}""");
checks.That("empty string, 0 and false are Value",
    ordinary.Nickname == Presence<string>.Value("") && ordinary.Age == Presence<int>.Value(0) && ordinary.Newsletter == Presence<bool>.Value(false));

checks.Text("Missing properties are omitted", "{}", Write(new ProfilePatch()));
checks.Text("Null properties are written as null", """{"nickname":null,"age":null,"newsletter":null}""",
    Write(new ProfilePatch { Nickname = Presence<string>.Null, Age = Presence<int>.Null, Newsletter = Presence<bool>.Null }));
const string mixed = """{"nickname":"","age":0}""";
checks.Text("ordinary values are written, Missing omitted", mixed,
    Write(new ProfilePatch { Nickname = Presence<string>.Value(""), Age = Presence<int>.Value(0) }));

var roundTrip = Read(Write(Read(mixed)));
checks.That("round trip keeps Value, Value and Missing",
    roundTrip.Nickname == Presence<string>.Value("") && roundTrip.Age == Presence<int>.Value(0) && roundTrip.Newsletter.IsMissing);
var nullTrip = Read(Write(nulls));
checks.That("round trip keeps Null", nullTrip.Nickname.IsNull && nullTrip.Age.IsNull && nullTrip.Newsletter.IsNull);

try
{
    Read("""{"age":"x"}""");
    checks.Fail("an invalid payload is rejected: no exception");
}
catch (JsonException exception)
{
    checks.That("an invalid payload is rejected with the payload error inside", exception.InnerException is JsonException);
}

var fieldOptions = new JsonSerializerOptions(options);
fieldOptions.Converters.Add(new PresenceFieldConverter<int>());
fieldOptions.Converters.Add(new PresenceFieldConverter<Tag>());
checks.Throws<JsonException>("Missing outside a property is not written", () => JsonSerializer.Serialize(Presence<int>.Missing, fieldOptions));
checks.Throws<JsonException>("a payload read as null from a non-null token is rejected",
    () => JsonSerializer.Deserialize<Presence<Tag>>("\"x\"", fieldOptions));

return checks.Complete();

[JsonConverter(typeof(NullTagConverter))]
internal sealed record Tag(string Text);

internal sealed class NullTagConverter : JsonConverter<Tag>
{
    public override Tag? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, Tag value, JsonSerializerOptions options) => writer.WriteStringValue(value.Text);
}
