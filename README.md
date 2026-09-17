# Desktop Organizer

A lightweight Windows desktop utility that keeps your Desktop tidy in the background using fast local rules and optional AI-assisted classification.

Desktop Organizer is built around a simple idea: the user should not have to constantly maintain folders, rename files, or manually clean a cluttered Desktop. The app watches for new files, handles obvious cases locally, and can use the OpenAI API when a file needs more context.

## What it does

- Watches the Windows Desktop without continuously scanning the disk.
- Sorts obvious file types locally for speed and low resource usage.
- Uses optional OpenAI-powered classification for ambiguous files.
- Stores the user's API key encrypted for the current Windows account.
- Runs quietly in the system tray and can be opened at any time.
- Keeps an activity history of file moves.
- Provides one-click Undo for organized files.
- Ignores shortcuts, folders, hidden/system files, and common temporary files.
- Keeps automatic sorting off by default until the user enables it.

## Design goals

**Simple for the user.** Open the app, add an API key if AI classification is wanted, and let it work.

**Quiet in the background.** The app uses Windows file-system events instead of repeatedly scanning the Desktop.

**AI where it is useful.** Images, installers, archives and other obvious files do not need an LLM. AI is reserved for files where filename/context can improve the decision.

**Safe by default.** Desktop Organizer does not delete files. File moves are logged and can be undone.

## Current status

Early development / alpha. The first Windows-native foundation is now in the repository. Expect behaviour and UI details to evolve while the core workflow is refined.

## Technology

- C# / .NET 8
- WPF desktop UI
- `FileSystemWatcher` for low-overhead background monitoring
- Windows DPAPI for local API-key protection
- OpenAI Responses API for optional AI classification

## Running from source

Requirements:

- Windows 10 or Windows 11
- .NET 8 SDK

```powershell
dotnet run --project src/DesktopOrganizer/DesktopOrganizer.csproj
```

The project is configured as a Windows GUI application (`WinExe`), so the released app does not depend on a command-prompt window.

## Privacy

The core organizer works locally. When AI classification is enabled, Desktop Organizer sends only the metadata/context needed to classify an ambiguous file. Small text snippets may be included for supported text-based files. The application does not intentionally upload entire files for classification.

## Safety

Desktop Organizer is deliberately conservative:

- it never deletes files;
- it does not move folders;
- it ignores shortcuts and temporary/system files;
- every successful move is written to the activity history;
- moved files can be restored with Undo;
- if a destination filename already exists, a unique filename is generated instead of overwriting it.

## License

Copyright © 2026 Thomas Stemmet. All rights reserved.

This repository is publicly viewable, but the software is proprietary. See [LICENSE](LICENSE) for the full terms.
