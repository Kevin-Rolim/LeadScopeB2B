$ErrorActionPreference = 'Stop'
Push-Location -LiteralPath $PSScriptRoot
try {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        throw 'Instale e abra o Docker Desktop antes de continuar.'
    }
    docker compose version | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Docker Compose indisponivel.' }
    $engine = docker info --format '{{.OSType}}'
    if ($LASTEXITCODE -ne 0) { throw 'Inicie o Docker Desktop e execute novamente.' }
    if ($engine -ne 'linux') { throw 'No Docker Desktop, selecione Switch to Linux containers.' }

    if (-not (Test-Path -LiteralPath '.env') -and -not $env:MSSQL_SA_PASSWORD) {
        $bytes = New-Object byte[] 24
        $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
        try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
        $password = [System.BitConverter]::ToString($bytes).Replace('-', '').ToLowerInvariant()
        Set-Content -LiteralPath '.env' -Value "MSSQL_SA_PASSWORD=Ls@$password" -Encoding Ascii
        Write-Host 'Configuracao local criada em .env.'
    }

    docker compose -f compose.yaml up -d --build --wait
    if ($LASTEXITCODE -ne 0) { throw 'A inicializacao falhou. Consulte docker compose logs db-init sqlserver.' }
    docker compose -f compose.yaml exec leadscope dotnet LeadScopeB2B.dll --criar-admin
    if ($LASTEXITCODE -ne 0) { throw 'A configuracao do administrador falhou.' }
    Write-Host 'Sistema iniciado: http://localhost:18080'
}
finally {
    Pop-Location
}
