# DebugDuck 網頁版 —— 本機開發用的靜態伺服器。
# 用法：在這個資料夾按右鍵「用 PowerShell 執行」，或：
#   powershell -ExecutionPolicy Bypass -File docs\serve.ps1
# 然後瀏覽器開 http://localhost:5500/ （會自動開）。Ctrl+C 結束。

$port = 5500
$root = $PSScriptRoot
$mime = @{
  '.html' = 'text/html; charset=utf-8'
  '.css'  = 'text/css; charset=utf-8'
  '.js'   = 'text/javascript; charset=utf-8'
  '.mjs'  = 'text/javascript; charset=utf-8'
  '.json' = 'application/json; charset=utf-8'
  '.png'  = 'image/png'
  '.jpg'  = 'image/jpeg'
  '.svg'  = 'image/svg+xml'
  '.ico'  = 'image/x-icon'
  '.mp3'  = 'audio/mpeg'
}

$listener = [System.Net.HttpListener]::new()
$listener.Prefixes.Add("http://localhost:$port/")
$listener.Start()
Write-Host "DebugDuck 網頁版：http://localhost:$port/  （Ctrl+C 結束）" -ForegroundColor Yellow
Start-Process "http://localhost:$port/"

try {
  while ($listener.IsListening) {
    $ctx = $listener.GetContext()
    try {
      $rel = [Uri]::UnescapeDataString($ctx.Request.Url.AbsolutePath.TrimStart('/'))
      if ($rel -eq '') { $rel = 'index.html' }
      $path = Join-Path $root $rel
      if (Test-Path $path -PathType Leaf) {
        $bytes = [System.IO.File]::ReadAllBytes($path)
        $ext = [System.IO.Path]::GetExtension($path).ToLower()
        $ctx.Response.ContentType = if ($mime.ContainsKey($ext)) { $mime[$ext] } else { 'application/octet-stream' }
        $ctx.Response.Headers.Add('Cache-Control', 'no-store')
        $ctx.Response.ContentLength64 = $bytes.Length
        $ctx.Response.OutputStream.Write($bytes, 0, $bytes.Length)
      } else {
        $ctx.Response.StatusCode = 404
      }
    } catch {
      $ctx.Response.StatusCode = 500
    } finally {
      $ctx.Response.OutputStream.Close()
    }
  }
} finally {
  $listener.Stop()
}
