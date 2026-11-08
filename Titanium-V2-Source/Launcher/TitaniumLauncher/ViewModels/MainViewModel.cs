using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Microsoft.Win32;
using TitaniumLauncher.Models;
using TitaniumLauncher.Services;
namespace TitaniumLauncher.ViewModels;
public enum BuildState
{
    None,
    Detecting,
    Found,
    Problem
}
public enum AppScreen
{
    Home,
    Chapter,
    Season,
    Credits
}
public sealed partial class MainViewModel : ViewModelBase
{
    private const string NoFolderText = "Select your Fortnite build folder";
    private const string CommunityUrl = "https://discord.com/invite/titanium";
    private static readonly Regex ProfileNameRule = new(@"^[A-Za-z0-9_\- ]{3,16}$", RegexOptions.Compiled);
    private readonly SettingsService _store;
    private readonly AppSettings _settings;
    private int _detectionId;
    private BuildState _state = BuildState.None;
    private BuildInfo? _build;
    private string _problemMessage = "";
    private string _gameDirectory = "";
    private bool _enableMenu;
    private bool _isSettingsOpen;
    private Profile? _selectedProfile;
    private string _newProfileName = "";
    private bool _newProfileIsProgression = true;
    private string _profileError = "";
    private string _statusMessage = "";
    private bool _launching;
    private AppScreen _currentScreen = AppScreen.Home;
    private ChapterInfo? _selectedChapter;
    private SeasonInfo? _selectedSeason;
    private SeasonInfo? _activeSeason;
    public MainViewModel():this(new SettingsService(),new PresetStore()){}
    public MainViewModel(SettingsService store,PresetStore presets)
    {
        _store=store;_presets=presets;
        _settings = _store.Load();
        _enableMenu = _settings.EnableMenu;
        InitializeDifficulty();
        foreach (var profile in _settings.Profiles) Profiles.Add(profile);
        Profiles.CollectionChanged += OnProfilesChanged;
        _selectedProfile = Profiles.FirstOrDefault(p =>
                               string.Equals(p.Name, _settings.ActiveProfile, StringComparison.OrdinalIgnoreCase))
                           ?? Profiles.FirstOrDefault();
        BrowseCommand = new RelayCommand(Browse);
        OpenCommunityCommand = new RelayCommand(OpenCommunity);
        HomeCommand = new RelayCommand(ShowHome);
        OpenCreditsCommand = new RelayCommand(ShowCredits);
        OpenChapterCommand = new RelayCommand(OpenChapter);
        BackToChaptersCommand = new RelayCommand(ShowHome);
        BackToSeasonsCommand = new RelayCommand(BackToSeasons);
        ChooseSeasonBuildCommand = new RelayCommand(ChooseSeasonBuild, () => CanChooseSeasonBuild);
        PlayCommand = new RelayCommand(Play, () => State == BuildState.Found && !_launching && !GameLauncher.IsRunning && Build is not null && Build.Version == "10.40" && Build.Changelist == 9380822);
        ToggleSettingsCommand = new RelayCommand(() => IsSettingsOpen = !IsSettingsOpen);
        CloseSettingsCommand = new RelayCommand(() => IsSettingsOpen = false);
        CreateProfileCommand = new RelayCommand(CreateProfile);
        DeleteProfileCommand = new RelayCommand(p => DeleteProfile(p as Profile));
        SelectProgressionModeCommand = new RelayCommand(() => NewProfileIsProgression = true);
        SelectUnlockedAllModeCommand = new RelayCommand(() => NewProfileIsProgression = false);
        SetDifficultyCommand = new RelayCommand(p =>
        {
            if (p is not null && int.TryParse(p.ToString(), out var level)) DifficultyIndex = level;
        });
        RestoreActiveSeason();
        if (!string.IsNullOrWhiteSpace(_settings.GameDirectory))
            SetGameDirectory(_settings.GameDirectory, persist: false);
    }
    // ---------------------------------------------------------------- commands
    public ICommand BrowseCommand { get; }
    public ICommand OpenCommunityCommand { get; }
    public ICommand HomeCommand { get; }
    public ICommand OpenCreditsCommand { get; }
    public ICommand OpenChapterCommand { get; }
    public ICommand BackToChaptersCommand { get; }
    public ICommand BackToSeasonsCommand { get; }
    public ICommand ChooseSeasonBuildCommand { get; }
    public ICommand PlayCommand { get; }
    public ICommand ToggleSettingsCommand { get; }
    public ICommand CloseSettingsCommand { get; }
    public ICommand CreateProfileCommand { get; }
    public ICommand DeleteProfileCommand { get; }
    public ICommand SelectProgressionModeCommand { get; }
    public ICommand SelectUnlockedAllModeCommand { get; }
    public ICommand SetDifficultyCommand { get; }
    // ---------------------------------------------------------------- game folder + build
    public string DisplayPath => HasDirectory ? _gameDirectory : NoFolderText;
    public bool HasDirectory => !string.IsNullOrWhiteSpace(_gameDirectory);        public BuildState State
        {
            get => _state;
            private set
            {
                if (!Set(ref _state, value)) return;
                OnPropertyChanged(nameof(PlayHint));
                OnPropertyChanged(nameof(BuildStateLabel));
                OnPropertyChanged(nameof(BuildStateDetail));
                OnPropertyChanged(nameof(BuildSourceText));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    public BuildInfo? Build
    {
        get => _build;
        private set
        {
            if (!Set(ref _build, value)) return;
            OnPropertyChanged(nameof(BuildDetail));
            OnPropertyChanged(nameof(PlayHint));
            OnPropertyChanged(nameof(BuildWarning));
            OnPropertyChanged(nameof(HasBuildWarning));
            OnPropertyChanged(nameof(BuildVersionText));
            OnPropertyChanged(nameof(BuildEraText));
            OnPropertyChanged(nameof(BuildSourceText));
            OnPropertyChanged(nameof(BuildStateLabel));
            OnPropertyChanged(nameof(BuildStateDetail));
        }
    }        public string ProblemMessage
        {
            get => _problemMessage;
            private set
            {
                if (!Set(ref _problemMessage, value)) return;
                OnPropertyChanged(nameof(BuildStateDetail));
            }
        }
        public string BuildVersionText => Build?.Version ?? "10.40";
        public string BuildEraText => Build?.Era ?? "Chapter 1, Season X";
        public string BuildSourceText => State == BuildState.Found
            ? Build?.SourceLabel ?? ""
            : "Choose your Fortnite install folder";
        public string BuildStateLabel => State switch
        {
            BuildState.Found => HasBuildWarning ? "BUILD MISMATCH" : "READY TO LAUNCH",
            BuildState.Detecting => "READING BUILD",
            BuildState.Problem => "BUILD NOT FOUND",
            _ => "NO BUILD LINKED"
        };
        public string BuildStateDetail => State switch
        {
            BuildState.Found => HasBuildWarning ? BuildWarning : BuildDetail,
            BuildState.Detecting => "Reading the version from your game files…",
            BuildState.Problem => ProblemMessage,
            _ => "Select the folder that holds Fortnite 10.40."
        };
        public string FolderActionText => HasDirectory ? "Change folder" : "Choose folder";
        public string LauncherVersionText => "V2.0";
    /// <summary>For example "Changelist 3700114. Read from game files."</summary>
    public string BuildDetail
    {
        get
        {
            if (Build is null) return "";
            return Build.Changelist is int cl
                ? $"Changelist {cl}. {Build.SourceLabel}."
                : $"{Build.SourceLabel}.";
        }
    }
    public string BuildWarning => Build is null ? "" :
        Build.Version == "10.40" && Build.Changelist == 9380822 && Build.Source == DetectionSource.GameFiles ? "" :
        "This package requires Fortnite 10.40 CL-9380822.";

    public bool HasBuildWarning => BuildWarning.Length > 0;
    private void OpenCommunity()
    {
        try
        {
            Process.Start(new ProcessStartInfo(CommunityUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't open the Titanium Discord: {ex.Message}";
        }
    }
    // ---------------------------------------------------------------- chapters + seasons
    public ObservableCollection<SeasonInfo> Seasons { get; } = new();
    public AppScreen CurrentScreen
    {
        get => _currentScreen;
        private set
        {
            if (!Set(ref _currentScreen, value)) return;
            OnPropertyChanged(nameof(IsHomeScreen));
            OnPropertyChanged(nameof(IsChapterScreen));
            OnPropertyChanged(nameof(IsSeasonScreen));
            OnPropertyChanged(nameof(IsCreditsScreen));
        }
    }
    public bool IsHomeScreen => CurrentScreen == AppScreen.Home;
    public bool IsChapterScreen => CurrentScreen == AppScreen.Chapter;
    public bool IsSeasonScreen => CurrentScreen == AppScreen.Season;
    public bool IsCreditsScreen => CurrentScreen == AppScreen.Credits;
    public ChapterInfo? SelectedChapter
    {
        get => _selectedChapter;
        private set
        {
            if (!Set(ref _selectedChapter, value)) return;
            OnPropertyChanged(nameof(ChapterLabel));
        }
    }
    public SeasonInfo? SelectedSeason
    {
        get => _selectedSeason;
        set
        {
            if (!Set(ref _selectedSeason, value)) return;
            OnPropertyChanged(nameof(SelectedSeasonName));
            OnPropertyChanged(nameof(SelectedSeasonHeading));
            OnPropertyChanged(nameof(SelectedSeasonStatus));
            OnPropertyChanged(nameof(SeasonActionText));
            OnPropertyChanged(nameof(CanChooseSeasonBuild));
            CommandManager.InvalidateRequerySuggested();
        }
    }
    public string ChapterLabel => SelectedChapter is null ? "" : $"CHAPTER {SelectedChapter.Number}";
    public string SelectedSeasonName => SelectedSeason?.Name ?? "Select a season";
    public string SelectedSeasonHeading => (SelectedSeason?.Name ?? "Select a season").ToUpperInvariant();
    public string SelectedSeasonStatus => SelectedSeason is null ? "" : SelectedSeason.IsReleased ? "AVAILABLE NOW" : "NOT RELEASED";
    public string SeasonActionText => SelectedSeason?.IsReleased == true ? "SELECT BUILD" : "NOT RELEASED";
    public bool CanChooseSeasonBuild => SelectedSeason?.IsReleased == true;
    public SeasonInfo? ActiveSeason
    {
        get => _activeSeason;
        private set
        {
            if (!Set(ref _activeSeason, value)) return;
            OnPropertyChanged(nameof(HasSeasonBackground));
        }
    }
    public bool HasSeasonBackground => ActiveSeason?.BackgroundImage is not null;
    private void RestoreActiveSeason()
    {
        ActiveSeason = FindSeason(_settings.LastBuildSeasonKey);
        if (ActiveSeason is null && !string.IsNullOrWhiteSpace(_settings.GameDirectory))
        {
            ActiveSeason = SeasonCatalog.Chapters
                .SelectMany(chapter => SeasonCatalog.GetSeasons(chapter.Number))
                .FirstOrDefault(season => season.IsReleased);
        }
        if (ActiveSeason is not null && string.IsNullOrWhiteSpace(_settings.LastBuildSeasonKey))
        {
            _settings.LastBuildSeasonKey = ActiveSeason.Key;
            Save();
        }
    }
    private static SeasonInfo? FindSeason(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        return SeasonCatalog.Chapters
            .SelectMany(chapter => SeasonCatalog.GetSeasons(chapter.Number))
            .FirstOrDefault(season => string.Equals(season.Key, key, StringComparison.OrdinalIgnoreCase));
    }
    private void ShowHome()
    {
        CurrentScreen = AppScreen.Home;
        IsSettingsOpen = false;
    }
    private void ShowCredits()
    {
        CurrentScreen = AppScreen.Credits;
        IsSettingsOpen = false;
    }
    private void BackToSeasons()
    {
        if (SelectedChapter is null)
        {
            ShowHome();
            return;
        }
        CurrentScreen = AppScreen.Chapter;
        IsSettingsOpen = false;
    }
    private void OpenChapter(object? parameter)
    {
        if (parameter is null || !int.TryParse(parameter.ToString(), out int number)) return;
        var chapter = SeasonCatalog.Chapters.FirstOrDefault(c => c.Number == number);
        if (chapter is null) return;
        SelectedChapter = chapter;
        Seasons.Clear();
        foreach (var season in SeasonCatalog.GetSeasons(number)) Seasons.Add(season);
        SelectedSeason = Seasons.FirstOrDefault(s => s.IsReleased) ?? Seasons.FirstOrDefault();
        CurrentScreen = AppScreen.Chapter;
        IsSettingsOpen = false;
    }
    private void ChooseSeasonBuild()
    {
        if (SelectedSeason is null || !SelectedSeason.IsReleased) return;
        var dialog = new OpenFolderDialog
        {
            Title = $"Select your {SelectedSeason.Name} build folder",
            Multiselect = false
        };
        if (HasDirectory && Directory.Exists(_gameDirectory))
            dialog.InitialDirectory = _gameDirectory;
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FolderName)) return;
        ActiveSeason = SelectedSeason;
        _settings.LastBuildSeasonKey = SelectedSeason.Key;
        SetGameDirectory(dialog.FolderName, persist: true);
        CurrentScreen = AppScreen.Season;
        IsSettingsOpen = false;
    }
    private void Browse()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select your Fortnite build folder",
            Multiselect = false
        };
        if (HasDirectory && Directory.Exists(_gameDirectory))
            dialog.InitialDirectory = _gameDirectory;
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
            SetGameDirectory(dialog.FolderName, persist: true);
    }
    private void SetGameDirectory(string path, bool persist)
    {
        _gameDirectory = path;
        OnPropertyChanged(nameof(DisplayPath));
        OnPropertyChanged(nameof(HasDirectory));
        OnPropertyChanged(nameof(FolderActionText));
        StatusMessage = "";
        if (persist)
        {
            _settings.GameDirectory = path;
            Save();
        }
        _ = DetectAsync(path);
    }
    private async Task DetectAsync(string path)
    {
        int id = ++_detectionId;
        Build = null;
        ProblemMessage = "";
        State = BuildState.Detecting;
        var result = await Task.Run(() => BuildDetector.Detect(path));
        if (id != _detectionId) return; // the player picked another folder meanwhile
        if (result.Build is not null)
        {
            Build = result.Build;
            State = BuildState.Found;
        }
        else
        {
            ProblemMessage = result.Error ?? "This folder doesn't look like a Fortnite build.";
            State = BuildState.Problem;
        }
    }
    // ---------------------------------------------------------------- play
    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (Set(ref _statusMessage, value)) OnPropertyChanged(nameof(HasStatus));
        }
    }
    public bool HasStatus => _statusMessage.Length > 0;
    public string PlayHint => State == BuildState.Found && Build is not null
        ? $"Launch Fortnite {Build.Version}"
        : "Choose a Fortnite build folder first";
    private async void Play()
    {
        StatusMessage = "";
        if (Build is null) return;
        if (SelectedProfile is null)
        {
            ProfileError = "Create a profile to play.";
            IsSettingsOpen = true;
            return;
        }
        try
        {
            var matchOptions=MatchOptions.Parse(_settings.BotCountRange,_settings.EditOnRelease) with {Difficulty=_currentPreset.Copy()};
            _launching = true;
            CommandManager.InvalidateRequerySuggested();
            await GameLauncher.LaunchAsync(Build, SelectedProfile, ActiveSeason?.SeasonNumber ?? 10, _settings.LaunchArguments, message =>
            {
                StatusMessage = message;
                CommandManager.InvalidateRequerySuggested();
            }, matchOptions, EnableMenu);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't start the game: {ex.Message}";
        }
        finally { _launching = false; CommandManager.InvalidateRequerySuggested(); }
    }
    // ---------------------------------------------------------------- settings drawer
    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set => Set(ref _isSettingsOpen, value);
    }
    public bool EnableMenu
    {
        get => _enableMenu;
        set { if (!Set(ref _enableMenu, value)) return; _settings.EnableMenu = value; Save(); }
    }
    // Bot difficulty -----------------------------------------------------
    public int DifficultyIndex
    {
        get=>_difficultyIndex;
        set {int level=Math.Clamp(value,0,5);ApplyPreset(BotPreset.Builtin(level));}
    }
    public string DifficultyName=>_currentPreset.Name;
    public string DifficultyDescription=>_currentPreset.Mixed ? "A stable mix of Trash, Easy, Medium, Hard and Sweat bots in the same match. Moving any custom slider switches to Custom." : "Higher sliders make that aspect more capable or more frequent. Settings apply to the next launched server.";
    public string BotChipText=>"AI: Titanium";
    // Profiles -----------------------------------------------------------
    public ObservableCollection<Profile> Profiles { get; } = new();
    public bool HasProfiles => Profiles.Count > 0;
    public bool HasProfile => SelectedProfile is not null;
    public Profile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (!Set(ref _selectedProfile, value)) return;
            _settings.ActiveProfile = value?.Name ?? "";
            Save();
            OnPropertyChanged(nameof(HasProfile));
            OnPropertyChanged(nameof(PlayerChipText));
            OnPropertyChanged(nameof(PlayerName));
            OnPropertyChanged(nameof(PlayerInitial));
            OnPropertyChanged(nameof(PlayerStatus));
            OnPropertyChanged(nameof(ProfileStateLabel));
            OnPropertyChanged(nameof(SelectedProfileModeLabel));
            OnPropertyChanged(nameof(SelectedProfileModeDescription));
        }
    }
    public string PlayerChipText => SelectedProfile is null ? "No profile yet" : $"Playing as {SelectedProfile.Name}";
    public string PlayerName => SelectedProfile?.Name ?? "No profile";
    public string PlayerInitial => string.IsNullOrEmpty(SelectedProfile?.Name)
        ? "?"
        : char.ToUpperInvariant(SelectedProfile!.Name[0]).ToString();
    public string PlayerStatus => SelectedProfile is null ? "Create a profile to play" : $"Local profile · {SelectedProfile.ModeLabel}";
    public string ProfileStateLabel => SelectedProfile is null ? "CREATE PROFILE" : "PROFILE READY";
    public string SelectedProfileModeLabel => SelectedProfile?.ModeLabel ?? "";
    public string SelectedProfileModeDescription => SelectedProfile?.Mode == ProfileMode.Progression
        ? "Progression Mode is for users who want to feel that Nostalgic grinding feeling again. You start at level 0, tier 0. You can complete challenges and work your way through the battle pass"
        : "Unlock All is for users who want to enjoy the classic game but be able to use all cosmetics";
    public bool NewProfileIsProgression
    {
        get => _newProfileIsProgression;
        set
        {
            if (!Set(ref _newProfileIsProgression, value)) return;
            OnPropertyChanged(nameof(NewProfileIsUnlockedAll));
            OnPropertyChanged(nameof(NewProfileModeDescription));
        }
    }
    public bool NewProfileIsUnlockedAll
    {
        get => !_newProfileIsProgression;
        set { if (value) NewProfileIsProgression = false; }
    }
    public string NewProfileModeDescription => NewProfileIsProgression
        ? "Progression Mode is for users who want to feel that Nostalgic grinding feeling again. You start at level 0, tier 0. You can complete challenges and work your way through the battle pass"
        : "Unlock All is for users who want to enjoy the classic game but be able to use all cosmetics";
    public string DifficultyMeta => $"AI: Titanium · {DifficultyName}";
    public string NewProfileName
    {
        get => _newProfileName;
        set
        {
            if (!Set(ref _newProfileName, value)) return;
            if (ProfileError.Length > 0) ProfileError = "";
        }
    }
    public string ProfileError
    {
        get => _profileError;
        private set
        {
            if (Set(ref _profileError, value)) OnPropertyChanged(nameof(HasProfileError));
        }
    }
    public bool HasProfileError => _profileError.Length > 0;
    private void CreateProfile()
    {
        var name = (NewProfileName ?? "").Trim();
        if (!ProfileNameRule.IsMatch(name))
        {
            ProfileError = "Use 3 to 16 letters, numbers, spaces, - or _.";
            return;
        }
        if (Profiles.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            ProfileError = "You already have a profile with that name.";
            return;
        }
        var profile = new Profile { Name = name, Mode = NewProfileIsProgression ? ProfileMode.Progression : ProfileMode.UnlockedAll };
        _settings.Profiles.Add(profile);
        Profiles.Add(profile);
        NewProfileName = "";
        ProfileError = "";
        SelectedProfile = profile; // also saves
    }
    private void DeleteProfile(Profile? profile)
    {
        if (profile is null) return;
        bool wasSelected = ReferenceEquals(profile, SelectedProfile);
        _settings.Profiles.Remove(profile);
        Profiles.Remove(profile);
        if (wasSelected) SelectedProfile = Profiles.FirstOrDefault();
        else Save();
    }
    private void OnProfilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => OnPropertyChanged(nameof(HasProfiles));
    public string BotCountRange
    {
        get=>_settings.BotCountRange;
        set {value??="";if(value==_settings.BotCountRange)return;_settings.BotCountRange=value;Save();OnPropertyChanged();OnPropertyChanged(nameof(BotCountError));}
    }
    public string BotCountError
    {
        get {try{MatchOptions.Parse(_settings.BotCountRange,_settings.EditOnRelease);return "";}catch(InvalidOperationException e){return e.Message;}}
    }
    public bool EditOnRelease
    {
        get=>_settings.EditOnRelease;
        set {if(value==_settings.EditOnRelease)return;_settings.EditOnRelease=value;Save();OnPropertyChanged();}
    }
    // Launch arguments ---------------------------------------------------
    public string LaunchArguments
    {
        get => _settings.LaunchArguments;
        set
        {
            value ??= "";
            if (value == _settings.LaunchArguments) return;
            _settings.LaunchArguments = value;
            Save();
            OnPropertyChanged();
        }
    }
    private void Save() => _store.Save(_settings);
}
