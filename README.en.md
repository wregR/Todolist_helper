# Todolist_helper

DesktopTodo · 今日计划

[中文](README.md) | **English**

Keep today's tasks visible on your desktop, and plan the coming days or an entire month in a separate planning window.

Current version: **0.1.0**. Target: **Windows 11 x64**. Licensed under [MIT](LICENSE).

> This is English documentation. The application interface is currently in Chinese.

## Features

- **Persistent desktop window:** always on top by default, opens at sign-in, stays off the taskbar, and is managed through the system tray.
- **Today's tasks:** changes automatically at midnight; unfinished older tasks appear under “待补办” (Backlog).
- **Whole-row gestures:** drag vertically to reorder or swipe right to complete. The initial direction locks to prevent accidental completion.
- **Monthly planning:** monthly goals, daily task tables, drafts across months, and multi-day text import.
- **Optional reminders:** deadlines, a soft chime, and catch-up reminders after the app resumes.
- **Local storage:** SQLite and daily backups; no account, cloud sync, or in-app telemetry.

## Requirements and verification scope

| Item | Current status |
|---|---|
| Recommended OS | Windows 11 x64, on a version supported by Microsoft |
| Verified machine | Windows 11 25H2, build 26200; core and UI checks ran on this machine |
| Windows 10 | Not tested on hardware; support depends on the particular OS version and .NET 10 lifecycle |
| ARM64, 32-bit Windows, macOS, Linux | No dedicated release packages; Windows ARM64 x64 emulation has not been tested |
| .NET | The portable package includes the runtime; users do not need the SDK |
| Fonts | Uses installed Noto Sans SC when available, otherwise Microsoft YaHei UI; no font files are bundled |
| Scripts | Build, packaging, and command registration scripts have been verified with PowerShell 7 |

The 100%, 125%, and 150% checks are WPF rendering checks, not a complete matrix of system DPI, monitor, and GPU configurations. See [Compatibility](docs/COMPATIBILITY.md).

## Install and launch

1. Obtain the Windows x64 ZIP from [GitHub Releases](https://github.com/wregR/Todolist_helper/releases/latest).
2. Compare its SHA-256 with the supplied `.sha256` file using `Get-FileHash <archive.zip> -Algorithm SHA256`.
3. Extract the complete folder into a stable location writable by your user account.
4. Run **app/DesktopTodo.exe**.

The app runs with normal user privileges. Its first launch enables sign-in startup for the current user by default; this can be disabled under right-click → “设置” (Settings). The public build uses the startup entry `DesktopTodo.Public`.

The executable is currently unsigned. A matching checksum establishes consistency with the publisher's supplied file, not publisher identity or a security audit.

## Daily use

| Action | How |
|---|---|
| Add or edit a task | Right-click the background or a task |
| Plan future dates or a month | “编辑全部计划” — Edit all plans |
| Reorder | Hold anywhere on the task and drag vertically, within the same date and completion state |
| Complete | Swipe right past 60% of the row width and at least 140 DIP, then release |
| Cancel a gesture | Press Esc; focus or mouse-capture loss also cancels |
| Undo completion | Right-click → “恢复未完成” |
| Set a deadline or reminder | Edit → “高级选项” (Advanced options) |
| Move the window | Drag the empty area of its header |
| Hide or exit | Use the context menu or tray; reminders continue while hidden |

Clicking or double-clicking never completes a task. In the single-task editor, Enter saves and Esc cancels unsubmitted changes. Leaving the editor saves valid input; cancelling later does not undo content already saved.

## Plan several days or an entire month

Open **“编辑全部计划” (Edit all plans)**. Write the month's goals at the top and edit daily tasks in the table. Each day can have multiple tasks. Switching months keeps drafts; **“保存全部” (Save all)** commits them together.

Choose **“粘贴多日计划” (Paste multi-day plan)**. Put each date on its own line, followed by one task per line:

```text
2026-11-03
1. Organize reference material
2. Check the records

2026-11-04
1. Finish the charts
```

Full dates, Chinese month/day headings, and the Chinese relative headings “今天”, “明天”, and “后天” are supported. Relative dates use the computer's current date; month/day-only headings use the editor's current year. Imported tasks enter the draft rather than directly overwriting saved tasks. Removing existing tasks requires confirmation when saving.

## Launch from a terminal

Run once from the extracted root directory:

```powershell
.\enable-command.ps1
```

Restart your terminal, then run from any directory:

```text
todo
```

If the application is already running, the command brings back that instance. Registration changes only the current user's PATH and stops if another `todo` command exists. If script policy prevents execution, the application can still be launched by double-clicking; follow your organization's script policy rather than lowering security settings to run the app.

## Data, updates, and removal

- `data/todo.db`: tasks, monthly plans, settings, and reminder state; **not encrypted**.
- `data/backups`: one backup on the first launch of each day, retaining the latest seven date-named backups.
- `data/error.log`: local error log, created when needed.
- Normal app use makes no network requests. Downloads, builds, dependency audits, and GitHub Actions require network access. Windows services operate independently of the app.

Before updating, exit through the tray, back up `data`, and replace the application files while keeping `data`. After moving the whole folder, launch once to update the startup entry; adjust the registered command PATH separately.

To remove the app, disable “开机自动展开” (Open at sign-in), then exit. If the command was registered, remove this copy's `bin` directory from your user PATH. Back up any data you want to keep before deleting the folder.

The app does not wake the computer or sound while shut down, asleep, or fully exited. It checks missed reminders when running again. Dismissed reminders do not repeatedly reappear, and dismissing a reminder does not complete its task. System volume and audio output affect the chime.

## Build from source

Use Windows and the .NET SDK selected by `global.json` (currently 10.0.403). Install that SDK or place it inside the project:

```powershell
.\setup-sdk.ps1
.\build.ps1
.\release.ps1
```

- `build.ps1` runs the core checks and builds into `app`. Exit any application using that output folder first.
- `release.ps1` builds a ZIP and SHA-256 in an isolated staging directory without overwriting an existing running installation.
- `artifacts/releases` contains packages; personal databases, the SDK, and machine-specific shortcuts are excluded.
- Project SDK state and dependency caches are under `.tools`.

Windows builds are configured for main and pull requests. Version tags create a **draft** GitHub Release. The Windows workflow has passed on GitHub; consult [Actions](https://github.com/wregR/Todolist_helper/actions) for subsequent builds. See [Publishing](docs/PUBLISHING.md).

## License and third-party components

This project is licensed under [MIT](LICENSE), permitting use, modification, and redistribution, including commercial use, while retaining the copyright and license notice. Copyright is attributed collectively to the project contributors.

Third-party components retain their own licenses. Packages include [Third-party notices](THIRD_PARTY_NOTICES.md) and [license texts](licenses/README.md); retain them when redistributing. See the [release audit](docs/RELEASE_AUDIT.md) for licensing, rights, and release conditions.

[Changelog](CHANGELOG.md) · [中文文档](README.md)
