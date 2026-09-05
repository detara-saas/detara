[CmdletBinding()]
param(
    [switch]$IncluirInfraestrutura
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$stateDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "detara-demo"
$statePath = Join-Path $stateDirectory "processes.json"
$composeFile = Join-Path $PSScriptRoot "compose.infrastructure.yml"
$envFile = Join-Path $repoRoot ".env"

function Stop-DemoProcess([int]$processId, [string]$expectedAssembly) {
    $process = Get-CimInstance Win32_Process -Filter "ProcessId = $processId" -ErrorAction SilentlyContinue
    if ($null -eq $process) {
        return
    }

    if ($process.CommandLine -notlike "*$expectedAssembly*") {
        Write-Warning "O PID $processId não pertence mais a $expectedAssembly e não será encerrado."
        return
    }

    Stop-Process -Id $processId
    Write-Host "$expectedAssembly encerrado (PID $processId)." -ForegroundColor Green
}

if (Test-Path -LiteralPath $statePath) {
    $state = Get-Content -Raw -LiteralPath $statePath | ConvertFrom-Json
    Stop-DemoProcess ([int]$state.WebProcessId) "Microsoft.AspNetCore.Components.WebAssembly.DevServer"
    Stop-DemoProcess ([int]$state.ApiProcessId) "Detara.Api.dll"
    Remove-Item -LiteralPath $statePath
}
else {
    Write-Host "Nenhum processo iniciado pelo script de demonstração foi encontrado."
}

if ($IncluirInfraestrutura) {
    Set-Location $repoRoot
    docker compose --project-name detara --env-file $envFile --file $composeFile stop sqlserver
    if ($LASTEXITCODE -ne 0) { throw "Não foi possível parar o SQL Server local." }
    Write-Host "SQL Server local interrompido." -ForegroundColor Green
}
else {
    Write-Host "SQL Server mantido em execução. Use -IncluirInfraestrutura para interrompê-lo também."
}
