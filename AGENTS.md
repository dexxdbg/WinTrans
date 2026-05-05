# AGENTS.md — WinTrans

## What this is

WinUI 3 / Windows App SDK desktop app (unpackaged). Global hotkey **Ctrl+Shift+T** grabs selected text via synthetic Ctrl+C, translates it via Claude API, and optionally pastes the result back. No MSIX packaging — runs as a plain exe.

## Build & run

```powershell
# from repo root or WinTrans/
dotnet restore WinTrans
dotnet build WinTrans -c Release -r win-x64   # -r is REQUIRED; omitting it fails with a WindowsAppSDKSelfContained arch error

# run in place (Debug is fine for dev)
dotnet run --project WinTrans/WinTrans.csproj

# publish self-contained single-file exe
dotnet publish WinTrans -c Release -r win-x64 --self-contained false
```

No test project exists — verify by building and running manually.

## Project layout

```
WinTrans.sln
WinTrans/
  Program.cs              # STAThread entry; sets MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY
  App.xaml.cs             # OnLaunched: creates MainWindow, Activate(), then InitializeHotkey()
  MainWindow.xaml.cs      # All UI logic, hotkey callback, translate flow
  Services/
    ClaudeApiClient.cs    # HTTP to Anthropic API; model constant here
    ClipboardHelper.cs    # Ctrl+C / Ctrl+V emulation with timed delays
    HotkeyManager.cs      # RegisterHotKey / WndProc subclass
    SettingsStore.cs      # %APPDATA%\WinTrans\settings.json
    TrayIcon.cs           # System tray icon and context menu
    Win32.cs              # P/Invoke declarations
```

Single-project solution — no multi-package boundaries.

## Key architecture facts

- **Startup flow**: `App.OnLaunched` → `MainWindow.Activate()` (must happen before hotkey registration to get a valid HWND) → `InitializeHotkey()` → `HideWindow()`. Window starts hidden in tray.
- **Closing = hide**: `AppWindow.Closing` is cancelled and redirects to `HideWindow()`. The window never actually closes until `ExitApp()` is called from the tray menu.
- **Hotkey flow**: hide our window → 120 ms delay → `ClipboardHelper.GetSelectedTextAsync()` (saves clipboard, sends Ctrl+C, waits 200 ms, reads result, restores clipboard) → show our window → optionally paste back.
- **Timing is load-bearing**: The `Task.Delay` calls in `ClipboardHelper` and `OnHotkeyPressedAsync` exist to let focus return to the previous window before synthetic key events fire. Do not remove them without testing.
- **DISABLE_XAML_GENERATED_MAIN**: defined in csproj so the custom `Program.cs` main is used instead of the generated one. Required for self-contained single-file builds.

## Changing model or hotkey

- Claude model: `Services/ClaudeApiClient.cs` — `private const string Model`
- Hotkey combo: `MainWindow.xaml.cs` — `MOD_CONTROL`, `MOD_SHIFT`, `VK_T` constants + `_hotkeyManager.Register(...)` call

## Settings

Persisted to `%APPDATA%\WinTrans\settings.json` — API key, base URL, language index, style index. No environment variables are read at runtime.

## No CI / no tests

No `.github/workflows`, no test project, no linter config. The only verification step is a clean `dotnet build`.
