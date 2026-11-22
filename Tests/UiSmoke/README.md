# WinForms runtime smoke tests

Package-free .NET 8 Windows console executable referencing the real Design project.
Run on Windows, or invoke Windows dotnet from WSL in the repository root:

```sh
"/mnt/c/Program Files/dotnet/dotnet.exe" build Tests/UiSmoke/UiSmoke.csproj
"/mnt/c/Program Files/dotnet/dotnet.exe" run --project Tests/UiSmoke/UiSmoke.csproj --no-build -- --capture
```

Omit `--capture` to skip PNGs. DrawToBitmap captures always stay inside
`Tests/UiSmoke/artifacts`; no desktop screenshots or screenshot services are used.
Exit code 0 means all assertions passed, 1 means missing behavior/runtime failures,
and 2 means the 28-second watchdog expired. Build/restore time is not part of that
runtime limit. The process runs an STA thread and the real
`Application.Run(new ChessApplicationContext())` message loop; WinForms timers
schedule tests and detect unexpected native dialogs on this thread only. Dialogs
are failed and closed, never accepted silently. Async UI exceptions are reported
as RUNTIME; assertion failures as MISSING FEATURE.

Tests use actual shown controls, PerformClick and the panel MouseClick event.
Reflection is test-only, for private controls/game state and AuthConnection.
Registration submits only invalid cases; certificate/endpoint process overrides
also fail closed if validation accidentally reaches the connection path. No
successful sign-in, ranked queue or live server is exercised.

Coverage: empty masked auth fields/AcceptButton, registration limits/mismatch and
back navigation, no fabricated dashboard history, logout session clearing,
deterministic center opening, occupied moves, pending-bot leave cleanup, inline
win/draw, finished banner across theme changes, and solo replay. The draw fixture
is checked independently to contain no existing five. Bot completion is bounded
to 2.5 seconds; the entire process is bounded to 28 seconds.

`artifacts/baseline-red.log` records the pre-integration binary's real failing run.
`artifacts/integration.log` records the latest exercised build. Baseline exposed a
real disposed RichTextBox callback from an orphaned bot timer, not a test startup
failure. Captures are overwritten by the latest `--capture` run.

Not checked: network authentication/ranked play, pixel-perfect layout, screen
reader output or a Visual Studio Designer round trip. Private control renames
require updating this deliberately focused harness.
