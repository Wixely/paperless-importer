[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string] $ExecutablePath,

    [string] $ServiceName = 'PaperlessImporter'
)

$resolvedExecutable = (Resolve-Path -LiteralPath $ExecutablePath).Path
if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    throw "Service '$ServiceName' already exists."
}

& sc.exe create $ServiceName binPath= ('"{0}"' -f $resolvedExecutable) start= auto DisplayName= 'Paperless Importer'
if ($LASTEXITCODE -ne 0) {
    throw "sc.exe create failed with exit code $LASTEXITCODE."
}

& sc.exe failure $ServiceName reset= 86400 actions= restart/10000/restart/30000/restart/60000
if ($LASTEXITCODE -ne 0) {
    throw "sc.exe failure failed with exit code $LASTEXITCODE."
}

Write-Host "Service '$ServiceName' installed. Configure its environment securely before starting it."
