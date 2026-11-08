using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TitaniumLauncher.Models;

/// <summary>A chapter entry used by the chapter cards and the season screen.</summary>
public sealed class ChapterInfo
{
    public required int Number { get; init; }
    public required string Title { get; init; }
    public required string Tagline { get; init; }
    public required bool IsReleased { get; init; }
}

/// <summary>A season entry. Availability is intentionally data-driven.</summary>
public sealed class SeasonInfo
{
    public required string Key { get; init; }
    public required int ChapterNumber { get; init; }
    public required string Code { get; init; }
    public int SeasonNumber => Code == "X" ? 10 : int.TryParse(Code, out var number) ? number : 0;
    public required string Name { get; init; }
    public required string Tagline { get; init; }
    public required string Description { get; init; }
    public required bool IsReleased { get; init; }
    public required Brush CoverBrush { get; init; }
    public ImageSource? WheelImage { get; init; }
    public ImageSource? BackgroundImage { get; init; }
    public ImageSource? WheelPageBackgroundImage { get; init; }
    public Point WheelImageFocus { get; init; } = new(0.5, 0.5);

    public string ReleaseLabel => IsReleased ? "AVAILABLE" : "NOT RELEASED";
    public string AvailabilityNote => IsReleased
        ? $"{Name} is available to play."
        : $"{Name} is not released yet.";
}

public static class SeasonCatalog
{
    private static readonly Brush LockedCover = Gradient("#263B4B", "#08131D");

    private static readonly IReadOnlyList<ChapterInfo> AllChapters = new[]
    {
        new ChapterInfo
        {
            Number = 1,
            Title = "Chapter 1",
            Tagline = "The Original Chapter",
            IsReleased = true
        },
        new ChapterInfo
        {
            Number = 2,
            Title = "Chapter 2",
            Tagline = "Return to the Island",
            IsReleased = false
        }
    };

    private static readonly IReadOnlyList<SeasonInfo> ChapterOneSeasons = new[]
    {
        Locked(1, "0", "Season 0", "Before the Storm", "The island before the storm. A rare look at the first version of the map."),
        Locked(1, "1", "Season 1", "The First Drop", "The island that started it all. Land, loot and the original Battle Royale."),
        Locked(1, "2", "Season 2", "Knights of the Island", "Medieval keeps, new weapons and the first Battle Pass."),
        Locked(1, "3", "Season 3", "A Sky on Fire", "A comet crosses the sky and the island begins to change."),
        Locked(1, "4", "Season 4", "Heroes and Villains", "Superheroes and supervillains collide as the comet's secret is revealed."),
        Locked(1, "5", "Season 5", "Worlds Collide", "Rifts open across the island, bringing new places and strange visitors."),
        Locked(1, "6", "Season 6", "Darkness Rises", "Darkness falls and the Cube leaves its mark on the island."),
        Locked(1, "7", "Season 7", "A Frozen Frontier", "A frozen front moves in, turning the island into a winter battlefield."),
        Locked(1, "8", "Season 8", "Treasure and Ash", "Volcanoes erupt, pirates arrive and the map breaks open."),
        Locked(1, "9", "Season 9", "The Future Is Now", "The future lands in Neo Tilted, and the island's clock starts ticking."),
        new SeasonInfo
        {
            Key = "1-X",
            ChapterNumber = 1,
            Code = "X",
            Name = "Season X",
            Tagline = "The End of the Beginning",
            Description = "The original island's final season. Rift zones rewrite familiar places as time collapses.",
            IsReleased = true,
            CoverBrush = CoverFor("X"),
            WheelImage = WheelImageFor(1, "X"),
            BackgroundImage = BackgroundFor(1, "X"),
            WheelPageBackgroundImage = WheelPageBackgroundFor(1, "X"),
            WheelImageFocus = WheelImageFocusFor(1, "X")
        }
    };

    private static readonly IReadOnlyList<SeasonInfo> ChapterTwoSeasons = new[]
    {
        Locked(2, "1", "Season 1", "A New Island", "A brand-new island, a new cast and a fresh start for Chapter 2."),
        Locked(2, "2", "Season 2", "Top Secret", "New agents, new locations and a mystery beneath the island."),
        Locked(2, "3", "Season 3", "Splash Down", "The island floods and the fight moves onto the water."),
        Locked(2, "4", "Season 4", "Nexus War", "Four factions clash as the Nexus comes apart."),
        Locked(2, "5", "Season 5", "Zero Point", "The Zero Point awakens and reality begins to bend."),
        Locked(2, "6", "Season 6", "Primal", "The island turns wild as primal creatures take over."),
        Locked(2, "7", "Season 7", "Invasion", "An alien fleet arrives and the island becomes a war zone."),
        Locked(2, "8", "Season 8", "The End", "The final season of Chapter 2. The island faces its last stand.")
    };

    public static IReadOnlyList<ChapterInfo> Chapters => AllChapters;

    public static IReadOnlyList<SeasonInfo> GetSeasons(int chapterNumber) =>
        chapterNumber == 1 ? ChapterOneSeasons :
        chapterNumber == 2 ? ChapterTwoSeasons :
        System.Array.Empty<SeasonInfo>();

    private static SeasonInfo Locked(int chapter, string code, string name, string tagline, string description) =>
        new SeasonInfo
        {
            Key = $"{chapter}-{code}",
            ChapterNumber = chapter,
            Code = code,
            Name = name,
            Tagline = tagline,
            Description = description,
            IsReleased = false,
            CoverBrush = CoverFor(code),
            WheelImage = WheelImageFor(chapter, code),
            WheelPageBackgroundImage = WheelPageBackgroundFor(chapter, code),
            WheelImageFocus = WheelImageFocusFor(chapter, code)
        };

    private static ImageSource? WheelImageFor(int chapter, string code)
    {
        if (chapter != 1 || code is not ("0" or "1" or "2" or "3" or "4" or "5" or "6" or "7" or "8" or "9" or "X"))
            return null;

        string assetCode = code == "X" ? "x" : code;
        return LoadImage($"season-{assetCode}.png");
    }

    private static ImageSource? BackgroundFor(int chapter, string code) =>
        chapter == 1 && code == "X" ? LoadImage("season-x-background-image.jpg") : null;

    private static ImageSource? WheelPageBackgroundFor(int chapter, string code)
    {
        if (chapter != 1) return null;

        string? fileName = code switch
        {
            "0" => "Season 0 Wheel page background.png",
            "4" => "Season 4 wheel page background.jpg",
            "5" => "Season 5 wheel page background.jpg",
            "6" => "Season 6 wheel page background.jpg",
            "7" => "Season 7 wheel page background.jpg",
            "8" => "Season 8 wheel page background.jpg",
            "9" => "Season 9 Wheel page background.png",
            "X" => "Season x wheel page background.png",
            _ => null
        };

        return fileName is null ? null : LoadImage(fileName);
    }

    private static ImageSource LoadImage(string fileName)
    {
        string assemblyName = typeof(SeasonCatalog).Assembly.GetName().Name ?? "TitaniumLauncher";
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(
            $"pack://application:,,,/{assemblyName};component/Assets/{fileName}",
            UriKind.Absolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        image.EndInit();
        image.Freeze();
        return image;
    }

    // Normalized centers of the large white season numbers in each supplied logo.
    private static Point WheelImageFocusFor(int chapter, string code) =>
        chapter == 1
            ? code switch
            {
                "0" => new Point(0.502, 0.489),
                "1" => new Point(0.492, 0.492),
                "2" => new Point(0.500, 0.477),
                "3" => new Point(0.511, 0.481),
                "4" => new Point(0.502, 0.481),
                "5" => new Point(0.472, 0.470),
                "6" => new Point(0.509, 0.480),
                "7" => new Point(0.502, 0.489),
                "8" => new Point(0.516, 0.489),
                "9" => new Point(0.523, 0.489),
                "X" => new Point(0.520, 0.486),
                _ => new Point(0.5, 0.5)
            }
            : new Point(0.5, 0.5);

    private static Brush CoverFor(string code) => code switch
    {
        "0" => Gradient("#2D7D72", "#071724"),
        "1" => Gradient("#3A5774", "#081724"),
        "2" => Gradient("#8C4D28", "#160F1A"),
        "3" => Gradient("#385B8E", "#17162F"),
        "4" => Gradient("#9B642F", "#24120F"),
        "5" => Gradient("#654295", "#10172B"),
        "6" => Gradient("#4B2D6F", "#0E0B18"),
        "7" => Gradient("#5C99B8", "#0B2234"),
        "8" => Gradient("#A66532", "#0A2822"),
        "9" => Gradient("#2D8590", "#111A35"),
        "X" => Gradient("#246F96", "#51318A"),
        _ => LockedCover
    };

    private static Brush Gradient(string from, string to)
    {
        var brush = new LinearGradientBrush(
            (Color)ColorConverter.ConvertFromString(from),
            (Color)ColorConverter.ConvertFromString(to),
            new Point(0, 0),
            new Point(1, 1));
        brush.Freeze();
        return brush;
    }
}
