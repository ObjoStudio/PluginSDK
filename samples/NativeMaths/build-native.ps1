$ErrorActionPreference = 'Stop'

$sampleRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$output = Join-Path $sampleRoot 'native\win-x64'
New-Item -ItemType Directory -Force -Path $output | Out-Null

if (-not [Environment]::Is64BitProcess) {
    throw 'This script builds a win-x64 library. Open the "x64 Native Tools Command Prompt for VS" (or a Developer PowerShell with -Arch x64) and run it again.'
}

if (-not (Get-Command cl -ErrorAction SilentlyContinue)) {
    throw 'cl was not found on PATH. Open the "x64 Native Tools Command Prompt for VS" (or a Visual Studio Developer PowerShell) and run this script again. Any Windows C compiler works too; README.md has one-line cl and gcc commands.'
}

Push-Location $sampleRoot
try {
    # Literal relative arguments keep the native command line free of spaces
    # and trailing backslashes, so PowerShell never has to quote it.
    cl /nologo /O2 /W3 /LD /Inative\include `
        /Fonative\win-x64\nativemaths.obj `
        /Fenative\win-x64\acme_nativemaths.dll `
        native\src\nativemaths.c
    if ($LASTEXITCODE -ne 0) {
        throw "cl failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

$dll = Join-Path $output 'acme_nativemaths.dll'
if (-not (Test-Path $dll)) {
    throw "cl reported success but did not produce $dll."
}
Write-Host "Built native maths asset for win-x64 in $output"
