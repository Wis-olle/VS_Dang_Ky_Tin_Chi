$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw 'Cần .NET Framework 4.x trên Windows.' }
$out = Join-Path $env:TEMP ('PhongValidation-' + [guid]::NewGuid().ToString('N') + '.exe')
try {
    & $csc /nologo /target:exe "/out:$out" /r:System.Data.dll (Join-Path $root 'Phong.WinForms\Models.cs') (Join-Path $root 'Phong.WinForms\Validation.cs') (Join-Path $PSScriptRoot 'ValidationTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Biên dịch test thất bại.' }
    & $out
    if ($LASTEXITCODE -ne 0) { throw 'Test thất bại.' }
} finally { Remove-Item $out -ErrorAction SilentlyContinue }
