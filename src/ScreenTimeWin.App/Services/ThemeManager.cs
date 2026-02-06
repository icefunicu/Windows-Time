using System.Windows;

namespace ScreenTimeWin.App.Services;

public static class ThemeManager
{
    public enum Theme { Light, Dark }

    public static Theme CurrentTheme { get; private set; } = Theme.Light;

    public static void ApplyTheme(Theme theme)
    {
        var dict = new ResourceDictionary { Source = new Uri($"pack://application:,,,/Themes/{theme}.xaml") };
        
        // Remove old theme
        var oldDict = Application.Current.Resources.MergedDictionaries.FirstOrDefault(d => d.Source != null && d.Source.ToString().Contains("Themes/"));
        if (oldDict != null)
        {
            Application.Current.Resources.MergedDictionaries.Remove(oldDict);
        }
        
        Application.Current.Resources.MergedDictionaries.Add(dict);
        CurrentTheme = theme;
    }
    
    public static void ToggleTheme()
    {
        ApplyTheme(CurrentTheme == Theme.Light ? Theme.Dark : Theme.Light);
    }
}
