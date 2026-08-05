param(
    [string]$Connection = $env:VENDINGAD_SEED_CONNECTION,
    [string]$PsqlPath = "psql",
    [string]$DockerPath = "docker",
    [string]$ContainerName = "vendingad-postgres",
    [string]$Database = "vendingad",
    [string]$Username = "vendingad",
    [Parameter(Mandatory = $true)]
    [string]$DemoUsername,
    [Parameter(Mandatory = $true)]
    [string]$DemoPassword,
    [Parameter(Mandatory = $true)]
    [string]$DeviceSecretPrefix,
    [switch]$AllowDisposableSeed
)

if (-not $AllowDisposableSeed) {
    throw "Refusing to seed data without -AllowDisposableSeed. Never run this script against production."
}

$scriptPath = Join-Path $PSScriptRoot "seed-fake-data-postgres.sql"

$psqlCommand = Get-Command $PsqlPath -ErrorAction SilentlyContinue
if ($psqlCommand) {
    if ([string]::IsNullOrWhiteSpace($Connection)) {
        throw "Set -Connection or VENDINGAD_SEED_CONNECTION when using the local psql client."
    }

    & $psqlCommand.Source $Connection -v ON_ERROR_STOP=1 -v "demo_username=$DemoUsername" -v "demo_password=$DemoPassword" -v "device_secret_prefix=$DeviceSecretPrefix" -f $scriptPath
    exit $LASTEXITCODE
}

$dockerCommand = Get-Command $DockerPath -ErrorAction SilentlyContinue
if ($dockerCommand) {
    Get-Content $scriptPath | & $dockerCommand.Source exec -i $ContainerName psql -v ON_ERROR_STOP=1 -v "demo_username=$DemoUsername" -v "demo_password=$DemoPassword" -v "device_secret_prefix=$DeviceSecretPrefix" -U $Username -d $Database -f -
    exit $LASTEXITCODE
}

throw "Neither 'psql' nor 'docker' is available. Install PostgreSQL client tools or run PostgreSQL via Docker."
