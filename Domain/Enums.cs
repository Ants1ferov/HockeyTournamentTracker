namespace HockeyTournamentTracker.Domain;

public enum TournamentStatus
{
    Planned,
    InProgress,
    Finished,
    Archived
}

public enum MatchStatus
{
    Scheduled = 0,
    Finished = 1,
    Cancelled = 2,
    InProgress = 3
}

public enum OutcomeType
{
    Regulation,
    Overtime,
    Shootout
}

