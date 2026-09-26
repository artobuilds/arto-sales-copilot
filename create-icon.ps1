# Generated vector icon; does not edit any user image.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore,WindowsBase
$assetDir=Join-Path $PSScriptRoot 'Assets'
[void](New-Item -Path $assetDir -ItemType Directory -Force)
$visual=New-Object Windows.Media.DrawingVisual
$draw=$visual.RenderOpen()
$converter=New-Object Windows.Media.BrushConverter
$dark=$converter.ConvertFromString('#14241D')
$accent=$converter.ConvertFromString('#C9DF94')
$draw.DrawRoundedRectangle($dark,$null,[Windows.Rect]::new(0,0,256,256),48,48)
$pen=[Windows.Media.Pen]::new($accent,16)
$pen.StartLineCap=[Windows.Media.PenLineCap]::Round
$pen.EndLineCap=[Windows.Media.PenLineCap]::Round
$draw.DrawLine($pen,[Windows.Point]::new(68,182),[Windows.Point]::new(128,65))
$draw.DrawLine($pen,[Windows.Point]::new(128,65),[Windows.Point]::new(188,182))
$draw.DrawLine($pen,[Windows.Point]::new(92,144),[Windows.Point]::new(164,144))
$draw.DrawEllipse($accent,$null,[Windows.Point]::new(199,65),12,12)
$draw.Close()
$bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new(256,256,96,96,[Windows.Media.PixelFormats]::Pbgra32)
$bitmap.Render($visual)
$encoder=New-Object Windows.Media.Imaging.PngBitmapEncoder
$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
$memory=New-Object IO.MemoryStream
$encoder.Save($memory)
$data=$memory.ToArray()
$output=[IO.File]::Create((Join-Path $assetDir 'app.ico'))
$binary=[IO.BinaryWriter]::new($output)
$binary.Write([uint16]0);$binary.Write([uint16]1);$binary.Write([uint16]1)
$binary.Write([byte]0);$binary.Write([byte]0);$binary.Write([byte]0);$binary.Write([byte]0)
$binary.Write([uint16]1);$binary.Write([uint16]32);$binary.Write([uint32]$data.Length);$binary.Write([uint32]22);$binary.Write($data)
$binary.Dispose();$memory.Dispose()
