# Contributing

Thanks for taking a look at Scheduler. Small, focused pull requests are easier to review than large mixed changes.

## Development

- Windows + .NET 10 SDK
- Optional: copy `src/Scheduler.App/appsettings.Local.json.example` to `appsettings.Local.json` for local overrides
- Debug user settings live in `doc/data/notifications/settings.json`. A published build stores them in `data/notifications/settings.json` next to the executable
- Build / run:

```powershell
dotnet build src/Scheduler.App/Scheduler.App.csproj
dotnet run --project src/Scheduler.App
```

Do not add or restore keystroke-content logging. Input activity must remain count-only.

## Branch and PR

1. Fork and create a branch from `main` (`fix/...` or `feat/...`).
2. Keep the PR scoped: one problem or one feature.
3. Update README when behavior or configuration changes.
4. Do not commit `appsettings.Local.json`, logs, `bin/`, or `obj/`.
5. `src/Scheduler.LockScreen` is legacy and is not built with the solution. `src/Scheduler.Service` runs the same notification loop as the tray app. Only one process may send; do not add a second scheduler.

CI builds `Scheduler.App` on Windows. Please wait for the check to pass.
