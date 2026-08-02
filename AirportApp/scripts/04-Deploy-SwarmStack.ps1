param(
    [string]$StackName = 'airportapp',
    [string]$PostgresNode = ''
)

$ErrorActionPreference = 'Stop'

function New-DockerTextSecret {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    $dockerPath = (Get-Command docker -ErrorAction Stop).Source
    $processInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $processInfo.FileName = $dockerPath
    $processInfo.Arguments = "secret create $Name -"
    $processInfo.UseShellExecute = $false
    $processInfo.RedirectStandardInput = $true
    $processInfo.RedirectStandardOutput = $true
    $processInfo.RedirectStandardError = $true

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $processInfo
    [void]$process.Start()

    $encoding = [System.Text.UTF8Encoding]::new($false)
    $bytes = $encoding.GetBytes($Value)
    $process.StandardInput.BaseStream.Write($bytes, 0, $bytes.Length)
    $process.StandardInput.BaseStream.Flush()
    $process.StandardInput.Close()

    $standardOutput = $process.StandardOutput.ReadToEnd()
    $standardError = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) {
        throw "No se pudo crear el secreto $Name`: $standardError"
    }
}

$projectRoot = Split-Path $PSScriptRoot -Parent
$envFile = Join-Path $projectRoot '.env'
$stackFile = Join-Path $projectRoot 'docker-stack.yml'

if (-not (Test-Path -LiteralPath $envFile)) {
    throw 'Falta .env. Créelo localmente a partir de .env.example.'
}

$nodes = @(docker node ls --format '{{.Hostname}}|{{.Status}}|{{.ManagerStatus}}')
$readyNodes = @($nodes | Where-Object { $_ -match '\|Ready\|' })
if ($readyNodes.Count -lt 2) {
    throw 'Se requieren al menos dos nodos Ready.'
}

if ([string]::IsNullOrWhiteSpace($PostgresNode)) {
    $leader = $nodes | Where-Object { $_ -match '\|Ready\|Leader$' } | Select-Object -First 1
    $PostgresNode = $leader.Split('|')[0]
}

docker node inspect $PostgresNode | Out-Null
docker node update --label-add airportapp.postgres-data=true $PostgresNode | Out-Null

$envValues = @{}
Get-Content -LiteralPath $envFile | ForEach-Object {
    if ($_ -match '^([^#][^=]*)=(.*)$') {
        $envValues[$Matches[1].Trim()] = $Matches[2].Trim().Trim("'")
    }
}

$secretMap = [ordered]@{
    postgres_password = 'POSTGRES_PASSWORD'
    email_password = 'EMAIL_PASSWORD'
    google_client_id = 'GOOGLE_CLIENT_ID'
    google_client_secret = 'GOOGLE_CLIENT_SECRET'
    paypal_client_id = 'PAYPAL_CLIENT_ID'
    paypal_client_secret = 'PAYPAL_CLIENT_SECRET'
    payphone_token = 'PAYPHONE_TOKEN'
    payphone_store_id = 'PAYPHONE_STORE_ID'
    data_protection_certificate_password = 'DATA_PROTECTION_CERTIFICATE_PASSWORD'
}

foreach ($item in $secretMap.GetEnumerator()) {
    if ([string]::IsNullOrWhiteSpace($envValues[$item.Value])) {
        throw "Falta $($item.Value) en .env."
    }
    if (-not (docker secret ls --filter "name=$($item.Key)" --format '{{.Name}}')) {
        New-DockerTextSecret -Name $item.Key -Value $envValues[$item.Value]
    }
}

if ([string]::IsNullOrWhiteSpace($envValues['DATA_PROTECTION_CERTIFICATE_PATH'])) {
    throw 'Falta DATA_PROTECTION_CERTIFICATE_PATH en .env.'
}
$certificatePath = Resolve-Path -LiteralPath $envValues['DATA_PROTECTION_CERTIFICATE_PATH']
if (-not (docker secret ls --filter 'name=data_protection_certificate' --format '{{.Name}}')) {
    docker secret create data_protection_certificate $certificatePath | Out-Null
}

if (-not (docker volume ls --filter 'name=airportapp_shared_keys' --format '{{.Name}}')) {
    throw 'Cree airportapp_shared_keys sobre almacenamiento compartido antes de desplegar.'
}

docker stack deploy --detach=true --resolve-image never -c $stackFile $StackName
Write-Host 'Stack solicitado. Ejecute 05-Verify-Swarm.ps1 para verificarlo.'
