using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
namespace TitaniumLauncher.Models;
public enum DetectionSource
{
    GameFiles,
    FolderName
}
/// <summary>A Fortnite build found in the folder the player selected.</summary>
public sealed record BuildInfo(
    string Version,
    int? Changelist,
    int Major,
    DetectionSource Source,
    string ExecutablePath)
{
    public bool IsKnown => Major > 0;
    public bool IsChapterOne => Major is >= 1 and <= 10;
    /// <summary>
    /// Chapter 1 ran from 1.x (Season 1) to 10.x (Season X),
    /// Chapter 2 from 11.x to 18.x.
    /// </summary>
    public string Era
    {
        get
        {
            if (!IsKnown) return "Version not recognised";
            if (Major <= 10) return $"Chapter 1, Season {(Major == 10 ? "X" : Major.ToString())}";
            if (Major <= 18) return $"Chapter 2, Season {Major - 10}";
            return "Chapter 3 or later";
        }
    }
    public string SourceLabel => !IsKnown
        ? "Could not read a version"
        : Source == DetectionSource.GameFiles ? "Read from game files" : "Taken from folder name";
}
