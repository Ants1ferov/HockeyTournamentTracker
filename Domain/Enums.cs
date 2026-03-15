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
    Scheduled,
    Finished,
    Cancelled
}

public enum OutcomeType
{
    Regulation,
    Overtime,
    Shootout
}

