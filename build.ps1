$ErrorActionPreference = "Stop"

function Align-ResourceWriter {
    param([System.IO.BinaryWriter]$Writer)

    while (($Writer.BaseStream.Position % 4) -ne 0) {
        $Writer.Write([byte]0)
    }
}

function Write-ResourceUnicodeString {
    param(
        [System.IO.BinaryWriter]$Writer,
        [string]$Value
    )

    foreach ($char in (($Value + [char]0).ToCharArray())) {
        $Writer.Write([UInt16][char]$char)
    }
}

function New-VersionResourceBlock {
    param(
        [string]$Key,
        [UInt16]$Type,
        [byte[]]$ValueBytes,
        [byte[][]]$Children
    )

    $stream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($stream, [System.Text.Encoding]::Unicode)
    try {
        $writer.Write([UInt16]0)
        $writer.Write([UInt16]($ValueBytes.Length))
        $writer.Write([UInt16]$Type)
        Write-ResourceUnicodeString -Writer $writer -Value $Key
        Align-ResourceWriter -Writer $writer
        if ($ValueBytes.Length -gt 0) {
            $writer.Write($ValueBytes)
        }
        Align-ResourceWriter -Writer $writer
        foreach ($child in $Children) {
            $writer.Write($child)
            Align-ResourceWriter -Writer $writer
        }

        $writer.Flush()
        $bytes = $stream.ToArray()
        [System.BitConverter]::GetBytes([UInt16]$bytes.Length).CopyTo($bytes, 0)
        return $bytes
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

function New-VersionStringBlock {
    param(
        [string]$Key,
        [string]$Value
    )

    $valueBytes = [System.Text.Encoding]::Unicode.GetBytes($Value + [char]0)
    $charLength = [UInt16]($Value.Length + 1)

    $stream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($stream, [System.Text.Encoding]::Unicode)
    try {
        $writer.Write([UInt16]0)
        $writer.Write($charLength)
        $writer.Write([UInt16]1)
        Write-ResourceUnicodeString -Writer $writer -Value $Key
        Align-ResourceWriter -Writer $writer
        $writer.Write($valueBytes)
        Align-ResourceWriter -Writer $writer
        $writer.Flush()
        $bytes = $stream.ToArray()
        [System.BitConverter]::GetBytes([UInt16]$bytes.Length).CopyTo($bytes, 0)
        return $bytes
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

function New-FixedFileInfoBytes {
    param([version]$Version)

    $stream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($stream)
    try {
        $writer.Write([UInt32]4277077181)
        $writer.Write([UInt32]0x00010000)
        $writer.Write([UInt32](($Version.Major -shl 16) -bor $Version.Minor))
        $writer.Write([UInt32](($Version.Build -shl 16) -bor $Version.Revision))
        $writer.Write([UInt32](($Version.Major -shl 16) -bor $Version.Minor))
        $writer.Write([UInt32](($Version.Build -shl 16) -bor $Version.Revision))
        $writer.Write([UInt32]0x3F)
        $writer.Write([UInt32]0)
        $writer.Write([UInt32]0x00040004)
        $writer.Write([UInt32]1)
        $writer.Write([UInt32]0)
        $writer.Write([UInt32]0)
        $writer.Write([UInt32]0)
        $writer.Flush()
        return $stream.ToArray()
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

function New-VersionResourceBytes {
    param(
        [string]$VersionText,
        [string]$ProductName,
        [string]$FileDescription,
        [string]$CompanyName
    )

    $version = [version]$VersionText
    if ($version.Build -lt 0) {
        $version = New-Object version($version.Major, $version.Minor, 0, 0)
    }
    elseif ($version.Revision -lt 0) {
        $version = New-Object version($version.Major, $version.Minor, $version.Build, 0)
    }

    $strings = @(
        (New-VersionStringBlock -Key "CompanyName" -Value $CompanyName),
        (New-VersionStringBlock -Key "FileDescription" -Value $FileDescription),
        (New-VersionStringBlock -Key "FileVersion" -Value $version.ToString()),
        (New-VersionStringBlock -Key "ProductName" -Value $ProductName),
        (New-VersionStringBlock -Key "ProductVersion" -Value $VersionText)
    )

    $stringTable = New-VersionResourceBlock -Key "040904B0" -Type 1 -ValueBytes @() -Children $strings
    $stringFileInfo = New-VersionResourceBlock -Key "StringFileInfo" -Type 1 -ValueBytes @() -Children @($stringTable)

    $translationBytes = [byte[]](0x09, 0x04, 0xB0, 0x04)
    $translation = New-VersionResourceBlock -Key "Translation" -Type 0 -ValueBytes $translationBytes -Children @()
    $varFileInfo = New-VersionResourceBlock -Key "VarFileInfo" -Type 1 -ValueBytes @() -Children @($translation)

    return New-VersionResourceBlock -Key "VS_VERSION_INFO" -Type 0 -ValueBytes (New-FixedFileInfoBytes -Version $version) -Children @($stringFileInfo, $varFileInfo)
}

function Set-ExecutableVersionResource {
    param(
        [string]$Path,
        [string]$VersionText,
        [string]$ProductName,
        [string]$FileDescription,
        [string]$CompanyName
    )

    $signature = @"
using System;
using System.Runtime.InteropServices;

public static class Win32VersionResource
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr BeginUpdateResource(string pFileName, bool bDeleteExistingResources);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UpdateResource(IntPtr hUpdate, IntPtr lpType, IntPtr lpName, ushort wLanguage, byte[] lpData, uint cbData);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EndUpdateResource(IntPtr hUpdate, bool fDiscard);
}
"@

    Add-Type -TypeDefinition $signature -Language CSharp | Out-Null

    $data = New-VersionResourceBytes -VersionText $VersionText -ProductName $ProductName -FileDescription $FileDescription -CompanyName $CompanyName
    $handle = [Win32VersionResource]::BeginUpdateResource($Path, $false)
    if ($handle -eq [IntPtr]::Zero) {
        throw "BeginUpdateResource fehlgeschlagen: $([Runtime.InteropServices.Marshal]::GetLastWin32Error())"
    }

    $rtVersion = [IntPtr]16
    $name = [IntPtr]1
    foreach ($language in @([UInt16]0, [UInt16]0x0409)) {
        $ok = [Win32VersionResource]::UpdateResource($handle, $rtVersion, $name, $language, $data, [uint32]$data.Length)
        if (-not $ok) {
            [void][Win32VersionResource]::EndUpdateResource($handle, $true)
            throw "UpdateResource fehlgeschlagen: $([Runtime.InteropServices.Marshal]::GetLastWin32Error())"
        }
    }

    if (-not [Win32VersionResource]::EndUpdateResource($handle, $false)) {
        throw "EndUpdateResource fehlgeschlagen: $([Runtime.InteropServices.Marshal]::GetLastWin32Error())"
    }
}

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$srcDir = Join-Path $projectRoot "src"
$distDir = Join-Path $projectRoot "dist"
$objDir = Join-Path $projectRoot "obj"
$assetsDir = Join-Path $projectRoot "assets"
$versionFile = Join-Path $projectRoot "VERSION.txt"
$iconPath = Join-Path $assetsDir "navimow.ico"

New-Item -ItemType Directory -Force -Path $distDir | Out-Null
New-Item -ItemType Directory -Force -Path $objDir | Out-Null

$version = (Get-Content -Path $versionFile -Raw).Trim()
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "VERSION.txt ist leer."
}

$assemblyInfoPath = Join-Path $objDir "GeneratedVersionInfo.cs"
@"
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("Navimow Desktop Controller")]
[assembly: AssemblyDescription("Windows-Desktop-App fuer Segway Navimow")]
[assembly: AssemblyCompany("Jailobeam")]
[assembly: AssemblyProduct("Navimow Desktop Controller")]
[assembly: AssemblyCopyright("Copyright (c) Jailobeam")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("$version")]
[assembly: AssemblyFileVersion("$version")]
[assembly: AssemblyInformationalVersion("$version")]
"@ | Set-Content -Path $assemblyInfoPath -Encoding UTF8

$framework = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$csc = Join-Path $framework "csc.exe"
$output = Join-Path $distDir "NavimowDesktopController.exe"

$sources = Get-ChildItem -Path $srcDir -Filter *.cs | ForEach-Object { $_.FullName }
$sources += $assemblyInfoPath

& $csc `
  /target:winexe `
  /out:$output `
  /win32icon:$iconPath `
  /r:System.dll `
  /r:System.Core.dll `
  /r:System.Drawing.dll `
  /r:System.Windows.Forms.dll `
  /r:System.Net.Http.dll `
  /r:System.Web.Extensions.dll `
  /r:System.Net.WebSockets.dll `
  /r:System.Net.WebSockets.Client.dll `
  $sources

Set-ExecutableVersionResource `
  -Path $output `
  -VersionText $version `
  -ProductName "Navimow Desktop Controller" `
  -FileDescription "Navimow Desktop Controller" `
  -CompanyName "Jailobeam"

Write-Host "Build fertig:" $output
