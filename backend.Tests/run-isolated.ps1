param([string]$BaselineRevision = '064032c2850ed6fd1bbf5bb9d65e9a363e48586a')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
$containerName = 'tecnm-document-checks-' + [guid]::NewGuid().ToString('N').Substring(0, 12)
try {
    docker run --detach --rm --name $containerName --env POSTGRES_PASSWORD=document-checks-local --env POSTGRES_DB=document_checks --publish 127.0.0.1::5432 postgres:18-alpine | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear el PostgreSQL temporal.' }
    $ready = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        docker exec $containerName pg_isready -U postgres -d document_checks *> $null
        if ($LASTEXITCODE -eq 0) { $ready = $true; break }
        Start-Sleep -Milliseconds 500
    }
    if (!$ready) { throw 'PostgreSQL temporal no está disponible.' }
    $baseline = git show "${BaselineRevision}:database/design/schema.sql"
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo leer el esquema v1 de referencia.' }
    $baseline | docker exec -i $containerName psql -U postgres -d document_checks -q -v ON_ERROR_STOP=1 2>$null
    if ($LASTEXITCODE -ne 0) { throw 'Falló el esquema v1 en el contenedor vacío.' }
    Get-Content database/migrations/20261106_company_type_prepare.sql -Raw | docker exec -i $containerName psql -U postgres -d document_checks -q -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) { throw 'Falló la preparación de tipología.' }
    Get-Content database/migrations/20261127_reports_school_year_and_activity_types.sql -Raw | docker exec -i $containerName psql -U postgres -d document_checks -q -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) { throw 'Falló la preparación de clasificación.' }
    Get-ChildItem database/seed/*.sql | Sort-Object Name | ForEach-Object {
        Get-Content -LiteralPath $_.FullName -Raw | docker exec -i $containerName psql -U postgres -d document_checks -q -v ON_ERROR_STOP=1 | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Falló seed $($_.Name)." }
    }
    Get-Content database/migrations/20261103_document_workflow_v2.sql -Raw | docker exec -i $containerName psql -U postgres -d document_checks -q -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) { throw 'Falló la migración incremental.' }
    Get-Content database/migrations/20261106_company_type_finalize.sql -Raw | docker exec -i $containerName psql -U postgres -d document_checks -q -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) { throw 'Falló la clasificación final.' }
    Get-Content database/migrations/20261107_student_accounts.sql -Raw | docker exec -i $containerName psql -U postgres -d document_checks -q -v ON_ERROR_STOP=1
    Get-Content database/migrations/20261108_student_social_service_follow_up.sql -Raw | docker exec -i $containerName psql -U postgres -d document_checks -q -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) { throw 'Falló la migración de seguimiento.' }
    Get-Content database/migrations/20261126_document_templates.sql -Raw | docker exec -i $containerName psql -U postgres -d document_checks -q -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) { throw 'Falló la migración del gestor de plantillas.' }
    Get-Content database/migrations/20261127_document_branding.sql -Raw | docker exec -i $containerName psql -U postgres -d document_checks -q -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) { throw "Falló la migración de cuentas." }
    $mapping = docker port $containerName 5432
    $port = ($mapping -split ':')[-1]
    $env:DOCUMENT_TEST_CONNECTION = "Host=127.0.0.1;Port=$port;Database=document_checks;Username=postgres;Password=document-checks-local;Timeout=3"
    dotnet run --project backend.Tests/TecNM.DocumentChecks.csproj --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw 'Fallaron las comprobaciones.' }
}
finally {
    docker stop $containerName 2>$null | Out-Null
    Pop-Location
}
