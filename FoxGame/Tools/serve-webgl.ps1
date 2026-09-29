# Minimal static server for the WebGL build (FoxGame/Builds/WebGL).
# Usage: powershell -ExecutionPolicy Bypass -File FoxGame/Tools/serve-webgl.ps1 [-Port 8080]
# (ASCII only: Windows PowerShell 5.1 misreads UTF-8 scripts without BOM)
param([int]$Port = 8080)

$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\Builds\WebGL'))
if (-not (Test-Path (Join-Path $root 'index.html'))) {
    Write-Error "No build at $root - run Unity menu Tools > Fox > WebGL first."
    exit 1
}

$mime = @{
    '.html' = 'text/html; charset=utf-8'; '.js' = 'application/javascript'; '.wasm' = 'application/wasm'
    '.data' = 'application/octet-stream'; '.json' = 'application/json'; '.css' = 'text/css'
    '.png' = 'image/png'; '.ico' = 'image/x-icon'
}

$listener = [Net.HttpListener]::new()
$listener.Prefixes.Add("http://localhost:$Port/")
$listener.Start()
Write-Host "FoxGame WebGL: http://localhost:$Port/  ($root)"

while ($listener.IsListening) {
    $ctx = $listener.GetContext()
    $res = $ctx.Response
    try {
        $rel = [Uri]::UnescapeDataString($ctx.Request.Url.AbsolutePath).TrimStart('/')
        if ($rel -eq '') { $rel = 'index.html' }
        $path = [IO.Path]::GetFullPath((Join-Path $root $rel))
        if ($path.StartsWith($root) -and (Test-Path $path -PathType Leaf)) {
            $ext = [IO.Path]::GetExtension($path).ToLower()
            $res.ContentType = if ($mime.ContainsKey($ext)) { $mime[$ext] } else { 'application/octet-stream' }
            $bytes = [IO.File]::ReadAllBytes($path)
            $res.ContentLength64 = $bytes.Length
            $res.OutputStream.Write($bytes, 0, $bytes.Length)
        } else {
            $res.StatusCode = 404
        }
    } catch {
        $res.StatusCode = 500
    } finally {
        $res.Close()
    }
}
