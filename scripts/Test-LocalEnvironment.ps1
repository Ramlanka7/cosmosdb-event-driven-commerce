[CmdletBinding()]
param(
    [switch]$SkipDockerCheck
)

$ErrorActionPreference = 'Stop'

$errors = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]
$details = New-Object System.Collections.Generic.List[string]

function Add-Detail {
    param(
        [string]$Message
    )

    $details.Add($Message)
}

function Add-ErrorMessage {
    param(
        [string]$Message
    )

    $errors.Add($Message)
}

function Add-WarningMessage {
    param(
        [string]$Message
    )

    $warnings.Add($Message)
}

function Test-DotNetSdk {
    $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue

    if ($null -eq $dotnetCommand) {
        Add-ErrorMessage 'dotnet is not installed or is not available on PATH.'
        return
    }

    $sdks = & dotnet --list-sdks 2>$null
    $hasSupportedSdk = $sdks | Where-Object { $_ -match '^(8|9|10)\.' }

    if ($null -eq $hasSupportedSdk) {
        Add-ErrorMessage '.NET SDK 8.0 or later is required for this repository.'
        return
    }

    Add-Detail ".NET SDKs: $($sdks -join ', ')"
}

function Test-CosmosOverrideConfiguration {
    return -not [string]::IsNullOrWhiteSpace($env:CosmosDb__Endpoint) -and -not [string]::IsNullOrWhiteSpace($env:CosmosDb__Key)
}

function Test-CosmosEmulator {
    $hasOverrideConfiguration = Test-CosmosOverrideConfiguration
    $emulatorExecutablePath = Join-Path ${env:ProgramFiles} 'Azure Cosmos DB Emulator\CosmosDB.Emulator.exe'
    $isInstalled = Test-Path $emulatorExecutablePath
    $isRunning = $false

    try {
        $isRunning = Test-NetConnection -ComputerName 'localhost' -Port 8081 -InformationLevel Quiet -WarningAction SilentlyContinue
    }
    catch {
        Add-WarningMessage 'Unable to verify port 8081 with Test-NetConnection. Ensure the Azure Cosmos DB Emulator is running if you are using the local defaults.'
    }

    if ($hasOverrideConfiguration) {
        Add-Detail "Custom Cosmos endpoint override detected: $($env:CosmosDb__Endpoint)"

        if (-not $isInstalled) {
            Add-WarningMessage 'Azure Cosmos DB Emulator is not installed, but custom CosmosDb__Endpoint and CosmosDb__Key overrides are present.'
            return
        }

        if (-not $isRunning) {
            Add-WarningMessage 'Azure Cosmos DB Emulator is installed but not running. This is acceptable because custom CosmosDb__Endpoint and CosmosDb__Key overrides are present.'
            return
        }

        Add-Detail 'Azure Cosmos DB Emulator is installed and running, although local overrides are currently configured.'
        return
    }

    if (-not $isInstalled) {
        Add-ErrorMessage 'Azure Cosmos DB Emulator is not installed. Install it or supply CosmosDb__Endpoint and CosmosDb__Key overrides.'
        return
    }

    if (-not $isRunning) {
        Add-ErrorMessage 'Azure Cosmos DB Emulator does not appear to be running on port 8081.'
        return
    }

    Add-Detail 'Azure Cosmos DB Emulator is installed and responding on localhost:8081.'
}

function Test-Docker {
    if ($SkipDockerCheck) {
        Add-Detail 'Docker check skipped.'
        return
    }

    $dockerCommand = Get-Command docker -ErrorAction SilentlyContinue

    if ($null -eq $dockerCommand) {
        Add-WarningMessage 'Docker is optional and was not found on PATH.'
        return
    }

    Add-Detail "Docker CLI: $($dockerCommand.Source)"
}

Test-DotNetSdk
Test-CosmosEmulator
Test-Docker

Write-Host 'Local environment verification summary'

foreach ($detail in $details) {
    Write-Host "  [info] $detail"
}

foreach ($warning in $warnings) {
    Write-Warning $warning
}

foreach ($errorMessage in $errors) {
    Write-Host "  [error] $errorMessage"
}

if ($errors.Count -gt 0) {
    Write-Host ''
    Write-Host 'Validation failed. Resolve the errors above before running bootstrap-cosmos or starting the services.'
    exit 1
}

Write-Host ''
Write-Host 'Validation passed. You can now run bootstrap-cosmos or bootstrap-and-start-commerce-platform.'