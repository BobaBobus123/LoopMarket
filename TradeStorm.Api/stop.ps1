# LoopMarket - Швидка зупинка всіх процесів сервера
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Clear-Host

Write-Host "======================================================================" -ForegroundColor Red
Write-Host "             [!] LOOPMARKET - ЗУПИНКА ВСІХ СЕРВІСІВ" -ForegroundColor Red
Write-Host "======================================================================" -ForegroundColor Red
Write-Host ""

$ports = @(65083, 65082)
$found = $false

foreach ($port in $ports) {
    $conns = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
    if ($conns) {
        foreach ($c in $conns) {
            $pidToKill = $c.OwningProcess
            try {
                $proc = Get-Process -Id $pidToKill -ErrorAction SilentlyContinue
                Write-Host ("[+] Зупиняємо процес PID $pidToKill ($($proc.ProcessName)) на порту $port") -ForegroundColor Yellow
                Stop-Process -Id $pidToKill -Force -ErrorAction SilentlyContinue
                $found = $true
            } catch {}
        }
    }
}

Get-Process -Name dotnet -ErrorAction SilentlyContinue | ForEach-Object {
    try {
        $cmd = (Get-WmiObject Win32_Process -Filter "ProcessId = $($_.Id)").CommandLine
        if ($cmd -like "*TradeStorm*" -or $cmd -like "*LoopMarket*") {
            Write-Host ("[+] Зупиняємо фоновий dotnet PID " + $_.Id) -ForegroundColor Yellow
            Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
            $found = $true
        }
    } catch {}
}

if (-not $found) {
    Write-Host "[i] Активних процесів LoopMarket не знайдено (порти вже вільні)." -ForegroundColor Gray
} else {
    Write-Host ""
    Write-Host "  [OK] Всі процеси LoopMarket успішно зупинено, порти звільнено!" -ForegroundColor Green
}

Write-Host ""
Start-Sleep -Seconds 1
