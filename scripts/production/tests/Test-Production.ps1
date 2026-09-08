# QA local Windows/Docker Desktop. Não é script de deploy do host Linux.
param([switch]$ConfirmDisposable)
$ErrorActionPreference = 'Stop'
if (-not $ConfirmDisposable) { throw 'Exige -ConfirmDisposable.' }
$repo = (Resolve-Path "$PSScriptRoot/../../..").Path
$project = 'detara-task49-' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
$temporary = Join-Path ([IO.Path]::GetTempPath()) $project
[IO.Directory]::CreateDirectory($temporary) | Out-Null
$environmentFile = Join-Path $temporary 'synthetic.env'
$overrideFile = Join-Path $temporary 'compose.qa.yml'
$caddyFile = Join-Path $temporary 'Caddyfile'
$certificateFile = Join-Path $temporary 'data-protection.pfx'
$sqlPassword = 'Aa1!' + [Guid]::NewGuid().ToString('N')
$runtimePassword = 'Bb2!' + [Guid]::NewGuid().ToString('N')
$migrationPassword = 'Cc3!' + [Guid]::NewGuid().ToString('N')
$certificatePassword = [Guid]::NewGuid().ToString('N')
$composeArgs = @('compose', '-p', $project, '--env-file', $environmentFile, '-f', "$repo/compose.production.yml", '-f', $overrideFile)
function Docker([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments) {
    $output = (& docker.exe @Arguments 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0) {
        foreach ($secret in @($sqlPassword, $runtimePassword, $migrationPassword, $certificatePassword)) { $output = $output.Replace($secret, '[REDACTED]') }
        throw "Docker falhou na etapa '$($Arguments[0])' (exit $LASTEXITCODE): $output"
    }
    return $output.Trim()
}
function Compose([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments) { Docker ($composeArgs + $Arguments) }
function Sql([string]$Query) {
    $result = $Query | & docker.exe exec -i "${project}-sqlserver-1" bash -c 'export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -m 1 -h -1 -W' 2>&1
    if ($LASTEXITCODE -ne 0) { throw 'SQL QA falhou (saída omitida para não registrar credenciais).' }
    return ($result | Out-String).Trim()
}
function Assert([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message }; Write-Host "PASS: $Message" }
try {
    $rsa = [Security.Cryptography.RSA]::Create(2048)
    $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=Detara Synthetic QA', $rsa, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $cert = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5), [DateTimeOffset]::UtcNow.AddDays(1))
    [IO.File]::WriteAllBytes($certificateFile, $cert.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $certificatePassword))
    $cert.Dispose(); $rsa.Dispose()
    $values = @{
        DETARA_APP_HOST='app.detara.test'; DETARA_API_HOST='api.detara.test'; DETARA_ACME_EMAIL='qa@detara.test'
        DETARA_API_IMAGE='detara-api:task49'; DETARA_WEB_IMAGE='detara-web:task49'; DETARA_WHATSAPP_GATEWAY_IMAGE='detara-whatsapp-gateway:task49'
        DETARA_SQL_ADMIN_PASSWORD=$sqlPassword; DETARA_SQL_RUNTIME_PASSWORD=$runtimePassword
        DETARA_JWT_KEY=[Guid]::NewGuid().ToString('N')+[Guid]::NewGuid().ToString('N')
        DETARA_PLATFORM_JWT_KEY=[Guid]::NewGuid().ToString('N')+[Guid]::NewGuid().ToString('N')
        DETARA_DATA_PROTECTION_PASSWORD=$certificatePassword; DETARA_DATA_PROTECTION_CERTIFICATE=$certificateFile.Replace('\','/')
        DETARA_S3_ENDPOINT='https://storage.detara.test'; DETARA_S3_BUCKET='synthetic-private'; DETARA_S3_REGION='auto'
        DETARA_S3_ACCESS_KEY='synthetic-access'; DETARA_S3_SECRET_KEY='synthetic-key'; DETARA_RESEND_API_KEY='synthetic-resend'
        DETARA_EMAIL_FROM_ADDRESS='qa@detara.test'; DETARA_WHATSAPP_GATEWAY_API_KEY=[Guid]::NewGuid().ToString('N')
    }
    [IO.File]::WriteAllLines($environmentFile, @($values.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }))
    $caddy = [IO.File]::ReadAllText("$repo/deploy/Caddyfile").Replace('admin off', "admin off`n    local_certs")
    [IO.File]::WriteAllText($caddyFile, $caddy)
    $override = @"
services:
  reverse-proxy:
    ports: !override []
    volumes:
      - '$($caddyFile.Replace('\','/')):/etc/caddy/Caddyfile:ro'
    networks:
      edge:
        ipv4_address: 172.29.49.2
  api:
    environment:
      ForwardedHeaders__KnownProxies__0: 172.29.49.2
    networks:
      edge:
        ipv4_address: 172.29.49.3
  web:
    networks:
      edge:
        ipv4_address: 172.29.49.4
  sqlserver:
    volumes:
      - qa-backup:/var/opt/mssql/backups
  whatsapp-gateway:
    volumes:
      - qa-sessions:/app/sessions
networks:
  edge:
    internal: true
    ipam:
      config: !override
        - subnet: 172.29.49.0/24
  whatsapp-egress:
    internal: true
volumes:
  qa-backup:
  qa-sessions:
"@
    [IO.File]::WriteAllText($overrideFile, $override)
    Compose @('config','--quiet') | Out-Null
    Compose @('up','-d','--wait','--wait-timeout','180','sqlserver') | Out-Null
    Assert ((Sql "SET NOCOUNT ON; SELECT SERVERPROPERTY('Edition');") -match 'Express') 'SQL real executa Express.'
    Docker @('exec','--user','0',"${project}-sqlserver-1",'chown','10001:0','/var/opt/mssql/backups') | Out-Null
    Sql "CREATE DATABASE Detara;`nGO`nCREATE LOGIN detara_runtime WITH PASSWORD=N'$runtimePassword';`nGO`nUSE Detara;`nCREATE USER detara_runtime FOR LOGIN detara_runtime;`nALTER ROLE db_datareader ADD MEMBER detara_runtime;`nALTER ROLE db_datawriter ADD MEMBER detara_runtime;" | Out-Null
    Sql "CREATE LOGIN detara_migrator WITH PASSWORD=N'$migrationPassword';`nGO`nUSE Detara;`nCREATE USER detara_migrator FOR LOGIN detara_migrator;`nALTER ROLE db_owner ADD MEMBER detara_migrator;" | Out-Null
    Assert ((Sql "USE Detara; SET NOCOUNT ON; SELECT IS_ROLEMEMBER('db_owner','detara_runtime');") -eq '0') 'Runtime SQL não é db_owner; migrator separado.'
    $env:ConnectionStrings__DefaultConnection = "Server=sqlserver,1433;Database=Detara;User Id=detara_migrator;Password=$migrationPassword;Encrypt=True;TrustServerCertificate=True"
    $migrations = Get-ChildItem "$repo/src/Detara.Infrastructure/Persistencia/Migrations" -Filter '*.cs' | Where-Object { $_.Name -match '^\d{14}_.+(?<!\.Designer)\.cs$' } | Sort-Object Name
    $previous = $migrations[-2].BaseName
    $migrationRun = @('run','--rm','--read-only','--memory','768m','--cpus','1','--pids-limit','150','--tmpfs','/tmp:size=256m,mode=1777','--cap-drop','ALL','--security-opt','no-new-privileges:true','--network',"${project}_data",'-e','ConnectionStrings__DefaultConnection','detara-migrations:task49')
    Docker ($migrationRun + @($previous)) | Out-Null
    Sql 'USE Detara; CREATE TABLE Task49RestoreProbe(Id int PRIMARY KEY, Value nvarchar(30)); INSERT INTO Task49RestoreProbe VALUES(1,N''synthetic-persisted'');' | Out-Null
    Docker @('exec',"${project}-sqlserver-1",'bash','-c','export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; bash /opt/detara/scripts/backup.sh') | Out-Null
    Docker $migrationRun | Out-Null
    Docker $migrationRun | Out-Null
    Assert ((Sql 'USE Detara; SET NOCOUNT ON; SELECT COUNT(*) FROM __EFMigrationsHistory;') -eq $migrations.Count.ToString()) 'Migrations vazia + incremental + rerun idempotente.'
    Compose @('up','-d','--no-deps','--force-recreate','--wait','--wait-timeout','180','sqlserver') | Out-Null
    Assert ((Sql 'USE Detara; SET NOCOUNT ON; SELECT Value FROM Task49RestoreProbe WHERE Id=1;') -eq 'synthetic-persisted') 'SQL persiste após replacement.'
    Docker @('exec',"${project}-sqlserver-1",'bash','-c','export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; bash /opt/detara/scripts/backup.sh') | Out-Null
    $backupName = Docker @('exec',"${project}-sqlserver-1",'bash','-c','ls -1 /var/opt/mssql/backups/Detara_full_*.bak | sort | tail -1')
    $backupName = ($backupName -split '/')[-1]
    Sql 'DROP DATABASE Detara;' | Out-Null
    Sql "RESTORE DATABASE Detara FROM DISK=N'/var/opt/mssql/backups/$backupName' WITH CHECKSUM;`nGO`nDBCC CHECKDB(Detara) WITH NO_INFOMSGS;" | Out-Null
    Assert ((Sql 'USE Detara; SET NOCOUNT ON; SELECT Value FROM Task49RestoreProbe WHERE Id=1;') -eq 'synthetic-persisted') 'Backup nativo, remoção do banco sintético, restore e dados validados.'
    Docker @('exec',"${project}-sqlserver-1",'bash','-c',"export SQLCMDPASSWORD=`"`$MSSQL_SA_PASSWORD`"; bash /opt/detara/scripts/restore-drill.sh --confirm-disposable $backupName") | Out-Null
    Sql 'CREATE DATABASE DetaraRestoreDrill_Existing;' | Out-Null
    & docker.exe exec "${project}-sqlserver-1" bash -c "export SQLCMDPASSWORD=`"`$MSSQL_SA_PASSWORD`"; bash /opt/detara/scripts/restore-drill.sh --confirm-disposable $backupName DetaraRestoreDrill_Existing" 2>&1 | Out-Null
    Assert ($LASTEXITCODE -ne 0) 'Restore drill recusa banco já existente.'
    Assert ((Sql "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name=N'DetaraRestoreDrill_Existing';") -eq '1') 'Guard de restore preserva banco pré-existente na fixture.'
    Docker @('cp',"${project}-sqlserver-1:/var/opt/mssql/backups/$backupName",(Join-Path $temporary 'input.bak')) | Out-Null
    Docker @('run','--rm','--network','none','--mount',"type=bind,source=$repo,target=/repo,readonly",'--mount',"type=bind,source=$temporary,target=/fixtures",'--mount','type=bind,source=/var/run/docker.sock,target=/var/run/docker.sock','detara-production-tools:task49','bash','scripts/production/tests/encrypted-restore.sh') | Out-Null
    Assert $true 'Backup age decifrado, restaurado e CHECKDB em segundo SQL Express sem rede.'
    Compose @('run','--rm','--no-deps','--user','0','--cap-add','CHOWN','--entrypoint','sh','reverse-proxy','-c','chown 1000:1000 /data /config') | Out-Null
    Compose @('up','-d','--no-deps','--wait','--wait-timeout','120','api','web','reverse-proxy','whatsapp-gateway') | Out-Null
    function Http([string]$HostName,[string]$Path,[string[]]$Extra=@()) {
        # -k SOMENTE para certificado local sintético; nunca no smoke de produção.
        # Cliente na mesma rede INTERNAL; Docker não publica portas de redes sem egress.
        $result = (& docker.exe run --rm --network "${project}_edge" --entrypoint curl detara-api:task49 -k -sS --http1.1 --max-time 25 --resolve "${HostName}:8443:172.29.49.2" -D - "https://${HostName}:8443$Path" @Extra | Out-String)
        if ($LASTEXITCODE -ne 0) { throw 'Cliente HTTP sintético falhou.' }
        return $result
    }
    Assert ((Http 'app.detara.test' '/') -match '200 OK') 'Web estático via TLS local.'
    Assert ((Http 'api.detara.test' '/health/ready') -match '"status"\s*:\s*"healthy"') 'API Production pronta no SQL real.'
    Assert ((Http 'api.detara.test' '/health/live' @('-H','Origin: https://app.detara.test')) -match 'access-control-allow-origin: https://app.detara.test') 'CORS permite somente Web configurada.'
    Assert ((Http 'api.detara.test' '/health/live' @('-H','Origin: https://evil.test')) -notmatch 'access-control-allow-origin:') 'Origem arbitrária sem CORS.'
    Assert ((Http 'api.detara.test' '/health/live' @('-H','Host: evil.test')) -notmatch '"status"\s*:\s*"healthy"') 'Host arbitrário rejeitado.'
    Compose @('stop','whatsapp-gateway') | Out-Null
    Assert ((Http 'api.detara.test' '/health/ready') -match '"status"\s*:\s*"healthy"') 'Gateway offline não bloqueia readiness.'
    Compose @('stop','sqlserver') | Out-Null
    Assert ((Http 'api.detara.test' '/health/live') -match '"status"\s*:\s*"healthy"') 'SQL offline não derruba liveness.'
    Assert ((Http 'api.detara.test' '/health/ready') -match '503') 'SQL offline torna readiness indisponível.'
    Compose @('start','sqlserver') | Out-Null
    Compose @('up','-d','--wait','--wait-timeout','180','sqlserver') | Out-Null
    Assert ((Http 'api.detara.test' '/health/ready') -match '"status"\s*:\s*"healthy"') 'API recupera readiness após retorno do SQL.'
    Compose @('start','whatsapp-gateway') | Out-Null
    Docker @('exec',"${project}-whatsapp-gateway-1",'node','-e',"require('node:fs').writeFileSync('/app/sessions/task49-sentinel','synthetic')") | Out-Null
    Compose @('up','-d','--no-deps','--force-recreate','--wait','--wait-timeout','120','whatsapp-gateway','api','reverse-proxy') | Out-Null
    Assert ((Docker @('exec',"${project}-whatsapp-gateway-1",'node','-e',"process.stdout.write(require('node:fs').readFileSync('/app/sessions/task49-sentinel','utf8'))")) -eq 'synthetic') 'Sessões preservadas no replacement; nenhum QR real utilizado.'
    Assert ((Http 'api.detara.test' '/health/ready') -match '"status"\s*:\s*"healthy"') 'API/Caddy recuperados após replacement.'
    Docker @('cp',"${project}-web-1:/usr/share/nginx/html",(Join-Path $temporary 'published')) | Out-Null
    & node "$repo/scripts/production/tests/published-assets.cjs" (Join-Path $temporary 'published')
    if ($LASTEXITCODE -ne 0) { throw 'Manifesto PWA publicado inconsistente.' }
    Write-Host 'QA Production concluído. Nenhum envio real, DNS ou VPS utilizados.'
}
catch {
    foreach ($service in @('api','web','reverse-proxy')) {
        $diagnostic = (& docker.exe logs --tail 30 "${project}-$service-1" 2>&1 | Out-String)
        foreach ($value in $values.Values) { if ($value.Length -ge 16) { $diagnostic = $diagnostic.Replace($value, '[REDACTED]') } }
        Write-Host "Diagnóstico sintético ${service}: $diagnostic"
    }
    throw
}
finally {
    Remove-Item Env:ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue
    if (Test-Path $overrideFile) { Compose @('down','--remove-orphans') | Out-Null }
    $volumes = Docker @('volume','ls','--filter',"label=com.docker.compose.project=$project",'--format','{{.Name}}')
    foreach ($volume in ($volumes -split "`n" | Where-Object { $_ })) {
        if (-not $volume.StartsWith("${project}_")) { throw 'Volume fora do escopo de QA.' }
        Docker @('volume','rm',$volume) | Out-Null
    }
    if ($temporary.StartsWith([IO.Path]::GetTempPath()) -and (Split-Path $temporary -Leaf) -eq $project) { Remove-Item -LiteralPath $temporary -Recurse -Force }
    Write-Host 'Containers, volumes e fixtures exclusivamente descartáveis removidos; ambiente local preservado.'
}
