using HockeyTournamentTracker.Data;
using HockeyTournamentTracker.Presentation;

namespace HockeyTournamentTracker;

public partial class App : Application
{
	public App(LocalDatabase database, IAppThemeSettings appThemeSettings)
	{
		InitializeComponent();
		_ = database.InitializeAsync();
		ApplySavedTheme(appThemeSettings.GetPreferredTheme());
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}

    private void ApplySavedTheme(AppThemePreference preference)
    {
        UserAppTheme = preference switch
        {
            AppThemePreference.Light => AppTheme.Light,
            AppThemePreference.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };
    }
}