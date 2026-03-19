using HockeyTournamentTracker.Domain;

namespace HockeyTournamentTracker.Data;

public interface IStageGroupRepository
{
    Task<IReadOnlyList<GroupInfo>> GetByStageAsync(Guid stageId);
    Task<GroupInfo?> AddGroupAsync(Guid stageId, string name);
    Task RenameGroupAsync(Guid stageId, Guid groupId, string newName);
    Task DeleteGroupAsync(Guid stageId, Guid groupId);
}

