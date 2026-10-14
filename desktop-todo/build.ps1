param([ValidateSet('build','test','publish','run')][string]$Action='build',[switch]$Offline,[switch]$Compatibility)
$ErrorActionPreference='Stop'
$taskRoot=$PSScriptRoot
$env:DOTNET_CLI_HOME=Join-Path $taskRoot '.cache/dotnet-home'
$env:NUGET_PACKAGES=Join-Path $taskRoot '.cache/nuget-packages'
$env:NUGET_HTTP_CACHE_PATH=Join-Path $taskRoot '.cache/nuget-http'
$env:NUGET_PLUGINS_CACHE_PATH=Join-Path $taskRoot '.cache/nuget-plugins'
$env:TEMP=Join-Path $taskRoot '.tmp'
$env:TMP=$env:TEMP
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:DOTNET_NOLOGO='1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE='1'
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
$env:MSBUILDDISABLENODEREUSE='1'
foreach ($taskDir in @($env:DOTNET_CLI_HOME,$env:NUGET_PACKAGES,$env:NUGET_HTTP_CACHE_PATH,$env:NUGET_PLUGINS_CACHE_PATH,$env:TEMP)) { New-Item -ItemType Directory -Force -Path $taskDir | Out-Null }
$restoreArgs=@()
$cetArg=if($Compatibility){'-p:CETCompat=false'}else{'-p:CETCompat=true'}
$publishDirectory=if($Compatibility){'artifacts/DesktopTodo-Compatibility'}else{'artifacts/DesktopTodo'}
if ($Offline) { $restoreArgs=@("-p:RestoreSources=$taskRoot/.cache/package-feed") }
Push-Location $taskRoot
try {
 switch ($Action) {
 'build' { dotnet build @restoreArgs $cetArg src/DesktopTodo.App/DesktopTodo.App.csproj -c Release --disable-build-servers }
 'test' { dotnet run @restoreArgs --project tests/DesktopTodo.Tests/DesktopTodo.Tests.csproj -c Release -- --test-root "$taskRoot/.tmp/tests" }
 'publish' {
  # Apphost generation is incremental; rebuilding prevents reuse of the other CET variant.
  dotnet build @restoreArgs $cetArg src/DesktopTodo.App/DesktopTodo.App.csproj -c Release -t:Rebuild --disable-build-servers
  if ($LASTEXITCODE -ne 0) { throw "Apphost rebuild failed: $LASTEXITCODE" }
  dotnet publish @restoreArgs $cetArg src/DesktopTodo.App/DesktopTodo.App.csproj -c Release --self-contained true -r win-x64 -p:PublishSingleFile=false -o $publishDirectory --disable-build-servers }
 'run' { dotnet run --project src/DesktopTodo.App/DesktopTodo.App.csproj -c Release --no-build }
 }
 if ($LASTEXITCODE -ne 0) { throw "dotnet failed with exit code $LASTEXITCODE" }
} finally { Pop-Location }



