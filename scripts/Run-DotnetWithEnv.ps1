[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$DotnetArgs
)

$ErrorActionPreference = 'Stop'

function Import-DotEnv {
    param(
        [string]$Path
    )

    if (-not (Test-Path $Path)) {
        return
    }

    Get-Content $Path | ForEach-Object {
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

$workspaceRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$dotenvPath = Join-Path $workspaceRoot '.env'
Import-DotEnv -Path $dotenvPath

if ([string]::IsNullOrWhiteSpace($env:CosmosDb__Endpoint) -and -not [string]::IsNullOrWhiteSpace($env:COSMOS_DB_ENDPOINT)) {
    $env:CosmosDb__Endpoint = $env:COSMOS_DB_ENDPOINT
}

if ([string]::IsNullOrWhiteSpace($env:CosmosDb__Key) -and -not [string]::IsNullOrWhiteSpace($env:COSMOS_DB_KEY)) {
    $env:CosmosDb__Key = $env:COSMOS_DB_KEY
}

if ([string]::IsNullOrWhiteSpace($env:CosmosDb__DatabaseName) -and -not [string]::IsNullOrWhiteSpace($env:COSMOS_DB_DATABASE_NAME)) {
    $env:CosmosDb__DatabaseName = $env:COSMOS_DB_DATABASE_NAME
}

if ($DotnetArgs.Count -eq 0) {
    throw 'No dotnet arguments were provided.'
}

& dotnet @DotnetArgs
exit $LASTEXITCODE
