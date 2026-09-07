using SPTarkov.Server.Core.Models.Spt.Mod;

namespace Terkoiz.Skipper.Server;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.terkoiz.skipper.server";
    public string Name { get; init; } = "Terkoiz.Skipper.Server";
    public string Author { get; init; } = "Terkoiz";
    public List<string>? Contributors { get; init; } = [];
    public SemanticVersioning.Version Version { get; init; } = new("1.2.0");
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");
    public bool HasPrepatcher { get; init; }
    public List<string>? Incompatibilities { get; init; } = [];
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; } = [];
    public string? Url { get; init; } = "https://github.com/danyhappy564-cmyk/SPT-Skipper";
    public string License { get; init; } = "MIT";
}
