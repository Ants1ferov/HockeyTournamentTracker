using HockeyTournamentTracker.Data;
using HockeyTournamentTracker.Domain;
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
		builder.Services.AddSingleton<IMatchRepository, MatchRepository>();

		builder.Services.AddSingleton<StatsService>();

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

		return builder.Build();
	}
}
