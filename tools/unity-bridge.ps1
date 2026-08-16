[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet(
        'ping',
        'editor_state',
        'refresh',
        'save_scene',
        'hierarchy',
        'capture_game_view',
        'rebuild_chamber',
        'enter_play_mode',
        'exit_play_mode',
        'run_tests',
        'get_logs',
        'clear_logs'
    )]
    [string] $Command = 'ping',

    [string] $Argument = '',
    [switch] $Force,
    [int] $Width = 1280,
    [int] $Height = 720,
    [ValidateRange(1, 600)]
    [int] $TimeoutSeconds = 90,
    [switch] $NoWait
)

$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$bridgeRoot = Join-Path $projectRoot 'Library\CodexBridge'
$requestFolder = Join-Path $bridgeRoot 'Requests'
$responseFolder = Join-Path $bridgeRoot 'Responses'
$statusPath = Join-Path $bridgeRoot 'status.json'

New-Item -ItemType Directory -Force -Path $requestFolder | Out-Null
New-Item -ItemType Directory -Force -Path $responseFolder | Out-Null

$id = [Guid]::NewGuid().ToString('N')
$request = [ordered]@{
    id = $id
    command = $Command
    argument = $Argument
    force = [bool]$Force
    width = $Width
    height = $Height
}

$requestPath = Join-Path $requestFolder "$id.json"
$temporaryPath = "$requestPath.tmp"
$request | ConvertTo-Json | Set-Content -LiteralPath $temporaryPath -Encoding UTF8
Move-Item -LiteralPath $temporaryPath -Destination $requestPath

if ($NoWait) {
    [pscustomobject]@{
        id = $id
        command = $Command
        requestPath = $requestPath
    } | ConvertTo-Json
    exit 0
}

$responsePath = Join-Path $responseFolder "$id.json"
$deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
while ([DateTime]::UtcNow -lt $deadline) {
    if (Test-Path -LiteralPath $responsePath) {
        $response = Get-Content -Raw -LiteralPath $responsePath | ConvertFrom-Json
        $response | ConvertTo-Json -Depth 8
        if (-not $response.success) {
            exit 1
        }
        exit 0
    }
    Start-Sleep -Milliseconds 200
}

Write-Error "Timed out waiting for Unity to process '$Command' after $TimeoutSeconds seconds."
if (Test-Path -LiteralPath $statusPath) {
    Write-Host 'Latest bridge status:'
    Get-Content -Raw -LiteralPath $statusPath
} else {
    Write-Host 'No bridge heartbeat exists yet. Focus Unity once or choose Assets > Refresh.'
}
exit 2
