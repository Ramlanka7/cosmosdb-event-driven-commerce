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
    $endpoint = Get-CosmosEndpoint
    $key = Get-CosmosKey
    return -not [string]::IsNullOrWhiteSpace($endpoint) -and -not [string]::IsNullOrWhiteSpace($key)
}

function Import-DotEnvIfPresent {
    $workspaceRoot = Split-Path -Parent $PSScriptRoot
    $dotenvPath = Join-Path $workspaceRoot '.env'

    if (-not (Test-Path $dotenvPath)) {
        return
    }

    Get-Content $dotenvPath | ForEach-Object {
        $line = $_.Trim()
        if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith('#')) {
            return
        }

        $parts = $line.Split('=', 2)
        if ($parts.Length -ne 2) {
            return
        }

        $key = $parts[0].Trim()
        $value = $parts[1].Trim()

        if (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'"))) {
            $value = $value.Substring(1, $value.Length - 2)
        }

        if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($key))) {
            [Environment]::SetEnvironmentVariable($key, $value)
        }
    }
}

function Get-CosmosEndpoint {
    if (-not [string]::IsNullOrWhiteSpace($env:CosmosDb__Endpoint)) {
        return $env:CosmosDb__Endpoint
    }

    if (-not [string]::IsNullOrWhiteSpace($env:COSMOS_DB_ENDPOINT)) {
        return $env:COSMOS_DB_ENDPOINT
    }

    return $null
}

function Get-CosmosKey {
    if (-not [string]::IsNullOrWhiteSpace($env:CosmosDb__Key)) {
        return $env:CosmosDb__Key
    }

    if (-not [string]::IsNullOrWhiteSpace($env:COSMOS_DB_KEY)) {
        return $env:COSMOS_DB_KEY
    }

    return $null
}

function Test-CosmosEmulator {
    $hasOverrideConfiguration = Test-CosmosOverrideConfiguration
    $configuredEndpoint = Get-CosmosEndpoint
    $emulatorExecutablePath = Join-Path ${env:ProgramFiles} 'Azure Cosmos DB Emulator\CosmosDB.Emulator.exe'
    $isInstalled = Test-Path $emulatorExecutablePath
    $isRunning = $false
    $requiresEmulator = $true

    if (-not [string]::IsNullOrWhiteSpace($configuredEndpoint)) {
        try {
            $endpointUri = [Uri]$configuredEndpoint
            $requiresEmulator = $endpointUri.Host -in @('localhost', '127.0.0.1', 'host.docker.internal', 'cosmos-emulator')
        }
        catch {
            Add-WarningMessage "Configured Cosmos endpoint '$configuredEndpoint' is not a valid URI."
        }
    }

    try {
        $isRunning = Test-NetConnection -ComputerName 'localhost' -Port 8081 -InformationLevel Quiet -WarningAction SilentlyContinue
    }
    catch {
        Add-WarningMessage 'Unable to verify port 8081 with Test-NetConnection. Ensure the Azure Cosmos DB Emulator is running if you are using the local defaults.'
    }

    if ($hasOverrideConfiguration) {
        Add-Detail "Cosmos endpoint override detected: $configuredEndpoint"

        if (-not $requiresEmulator) {
            Add-Detail 'Using non-local Cosmos endpoint. Local emulator is not required.'
            return
        }

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
Import-DotEnvIfPresent
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