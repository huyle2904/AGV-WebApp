$ErrorActionPreference = "Stop"

$env:NUGET_PACKAGES = "C:\Users\TD-997\Documents\NewAGV\.tmp\nuget-packages"
$env:DOTNET_CLI_HOME = "C:\Users\TD-997\Documents\NewAGV\.tmp\dotnet-cli"
$env:MSBuildEnableWorkloadResolver = "false"

dotnet test NewAGV.sln --no-restore
