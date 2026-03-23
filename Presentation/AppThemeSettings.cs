using Microsoft.Maui.Storage;

namespace HockeyTournamentTracker.Presentation;

public interface IAppThemeSettings
{
    AppThemePreference GetPreferredTheme();
    AppThemePreference ThemePreference { get; }
    int GetSelectedIndex();
    void ApplyByIndex(int selectedIndex);
}

public enum AppThemePreference
{
    System = 0,
    Light = 1,
    Dark = 2
}

public sealed class AppThemeSettings : IAppThemeSettings
{
    private const string PreferenceKey = "app_theme_preference";

    public AppThemePreference ThemePreference => GetPreferredTheme();

    public int GetSelectedIndex() => (int)ThemePreference;

    public void ApplyByIndex(int selectedIndex)
    {
        if (!Enum.IsDefined(typeof(AppThemePreference), selectedIndex))
            selectedIndex = (int)AppThemePreference.System;

        SetPreferredTheme((AppThemePreference)selectedIndex);
    }

    public AppThemePreference GetPreferredTheme()
    {
        var raw = Preferences.Default.Get(PreferenceKey, (int)AppThemePreference.System);
        return Enum.IsDefined(typeof(AppThemePreference), raw)
            ? (AppThemePreference)raw
            : AppThemePreference.System;
    }

    public static void SetPreferredTheme(AppThemePreference preference)
    {
        Preferences.Default.Set(PreferenceKey, (int)preference);
        Apply(preference);
    }

    public static void Apply(AppThemePreference preference)
    {
        if (Application.Current is null)
            return;

        Application.Current.UserAppTheme = preference switch
        {
            AppThemePreference.Light => AppTheme.Light,
            AppThemePreference.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };
    }
}
