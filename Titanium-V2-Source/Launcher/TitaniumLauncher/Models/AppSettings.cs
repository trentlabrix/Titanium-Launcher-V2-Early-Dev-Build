using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
namespace TitaniumLauncher.Models;
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BotDifficulty
{
    Easy = 0,
    Medium = 1,
    Hard = 2
}
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProfileMode
{
    UnlockedAll = 0,
    Progression = 1
}
public sealed class Profile
{
    public string Name { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public ProfileMode Mode { get; set; } = ProfileMode.UnlockedAll;
    [JsonIgnore]
    public string ModeLabel => Mode == ProfileMode.Progression ? "Progression Mode" : "Unlocked All";
}
/// <summary>Everything the launcher remembers between runs (saved to %AppData%\TitaniumLauncher\settings.json).</summary>
public sealed class AppSettings
{
    public string GameDirectory { get; set; } = "";
    public string LastBuildSeasonKey { get; set; } = "";
    public BotDifficulty Difficulty { get; set; } = BotDifficulty.Medium;
    public List<Profile> Profiles { get; set; } = new();
    public string ActiveProfile { get; set; } = "";
    public string BotCountRange { get; set; } = "";
    public bool EditOnRelease { get; set; } = true;
    public bool EnableMenu { get; set; }
    public BotPreset? BotPreset { get; set; }
    public string LaunchArguments { get; set; } = "";
}
