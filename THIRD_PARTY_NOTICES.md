# Third-party notices / 第三方声明

Reviewed on 2026-11-02 for DesktopTodo 0.1.0. These notices apply to third-party components, not to the project's own code. 本文件不为 DesktopTodo 自身代码授予许可证。

| Component | Version | Terms / 条款 | Retained text |
|---|---|---|---|
| Microsoft.Data.Sqlite; Microsoft.Data.Sqlite.Core | 10.0.0 | MIT; © Microsoft Corporation; .NET Foundation and Contributors | [License](licenses/microsoft-data-sqlite-LICENSE.txt) |
| SQLitePCLRaw.bundle_e_sqlite3; SQLitePCLRaw.config.e_sqlite3 | 3.0.5 | Apache-2.0; Copyright 2014–2026 SourceGear, LLC | [License](licenses/sqlitepclraw-LICENSE.txt) |
| SQLitePCLRaw.core; SQLitePCLRaw.provider.e_sqlite3 | 3.0.5 | Apache-2.0; Copyright 2014–2025 SourceGear, LLC | [License](licenses/sqlitepclraw-LICENSE.txt) |
| SQLite native library, NuGet package SQLite | 3.53.4 | Public-domain declaration in package LICENSE.txt; package metadata: Copyright 2014–2026 SourceGear, LLC | [Declaration](licenses/sqlite-LICENSE.txt), [upstream explanation](https://sqlite.org/copyright.html) |
| Microsoft.NETCore.App.Runtime.win-x64 | 10.0.14 | MIT plus bundled third-party terms | [License](licenses/dotnet-runtime-LICENSE.txt), [notices](licenses/dotnet-runtime-NOTICES.txt) |
| Microsoft.WindowsDesktop.App.Runtime.win-x64 | 10.0.14 | MIT plus WPF/Windows Forms third-party terms | [License](licenses/dotnet-desktop-LICENSE.txt), [WPF notices](licenses/wpf-NOTICES.txt), [Windows Forms notices](licenses/winforms-NOTICES.txt) |

Upstream notices are preserved as supplied, including entries that may apply only to a subset of runtime builds. No third-party component source was patched in this release; the SQLite dependency versions were updated.

Redistributors must retain the applicable license and attribution texts. Apache-2.0 also addresses patent grants, termination, modification notices, and trademarks. The original license texts control.

No font files are distributed. Noto Sans SC and Microsoft YaHei UI are used only if installed on the user's machine. The application's icon and reminder WAV were generated during this project's development; no third-party image or sound recording was imported.

The SDK and ASP.NET Core runtime package may exist in the developer's cache but are not included as application frameworks in this package. The included frameworks are .NET Core and Windows Desktop, as recorded in DesktopTodo.runtimeconfig.json.

See [license provenance](licenses/README.md) and [release audit](docs/RELEASE_AUDIT.md).
