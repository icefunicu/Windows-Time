[Setup]
AppId={{5B242788-3442-4217-91EA-7443154C7508}
AppName=ScreenTimeWin
AppVersion=1.0
;AppVerName=ScreenTimeWin 1.0
AppPublisher=My Company, Inc.
AppPublisherURL=https://www.example.com/
AppSupportURL=https://www.example.com/
AppUpdatesURL=https://www.example.com/
DefaultDirName={autopf}\ScreenTimeWin
DisableProgramGroupPage=yes
; Remove the following line to run in administrative install mode (install for all users.)
PrivilegesRequired=lowest
OutputDir=Output
OutputBaseFilename=ScreenTimeWin_Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "src\ScreenTimeWin.App\bin\Debug\net8.0-windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{autoprograms}\ScreenTimeWin"; Filename: "{app}\ScreenTimeWin.App.exe"
Name: "{autodesktop}\ScreenTimeWin"; Filename: "{app}\ScreenTimeWin.App.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\ScreenTimeWin.App.exe"; Description: "{cm:LaunchProgram,ScreenTimeWin}"; Flags: nowait postinstall skipifsilent
