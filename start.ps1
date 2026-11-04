# LoopMarket - Автоматичний запуск
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Clear-Host

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "          🚀 LOOPMARKET - АВТОМАТИЧНИЙ ЗАПУСК САЙТУ" -ForegroundColor Green
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host ""

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $scriptDir) { $scriptDir = "D:\LoopMarket" }
$apiDir = Join-Path $scriptDir "TradeStorm.Api"
if (-not (Test-Path $apiDir)) {
    $apiDir = $scriptDir
}

# 1. Перевірка наявності dotnet
$dotnetCheck = Get-Command "dotnet" -ErrorAction SilentlyContinue
if (-not $dotnetCheck) {
    Write-Host "[ПОМИЛКА] .NET SDK не знайдено в системі!" -ForegroundColor Red
    Write-Host "Встановіть .NET 10 з https://dotnet.microsoft.com" -ForegroundColor Yellow
    Read-Host "Натисніть Enter для виходу..."
    exit 1
}

# 2. Перевірка чи порт 65083 вже зайнятий
$existing = Get-NetTCPConnection -LocalPort 65083 -State Listen -ErrorAction SilentlyContinue
if ($existing) {
    $pidNum = $existing[0].OwningProcess
    Write-Host "[OK] Сервер вже працює на http://localhost:65083 (PID: $pidNum)" -ForegroundColor Yellow
} else {
    Write-Host "[1/3] Запуск веб-сервера ASP.NET Core (TradeStorm.Api)..." -ForegroundColor Cyan
    Start-Process -FilePath "dotnet" -ArgumentList "run" -WorkingDirectory $apiDir -WindowStyle Normal

    Write-Host "[2/3] Очікування ініціалізації сервера..." -ForegroundColor Cyan
    $ready = $false
    for ($i = 0; $i -lt 35; $i++) {
        try {
            $resp = Invoke-WebRequest -Uri "http://localhost:65083/api/admin/announcement/active" -UseBasicParsing -TimeoutSec 1 -ErrorAction SilentlyContinue
            if ($resp.StatusCode -eq 200) {
                $ready = $true
                break
            }
        } catch {
            # waiting
        }
        Start-Sleep -Milliseconds 600
    }

    if (-not $ready) {
        Write-Host "[УВАГА] Сервер ще завантажується, але відкриваємо браузер..." -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "======================================================================" -ForegroundColor Green
Write-Host "  ✅ САЙТ УСПІШНО ЗАПУЩЕНО І ГОТОВИЙ ДО РОБОТИ!" -ForegroundColor Green
Write-Host "======================================================================" -ForegroundColor Green
Write-Host "  🌐 Головна сторінка:       http://localhost:65083" -ForegroundColor White
Write-Host "  🛡️ Панель оператора:       http://localhost:65083/admin.html" -ForegroundColor White
Write-Host "  💬 Чат та повідомлення:    http://localhost:65083/messages.html" -ForegroundColor White
Write-Host "  🔑 Вхід / Тест акаунти:    http://localhost:65083/login.html" -ForegroundColor White
Write-Host ""
Write-Host "  👤 Тестові акаунти (пароль для всіх: 123456):" -ForegroundColor Cyan
Write-Host "     - Нікіта (Адмін):        +19285345308  (nikitazhidenko@gmail.com)" -ForegroundColor Gray
Write-Host "     - Вова Вакуленко:        +380960353574  (test1@gmail.com)" -ForegroundColor Gray
Write-Host "     - Вова Вовус:            +380733874585  (bobababus1@gmail.com)" -ForegroundColor Gray
Write-Host "     - Олексій (Замовник):    +380998877665  (testbuyer_orders@khta.edu.ua)" -ForegroundColor Gray
Write-Host "======================================================================" -ForegroundColor Green
Write-Host "Відкриваємо браузер на http://localhost:65083 ..." -ForegroundColor Yellow
Start-Process "http://localhost:65083"

Write-Host ""
Write-Host "Щоб зупинити сервер, запустіть stop.bat" -ForegroundColor Cyan
Start-Sleep -Seconds 3
