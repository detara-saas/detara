[CmdletBinding()]
param(
    [switch]$NaoAbrirNavegador,
    [switch]$PularPreparacao
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$stateDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "detara-demo"
$statePath = Join-Path $stateDirectory "processes.json"
$composeFile = Join-Path $PSScriptRoot "compose.infrastructure.yml"
$envFile = Join-Path $repoRoot ".env"
$apiUrl = "http://localhost:5090"
$webUrl = "http://localhost:5080"

function Assert-CommandAvailable([string]$command) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "Dependência ausente: $command. Instale-a antes de iniciar a demonstração."
    }
}

function Assert-PortAvailable([int]$port) {
    if (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue) {
        throw "A porta $port já está em uso. Execute scripts\demo\Parar-Detara-Demo.ps1 ou encerre o processo responsável."
    }
}

function Wait-Port([int]$port, [System.Diagnostics.Process]$process, [int]$timeoutSeconds = 45) {
    $deadline = [DateTime]::UtcNow.AddSeconds($timeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        $listener = Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -ne $listener) {
            return [int]$listener.OwningProcess
        }

        if ($process.HasExited) {
            throw "O processo iniciado para a porta $port foi encerrado antes de ficar disponível."
        }

        Start-Sleep -Milliseconds 500
    }

    throw "A porta $port não ficou disponível dentro do tempo esperado."
}

Set-Location $repoRoot
Assert-CommandAvailable "dotnet"
Assert-CommandAvailable "docker"
Assert-PortAvailable 5080
Assert-PortAvailable 5090

if (-not (Test-Path -LiteralPath (Join-Path $repoRoot ".env"))) {
    throw "Crie o arquivo .env local a partir de .env.example antes de iniciar a demonstração."
}

docker info *> $null
if ($LASTEXITCODE -ne 0) {
    throw "O Docker não está disponível. Inicie o Docker Desktop e tente novamente."
}

Write-Host "[1/6] Iniciando SQL Server local..." -ForegroundColor Cyan
docker compose --project-name detara --env-file $envFile --file $composeFile up -d sqlserver
if ($LASTEXITCODE -ne 0) { throw "Não foi possível iniciar o SQL Server local." }

$sqlDeadline = [DateTime]::UtcNow.AddSeconds(60)
while ([DateTime]::UtcNow -lt $sqlDeadline -and -not (Test-NetConnection -ComputerName 127.0.0.1 -Port 1433 -InformationLevel Quiet -WarningAction SilentlyContinue)) {
    Start-Sleep -Seconds 2
}
if (-not (Test-NetConnection -ComputerName 127.0.0.1 -Port 1433 -InformationLevel Quiet -WarningAction SilentlyContinue)) {
    throw "O SQL Server não ficou disponível na porta 1433."
}

Write-Host "[2/6] Aplicando migrations pendentes..." -ForegroundColor Cyan
dotnet tool restore
if ($LASTEXITCODE -ne 0) { throw "Não foi possível restaurar as ferramentas .NET." }
dotnet ef database update --project src/Detara.Infrastructure/Detara.Infrastructure.csproj --startup-project src/Detara.Api/Detara.Api.csproj
if ($LASTEXITCODE -ne 0) { throw "Não foi possível aplicar as migrations locais." }

$env:ASPNETCORE_ENVIRONMENT = "Development"
if (-not $PularPreparacao) {
    Write-Host "[3/6] Preparando o cenário comercial Prime Detail..." -ForegroundColor Cyan
    dotnet run --project tools/Detara.DemoBootstrap -- presentation --confirm-local-demo
    if ($LASTEXITCODE -ne 0) { throw "Não foi possível preparar o cenário de demonstração." }
}
else {
    Write-Host "[3/6] Preparação do cenário ignorada por solicitação." -ForegroundColor DarkYellow
}

Write-Host "[4/6] Compilando API e Web em Release..." -ForegroundColor Cyan
dotnet build --configuration Release
if ($LASTEXITCODE -ne 0) { throw "A compilação falhou; os serviços não foram iniciados." }

New-Item -ItemType Directory -Path $stateDirectory -Force | Out-Null
$apiOutput = Join-Path $stateDirectory "api.stdout.log"
$apiError = Join-Path $stateDirectory "api.stderr.log"
$webOutput = Join-Path $stateDirectory "web.stdout.log"
$webError = Join-Path $stateDirectory "web.stderr.log"

Write-Host "[5/6] Iniciando API..." -ForegroundColor Cyan
$apiProjectDirectory = Join-Path $repoRoot "src\Detara.Api"
$apiDll = Join-Path $apiProjectDirectory "bin\Release\net10.0\Detara.Api.dll"
$apiProcess = Start-Process -FilePath "dotnet" -ArgumentList @($apiDll, "--urls", $apiUrl) -WorkingDirectory $apiProjectDirectory -WindowStyle Hidden -RedirectStandardOutput $apiOutput -RedirectStandardError $apiError -PassThru

try {
    $apiListenerProcessId = Wait-Port 5090 $apiProcess
    Write-Host "[6/6] Iniciando aplicação Web..." -ForegroundColor Cyan
    $webProjectDirectory = Join-Path $repoRoot "src\Detara.Web"
    $webProject = Join-Path $webProjectDirectory "Detara.Web.csproj"
    $webProcess = Start-Process -FilePath "dotnet" -ArgumentList @("run", "--no-build", "--configuration", "Release", "--project", $webProject, "--urls", $webUrl) -WorkingDirectory $webProjectDirectory -WindowStyle Hidden -RedirectStandardOutput $webOutput -RedirectStandardError $webError -PassThru
    $webListenerProcessId = Wait-Port 5080 $webProcess

    [pscustomobject]@{
        ApiProcessId = $apiListenerProcessId
        WebProcessId = $webListenerProcessId
        Repository = $repoRoot
        StartedAtUtc = [DateTime]::UtcNow.ToString("O")
    } | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding utf8
}
catch {
    if ($null -ne $apiListenerProcessId) { Stop-Process -Id $apiListenerProcessId -ErrorAction SilentlyContinue }
    elseif (-not $apiProcess.HasExited) { Stop-Process -Id $apiProcess.Id }
    if ($null -ne $webListenerProcessId) { Stop-Process -Id $webListenerProcessId -ErrorAction SilentlyContinue }
    elseif ($null -ne $webProcess -and -not $webProcess.HasExited) { Stop-Process -Id $webProcess.Id }
    throw
}

Write-Host "Detara pronta para demonstração." -ForegroundColor Green
Write-Host "Web: $webUrl"
Write-Host "API: $apiUrl"
Write-Host "Login: demo@detara.local"
Write-Host "Logs locais: $stateDirectory"

if (-not $NaoAbrirNavegador) {
    Start-Process $webUrl
}
