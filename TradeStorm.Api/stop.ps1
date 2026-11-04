# LoopMarket - Зупинка сервера
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Clear-Host

Write-Host '======================================================================' -ForegroundColor Red
Write-Host '          🛑 LOOPMARKET - ЗУПИНКА СЕРВЕРА' -ForegroundColor Red
Write-Host '======================================================================' -ForegroundColor Red
Write-Host ''

 = @(65083, 65082)
 = False

foreach ( in ) {
     = Get-NetTCPConnection -LocalPort  -State Listen -ErrorAction SilentlyContinue
    if () {
        foreach ( in ) {
             = .OwningProcess
            try {
                 = Get-Process -Id  -ErrorAction SilentlyContinue
                Write-Host ('[+] Зупиняємо процес PID ' +  + ' (' + .ProcessName + ') на порту ' + ) -ForegroundColor Yellow
                Stop-Process -Id  -Force -ErrorAction SilentlyContinue
                 = True
            } catch {}
        }
    }
}

if (-not ) {
    Write-Host '[i] Активних процесів LoopMarket на портах 65083/65082 не виявлено.' -ForegroundColor Gray
} else {
    Write-Host ''
    Write-Host '[OK] Сервер LoopMarket успішно зупинено!' -ForegroundColor Green
}

Write-Host ''
Start-Sleep -Seconds 2
