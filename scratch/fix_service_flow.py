import re

# 1. UPDATE details.html
details_path = r"TradeStorm.Api/wwwroot/details.html"
with open(details_path, "r", encoding="utf-8") as f:
    d_content = f.read()

# Replace Section 2 and Section 3 in details.html
old_sec2_and_3 = re.search(r'<!-- 2\. Обсяг послуги \(Години та Кількість людей\) -->.*?<!-- 4\. Спосіб розрахунку -->', d_content, re.DOTALL)

new_sec2_and_3 = '''<!-- 2. Обсяг та параметри робіт -->
                        <div style="margin-bottom: 20px; background: var(--bg-color); padding: 16px; border-radius: 14px; border: 1px solid var(--border-color);">
                            <div style="font-size: 0.88rem; font-weight: 800; text-transform: uppercase; color: var(--text-muted); letter-spacing: 0.5px; margin-bottom: 12px; display: flex; align-items: center; justify-content: space-between;">
                                <span style="display: flex; align-items: center; gap: 8px;">
                                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>
                                    2. Інформація та умови послуги
                                </span>
                                <span style="font-size: 0.78rem; font-weight: 700; color: #10b981; background: rgba(16,185,129,0.1); padding: 2px 8px; border-radius: 6px;">✓ Вказано в оголошенні</span>
                            </div>

                            <!-- Відображення заданої інформації з послуги -->
                            <div style="background: var(--surface); border: 1.5px solid var(--border-color); border-radius: 12px; padding: 12px 14px; margin-bottom: 14px;">
                                <div style="font-size: 0.76rem; font-weight: 700; text-transform: uppercase; color: var(--text-muted); margin-bottom: 3px;">Послуга / найменування:</div>
                                <div id="servicePresetTitleDisplay" style="font-weight: 800; font-size: 1.05rem; color: var(--text-main); margin-bottom: 8px;"></div>
                                
                                <div style="font-size: 0.76rem; font-weight: 700; text-transform: uppercase; color: var(--text-muted); margin-bottom: 3px;">Опис робіт та умови від автора:</div>
                                <div id="servicePresetDescDisplay" style="font-size: 0.88rem; color: var(--text-main); line-height: 1.45; max-height: 90px; overflow-y: auto; background: var(--bg-color); padding: 8px 10px; border-radius: 8px;"></div>
                            </div>
                            
                            <input type="hidden" id="serviceTaskType" value="">

                            <!-- Степпери годин та фахівців (тільки для погодинних послуг) -->
                            <div id="serviceHourlyControlsBox" style="display: grid; grid-template-columns: 1fr 1fr; gap: 14px; margin-bottom: 12px;">
                                <!-- Години -->
                                <div style="background: var(--surface); padding: 12px; border-radius: 12px; border: 1px solid var(--border-color);">
                                    <div style="font-size: 0.8rem; font-weight: 700; color: var(--text-muted); margin-bottom: 6px;">Тривалість робіт:</div>
                                    <div style="display: flex; align-items: center; justify-content: space-between; margin-bottom: 8px;">
                                        <button type="button" onclick="changeServiceHours(-1)" style="width: 32px; height: 32px; border-radius: 8px; border: 1px solid var(--border-color); background: var(--bg-color); color: var(--text-main); font-weight: 800; font-size: 1.1rem; cursor: pointer;">−</button>
                                        <span id="serviceHoursDisplay" style="font-size: 1.25rem; font-weight: 800; color: var(--text-main);">2 год</span>
                                        <button type="button" onclick="changeServiceHours(1)" style="width: 32px; height: 32px; border-radius: 8px; border: 1px solid var(--border-color); background: var(--bg-color); color: var(--text-main); font-weight: 800; font-size: 1.1rem; cursor: pointer;">+</button>
                                    </div>
                                    <div style="display: flex; gap: 4px; flex-wrap: wrap;">
                                        <button type="button" class="city-pill-btn" onclick="setServiceHours(1)">1 год</button>
                                        <button type="button" class="city-pill-btn" onclick="setServiceHours(2)">2 год</button>
                                        <button type="button" class="city-pill-btn" onclick="setServiceHours(3)">3 год</button>
                                        <button type="button" class="city-pill-btn" onclick="setServiceHours(4)">4 год</button>
                                    </div>
                                </div>

                                <!-- Кількість фахівців -->
                                <div style="background: var(--surface); padding: 12px; border-radius: 12px; border: 1px solid var(--border-color);">
                                    <div style="font-size: 0.8rem; font-weight: 700; color: var(--text-muted); margin-bottom: 6px;">Кількість фахівців:</div>
                                    <div style="display: flex; align-items: center; justify-content: space-between; margin-bottom: 8px;">
                                        <button type="button" onclick="changeServiceWorkers(-1)" style="width: 32px; height: 32px; border-radius: 8px; border: 1px solid var(--border-color); background: var(--bg-color); color: var(--text-main); font-weight: 800; font-size: 1.1rem; cursor: pointer;">−</button>
                                        <span id="serviceWorkersDisplay" style="font-size: 1.25rem; font-weight: 800; color: var(--text-main);">1 особа</span>
                                        <button type="button" onclick="changeServiceWorkers(1)" style="width: 32px; height: 32px; border-radius: 8px; border: 1px solid var(--border-color); background: var(--bg-color); color: var(--text-main); font-weight: 800; font-size: 1.1rem; cursor: pointer;">+</button>
                                    </div>
                                    <div style="display: flex; gap: 4px; flex-wrap: wrap;">
                                        <button type="button" class="city-pill-btn" onclick="setServiceWorkers(1)">1 особа</button>
                                        <button type="button" class="city-pill-btn" onclick="setServiceWorkers(2)">2 особи</button>
                                        <button type="button" class="city-pill-btn" onclick="setServiceWorkers(3)">3 особи</button>
                                    </div>
                                </div>
                            </div>

                            <!-- Фіксований тариф (без степперів) -->
                            <div id="serviceFixedNoticeBox" style="display: none; background: rgba(37,99,235,0.08); border: 1.5px solid rgba(37,99,235,0.25); border-radius: 10px; padding: 10px 14px; margin-bottom: 12px; color: #1d4ed8; font-size: 0.88rem; font-weight: 700;">
                                🏷 <strong>Фіксований тариф:</strong> Вартість робіт становить фіксовану суму за виконання всієї послуги.
                            </div>

                            <!-- Дата та час -->
                            <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 12px;">
                                <div>
                                    <label style="display: block; font-size: 0.8rem; font-weight: 600; color: var(--text-muted); margin-bottom: 4px;">Дата виконання *</label>
                                    <input type="date" id="serviceDate" required style="width: 100%; box-sizing: border-box; background: var(--surface); border: 1.5px solid var(--border-color); border-radius: 10px; padding: 9px 12px; color: var(--text-main); font-size: 0.88rem; outline: none;">
                                </div>
                                <div>
                                    <label style="display: block; font-size: 0.8rem; font-weight: 600; color: var(--text-muted); margin-bottom: 4px;">Бажаний час *</label>
                                    <input type="time" id="serviceTime" required value="10:00" style="width: 100%; box-sizing: border-box; background: var(--surface); border: 1.5px solid var(--border-color); border-radius: 10px; padding: 9px 12px; color: var(--text-main); font-size: 0.88rem; outline: none;">
                                </div>
                            </div>
                        </div>

                        <!-- 3. Адреса локації -->
                        <div>
                            <div style="font-size: 0.88rem; font-weight: 800; text-transform: uppercase; color: var(--text-muted); letter-spacing: 0.5px; margin-bottom: 10px; display: flex; align-items: center; justify-content: space-between;">
                                <span style="display: flex; align-items: center; gap: 8px;">
                                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z"/><circle cx="12" cy="10" r="3"/></svg>
                                    3. Адреса виконання послуги
                                </span>
                                <span id="serviceOrderMapBadge" style="font-size: 0.74rem; color: #10b981; font-weight: 700; background: rgba(16,185,129,0.1); padding: 2px 8px; border-radius: 6px;">✓ Вказано в оголошенні</span>
                            </div>

                            <!-- Відображення вже заданої адреси -->
                            <div style="background: var(--surface); border: 1.5px solid var(--border-color); border-radius: 12px; padding: 12px 14px; margin-bottom: 10px; display: flex; align-items: center; justify-content: space-between;">
                                <div style="display: flex; align-items: center; gap: 10px;">
                                    <span style="font-size: 1.3rem;">📍</span>
                                    <div>
                                        <div style="font-size: 0.74rem; font-weight: 700; text-transform: uppercase; color: var(--text-muted);">Локація / адреса з оголошення:</div>
                                        <div id="servicePresetAddressText" style="font-weight: 800; font-size: 0.98rem; color: var(--text-main);">...</div>
                                    </div>
                                </div>
                                <button type="button" onclick="toggleEditAddressField()" style="background: var(--bg-color); border: 1px solid var(--border-color); border-radius: 8px; padding: 5px 10px; font-size: 0.78rem; font-weight: 700; color: #2563eb; cursor: pointer;">
                                    ✏️ Уточнити
                                </button>
                            </div>

                            <!-- Інтерактивна карта (відображає точку з оголошення) -->
                            <div id="serviceOrderMapBox" style="margin-bottom: 10px;">
                                <div id="serviceMap" style="height: 160px; border-radius: 10px; border: 1.5px solid var(--border-color); margin-bottom: 8px; z-index: 1;"></div>
                            </div>

                            <!-- Приховані / розгортані поля для уточнення адреси -->
                            <div id="serviceAddressEditBox" style="display: none; margin-bottom: 10px; padding: 10px; background: var(--bg-color); border-radius: 10px; border: 1px dashed var(--border-color);">
                                <div style="display: grid; grid-template-columns: 1fr 1.5fr; gap: 8px; margin-bottom: 6px;">
                                    <input type="text" id="serviceCity" required placeholder="Місто (напр. Кременчук)" style="width: 100%; box-sizing: border-box; background: var(--surface); border: 1.5px solid var(--border-color); border-radius: 8px; padding: 8px 12px; color: var(--text-main); font-size: 0.88rem; outline: none;">
                                    <input type="text" id="serviceAddress" required placeholder="Вулиця, номер будинку" style="width: 100%; box-sizing: border-box; background: var(--surface); border: 1.5px solid var(--border-color); border-radius: 8px; padding: 8px 12px; color: var(--text-main); font-size: 0.88rem; outline: none;">
                                </div>
                            </div>

                            <input type="text" id="serviceFloorInfo" placeholder="Поверх, під'їзд або уточнення адреси (необов'язково)" style="width: 100%; box-sizing: border-box; background: var(--bg-color); border: 1.5px solid var(--border-color); border-radius: 10px; padding: 9px 14px; color: var(--text-main); font-size: 0.88rem; outline: none;">
                        </div>
                    </div>

                    <!-- ПРАВА КОЛОНКА (50%): Оплата, деталі, розрахунок, кнопки -->
                    <div>
                        <!-- 4. Спосіб розрахунку -->'''

if old_sec2_and_3:
    d_content = d_content[:old_sec2_and_3.start()] + new_sec2_and_3 + d_content[old_sec2_and_3.end():]
    print("Replaced Section 2 & 3 in details.html modal")

# Update openServiceOrderModal in details.html to pre-fill all information
old_open_func = re.search(r'window\.openServiceOrderModal = function\(\) \{.*?updateServiceCostSummary\(\);\s*modal\.style\.display = \'flex\';', d_content, re.DOTALL)

new_open_func = '''window.toggleEditAddressField = function() {
            const box = document.getElementById('serviceAddressEditBox');
            if (box) {
                box.style.display = (box.style.display === 'none') ? 'block' : 'none';
            }
        };

        window.openServiceOrderModal = function() {
            let token = localStorage.getItem('loopToken');
            if (!token) {
                alert('Щоб оформити послугу, увійдіть у свій акаунт.');
                window.location.href = '/login.html';
                return;
            }

            let currentUserId = null;
            try {
                const user = parseJwt(token);
                if (user) currentUserId = parseInt(user["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"]);
            } catch(e) {}

            if (currentUserId && window.currentListingData && currentUserId === window.currentListingData.sellerId) {
                alert('Ви не можете оформити замовлення на власну послугу.');
                return;
            }

            const modal = document.getElementById('serviceOrderModal');
            if (modal && window.currentListingData) {
                const item = window.currentListingData;
                serviceHourlyRate = item.price || 50;
                const cond = item.condition || '';
                const loc = item.location || 'Кременчук';
                const title = item.title || 'Послуга';

                isServiceFixedPrice = cond.includes('Фіксован');
                isServiceOnlineFormat = cond.includes('Онлайн') || loc.toLowerCase().includes('онлайн');
                isServiceMover = title.toLowerCase().includes('вантаж') || title.toLowerCase().includes('переїзд');

                // 1. Hero банер
                document.getElementById('serviceHeroTitle').innerText = title;
                if (isServiceFixedPrice) {
                    document.getElementById('serviceHeroRate').innerText = `${serviceHourlyRate} ₴ (фіксовано)`;
                } else {
                    document.getElementById('serviceHeroRate').innerText = `${serviceHourlyRate} ₴ / год`;
                }

                // 2. Відображення заданої інформації з послуги
                document.getElementById('servicePresetTitleDisplay').innerText = title;
                document.getElementById('serviceTaskType').value = title;
                document.getElementById('servicePresetDescDisplay').innerText = item.description || "Опис послуги згідно з оголошенням";

                // Адаптація блоків: фіксований vs погодинний
                const hourlyBox = document.getElementById('serviceHourlyControlsBox');
                const fixedBox = document.getElementById('serviceFixedNoticeBox');
                if (isServiceFixedPrice) {
                    if (hourlyBox) hourlyBox.style.display = 'none';
                    if (fixedBox) fixedBox.style.display = 'block';
                    serviceHours = 1;
                    serviceWorkers = 1;
                } else {
                    if (hourlyBox) hourlyBox.style.display = 'grid';
                    if (fixedBox) fixedBox.style.display = 'none';
                    serviceWorkers = isServiceMover ? 2 : 1;
                    serviceHours = 2;
                }

                // 3. Автозаповнення адреси з оголошення
                const presetAddrEl = document.getElementById('servicePresetAddressText');
                const cityInput = document.getElementById('serviceCity');
                const addrInput = document.getElementById('serviceAddress');
                const mapBox = document.getElementById('serviceOrderMapBox');
                const mapBadge = document.getElementById('serviceOrderMapBadge');

                if (isServiceOnlineFormat) {
                    if (presetAddrEl) presetAddrEl.innerText = '🌐 Онлайн / Дистанційно (по всій Україні)';
                    cityInput.value = 'Онлайн';
                    addrInput.value = 'Дистанційно (Zoom / Telegram / Meet)';
                    if (mapBox) mapBox.style.display = 'none';
                    if (mapBadge) mapBadge.innerText = 'Онлайн формат';
                } else {
                    if (presetAddrEl) presetAddrEl.innerText = loc;
                    if (mapBox) mapBox.style.display = 'block';
                    if (mapBadge) mapBadge.innerText = '✓ Вказано в оголошенні';

                    // Розбиваємо місто та адресу
                    if (loc.includes(',')) {
                        const parts = loc.split(',');
                        cityInput.value = parts[parts.length - 1].trim();
                        addrInput.value = parts.slice(0, parts.length - 1).join(',').trim();
                    } else {
                        cityInput.value = loc;
                        addrInput.value = loc;
                    }
                    
                    // Ініціалізуємо карту на вказаній адресі
                    setTimeout(() => {
                        initServiceOrderMap(loc);
                    }, 200);
                }

                const sName = item.seller ? 
                    `${item.seller.firstName || ''} ${item.seller.lastName || ''}`.trim() : 'Виконавець';
                document.getElementById('serviceHeroProvider').innerText = `Автор оголошення: ${sName || 'Користувач'}`;

                let img = 'https://placehold.co/600x400/e2e8f0/64748b?text=Loop+Service';
                if (item.imageUrls && item.imageUrls.length > 0) img = item.imageUrls[0];
                else if (item.imageUrl) img = item.imageUrl.split(';')[0];
                const sHeroImg = document.getElementById('serviceHeroImg');
                sHeroImg.onerror = function() { this.onerror = null; this.src = 'https://placehold.co/600x400/e2e8f0/64748b?text=Loop+Service'; };
                sHeroImg.src = img;

                // Дата за замовчуванням: завтра
                const dateInput = document.getElementById('serviceDate');
                if (!dateInput.value) {
                    const tomorrow = new Date();
                    tomorrow.setDate(tomorrow.getDate() + 1);
                    dateInput.value = tomorrow.toISOString().split('T')[0];
                    dateInput.min = new Date().toISOString().split('T')[0];
                }

                // Ім'я замовника з токена
                try {
                    const user = parseJwt(token);
                    if (user && user["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"]) {
                        const nameInput = document.getElementById('serviceCustomerName');
                        if (!nameInput.value) {
                            nameInput.value = user["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"];
                        }
                    }
                } catch(e) {}

                document.getElementById('serviceOrderErrorMsg').style.display = 'none';
                updateServiceCostSummary();
                modal.style.display = 'flex';'''

if old_open_func:
    d_content = d_content[:old_open_func.start()] + new_open_func + d_content[old_open_func.end():]
    print("Replaced openServiceOrderModal in details.html")

# Update initServiceOrderMap to accept initialAddress
d_content = d_content.replace(
    "function initServiceOrderMap() {",
    "function initServiceOrderMap(targetAddress = '') {"
)
d_content = d_content.replace(
    "if (!serviceOrderMap) {",
    '''if (!serviceOrderMap) {
                    const defLat = 49.0629;
                    const defLng = 33.4042;
                    serviceOrderMap = L.map('serviceMap').setView([defLat, defLng], 13);
                    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png').addTo(serviceOrderMap);
                    serviceOrderMarker = L.marker([defLat, defLng], { draggable: true }).addTo(serviceOrderMap);

                    serviceOrderMap.on('click', function(e) {
                        serviceOrderMarker.setLatLng(e.latlng);
                        reverseGeocodeServiceOrder(e.latlng.lat, e.latlng.lng);
                    });

                    serviceOrderMarker.on('dragend', function() {
                        const pos = serviceOrderMarker.getLatLng();
                        reverseGeocodeServiceOrder(pos.lat, pos.lng);
                    });
                }
                if (targetAddress && !targetAddress.toLowerCase().includes('онлайн')) {
                    fetch(`https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(targetAddress)}&limit=1&accept-language=uk`)
                        .then(r => r.json())
                        .then(data => {
                            if (data && data.length > 0) {
                                const lat = parseFloat(data[0].lat);
                                const lon = parseFloat(data[0].lon);
                                serviceOrderMap.setView([lat, lon], 15);
                                serviceOrderMarker.setLatLng([lat, lon]);
                            }
                        }).catch(() => {});
                }'''
)

with open(details_path, "w", encoding="utf-8") as f:
    f.write(d_content)
print("Saved details.html successfully")

# 2. UPDATE create.html (Remove tedious dummy spec boxes from creation)
create_path = r"TradeStorm.Api/wwwroot/create.html"
with open(create_path, "r", encoding="utf-8") as f:
    c_content = f.read()

# Replace serviceConfigBox with clean, simple service parameters
old_c_config = re.search(r'<!-- БЛОК РОЗУМНОГО НАЛАШТУВАННЯ ПОСЛУГИ -->.*?<!-- БЛОК СТАНУ ДЛЯ ЗВИЧАЙНИХ ТОВАРІВ -->', c_content, re.DOTALL)

new_c_config = '''<!-- БЛОК НАЛАШТУВАННЯ ПОСЛУГИ -->
                <div id="serviceConfigBox" style="display: none; background: rgba(37, 99, 235, 0.05); border: 1.5px solid rgba(37, 99, 235, 0.3); border-radius: 16px; padding: 20px; margin-bottom: 20px;">
                    <div style="font-weight: 800; font-size: 1.05rem; color: #2563eb; display: flex; align-items: center; justify-content: space-between; margin-bottom: 14px;">
                        <span style="display: flex; align-items: center; gap: 8px;">
                            <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2"><path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z"/></svg>
                            Параметри та умови послуги
                        </span>
                    </div>

                    <div class="two-columns">
                        <div class="form-group">
                            <label style="font-size: 0.85rem; font-weight: 700; color: var(--text-main);">Тип оплати (Тариф)</label>
                            <select id="serviceRateType" class="modern-input" onchange="onServiceRateTypeChanged()">
                                <option value="hourly" selected>⏱ Погодинна оплата (грн / год)</option>
                                <option value="fixed">🏷 Фіксована вартість за всю роботу (грн)</option>
                                <option value="negotiated">🤝 За домовленістю / від базової суми</option>
                            </select>
                        </div>
                        <div class="form-group">
                            <label style="font-size: 0.85rem; font-weight: 700; color: var(--text-main);">Формат надання</label>
                            <select id="serviceFormat" class="modern-input" onchange="onServiceFormatChanged()">
                                <option value="visit" selected>🚗 З виїздом за адресою клієнта</option>
                                <option value="on_site">📍 У майстерні / в офісі виконавця</option>
                                <option value="online">🌐 Онлайн / Дистанційно (Zoom, Telegram, Meet)</option>
                            </select>
                        </div>
                    </div>

                    <!-- Швидкий перемикач: Працюю тільки онлайн -->
                    <div style="display: flex; align-items: center; gap: 10px; margin-top: 14px; padding: 10px 14px; background: rgba(37,99,235,0.08); border-radius: 10px; border: 1px solid rgba(37,99,235,0.2);">
                        <input type="checkbox" id="serviceIsOnlineOnly" onchange="toggleOnlineMode(this.checked)" style="width: 18px; height: 18px; accent-color: #2563eb; cursor: pointer; margin: 0;">
                        <label for="serviceIsOnlineOnly" style="margin: 0; font-weight: 700; font-size: 0.88rem; color: #2563eb; cursor: pointer; user-select: none;">🌐 Надавати послугу виключно онлайн (дистанційно по всій Україні, приховати карту)</label>
                    </div>
                </div>

                <!-- БЛОК СТАНУ ДЛЯ ЗВИЧАЙНИХ ТОВАРІВ -->'''

if old_c_config:
    c_content = c_content[:old_c_config.start()] + new_c_config + c_content[old_c_config.end():]
    print("Replaced serviceConfigBox in create.html")

# Remove onServiceKindChanged call in create.html
c_content = c_content.replace("onServiceKindChanged();", "")

with open(create_path, "w", encoding="utf-8") as f:
    f.write(c_content)
print("Saved create.html successfully")
