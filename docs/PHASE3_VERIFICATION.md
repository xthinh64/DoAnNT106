# Phase 3 reproducible release evidence

## Execution contract

`build-release.yml` now blocks downloadable packages on a dedicated MySQL 8.4 service job. It runs the entire Transport suite (excluding the explicitly opt-in benchmark), then the benchmark serially without rebuilding. TRX and summaries are uploaded even on failure under `phase3-evidence-<full commit>`. The verifier rejects nonzero test exits, zero tests, **any unexpected skip**, or a missing/non-passing required SQL fencing or SQL worker-failover case. A machine without `MATCH_TEST_MYSQL` fails before launching tests; absence of DB is not a green gate.

No existing test or server file is modified by this harness. Do not run parallel `dotnet build/test` against these project directories while other agents are compiling.

## Windows isolated MySQL (no installed credentials)

Discovered executable directory: `C:\Program Files\MySQL\MySQL Server 26.10\bin`. A probe genuinely initialized a **new** GUID datadir, started a loopback server on port 33370, executed `SELECT VERSION(); CREATE DATABASE phase3_probe; DROP DATABASE phase3_probe;`, and returned `26.10.0` with exit 0. No installed `.env`, password file, `my.ini`, service configuration, or existing datadir was read. Existing MySQL services remain untouched.

From repository root in Windows PowerShell:

```powershell
# Probe initialization / create/drop permission without building anything.
./scripts/phase3/with-temporary-mysql.ps1 -ProbeOnly
# Run these SERIALly once other agents have stopped compiling:
./scripts/phase3/with-temporary-mysql.ps1
./scripts/phase3/with-temporary-mysql.ps1 -Benchmark -NoBuild
```

From WSL use `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/phase3/with-temporary-mysql.ps1` and append switches as needed. `-MySqlBin` and `-Port` are configurable. An occupied port is rejected before initialization. All server/client invocations put `--no-defaults` first. The empty-password root account exists only on the newly initialized loopback-only temporary instance. Each test creates and drops an independently named schema; the benchmark uses `phase3_bench_<guid>`. The wrapper restores `MATCH_TEST_MYSQL` and removes only its own GUID datadir in `finally`.

Windows MySQL 26.10 spawns a child `mysqld.exe`; stopping only the launcher leaves files locked. Cleanup therefore enumerates only `mysqld.exe` command lines containing the wrapper's unique datadir, stops those PIDs, and retries deletion. The first probe exposed this behavior; the corrected probe completed successfully. It does not stop any generic MySQL service by name.

For an already-owned isolated loopback DB:

```powershell
$env:MATCH_TEST_MYSQL = 'Server=127.0.0.1;Port=33379;User ID=root;Password=;SslMode=None;Pooling=false'
./scripts/phase3/verify.ps1
./scripts/phase3/verify.ps1 -Benchmark -NoBuild
```

## What the benchmark measures

Defaults: two independent worker PIDs; per-worker capacity/admission budget 3; CARO history lengths 2 and 60; active-room counts 1, 2 (near), 3 (exact admission capacity); ten timed moves per room plus one continuation move after recovery. Limits are configurable by `PHASE3_CAPACITY` (2–32), `PHASE3_SAMPLES` (1–40), `PHASE3_LONG_HISTORY` (2–100). Distinct levels are deduplicated when capacity is 2.

Real pinned self-signed TLS clients sign in through the Gateway, obtain tickets, match, send legal alternating moves, and receive both matching STATE revisions. Authentication uses an in-process identity fixture, **not the account DB**. Match durability uses the production `MySqlMatchStore`. Moves across all rooms are issued concurrently per round; rounds are sequential. Both players' STATE and persisted revision/history are checked. Unmeasured history warmup is followed by measured requests. The MOVE→STATE stopwatch stops before the extra SQL verification read, so the latter is not included in reported transport latency.

For each scenario the initial worker is deliberately killed by its exact pool PID; the survivor is undrained, every room must receive RECOVERING then generation-2 STATE with its last acknowledged revision, and another move must commit. Recovery timing starts before `Kill()` and ends at receipt of both clients' recovered states. Failure details are recorded; a failing scenario makes the benchmark test fail rather than inventing samples. The reported worker `AvgApplyMs` is the production heartbeat's average, **not a percentile of individual rule operations**. DB samples time production `UpdateAsync` including connection/await overhead, not server SQL execution alone.

## Files and interpretation

`artifacts/phase3/<full HEAD>/` contains:

- `transport-<HEAD>.trx`, `transport-summary.json`: passed/failed/skipped counts and required-case status.
- `benchmark-<HEAD>.trx`, `benchmark-summary.json`: benchmark execution status.
- `benchmark.json`: measured scenario metrics, failure list, OS/.NET/MySQL/CPU count, capacities, topology and worker snapshots/PIDs.
- `samples.csv`: actual raw MOVE→STATE, DB update and recovery samples, with scenario, room, revision and milliseconds.

Percentiles use nearest rank and always include sample counts. Small default p99 values are exploratory, not strong tail estimates. No latency threshold has been selected without a baseline. No multi-PC LAN, Wi-Fi reconnect, WinForms UI, abrupt Gateway-process crash, full account database authentication, or UDP RTT claim is made. CI workflow execution still requires a GitHub runner; local probes are not evidence of CI completion.

## Local evidence status at handoff

After correcting the independent review findings, the parent reran the integrated Windows working tree serially against disposable MySQL 26.10.0: **102 Transport tests passed, 0 failed, 0 skipped**; the opt-in benchmark passed all six scenarios. Separate runs produced **15 ClientRecoverySmoke passes, 15 UiSmoke passes and 21 Python passes**. Seven new server regression cases cover ambiguous move/terminal commits, repeated clock compensation and socket loss overlapping worker recovery. The client smoke covers cancelled ordinary matches, genuine resumption and stale callbacks. The follow-up read-only review accepted the server fixes and identified one late-packet cancel/requeue race. That race is now fixed by a per-attempt `QueueAttemptId`, captured when each player queues and echoed individually by the server. The new TLS receive-loop smoke rejects delayed attempt A after requeue B, sends LEAVE_ROOM for A, and opens only correlated B. The integrated results above were rerun after this fix; live multi-machine acceptance remains outstanding.

Measured baseline (single-host Windows, .NET 8.0.31, 16 logical processors, capacity/admission 3):

| History | Rooms | MOVE→STATE samples | p50 ms | p95 ms | p99 ms | Recovery max ms | Request failures |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 2 | 1 | 11 | 4.455 | 36.5959 | 36.5959 | 190.1305 | 0 |
| 2 | 2 | 22 | 5.5053 | 45.7869 | 46.3878 | 94.4069 | 0 |
| 2 | 3 | 33 | 4.796 | 31.7171 | 31.9496 | 219.8519 | 0 |
| 60 | 1 | 11 | 2.4744 | 25.3986 | 25.3986 | 150.3595 | 0 |
| 60 | 2 | 22 | 2.8615 | 28.8191 | 29.2387 | 115.1524 | 0 |
| 60 | 3 | 33 | 3.5363 | 30.1994 | 30.3049 | 513.211 | 0 |

Evidence resides in `artifacts/phase3/e1bc53a88f63c17f2447f33109dadf2cf5172642/`. The working tree differs from HEAD: that hash identifies the base commit only, not a committed version of these changes. Preserve the final source diff alongside evidence before publishing. No GitHub CI or multi-machine acceptance was executed.

If Windows Git is unavailable when invoking from WSL, supply the actual hash explicitly: obtain it with `git rev-parse HEAD` in WSL, then set `$env:PHASE3_COMMIT` in the PowerShell command before calling the wrapper. The verifier accepts this fallback only as a full 40-character hexadecimal hash.
