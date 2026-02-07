namespace ScreenTimeWin.Core.Models;

public enum ActionOnLimit
{
    NotifyOnly,
    BlockNew,
    ForceClose
}

public enum FocusMode
{
    Normal,
    Pomodoro,
    DeepWork
}

public enum FocusType
{
    Whitelist,
    Blacklist
}
