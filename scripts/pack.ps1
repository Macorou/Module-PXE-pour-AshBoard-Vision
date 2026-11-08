param([string]$ModuleId,[string]$Author,[string]$Repository,[string]$Version,[switch]$ValidateOnly,[switch]$TechnicalTest,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$env:DOTNET_CLI_HOME=Join-Path $root '.dotnet-home'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='0'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
try {
    $sourceManifest=Join-Path $root 'src\ProxmoxMonitor\manifest.json'
    $manifest=[IO.File]::ReadAllText($sourceManifest) | ConvertFrom-Json
    if ($TechnicalTest) {
        $ModuleId='dev.proxmox-monitor';$Author='Development test';$Repository='';$Version='0.1.0-test'
    } else {
        if ([string]::IsNullOrWhiteSpace($ModuleId) -or $ModuleId.Length -gt 40 -or $ModuleId -cnotmatch '^[a-z0-9][a-z0-9-]*\.[a-z0-9][a-z0-9-]*$' -or $ModuleId.StartsWith('dev.')) { throw 'Identifiant permanent du module invalide.' }
        if ($manifest.id -notlike 'dev.*' -and $ModuleId -cne $manifest.id) { throw "Ne change pas l’identifiant permanent de ce module." }
        if ([string]::IsNullOrWhiteSpace($Author) -or $Author -match '[\r\n]' -or $Author -like '*local*' -or $Author -like '*test*') { throw 'Indique un auteur public.' }
        if ([string]::IsNullOrWhiteSpace($Repository) -or $Repository -notmatch '^[A-Za-z0-9][A-Za-z0-9-]{0,38}/[A-Za-z0-9._-]{1,100}$') { throw 'Indique le depot au format compte/nom-du-depot.' }
        $semver='^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$'
        if ([string]::IsNullOrWhiteSpace($Version) -or $Version -cnotmatch $semver) { throw 'Version SemVer invalide.' }
        if ($Matches[4]) { foreach ($identifier in $Matches[4].Split('.')) { if ($identifier -match '^0[0-9]+$') { throw 'Identifiant de preversion numerique invalide.' } } }
    }
    if ($ValidateOnly) { Write-Output "Metadonnees valides : $ModuleId $Version"; exit 0 }
    $artifacts=[IO.Path]::GetFullPath((Join-Path $root 'artifacts'))
    if (-not $OutputDirectory) { $OutputDirectory=Join-Path $artifacts $(if($TechnicalTest){'technical-test'}else{'packages'}) }
    $output=[IO.Path]::GetFullPath($OutputDirectory)
    if (-not $output.StartsWith($artifacts+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Le paquet doit rester dans artifacts.' }
    & dotnet build (Join-Path $root 'src\ProxmoxMonitor\ProxmoxMonitor.csproj') -c Release --no-restore "-p:Version=$Version"
    if ($LASTEXITCODE -ne 0) { throw 'La construction du module a echoue.' }
    $stage=Join-Path $artifacts ('staging\'+$ModuleId+'-'+[Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $stage,$output | Out-Null
    Copy-Item -LiteralPath (Join-Path $root 'src\ProxmoxMonitor\bin\Release\net10.0\ProxmoxMonitor.dll') -Destination $stage
    Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $stage
    $manifest.id=$ModuleId;$manifest.author=$Author;$manifest.version=$Version
    if ($TechnicalTest) { $manifest.PSObject.Properties.Remove('repository');[IO.File]::WriteAllText((Join-Path $stage 'TECHNICAL-TEST.txt'),'Technical test only; do not install permanently.') } else { $manifest.repository=$Repository }
    [IO.File]::WriteAllText((Join-Path $stage 'manifest.json'),($manifest | ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
    $tool=Get-Command ashboard-module -ErrorAction SilentlyContinue
    if (-not $tool) { throw 'Installe les outils officiels du SDK AshBoard Vision avant de fabriquer le paquet.' }
    & $tool.Source pack $stage --out $output
    if ($LASTEXITCODE -ne 0) { throw 'La fabrication officielle du paquet a echoue.' }
    Write-Output "Paquet et empreinte disponibles dans $output"
    exit 0
} catch { Write-Error $_.Exception.Message; exit 1 }
