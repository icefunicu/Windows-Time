# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased] - 2026-02-07

### Added
- **Smoke Test**: Added `smoke_test.ps1` for automated build and test verification.
- **Model Tests**: Added `ModelTests.cs` to verify entity defaults (`LimitRule`, `DailyAggregate`).
- **Hygiene**: Added `.gitignore` to exclude build artifacts and user-specific files.

### Changed
- **Database Path**: Unified SQLite database path to `%LocalAppData%\ScreenTimeWin\ScreenTimeWin.db` to prevent schema drift and ensure persistence across builds.
- **Resource Loading**: optimized `App.xaml` resource dictionary order to fix startup crash (`XamlParseException`).
- **Limit Enforcement**: `Worker.cs` now properly terminates processes using `NativeHelper.KillProcess` when "Force Close" is triggered.
- **Screen Time Accuracy**: Refactored `Worker.cs` to only track the **Foreground Window**. Background apps are no longer counted in "Screen Time".
- **IPC Robustness**:
  - Server: Implemented `ReadExactAsync` to handle message framing correctly.
  - Client: Added error handling to prevent UI crashes on IPC failure.

### Fixed
- **Startup Error**: Fixed `SecondsToTimeConverter` missing resource exception.
- **Limit Logic**: Fixed issue where "Force Close" limits wouldn't actually kill the process.
- **IPC Crashes**: Fixed client-side crash when service is unavailable.
- **ViewModel TODOs**: Replaced "Mock" buttons with helpful dialogs guiding users to correct features.

### Security
- **Path Isolation**: Moved DB out of application bin folder to user-specific data folder.
