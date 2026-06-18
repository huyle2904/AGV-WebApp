param(
    [switch]$NoBuild,
    [switch]$KeepExistingPorts
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$apiProject = Join-Path $root "src\NewAGV.Api\NewAGV.Api.csproj"
$workerProject = Join-Path $root "src\NewAGV.Worker\NewAGV.Worker.csproj"
$webProject = Join-Path $root "src\NewAGV.Web\NewAGV.Web.csproj"
$webUrl = "http://localhost:5209"

function Stop-PortOwner {
    param(
        [int]$Port,
        [string]$Name
    )

    $connections = netstat -ano | Select-String ":$Port\s"
    if (-not $connections) {
        return
    }

    $ownerProcessIds = $connections |
        ForEach-Object {
            $parts = ($_.Line -split "\s+") | Where-Object { $_ }
            $parts[-1]
        } |
        Sort-Object -Unique

    foreach ($ownerProcessId in $ownerProcessIds) {
        if ($ownerProcessId -match "^\d+$") {
            Write-Host "Stopping process $ownerProcessId using $Name port $Port..."
            Stop-Process -Id ([int]$ownerProcessId) -Force -ErrorAction SilentlyContinue
        }
    }
}

function Start-ServiceWindow {
    param(
        [string]$Title,
        [string]$Project,
        [string]$Profile
    )

    $command = @"
`$Host.UI.RawUI.WindowTitle = '$Title'
Set-Location -LiteralPath '$root'
dotnet run --no-build --project '$Project' --launch-profile '$Profile'
Read-Host 'Press Enter to close this window'
"@

    Start-Process powershell -ArgumentList @(
        "-NoExit",
        "-ExecutionPolicy", "Bypass",
        "-Command", $command
    )
}

Write-Host "Starting NewAGV from $root"

if (-not $KeepExistingPorts) {
    Stop-PortOwner -Port 5222 -Name "API"
    Stop-PortOwner -Port 5230 -Name "Worker"
    Stop-PortOwner -Port 5209 -Name "Web"
}

if (-not $NoBuild) {
    Write-Host "Building projects..."
    dotnet build $apiProject
    dotnet build $workerProject
    dotnet build $webProject
}

Start-ServiceWindow -Title "NewAGV API - http://localhost:5222" -Project $apiProject -Profile "http"
Start-Sleep -Seconds 2
Start-ServiceWindow -Title "NewAGV Worker - http://localhost:5230" -Project $workerProject -Profile "NewAGV.Worker"
Start-Sleep -Seconds 2
Start-ServiceWindow -Title "NewAGV Web - http://localhost:5209" -Project $webProject -Profile "http"

Start-Sleep -Seconds 4
Start-Process $webUrl

Write-Host ""
Write-Host "NewAGV is starting."
Write-Host "Web:    $webUrl"
Write-Host "API:    http://localhost:5222"
Write-Host "Worker: http://localhost:5230"
Write-Host ""
Write-Host "Close the three service windows when you want to stop the app."
