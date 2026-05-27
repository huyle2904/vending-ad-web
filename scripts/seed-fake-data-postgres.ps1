param(
    [string]$Connection = "host=localhost port=5432 dbname=vendingad user=vendingad password=vendingad",
    [string]$PsqlPath = "psql",
    [string]$DockerPath = "docker",
    [string]$ContainerName = "vendingad-postgres",
    [string]$Database = "vendingad",
    [string]$Username = "vendingad"
)

$scriptPath = Join-Path $PSScriptRoot "seed-fake-data-postgres.sql"

$psqlCommand = Get-Command $PsqlPath -ErrorAction SilentlyContinue
if ($psqlCommand) {
    & $psqlCommand.Source $Connection -v ON_ERROR_STOP=1 -f $scriptPath
    exit $LASTEXITCODE
}

$dockerCommand = Get-Command $DockerPath -ErrorAction SilentlyContinue
if ($dockerCommand) {
    Get-Content $scriptPath | & $dockerCommand.Source exec -i $ContainerName psql -v ON_ERROR_STOP=1 -U $Username -d $Database -f -
    exit $LASTEXITCODE
}

throw "Neither 'psql' nor 'docker' is available. Install PostgreSQL client tools or run PostgreSQL via Docker."
