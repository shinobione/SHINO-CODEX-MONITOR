# Native Windows radar icon QA: decode all ICO frames and inspect actual GDI+ pixels.
# Does not require an installed app, account, network, elevation or user data.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$file = Join-Path $PSScriptRoot 'CODEX-PERISCOPE.ico'
if (!(Test-Path -LiteralPath $file)) { throw 'Radar ICO not generated.' }
$expected = @(16, 32, 48, 256)
$stream = [System.IO.File]::Open($file, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
$reader = [System.IO.BinaryReader]::new($stream)
try {
    if ($reader.ReadUInt16() -ne 0 -or $reader.ReadUInt16() -ne 1) {
        throw 'Incorrect ICO header.'
    }
    $count = [int]$reader.ReadUInt16()
    if ($count -ne $expected.Count) { throw 'Incorrect number of radar icon sizes.' }
    $frames = @()
    for ($i = 0; $i -lt $count; $i++) {
        $w = [int]$reader.ReadByte()
        $h = [int]$reader.ReadByte()
        if ($w -eq 0) { $w = 256 }
        if ($h -eq 0) { $h = 256 }
        $null = $reader.ReadByte() # palette count
        $null = $reader.ReadByte() # reserved
        $planes = $reader.ReadUInt16()
        $depth = $reader.ReadUInt16()
        $length = [int]$reader.ReadUInt32()
        $offset = [int]$reader.ReadUInt32()
        if ($w -ne $expected[$i] -or $h -ne $expected[$i] -or
            $planes -ne 1 -or $depth -ne 32 -or $length -lt 100 -or
            $offset -lt (6 + 16 * $count) -or
            ([long]$offset + [long]$length) -gt $stream.Length) {
            throw "Invalid ICO entry $i"
        }
        $frames += @{ Size = $w; Length = $length; Offset = $offset }
    }
    foreach ($frame in $frames) {
        $stream.Position = [long]$frame.Offset
        $blob = $reader.ReadBytes([int]$frame.Length)
        $imageStream = [System.IO.MemoryStream]::new($blob, $false)
        try {
            $image = [System.Drawing.Bitmap]::new($imageStream)
            try {
                if ($image.Width -ne $frame.Size -or $image.Height -ne $frame.Size) {
                    throw "Radar image decode size mismatch: $($frame.Size)"
                }
                $visible = 0
                $teal = 0
                $stride = if ($frame.Size -eq 256) { 2 } else { 1 }
                for ($y = 0; $y -lt $image.Height; $y += $stride) {
                    for ($x = 0; $x -lt $image.Width; $x += $stride) {
                        $pixel = $image.GetPixel($x, $y)
                        if ($pixel.A -gt 128) { $visible++ }
                        if ($pixel.A -gt 128 -and $pixel.G -gt 130 -and
                            ($pixel.G - $pixel.R) -gt 70) { $teal++ }
                    }
                }
                if ($visible -lt 12 -or $teal -lt 2) {
                    throw "Radar frame lacks visible lens or sweep pixels: $($frame.Size)px"
                }
                Write-Host "PASS: radar ICO $($frame.Size)px GDI+ decoded ($visible visible, $teal cyan/teal)" -ForegroundColor Green
            } finally { $image.Dispose() }
        } finally { $imageStream.Dispose() }
    }
} finally { $reader.Dispose(); $stream.Dispose() }
