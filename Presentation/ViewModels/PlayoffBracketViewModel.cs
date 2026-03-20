using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using HockeyTournamentTracker.Data;
using HockeyTournamentTracker.Domain;

namespace HockeyTournamentTracker.Presentation.ViewModels;

public sealed class PlayoffBracketViewModel : INotifyPropertyChanged
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly IStageRepository _stageRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly IStageTeamRepository _stageTeamRepository;
    private readonly IPlayoffRepository _playoffRepository;
    private readonly StatsService _statsService;

    private Tournament? _tournament;
    private Stage? _stage;
    private bool _useReseeding;
    private bool _hasThirdPlaceMatch;

    public Tournament? Tournament
    {
        get => _tournament;
        private set => SetField(ref _tournament, value);
    }

    public Stage? Stage
    {
        get => _stage;
        private set
        {
            if (SetField(ref _stage, value))
                OnPropertyChanged(nameof(PageTitle));
        }
    }

    public string PageTitle => Stage is null ? "Сетка плей-офф" : $"Сетка: {Stage.Name}";

    public bool UseReseeding
    {
        get => _useReseeding;
        set => SetField(ref _useReseeding, value);
    }

    public bool HasThirdPlaceMatch
    {
        get => _hasThirdPlaceMatch;
        set => SetField(ref _hasThirdPlaceMatch, value);
    }

    public ObservableCollection<Team> Teams { get; } = new();
    public ObservableCollection<Stage> SeedSourceStages { get; } = new();
    public ObservableCollection<PlayoffRoundUi> Rounds { get; } = new();

    public PlayoffBracketViewModel(
        ITournamentRepository tournamentRepository,
        IStageRepository stageRepository,
        ITeamRepository teamRepository,
        IMatchRepository matchRepository,
        IStageTeamRepository stageTeamRepository,
        IPlayoffRepository playoffRepository,
        StatsService statsService)
    {
        _tournamentRepository = tournamentRepository;
        _stageRepository = stageRepository;
        _teamRepository = teamRepository;
        _matchRepository = matchRepository;
        _stageTeamRepository = stageTeamRepository;
        _playoffRepository = playoffRepository;
        _statsService = statsService;
    }

    public async Task LoadAsync(Guid tournamentId, Guid stageId)
    {
        Tournament = await _tournamentRepository.GetByIdAsync(tournamentId);
        Stage = await _stageRepository.GetByIdAsync(stageId);
        if (Tournament is null || Stage is null)
            return;

        var settings = await _playoffRepository.GetSettingsAsync(stageId);
        UseReseeding = settings.UseReseeding;
        HasThirdPlaceMatch = settings.HasThirdPlaceMatch;

        var allTeams = await _teamRepository.GetByTournamentAsync(tournamentId);
        var stageTeamIds = await _stageTeamRepository.GetTeamIdsByStageAsync(stageId);
        var stageTeamSet = stageTeamIds.ToHashSet();
        var teamsForPlayoff = stageTeamSet.Count > 0
            ? allTeams.Where(t => stageTeamSet.Contains(t.Id))
            : allTeams;

        var sortedTeams = teamsForPlayoff
            .OrderBy(t => t.Name, StringComparer.Create(CultureInfo.CurrentCulture, true))
            .ToList();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Teams.Clear();
            foreach (var t in sortedTeams)
                Teams.Add(t);
        });

        var stages = await _stageRepository.GetByTournamentAsync(tournamentId);
        var seedStages = stages
            .Where(s => s.Id != stageId && s.StageType == StageType.Swiss)
            .OrderBy(s => s.Order)
            .ThenBy(s => s.Name)
            .ToList();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            SeedSourceStages.Clear();
            foreach (var s in seedStages)
                SeedSourceStages.Add(s);
        });

        await EnsureThirdPlaceSeriesAsync();
        await RecalculateWinnersAndAdvanceAsync();
        await LoadRoundsUiAsync();
    }

    public async Task SaveSettingsAsync()
    {
        if (Stage is null)
            return;

        await _playoffRepository.SaveSettingsAsync(new PlayoffSettings
        {
            StageId = Stage.Id,
            UseReseeding = UseReseeding,
            HasThirdPlaceMatch = HasThirdPlaceMatch
        });

        await EnsureThirdPlaceSeriesAsync();
        await RecalculateWinnersAndAdvanceAsync();
        await LoadRoundsUiAsync();
    }

    public async Task AddRoundAsync(string name, int bestOf)
    {
        if (Stage is null || string.IsNullOrWhiteSpace(name))
            return;

        var existing = await _playoffRepository.GetRoundsAsync(Stage.Id);
        await _playoffRepository.SaveRoundAsync(new PlayoffRound
        {
            Id = Guid.NewGuid(),
            StageId = Stage.Id,
            Name = name.Trim(),
            Order = existing.Count > 0 ? existing.Max(r => r.Order) + 1 : 0,
            DefaultBestOf = NormalizeBestOf(bestOf)
        });

        await EnsureThirdPlaceSeriesAsync();
        await LoadRoundsUiAsync();
    }

    public async Task RenameRoundAsync(Guid roundId, string newName)
    {
        if (Stage is null || string.IsNullOrWhiteSpace(newName))
            return;

        var round = (await _playoffRepository.GetRoundsAsync(Stage.Id)).FirstOrDefault(r => r.Id == roundId);
        if (round is null)
            return;

        round.Name = newName.Trim();
        await _playoffRepository.SaveRoundAsync(round);
        await LoadRoundsUiAsync();
    }

    public async Task UpdateRoundBestOfAsync(Guid roundId, int bestOf)
    {
        if (Stage is null)
            return;

        var round = (await _playoffRepository.GetRoundsAsync(Stage.Id)).FirstOrDefault(r => r.Id == roundId);
        if (round is null)
            return;

        round.DefaultBestOf = NormalizeBestOf(bestOf);
        await _playoffRepository.SaveRoundAsync(round);

        await RecalculateWinnersAndAdvanceAsync();
        await LoadRoundsUiAsync();
    }

    public async Task DeleteRoundAsync(Guid roundId)
    {
        await _playoffRepository.DeleteRoundAsync(roundId);
        await EnsureThirdPlaceSeriesAsync();
        await RecalculateWinnersAndAdvanceAsync();
        await LoadRoundsUiAsync();
    }

    public async Task AddSeriesAsync(Guid roundId, bool isThirdPlace = false)
    {
        if (Stage is null)
            return;

        var series = await _playoffRepository.GetSeriesByRoundAsync(roundId);
        var nextSlot = series.Count > 0 ? series.Max(s => s.Slot) + 1 : 0;
        await _playoffRepository.SaveSeriesAsync(new PlayoffSeries
        {
            Id = Guid.NewGuid(),
            StageId = Stage.Id,
            RoundId = roundId,
            Slot = nextSlot,
            IsThirdPlace = isThirdPlace
        });

        await RecalculateWinnersAndAdvanceAsync();
        await LoadRoundsUiAsync();
    }

    public async Task DeleteSeriesAsync(Guid seriesId)
    {
        if (Stage is null || Tournament is null)
            return;

        var matches = await _matchRepository.GetByTournamentAsync(Tournament.Id);
        var linked = matches.Where(m => m.SeriesId == seriesId).ToList();
        foreach (var m in linked)
        {
            m.SeriesId = null;
            await _matchRepository.SaveAsync(m);
        }

        await _playoffRepository.DeleteSeriesAsync(seriesId);
        await RecalculateWinnersAndAdvanceAsync();
        await LoadRoundsUiAsync();
    }

    public async Task<bool> UpdateSeriesAsync(Guid seriesId, Guid? homeTeamId, Guid? awayTeamId, int? bestOfOverride)
    {
        if (Stage is null)
            return false;
        if (homeTeamId.HasValue && awayTeamId.HasValue && homeTeamId == awayTeamId)
            return false;

        var allSeries = await _playoffRepository.GetSeriesByStageAsync(Stage.Id);
        var series = allSeries.FirstOrDefault(s => s.Id == seriesId);
        if (series is null)
            return false;

        var homeChanged = series.HomeTeamId != homeTeamId;
        var awayChanged = series.AwayTeamId != awayTeamId;

        series.HomeTeamId = homeTeamId;
        series.AwayTeamId = awayTeamId;
        series.BestOfOverride = bestOfOverride is > 0 ? NormalizeBestOf(bestOfOverride.Value) : null;

        if (homeChanged)
            series.HomeSeed = null;
        if (awayChanged)
            series.AwaySeed = null;
        if (homeChanged || awayChanged)
            series.WinnerTeamId = null;

        await _playoffRepository.SaveSeriesAsync(series);
        await RecalculateWinnersAndAdvanceAsync();
        await LoadRoundsUiAsync();
        return true;
    }

    public async Task<bool> AutoFillFirstRoundFromStageAsync(Guid sourceStageId)
    {
        if (Stage is null || Tournament is null)
            return false;

        var rounds = (await _playoffRepository.GetRoundsAsync(Stage.Id))
            .OrderBy(r => r.Order)
            .ToList();
        if (rounds.Count == 0)
            return false;

        var firstRound = rounds[0];
        var series = (await _playoffRepository.GetSeriesByRoundAsync(firstRound.Id))
            .Where(s => !s.IsThirdPlace)
            .OrderBy(s => s.Slot)
            .ToList();
        if (series.Count == 0)
            return false;

        var seededTeams = await BuildSeededTeamsFromStageAsync(sourceStageId);
        var required = Math.Min(series.Count * 2, seededTeams.Count);
        if (required < 2)
            return false;

        for (var i = 0; i < series.Count; i++)
        {
            var homeIdx = i;
            var awayIdx = required - 1 - i;
            if (homeIdx >= required || awayIdx < 0 || homeIdx >= awayIdx)
                break;

            var home = seededTeams[homeIdx];
            var away = seededTeams[awayIdx];

            series[i].HomeTeamId = home.TeamId;
            series[i].AwayTeamId = away.TeamId;
            series[i].HomeSeed = home.Seed;
            series[i].AwaySeed = away.Seed;
            series[i].WinnerTeamId = null;
            await _playoffRepository.SaveSeriesAsync(series[i]);
        }

        await RecalculateWinnersAndAdvanceAsync();
        await LoadRoundsUiAsync();
        return true;
    }

    private async Task<List<SeededTeam>> BuildSeededTeamsFromStageAsync(Guid sourceStageId)
    {
        if (Tournament is null)
            return new List<SeededTeam>();

        var allTeams = await _teamRepository.GetByTournamentAsync(Tournament.Id);
        var matches = await _matchRepository.GetByTournamentAsync(Tournament.Id);
        var sourceMatches = matches.Where(m => m.StageId == sourceStageId).ToList();

        var stageTeamIds = (await _stageTeamRepository.GetTeamIdsByStageAsync(sourceStageId)).ToHashSet();
        if (stageTeamIds.Count == 0)
        {
            foreach (var tid in sourceMatches.SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId }).Distinct())
                stageTeamIds.Add(tid);
        }

        var teamsInStage = allTeams.Where(t => stageTeamIds.Contains(t.Id)).ToList();
        var standings = _statsService.CalculateStandings(Tournament, teamsInStage, sourceMatches);
        var sorted = StatsService.SortByRules(standings, Tournament.Rules ?? new TournamentRules());

        var result = new List<SeededTeam>();
        var seed = 1;
        foreach (var row in sorted)
        {
            result.Add(new SeededTeam(row.TeamId, seed++));
        }

        return result;
    }

    private async Task EnsureThirdPlaceSeriesAsync()
    {
        if (Stage is null)
            return;

        var rounds = (await _playoffRepository.GetRoundsAsync(Stage.Id))
            .OrderBy(r => r.Order)
            .ToList();
        if (rounds.Count == 0)
            return;

        var finalRound = rounds[^1];
        var finalSeries = await _playoffRepository.GetSeriesByRoundAsync(finalRound.Id);
        var third = finalSeries.FirstOrDefault(s => s.IsThirdPlace);

        if (HasThirdPlaceMatch)
        {
            if (third is null)
            {
                var slot = finalSeries.Count > 0 ? finalSeries.Max(s => s.Slot) + 1 : 1;
                await _playoffRepository.SaveSeriesAsync(new PlayoffSeries
                {
                    Id = Guid.NewGuid(),
                    StageId = Stage.Id,
                    RoundId = finalRound.Id,
                    Slot = slot,
                    IsThirdPlace = true
                });
            }
        }
        else if (third is not null)
        {
            await _playoffRepository.DeleteSeriesAsync(third.Id);
        }
    }

    private async Task RecalculateWinnersAndAdvanceAsync()
    {
        if (Stage is null || Tournament is null)
            return;

        var rounds = (await _playoffRepository.GetRoundsAsync(Stage.Id))
            .OrderBy(r => r.Order)
            .ToList();
        if (rounds.Count == 0)
            return;

        var allSeries = (await _playoffRepository.GetSeriesByStageAsync(Stage.Id)).ToList();
        var matches = (await _matchRepository.GetByTournamentAsync(Tournament.Id))
            .Where(m => m.StageId == Stage.Id)
            .ToList();

        var changedSeries = new List<PlayoffSeries>();
        foreach (var s in allSeries)
        {
            var round = rounds.FirstOrDefault(r => r.Id == s.RoundId);
            if (round is null)
                continue;

            var bestOf = GetEffectiveBestOf(s, round.DefaultBestOf);
            var winsNeeded = bestOf / 2 + 1;
            var finished = matches.Where(m => m.SeriesId == s.Id && m.Status == MatchStatus.Finished).ToList();
            var homeWins = s.HomeTeamId.HasValue ? finished.Count(m => m.WinnerTeamId == s.HomeTeamId.Value) : 0;
            var awayWins = s.AwayTeamId.HasValue ? finished.Count(m => m.WinnerTeamId == s.AwayTeamId.Value) : 0;

            Guid? winner = null;
            if (s.HomeTeamId.HasValue && homeWins >= winsNeeded)
                winner = s.HomeTeamId.Value;
            else if (s.AwayTeamId.HasValue && awayWins >= winsNeeded)
                winner = s.AwayTeamId.Value;

            if (winner != s.WinnerTeamId)
            {
                s.WinnerTeamId = winner;
                changedSeries.Add(s);
            }
        }

        for (var idx = 0; idx < rounds.Count - 1; idx++)
        {
            var currentRound = rounds[idx];
            var nextRound = rounds[idx + 1];

            var currentSeries = allSeries
                .Where(s => s.RoundId == currentRound.Id && !s.IsThirdPlace)
                .OrderBy(s => s.Slot)
                .ToList();
            var nextSeries = allSeries
                .Where(s => s.RoundId == nextRound.Id && !s.IsThirdPlace)
                .OrderBy(s => s.Slot)
                .ToList();
            if (nextSeries.Count == 0)
                continue;

            if (UseReseeding)
            {
                var winners = currentSeries
                    .Where(s => s.WinnerTeamId.HasValue)
                    .Select(s => new SeededTeam(s.WinnerTeamId!.Value, GetWinnerSeed(s)))
                    .OrderBy(t => t.Seed <= 0 ? int.MaxValue : t.Seed)
                    .ToList();

                for (var n = 0; n < nextSeries.Count; n++)
                {
                    var home = winners.ElementAtOrDefault(n);
                    var away = winners.ElementAtOrDefault(winners.Count - 1 - n);
                    if (home is null || away is null || home.TeamId == away.TeamId)
                        break;

                    if (ApplyTeamsToSeries(nextSeries[n], home, away))
                        changedSeries.Add(nextSeries[n]);
                }
            }
            else
            {
                for (var n = 0; n < nextSeries.Count; n++)
                {
                    var left = currentSeries.ElementAtOrDefault(n * 2);
                    var right = currentSeries.ElementAtOrDefault(n * 2 + 1);
                    var home = left?.WinnerTeamId is { } h ? new SeededTeam(h, GetWinnerSeed(left)) : null;
                    var away = right?.WinnerTeamId is { } a ? new SeededTeam(a, GetWinnerSeed(right)) : null;
                    if (home is null || away is null)
                        continue;

                    if (ApplyTeamsToSeries(nextSeries[n], home, away))
                        changedSeries.Add(nextSeries[n]);
                }
            }
        }

        if (HasThirdPlaceMatch && rounds.Count >= 2)
        {
            var semiRound = rounds[^2];
            var finalRound = rounds[^1];
            var semiSeries = allSeries
                .Where(s => s.RoundId == semiRound.Id && !s.IsThirdPlace)
                .OrderBy(s => s.Slot)
                .ToList();
            var third = allSeries.FirstOrDefault(s => s.RoundId == finalRound.Id && s.IsThirdPlace);

            if (semiSeries.Count >= 2 && third is not null)
            {
                var loser1 = GetLoserTeam(semiSeries[0]);
                var loser2 = GetLoserTeam(semiSeries[1]);
                if (loser1 is not null && loser2 is not null)
                {
                    if (ApplyTeamsToSeries(third, loser1, loser2))
                        changedSeries.Add(third);
                }
            }
        }

        foreach (var s in changedSeries.DistinctBy(x => x.Id))
            await _playoffRepository.SaveSeriesAsync(s);
    }

    private static bool ApplyTeamsToSeries(PlayoffSeries target, SeededTeam home, SeededTeam away)
    {
        var changed =
            target.HomeTeamId != home.TeamId ||
            target.AwayTeamId != away.TeamId ||
            target.HomeSeed != home.Seed ||
            target.AwaySeed != away.Seed;

        if (!changed)
            return false;

        target.HomeTeamId = home.TeamId;
        target.AwayTeamId = away.TeamId;
        target.HomeSeed = home.Seed;
        target.AwaySeed = away.Seed;
        target.WinnerTeamId = null;
        return true;
    }

    private static SeededTeam? GetLoserTeam(PlayoffSeries series)
    {
        if (!series.WinnerTeamId.HasValue || !series.HomeTeamId.HasValue || !series.AwayTeamId.HasValue)
            return null;

        if (series.WinnerTeamId == series.HomeTeamId)
            return new SeededTeam(series.AwayTeamId.Value, series.AwaySeed ?? 0);
        if (series.WinnerTeamId == series.AwayTeamId)
            return new SeededTeam(series.HomeTeamId.Value, series.HomeSeed ?? 0);
        return null;
    }

    private static int GetWinnerSeed(PlayoffSeries series)
    {
        if (!series.WinnerTeamId.HasValue)
            return 0;

        if (series.WinnerTeamId == series.HomeTeamId)
            return series.HomeSeed ?? 0;
        if (series.WinnerTeamId == series.AwayTeamId)
            return series.AwaySeed ?? 0;

        return 0;
    }

    private async Task LoadRoundsUiAsync()
    {
        if (Stage is null || Tournament is null)
            return;

        var rounds = (await _playoffRepository.GetRoundsAsync(Stage.Id))
            .OrderBy(r => r.Order)
            .ToList();
        var allSeries = await _playoffRepository.GetSeriesByStageAsync(Stage.Id);
        var matches = (await _matchRepository.GetByTournamentAsync(Tournament.Id))
            .Where(m => m.StageId == Stage.Id)
            .ToList();
        var teamById = Teams.ToDictionary(t => t.Id);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Rounds.Clear();
            foreach (var round in rounds)
            {
                var roundUi = new PlayoffRoundUi
                {
                    Id = round.Id,
                    Name = round.Name,
                    Order = round.Order,
                    DefaultBestOf = round.DefaultBestOf
                };

                var seriesInRound = allSeries
                    .Where(s => s.RoundId == round.Id)
                    .OrderBy(s => s.IsThirdPlace ? int.MaxValue : s.Slot)
                    .ToList();
                foreach (var series in seriesInRound)
                {
                    var bestOf = GetEffectiveBestOf(series, round.DefaultBestOf);
                    var winsNeeded = bestOf / 2 + 1;
                    var finished = matches.Where(m => m.SeriesId == series.Id && m.Status == MatchStatus.Finished).ToList();
                    var homeWins = series.HomeTeamId.HasValue ? finished.Count(m => m.WinnerTeamId == series.HomeTeamId.Value) : 0;
                    var awayWins = series.AwayTeamId.HasValue ? finished.Count(m => m.WinnerTeamId == series.AwayTeamId.Value) : 0;

                    var homeName = series.HomeTeamId.HasValue && teamById.TryGetValue(series.HomeTeamId.Value, out var ht)
                        ? ht.Name
                        : "—";
                    var awayName = series.AwayTeamId.HasValue && teamById.TryGetValue(series.AwayTeamId.Value, out var at)
                        ? at.Name
                        : "—";

                    var winnerName = series.WinnerTeamId.HasValue && teamById.TryGetValue(series.WinnerTeamId.Value, out var wt)
                        ? wt.Name
                        : string.Empty;

                    var homeSeedText = series.HomeSeed is > 0 ? $"#{series.HomeSeed} " : string.Empty;
                    var awaySeedText = series.AwaySeed is > 0 ? $"#{series.AwaySeed} " : string.Empty;

                    roundUi.Series.Add(new PlayoffSeriesUi
                    {
                        Id = series.Id,
                        RoundId = round.Id,
                        Slot = series.Slot,
                        HomeTeamId = series.HomeTeamId,
                        AwayTeamId = series.AwayTeamId,
                        HomeTeamName = homeName,
                        AwayTeamName = awayName,
                        HomeSeed = series.HomeSeed,
                        AwaySeed = series.AwaySeed,
                        BestOfOverride = series.BestOfOverride,
                        EffectiveBestOf = bestOf,
                        IsThirdPlace = series.IsThirdPlace,
                        WinnerTeamId = series.WinnerTeamId,
                        Title = $"{(series.IsThirdPlace ? "Матч за 3-е место" : $"Серия {series.Slot + 1}")}: {homeSeedText}{homeName} — {awaySeedText}{awayName}",
                        ScoreText = $"Счёт серии: {homeWins}:{awayWins} (до {winsNeeded} побед)",
                        WinnerText = string.IsNullOrWhiteSpace(winnerName) ? string.Empty : $"Победитель серии: {winnerName}"
                    });
                }

                Rounds.Add(roundUi);
            }
        });
    }

    private static int NormalizeBestOf(int bestOf)
    {
        if (bestOf <= 0)
            bestOf = 1;
        if (bestOf % 2 == 0)
            bestOf += 1;
        return bestOf;
    }

    private static int GetEffectiveBestOf(PlayoffSeries series, int defaultBestOf) =>
        NormalizeBestOf(series.BestOfOverride ?? defaultBestOf);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private sealed record SeededTeam(Guid TeamId, int Seed);
}

public sealed class PlayoffRoundUi
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public int DefaultBestOf { get; set; }
    public ObservableCollection<PlayoffSeriesUi> Series { get; } = new();
}

public sealed class PlayoffSeriesUi
{
    public Guid Id { get; set; }
    public Guid RoundId { get; set; }
    public int Slot { get; set; }
    public Guid? HomeTeamId { get; set; }
    public Guid? AwayTeamId { get; set; }
    public int? HomeSeed { get; set; }
    public int? AwaySeed { get; set; }
    public int? BestOfOverride { get; set; }
    public int EffectiveBestOf { get; set; }
    public bool IsThirdPlace { get; set; }
    public Guid? WinnerTeamId { get; set; }
    public string HomeTeamName { get; set; } = string.Empty;
    public string AwayTeamName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ScoreText { get; set; } = string.Empty;
    public string WinnerText { get; set; } = string.Empty;
}
