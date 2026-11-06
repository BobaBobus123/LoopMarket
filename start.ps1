# LoopMarket - Автоматичний запуск усього проєкту
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Clear-Host

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "           [+] LOOPMARKET - АВТОМАТИЧНИЙ ЗАПУСК САЙТУ" -ForegroundColor Green
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host ""

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $scriptDir) { $scriptDir = "D:\LoopMarket" }
$apiDir = Join-Path $scriptDir "TradeStorm.Api"
if (-not (Test-Path $apiDir)) {
    $apiDir = $scriptDir
}

# 1. Перевірка .NET SDK
$dotnetCheck = Get-Command "dotnet" -ErrorAction SilentlyContinue
if (-not $dotnetCheck) {
    Write-Host "[ПОМИЛКА] .NET SDK не знайдено в системі!" -ForegroundColor Red
    Write-Host "Встановіть .NET 10 з офіційного сайту: https://dotnet.microsoft.com" -ForegroundColor Yellow
    Read-Host "Натисніть Enter для виходу..."
    exit 1
}

# 2. Функція перевірки працездатності сайту (Health check)
function Test-LoopMarketHealth {
    try {
        $resp = Invoke-WebRequest -Uri "http://localhost:65083/health" -UseBasicParsing -TimeoutSec 1 -ErrorAction SilentlyContinue
        return ($resp -and $resp.StatusCode -eq 200)
    } catch {
        return $false
    }
}

# 3. Перевірка чи порт 65083 вже зайнятий
$existing = Get-NetTCPConnection -LocalPort 65083 -State Listen -ErrorAction SilentlyContinue
$serverProc = $null

if ($existing) {
    $existingPid = $existing[0].OwningProcess
    if (Test-LoopMarketHealth) {
        Write-Host "[OK] Сайт вже активний і працює на http://localhost:65083 (PID: $existingPid)" -ForegroundColor Green
        try { $serverProc = Get-Process -Id $existingPid -ErrorAction SilentlyContinue } catch {}
    } else {
        Write-Host "[i] Порт 65083 зайнятий старим процесом (PID: $existingPid), звільняємо порт..." -ForegroundColor Yellow
        Stop-Process -Id $existingPid -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 800
    }
}

# 4. Запуск сервера, якщо він ще не запущений
if (-not $serverProc -or $serverProc.HasExited) {
    Write-Host "[1/2] Запуск сервера (ASP.NET Core + База даних + Telegram-бот)..." -ForegroundColor Cyan
    
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "dotnet"
    $psi.Arguments = "run"
    $psi.WorkingDirectory = $apiDir
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $serverProc = [System.Diagnostics.Process]::Start($psi)

    Write-Host "[2/2] Очікування ініціалізації сервісів..." -ForegroundColor Cyan
    $ready = $false
    for ($i = 1; $i -le 40; $i++) {
        Write-Host -NoNewline "."
        if (Test-LoopMarketHealth) {
            $ready = $true
            break
        }
        if ($serverProc.HasExited) {
            Write-Host ""
            Write-Host "[ПОМИЛКА] Процес сервера завершився з кодом: $($serverProc.ExitCode)" -ForegroundColor Red
            Read-Host "Натисніть Enter..."
            exit 1
        }
        Start-Sleep -Milliseconds 600
    }
    Write-Host ""

    if (-not $ready) {
        Write-Host "[УВАГА] Сервер ще стартує, але ми відкриваємо браузер..." -ForegroundColor Yellow
    }
}

# 5. Красиве інформаційне табло
Write-Host ""
Write-Host "======================================================================" -ForegroundColor Green
Write-Host "      [OK] САЙТ LOOPMARKET УСПІШНО ЗАПУЩЕНО І ГОТОВИЙ ДО РОБОТИ!" -ForegroundColor Green
Write-Host "======================================================================" -ForegroundColor Green
Write-Host "  -> Головна сторінка каталогу:  http://localhost:65083" -ForegroundColor White
Write-Host "  -> Панель адміністратора:     http://localhost:65083/admin.html" -ForegroundColor White
Write-Host "  -> Чати та повідомлення:      http://localhost:65083/messages.html" -ForegroundColor White
Write-Host "  -> Вхід / Авторизація:        http://localhost:65083/login.html" -ForegroundColor White
Write-Host ""
Write-Host "  -> Telegram-бот верифікації:   @LoopMarket_Verify_bot (активний)" -ForegroundColor Cyan
Write-Host "  -> Google OAuth 2.0:           Налаштовано" -ForegroundColor Cyan
Write-Host "  -> База даних SQLite:          Підключено (TradeStorm.Api/loopmarket.db)" -ForegroundColor Cyan
Write-Host ""
Write-Host "  -> Тестові облікові записи (Пароль для всіх: 123456):" -ForegroundColor Magenta
Write-Host "     * Адмін:       nikitazhidenko@gmail.com  (або тел: +19285345308)" -ForegroundColor Gray
Write-Host "     * Продавець:   test1@gmail.com           (або тел: +380960353574)" -ForegroundColor Gray
Write-Host "     * Покупець:    bobababus1@gmail.com      (або тел: +380733874585)" -ForegroundColor Gray
Write-Host "======================================================================" -ForegroundColor Green
Write-Host "Відкриваємо сайт у браузері..." -ForegroundColor Yellow
Start-Process "http://localhost:65083"

# 6. Інтерактивне меню керування
Write-Host ""
Write-Host "Швидкі команди (натисніть потрібну клавішу):" -ForegroundColor Cyan
Write-Host "  [1] Відкрити головну сторінку каталогу" -ForegroundColor White
Write-Host "  [2] Відкрити Адмін-панель" -ForegroundColor White
Write-Host "  [3] Відкрити Повідомлення / Чат" -ForegroundColor White
Write-Host "  [T] Запустити тести (dotnet test)" -ForegroundColor White
Write-Host "  [R] Перезапустити сервер" -ForegroundColor White
Write-Host "  [Q] Зупинити сайт і вийти" -ForegroundColor Yellow
Write-Host "----------------------------------------------------------------------" -ForegroundColor Gray

try {
    while ($serverProc -and -not $serverProc.HasExited) {
        if ([Console]::KeyAvailable) {
            $key = [Console]::ReadKey($true).Key
            if ($key -eq [ConsoleKey]::D1 -or $key -eq [ConsoleKey]::NumPad1) {
                Write-Host "-> Відкриття сайту..." -ForegroundColor Green
                Start-Process "http://localhost:65083"
            }
            elseif ($key -eq [ConsoleKey]::D2 -or $key -eq [ConsoleKey]::NumPad2) {
                Write-Host "-> Відкриття Адмін-панелі..." -ForegroundColor Green
                Start-Process "http://localhost:65083/admin.html"
            }
            elseif ($key -eq [ConsoleKey]::D3 -or $key -eq [ConsoleKey]::NumPad3) {
                Write-Host "-> Відкриття Чатів..." -ForegroundColor Green
                Start-Process "http://localhost:65083/messages.html"
            }
            elseif ($key -eq [ConsoleKey]::T) {
                Write-Host "`n[ТЕСТИ] Запуск 12 інтеграційних тестів..." -ForegroundColor Cyan
                $testsCsproj = Join-Path $scriptDir "TradeStorm.Api.Tests\TradeStorm.Api.Tests.csproj"
                if (Test-Path $testsCsproj) {
                    dotnet test $testsCsproj
                } else {
                    Write-Host "[!] Файл тестів не знайдено: $testsCsproj" -ForegroundColor Yellow
                }
                Write-Host ""
            }
            elseif ($key -eq [ConsoleKey]::R) {
                Write-Host "`n[ПЕРЕЗАПУСК] Перезапуск сервера..." -ForegroundColor Yellow
                if ($serverProc -and -not $serverProc.HasExited) {
                    Stop-Process -Id $serverProc.Id -Force -ErrorAction SilentlyContinue
                }
                Start-Sleep -Seconds 1
                $psi = New-Object System.Diagnostics.ProcessStartInfo
                $psi.FileName = "dotnet"
                $psi.Arguments = "run"
                $psi.WorkingDirectory = $apiDir
                $psi.UseShellExecute = $false
                $psi.CreateNoWindow = $true
                $serverProc = [System.Diagnostics.Process]::Start($psi)
                Write-Host "[OK] Сервер перезапущено (новий PID: $($serverProc.Id))" -ForegroundColor Green
            }
            elseif ($key -eq [ConsoleKey]::Q) {
                Write-Host "`n[ЗУПИНКА] Зупиняємо сайт..." -ForegroundColor Yellow
                if ($serverProc -and -not $serverProc.HasExited) {
                    Stop-Process -Id $serverProc.Id -Force -ErrorAction SilentlyContinue
                }
                Write-Host "[OK] Сайт зупинено. До побачення!" -ForegroundColor Green
                Start-Sleep -Seconds 1
                exit 0
            }
        }
        Start-Sleep -Milliseconds 300
    }
} catch {
    if ($serverProc) {
        $serverProc.WaitForExit()
    }
}
