$ErrorActionPreference = 'Stop'

$sampleRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$output = Join-Path $sampleRoot 'native\win-x64'
New-Item -ItemType Directory -Force -Path $output | Out-Null

if (-not [Environment]::Is64BitProcess) {
    throw 'This script builds a win-x64 library. Open the "x64 Native Tools Command Prompt for VS" (or a Developer PowerShell with -Arch x64) and run it again.'
}

if (-not (Get-Command cl.exe -ErrorAction SilentlyContinue)) {
    throw 'cl.exe was not found on PATH. Open the "x64 Native Tools Command Prompt for VS" (or a Visual Studio Developer PowerShell) and run this script again.'
}

cl /nologo /O2 /W3 /LD `
  "/I${sampleRoot}\native\include" `
  "/Fo${output}\" `
  "/Fe${output}\acme_nativemaths.dll" `
  "${sampleRoot}\native\src\nativemaths.c"

Write-Host "Built native maths asset for win-x64 in $output"
