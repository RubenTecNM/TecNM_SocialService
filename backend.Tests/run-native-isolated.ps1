param([string]$PostgresBin = 'C:/Program Files/PostgreSQL/18/bin', [string]$BaselineRevision = '064032c2850ed6fd1bbf5bb9d65e9a363e48586a')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$testRoot = Join-Path $projectRoot 'tmp/document-checks'
$cluster = Join-Path $testRoot ([guid]::NewGuid().ToString('N'))
$previousConnection = $env:DOCUMENT_TEST_CONNECTION
$started = $false
Push-Location $projectRoot
try {
    New-Item -ItemType Directory -Path $cluster -Force | Out-Null
    & "$PostgresBin/initdb.exe" -D "$cluster/data" -U postgres --auth=trust --encoding=UTF8 --locale=C > "$cluster/init.log" 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Falló initdb; consultar $cluster/init.log" }
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = $listener.LocalEndpoint.Port
    $listener.Stop()
    & "$PostgresBin/pg_ctl.exe" -D "$cluster/data" -l "$cluster/server.log" -o "-h 127.0.0.1 -p $port" -w start
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo iniciar PostgreSQL temporal.' }
    $started = $true
    & "$PostgresBin/createdb.exe" -h 127.0.0.1 -p $port -U postgres document_checks
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear document_checks.' }
    function Invoke-TestSql([string]$sql) {
        $sql | & "$PostgresBin/psql.exe" -h 127.0.0.1 -p $port -U postgres -d document_checks -q -v ON_ERROR_STOP=1
        if ($LASTEXITCODE -ne 0) { throw 'Falló SQL en la base temporal.' }
    }
    $baseline = git show "${BaselineRevision}:database/design/schema.sql"
    if ($LASTEXITCODE -ne 0) { throw 'No existe la referencia de migración.' }
    Invoke-TestSql ($baseline -join "`n")
    Invoke-TestSql (Get-Content database/migrations/20261106_company_type_prepare.sql -Raw)
    Invoke-TestSql (Get-Content database/migrations/20261127_reports_school_year_and_activity_types.sql -Raw)
    Get-ChildItem database/seed/*.sql | Sort-Object Name | ForEach-Object { Invoke-TestSql (Get-Content -LiteralPath $_.FullName -Raw) }
    Invoke-TestSql (Get-Content database/migrations/20261103_document_workflow_v2.sql -Raw)
    Invoke-TestSql (Get-Content database/migrations/20261106_company_type_finalize.sql -Raw)
    Invoke-TestSql (Get-Content database/migrations/20261107_student_accounts.sql -Raw)
    Invoke-TestSql (Get-Content database/migrations/20261108_student_social_service_follow_up.sql -Raw)
    Invoke-TestSql (Get-Content database/migrations/20261126_document_templates.sql -Raw)
    Invoke-TestSql (Get-Content database/migrations/20261127_document_branding.sql -Raw)
    $env:DOCUMENT_TEST_CONNECTION = "Host=127.0.0.1;Port=$port;Database=document_checks;Username=postgres;Timeout=3"
    dotnet run --project backend.Tests/TecNM.DocumentChecks.csproj --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw 'Fallaron las comprobaciones.' }
}
finally {
    if ($started) { & "$PostgresBin/pg_ctl.exe" -D "$cluster/data" -m fast -w stop }
    $env:DOCUMENT_TEST_CONNECTION = $previousConnection
    $resolved = [System.IO.Path]::GetFullPath($cluster)
    $boundary = [System.IO.Path]::GetFullPath($testRoot).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if (!$resolved.StartsWith($boundary, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Ruta temporal fuera del directorio de pruebas.' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
    Pop-Location
}
