$ErrorActionPreference = "Stop"

$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
}
if (-not (Test-Path $csc)) { throw "找不到 .NET Framework 编译器 csc.exe" }

$sources = Get-ChildItem -Path $PSScriptRoot -Filter *.cs | ForEach-Object { $_.FullName }
$out = Join-Path $PSScriptRoot "FindMyCursor.exe"

& $csc /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 `
    /win32icon:"$PSScriptRoot\FindMyCursor.ico" /out:"$out" `
    /reference:System.dll,System.Drawing.dll,System.Windows.Forms.dll $sources

if ($LASTEXITCODE -ne 0) { throw "编译失败 (exit $LASTEXITCODE)" }
Write-Host "已生成: $out"
