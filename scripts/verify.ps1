param([string]$OfflineFallback)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$env:DOTNET_CLI_HOME=Join-Path $root '.dotnet-home'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='0'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
function Invoke-Checked([string]$Command,[string[]]$Arguments) {
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Echec de $Command ($LASTEXITCODE)." }
}
$solution=Join-Path $root 'ProxmoxMonitor.slnx'
$restore=@('restore',$solution,'--locked-mode')
if ($OfflineFallback) { $restore+=@("-p:RestoreFallbackFolders=$OfflineFallback",'-p:NuGetAudit=false','--ignore-failed-sources') }
Invoke-Checked 'dotnet' $restore
Invoke-Checked 'dotnet' @('build',$solution,'-c','Release','--no-restore')
Invoke-Checked 'dotnet' @('test',$solution,'-c','Release','--no-build','--logger','console;verbosity=minimal')
$tool=Get-Command ashboard-module -ErrorAction Stop
# The SDK grants its test AppContainer read access to this directory. A fresh
# directory belongs to the account running verification, even if build output
# was created earlier by a different restricted account.
$checkStage=Join-Path $root ('artifacts\sdk-check-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $checkStage | Out-Null
foreach ($file in @('ProxmoxMonitor.dll','manifest.json')) {
    Copy-Item -LiteralPath (Join-Path $root "src\ProxmoxMonitor\bin\Release\net10.0\$file") -Destination $checkStage
}
Invoke-Checked $tool.Source @('check',$checkStage,'--out',(Join-Path $root 'artifacts\check'))
