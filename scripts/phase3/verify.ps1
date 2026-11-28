param([switch]$Benchmark, [string]$Output = 'artifacts/phase3', [switch]$NoBuild)
$ErrorActionPreference = 'Stop'
if (-not $env:MATCH_TEST_MYSQL) { throw 'MATCH_TEST_MYSQL is required: use a disposable loopback instance, never production.' }
if (Get-Command git -ErrorAction SilentlyContinue) {
    $commit = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify commit' }
} elseif ($env:PHASE3_COMMIT -match '^[0-9a-f]{40}$') {
    # WSL callers may supply the commit when Windows Git is not installed.
    $commit = $env:PHASE3_COMMIT
} else { throw 'Cannot identify commit: install Git or supply PHASE3_COMMIT from git rev-parse HEAD.' }
$Output = [IO.Path]::GetFullPath((Join-Path $Output $commit))
New-Item -ItemType Directory -Force $Output | Out-Null
$env:PHASE3_COMMIT = $commit
$env:PHASE3_OUTPUT = $Output
$env:PHASE3_BENCHMARK = if ($Benchmark) { '1' } else { '0' }
$filter = if ($Benchmark) { 'FullyQualifiedName~Phase3BenchmarkTests' } else { 'FullyQualifiedName!~Phase3BenchmarkTests' }
$name = if ($Benchmark) { 'benchmark' } else { 'transport' }
$argsList = @('test','Tests/TransportTests/TransportTests.csproj','--configuration','Release','--filter',$filter,'--logger',"trx;LogFileName=$name-$commit.trx",'--results-directory',$Output)
if ($NoBuild) { $argsList += '--no-build' }
& dotnet @argsList
$testExit = $LASTEXITCODE
$trxPath = Join-Path $Output "$name-$commit.trx"
if (-not (Test-Path $trxPath)) { throw 'Test run did not produce TRX' }
[xml]$trx = Get-Content -Raw $trxPath
$results = @($trx.TestRun.Results.UnitTestResult)
$skipped = @($results | Where-Object outcome -eq 'NotExecuted')
$required = if ($Benchmark) { @('RealTlsMySqlMoveStateCapacityAndRecovery') } else { @('MigrationPersistsSnapshotAndFencesStaleAndDuplicateTerminalWrites','ConfirmedChessMovesSurviveWorkerFailoverWithMySql','ReclaimRejectsSnapshotSupersededByAcknowledgedMove','AbortPersistsTerminalRevisionInRowAndSnapshot') }
$missing = @($required | Where-Object { $namePart = $_; -not ($results | Where-Object { $_.testName -like "*$namePart*" -and $_.outcome -eq 'Passed' }) })
$summary = @{ commit=$commit; configuredDatabase=$true; mode=$name; passed=@($results | Where-Object outcome -eq 'Passed').Count; failed=@($results | Where-Object outcome -eq 'Failed').Count; skipped=$skipped.Count; missingRequired=$missing; exitCode=$testExit; trx=$trxPath }
$summary | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $Output "$name-summary.json") -Encoding utf8
$summary | ConvertTo-Json -Depth 6 | Write-Output
if ($testExit -ne 0 -or $skipped.Count -gt 0 -or $missing.Count -gt 0 -or $results.Count -eq 0) { throw 'Phase3 gate failed: failed/skipped/missing tests (see TRX and summary).' }
