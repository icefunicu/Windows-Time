using ScreenTimeWin.App.Properties;

namespace ScreenTimeWin.App.Helpers;

public static class CategoryHelper
{
    public static string GetLocalizedCategory(string englishName)
    {
        return englishName switch
        {
            "Development" => Resources.CategoryDevelopment,
            "Work" => Resources.CategoryWork,
            "Browser" => Resources.CategoryBrowser,
            "Social" => Resources.CategorySocial,
            "Entertainment" => Resources.CategoryEntertainment,
            "Games" => Resources.CategoryGames,
            "Learning" => Resources.CategoryLearning,
            "Communication" => Resources.CategoryCommunication,
            "Productivity" => Resources.CategoryProductivity,
            "Media" => Resources.CategoryMedia,
            "Other" => Resources.CategoryOther,
            _ => englishName
        };
    }

    public static string GetEnglishCategory(string localizedName)
    {
        if (localizedName == Resources.CategoryDevelopment) return "Development";
        if (localizedName == Resources.CategoryWork) return "Work";
        if (localizedName == Resources.CategoryBrowser) return "Browser";
        if (localizedName == Resources.CategorySocial) return "Social";
        if (localizedName == Resources.CategoryEntertainment) return "Entertainment";
        if (localizedName == Resources.CategoryGames) return "Games";
        if (localizedName == Resources.CategoryLearning) return "Learning";
        if (localizedName == Resources.CategoryCommunication) return "Communication";
        if (localizedName == Resources.CategoryProductivity) return "Productivity";
        if (localizedName == Resources.CategoryMedia) return "Media";
        if (localizedName == Resources.CategoryOther) return "Other";
        return localizedName;
    }
}
