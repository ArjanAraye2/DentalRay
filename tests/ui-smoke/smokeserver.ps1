$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add("http://localhost:8765/")
$listener.Start()
while ($true) {
  $ctx = $listener.GetContext()
  $p = $ctx.Request.Url.AbsolutePath.TrimStart("/")
  if ($p -eq "") { $p = "smoke3.html" }
  $file = Join-Path "C:\Users\arjan\AppData\Local\Temp\opencode" $p
  if (Test-Path $file) {
    $bytes = [IO.File]::ReadAllBytes($file)
    $ctx.Response.ContentType = if ($file.EndsWith(".js")) { "application/javascript" } else { "text/html" }
    $ctx.Response.OutputStream.Write($bytes, 0, $bytes.Length)
  } else {
    $ctx.Response.StatusCode = 404
  }
  $ctx.Response.Close()
}
