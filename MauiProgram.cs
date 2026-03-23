using HockeyTournamentTracker.Data;
using HockeyTournamentTracker.Domain;
using HockeyTournamentTracker.Presentation;
using HockeyTournamentTracker.Presentation.ViewModels;
using HockeyTournamentTracker.Presentation.Views;
using Microsoft.Extensions.Logging;

namespace HockeyTournamentTracker;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		// Регистрация зависимостей
		builder.Services.AddSingleton(provider =>
		{
			var dbPath = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				DatabaseConstants.DatabaseFilename);

			return new LocalDatabase(dbPath);
		});

		builder.Services.AddSingleton<ITournamentRepository, TournamentRepository>();
		builder.Services.AddSingleton<ITeamRepository, TeamRepository>();
		builder.Services.AddSingleton<IStageRepository, StageRepository>();
		builder.Services.AddSingleton<IMatchRepository, MatchRepository>();
		builder.Services.AddSingleton<IStageTeamRepository, StageTeamRepository>();
		builder.Services.AddSingleton<IStageGroupRepository, StageGroupRepository>();
		builder.Services.AddSingleton<IPlayoffRepository, PlayoffRepository>();

		builder.Services.AddSingleton<StatsService>();
		builder.Services.AddSingleton<IAppThemeSettings, AppThemeSettings>();

		builder.Services.AddSingleton<TournamentsListViewModel>();
		builder.Services.AddTransient<MainPage>();

		builder.Services.AddTransient<TournamentEditViewModel>();
		builder.Services.AddTransient<TournamentEditPage>();

		builder.Services.AddTransient<TournamentDetailsViewModel>();
		builder.Services.AddTransient<TournamentDetailsPage>();

		builder.Services.AddTransient<MatchEditViewModel>();
		builder.Services.AddTransient<MatchEditPage>();

		builder.Services.AddTransient<TeamsListViewModel>();
		builder.Services.AddTransient<TeamsListPage>();

		builder.Services.AddTransient<TeamEditViewModel>();
		builder.Services.AddTransient<TeamEditPage>();

		builder.Services.AddTransient<TournamentRulesEditViewModel>();
		builder.Services.AddTransient<TournamentRulesEditPage>();

		builder.Services.AddTransient<GroupsListViewModel>();
		builder.Services.AddTransient<GroupsListPage>();

		builder.Services.AddTransient<StageEditViewModel>();
		builder.Services.AddTransient<StageEditPage>();

		builder.Services.AddTransient<StageDetailsViewModel>();
		builder.Services.AddTransient<StageDetailsPage>();
		builder.Services.AddTransient<StageRosterPage>();
		builder.Services.AddTransient<StageMatchesPage>();
		builder.Services.AddTransient<PlayoffBracketViewModel>();
		builder.Services.AddTransient<PlayoffBracketPage>();

		return builder.Build();
	}
}
