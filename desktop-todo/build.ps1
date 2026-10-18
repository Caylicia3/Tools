param(
    [ValidateSet('build', 'test', 'format-check', 'publish', 'run')]
    [string]$Action = 'build',
    [switch]$Offline,
    [switch]$Compatibility
)

$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $taskRoot '.cache/dotnet-home'
$env:NUGET_PACKAGES = Join-Path $taskRoot '.cache/nuget-packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $taskRoot '.cache/nuget-http'
$env:NUGET_PLUGINS_CACHE_PATH = Join-Path $taskRoot '.cache/nuget-plugins'
$env:TEMP = Join-Path $taskRoot '.tmp'
$env:TMP = $env:TEMP
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = '1'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
$env:MSBUILDDISABLENODEREUSE = '1'

foreach ($taskDir in @(
    $env:DOTNET_CLI_HOME, $env:NUGET_PACKAGES, $env:NUGET_HTTP_CACHE_PATH,
    $env:NUGET_PLUGINS_CACHE_PATH, $env:TEMP
)) {
    New-Item -ItemType Directory -Force -Path $taskDir | Out-Null
}

$restoreArgs = @()
$cetArg = if ($Compatibility) { '-p:CETCompat=false' } else { '-p:CETCompat=true' }
$publishDirectory = if ($Compatibility) {
    'artifacts/DesktopTodo-Compatibility'
} else {
    'artifacts/DesktopTodo'
}
if ($Offline) {
    $feed = Join-Path $taskRoot '.cache/package-feed'
    if (-not (Test-Path -LiteralPath $feed -PathType Container)) {
        throw 'Offline package feed is missing. Run without -Offline for the first restore.'
    }
    $restoreArgs = @("-p:RestoreSources=$feed")
}

function Assert-DotnetSuccess {
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet failed with exit code $LASTEXITCODE"
    }
}

Push-Location $taskRoot
try {
    switch ($Action) {
        'build' {
            dotnet build @restoreArgs $cetArg src/DesktopTodo.App/DesktopTodo.App.csproj -c Release --disable-build-servers
            Assert-DotnetSuccess
        }
        'test' {
            dotnet run @restoreArgs --project tests/DesktopTodo.Tests/DesktopTodo.Tests.csproj -c Release -- --test-root "$taskRoot/.tmp/tests"
            Assert-DotnetSuccess
        }
        'format-check' {
            foreach ($project in @(
                'src/DesktopTodo.App/DesktopTodo.App.csproj',
                'src/DesktopTodo.Core/DesktopTodo.Core.csproj',
                'tests/DesktopTodo.Tests/DesktopTodo.Tests.csproj'
            )) {
                dotnet format whitespace $project --verify-no-changes --no-restore
                Assert-DotnetSuccess
            }
        }
        'publish' {
            # Rebuild to avoid reusing an apphost from the other CET variant.
            dotnet build @restoreArgs $cetArg src/DesktopTodo.App/DesktopTodo.App.csproj -c Release -t:Rebuild --disable-build-servers
            Assert-DotnetSuccess
            dotnet publish @restoreArgs $cetArg src/DesktopTodo.App/DesktopTodo.App.csproj -c Release --self-contained true -r win-x64 -p:PublishSingleFile=false -o $publishDirectory --disable-build-servers
            Assert-DotnetSuccess

            foreach ($file in @('README.md', 'USAGE.txt', 'TEST-REPORT.md', 'ACCEPTANCE-0.1.2.md', 'ACCEPTANCE-0.1.3.md', 'ACCEPTANCE-0.1.3.2.md', 'LICENSE', 'CHANGELOG.md', 'CONTRIBUTING.md')) {
                Copy-Item -LiteralPath $file -Destination (Join-Path $publishDirectory $file) -Force
            }
            foreach ($directory in @('docs', 'THIRD-PARTY-NOTICES')) {
                foreach ($file in Get-ChildItem -LiteralPath $directory -File -Recurse) {
                    $relative = [IO.Path]::GetRelativePath($taskRoot, $file.FullName)
                    $destination = Join-Path $publishDirectory $relative
                    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
                    Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
                }
            }
        }
        'run' {
            dotnet run --project src/DesktopTodo.App/DesktopTodo.App.csproj -c Release --no-build
            Assert-DotnetSuccess
        }
    }
} finally {
    Pop-Location
}
