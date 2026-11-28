param(
    [string]$MySqlBin = 'C:\Program Files\MySQL\MySQL Server 26.10\bin',
    [int]$Port = 33370,
    [switch]$Benchmark,
    [string]$Output = 'artifacts/phase3',
    [switch]$NoBuild,
    [switch]$ProbeOnly,
    [switch]$Elo
)
# Never reads installed my.ini, credentials, .env, services or existing datadirs.
# --no-defaults MUST be first. Only this owned child PID and GUID directory are cleaned.
$ErrorActionPreference = 'Stop'
$mysqld = Join-Path $MySqlBin 'mysqld.exe'
$mysql = Join-Path $MySqlBin 'mysql.exe'
if (-not (Test-Path $mysqld) -or -not (Test-Path $mysql)) { throw 'MySQL server/client executables not found; set -MySqlBin.' }
$probe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $Port)
try { $probe.Start() } finally { $probe.Stop() }
$root = Join-Path ([IO.Path]::GetTempPath()) ('phase3-mysql-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $root | Out-Null
$data = Join-Path $root 'data'
$errorLog = Join-Path $root 'mysql-error.log'
$previous = $env:MATCH_TEST_MYSQL
$previousAuth = $env:AUTH_TEST_MYSQL
$child = $null
try {
    & $mysqld --no-defaults --initialize-insecure "--datadir=$data" "--log-error=$errorLog"
    if ($LASTEXITCODE -ne 0) { throw "Temporary MySQL initialization failed: $errorLog" }
    $serverArgs = @('--no-defaults', ('--datadir="' + $data + '"'), '--bind-address=127.0.0.1', "--port=$Port", '--mysqlx=OFF', '--local-infile=OFF', ('--log-error="' + $errorLog + '"'))
    $child = Start-Process -FilePath $mysqld -ArgumentList $serverArgs -PassThru -WindowStyle Hidden
    $ready = $false
    for ($i=0; $i -lt 90; $i++) {
        if ($child.HasExited) { throw "Temporary MySQL exited; see $errorLog" }
        $ErrorActionPreference = 'Continue'
        & $mysql --no-defaults --protocol=TCP --host=127.0.0.1 "--port=$Port" --user=root --execute='SELECT 1' 2>$null | Out-Null
        $probeExit = $LASTEXITCODE
        $ErrorActionPreference = 'Stop'
        if ($probeExit -eq 0) { $ready = $true; break }
        Start-Sleep -Seconds 1
    }
    if (-not $ready) { throw 'Temporary MySQL readiness timeout' }
    # Empty root password exists ONLY on this new loopback-only disposable instance.
    $env:MATCH_TEST_MYSQL = "Server=127.0.0.1;Port=$Port;User ID=root;Password=;SslMode=None;Pooling=false;Connection Timeout=5"
    if ($Elo) {
        $env:AUTH_TEST_MYSQL = $env:MATCH_TEST_MYSQL
        New-Item -ItemType Directory -Force $Output | Out-Null
        & dotnet test OnlineChessGame.slnx -m:1 --configuration Release --filter 'FullyQualifiedName!~Phase3BenchmarkTests' --logger 'trx' --results-directory $Output
        if ($LASTEXITCODE -ne 0) { throw 'Elo full solution tests failed; see TRX.' }
        $results = @(Get-ChildItem $Output -Filter '*.trx' | ForEach-Object { ([xml](Get-Content -Raw $_.FullName)).TestRun.Results.UnitTestResult })
        if (-not $results.Count -or @($results | Where-Object outcome -ne 'Passed').Count) { throw 'Elo verification requires all tests passed, no skips.' }
        Write-Output ("ELO_FULL_SOLUTION_PASSED=" + $results.Count)
    } elseif ($ProbeOnly) {
        & $mysql --no-defaults --protocol=TCP --host=127.0.0.1 "--port=$Port" --user=root --execute='SELECT VERSION(); CREATE DATABASE phase3_probe; DROP DATABASE phase3_probe;'
        if ($LASTEXITCODE -ne 0) { throw 'Disposable database probe failed' }
    } else {
        & "$PSScriptRoot/verify.ps1" -Benchmark:$Benchmark -Output $Output -NoBuild:$NoBuild
    }
}
finally {
    $env:MATCH_TEST_MYSQL = $previous
    $env:AUTH_TEST_MYSQL = $previousAuth
    # Windows mysqld may spawn another mysqld process; scope cleanup to this unique datadir.
    if ($child) {
        Get-CimInstance Win32_Process -Filter "name='mysqld.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($data) } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
        if (-not $child.HasExited) { $child.WaitForExit(10000) | Out-Null }
    }
    New-Item -ItemType Directory -Force $Output | Out-Null
    if (Test-Path $errorLog) { Copy-Item $errorLog (Join-Path $Output 'temporary-mysql-error.log') -Force }
    if ($root -like '*phase3-mysql-*' -and (Test-Path $root)) {
        for ($attempt=0; $attempt -lt 20; $attempt++) {
            try { Remove-Item -LiteralPath $root -Recurse -Force; break }
            catch { if ($attempt -eq 19) { throw }; Start-Sleep -Milliseconds 500 }
        }
    }
}
