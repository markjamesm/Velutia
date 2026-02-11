using System.Text.Json.Serialization;

namespace Velutia.Models;

public record SingleStepTest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("initial")] Initial Initial,
    [property: JsonPropertyName("final")] Final Final,
    [property: JsonPropertyName("cycles")] object[][] Cycles
);

public record Final(
    [property: JsonPropertyName("pc")] ushort Pc,
    [property: JsonPropertyName("s")] byte S,
    [property: JsonPropertyName("a")] byte A,
    [property: JsonPropertyName("x")] byte X,
    [property: JsonPropertyName("y")] byte Y,
    [property: JsonPropertyName("p")] byte P,
    [property: JsonPropertyName("ram")] ushort[][] Ram
);

public record Initial(
    [property: JsonPropertyName("pc")] ushort Pc,
    [property: JsonPropertyName("s")] byte S,
    [property: JsonPropertyName("a")] byte A,
    [property: JsonPropertyName("x")] byte X,
    [property: JsonPropertyName("y")] byte Y,
    [property: JsonPropertyName("p")] byte P,
    [property: JsonPropertyName("ram")] ushort[][] Ram
);