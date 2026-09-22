<#
    بررسی سخت‌افزار برای DentalRay / Dentix
    ----------------------------------------
    این فایل را روی کامپیوتر مطب اجرا کنید. یک گزارش فارسی می‌سازد که
    می‌گوید آن کامپیوتر برای نصب برنامه مناسب است یا نه.

    روش اجرا:
        راست‌کلیک روی فایل  →  Run with PowerShell
    یا در PowerShell:
        powershell -ExecutionPolicy Bypass -File .\Check-Hardware.ps1
#>

$ErrorActionPreference = "SilentlyContinue"

# ---------- حداقل‌ها و پیشنهادها ----------
$MIN = @{
    CpuCores   = 2
    CpuGHz     = 2.0
    RamGB      = 8
    FreeDiskGB = 40
    Screen     = 1366
}
$REC = @{
    CpuCores   = 4
    CpuGHz     = 2.5
    RamGB      = 16
    FreeDiskGB = 120
    Screen     = 1920
    GpuVramGB  = 8      # برای تحلیل AI محلی (MedGemma)
}

# ---------- جمع‌آوری اطلاعات ----------
$cs   = Get-CimInstance Win32_ComputerSystem
$os   = Get-CimInstance Win32_OperatingSystem
$cpu  = Get-CimInstance Win32_Processor | Select-Object -First 1
$gpus = Get-CimInstance Win32_VideoController
$disks = Get-PSDrive -PSProvider FileSystem

$ramGB      = [math]::Round($cs.TotalPhysicalMemory / 1GB, 1)
$cpuCores   = [int]$cpu.NumberOfCores
$cpuGHz     = [math]::Round($cpu.MaxClockSpeed / 1000, 1)
$osCaption  = $os.Caption
$osBuild    = $os.BuildNumber
$dotnetOK   = $false
$sqlOK      = $false
$freeCGB    = [math]::Round((($disks | Where-Object { $_.Name -eq "C" }).Free) / 1GB, 1)

try { $dv = (& dotnet --list-sdks 2>$null); if ($dv) { $dotnetOK = $true } } catch {}
$sqlSvc = Get-Service -Name "MSSQL*" | Where-Object { $_.Status -eq "Running" }
if ($sqlSvc) { $sqlOK = $true }

# ---------- بررسی ----------
$rows = @()
function Add-Row($name, $value, $min, $rec, $ok) {
    $rows += [pscustomobject]@{ نام=$name; مقدار=$value; حداقل=$min; پیشنهاد=$rec; وضعیت=$(if($ok){"OK"}else{"کم"}) }
}

$cpuOK  = ($cpuCores -ge $MIN.CpuCores) -and ($cpuGHz -ge $MIN.CpuGHz)
$ramOK  = $ramGB -ge $MIN.RamGB
$diskOK = $freeCGB -ge $MIN.FreeDiskGB
$osOK   = ([int]$osBuild -ge 17763)   # Windows 10 1809+

$rows += [pscustomobject]@{ نام="پردازنده (هسته)"; مقدار=$cpuCores; حداقل=$MIN.CpuCores; پیشنهاد=$REC.CpuCores; وضعیت=$(if($cpuOK){"OK"}else{"کم"}) }
$rows += [pscustomobject]@{ نام="پردازنده (سرعت)"; مقدار="$cpuGHz GHz"; حداقل="$($MIN.CpuGHz) GHz"; پیشنهاد="$($REC.CpuGHz) GHz"; وضعیت=$(if($cpuGHz -ge $MIN.CpuGHz){"OK"}else{"کم"}) }
$rows += [pscustomobject]@{ نام="حافظه (RAM)"; مقدار="$ramGB GB"; حداقل="$($MIN.RamGB) GB"; پیشنهاد="$($REC.RamGB) GB"; وضعیت=$(if($ramOK){"OK"}else{"کم"}) }
$rows += [pscustomobject]@{ نام="فضای خالی C:"; مقدار="$freeCGB GB"; حداقل="$($MIN.FreeDiskGB) GB"; پیشنهاد="$($REC.FreeDiskGB) GB"; وضعیت=$(if($diskOK){"OK"}else{"کم"}) }
$rows += [pscustomobject]@{ نام="سیستم‌عامل"; مقدار=$osCaption; حداقل="Windows 10 1809"; پیشنهاد="Windows 11"; وضعیت=$(if($osOK){"OK"}else{"کم"}) }
$rows += [pscustomobject]@{ نام=".NET 10"; مقدار=$(if($dotnetOK){"نصب است"}else{"نصب نیست"}); حداقل="لازم"; پیشنهاد="لازم"; وضعیت=$(if($dotnetOK){"OK"}else{"کم"}) }
$rows += [pscustomobject]@{ نام="SQL Server"; مقدار=$(if($sqlOK){"نصب است"}else{"نصب نیست"}); حداقل="Express"; پیشنهاد="Express/Standard"; وضعیت=$(if($sqlOK){"OK"}else{"کم"}) }

# ---------- گزارش ----------
Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "   بررسی سخت‌افزار برای DentalRay / Dentix" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  نام کامپیوتر : $($cs.Name)" -ForegroundColor White
Write-Host "  کاربر        : $($cs.UserName)"
Write-Host ""

$rows | Format-Table -AutoSize | Out-String | Write-Host

# ---------- GPU (برای AI) ----------
Write-Host "--- کارت گرافیک (فقط برای تحلیل AI محلی لازم است) ---" -ForegroundColor Yellow
foreach ($g in $gpus) {
    $vram = [math]::Round($g.AdapterRAM / 1GB, 1)
    $gpuAI = $vram -ge $REC.GpuVramGB
    Write-Host ("  {0}  -  {1} GB حافظه  ->  {2}" -f $g.Name, $vram, $(if($gpuAI){"مناسب برای AI"}else{"برای AI کافی نیست"}))
}
Write-Host ""

# ---------- وضعیت کلی ----------
$failCount = ($rows | Where-Object { $_.وضعیت -eq "کم" }).Count
$aiReady = ($gpus | Where-Object { [math]::Round($_.AdapterRAM/1GB,1) -ge $REC.GpuVramGB }).Count -gt 0

Write-Host "================================================================" -ForegroundColor Cyan
if ($failCount -eq 0) {
    Write-Host "  نتیجه: این کامپیوتر برای نصب DentalRay مناسب است" -ForegroundColor Green
} else {
    Write-Host "  نتیجه: $failCount مورد کمتر از حداقل است" -ForegroundColor Red
    Write-Host "  موارد زیر را ارتقا دهید:" -ForegroundColor Red
    $rows | Where-Object { $_.وضعیت -eq "کم" } | ForEach-Object {
        Write-Host "    - $($_.نام)  (فعلی: $($_.مقدار)  |  حداقل: $($_.حداقل))" -ForegroundColor Red
    }
}
Write-Host ""
if ($aiReady) {
    Write-Host "  تحلیل AI محلی: ممکن است (کارت گرافیک کافی است)" -ForegroundColor Green
} else {
    Write-Host "  تحلیل AI محلی: ممکن نیست (کارت گرافیک ضعیف است)" -ForegroundColor Yellow
    Write-Host "    جایگزین: بدون AI کار می‌کند، یا از سرویس ابری استفاده شود" -ForegroundColor Gray
}
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  این گزارش را برای نصب‌کننده بفرستید." -ForegroundColor Gray
Write-Host ""

# ---------- ذخیره گزارش ----------
$out = Join-Path $env:TEMP "DentalRay-Hardware-Report.txt"
$rows | Out-File $out -Encoding UTF8
Add-Content $out ""
Add-Content $out "نام کامپیوتر: $($cs.Name)"
Add-Content $out "کارت گرافیک: $($gpus | ForEach-Object { $_.Name })"
Add-Content $out "نتیجه: $(if($failCount -eq 0){'مناسب'}else{"$failCount مورد کم است"})"
Add-Content $out "AI محلی: $(if($aiReady){'ممکن'}else{'ممکن نیست'})"
Write-Host "  گزارش ذخیره شد: $out" -ForegroundColor Gray
Write-Host ""
