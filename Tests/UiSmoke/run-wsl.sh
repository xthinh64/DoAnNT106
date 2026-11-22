#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
dotnet='/mnt/c/Program Files/dotnet/dotnet.exe'
"$dotnet" build Tests/UiSmoke/UiSmoke.csproj --nologo -v:minimal
mkdir -p Tests/UiSmoke/artifacts
"$dotnet" run --project Tests/UiSmoke/UiSmoke.csproj --no-build -- "$@" 2>&1 | tee Tests/UiSmoke/artifacts/integration.log
