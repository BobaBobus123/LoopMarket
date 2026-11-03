import re

target_file = r"TradeStorm.Api/wwwroot/edit.html"

with open(target_file, "r", encoding="utf-8") as f:
    content = f.read()

# 1. Clean campus text
content = content.replace("на кампусі", "в офісі")

# 2. Replace serviceConfigBox
old_config = re.search(r'<!-- БЛОК РУЧНОГО НАЛАШТУВАННЯ ПОСЛУГИ -->.*?<!-- БЛОК СТАНУ ДЛЯ ТОВАРІВ -->', content, re.DOTALL)

new_config = '''<!-- БЛОК РОЗУМНОГО НАЛАШТУВАННЯ ПОСЛУГИ -->
                <div id="serviceConfigBox" style="display: none; background: rgba(37, 99, 235, 0.05); border: 1.5px solid rgba(37, 99, 235, 0.3); border-radius: 16px; padding: 20px; margin-bottom: 20px;">
                    <div style="font-weight: 800; font-size: 1.05rem; color: #2563eb; display: flex; align-items: center; justify-content: space-between; margin-bottom: 14px;">
                        <span style="display: flex; align-items: center; gap: 8px;">
                            <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2"><path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z"/></svg>
                            Параметри та специфікація послуги
                        </span>
                        <span style="font-size: 0.78rem; font-weight: 700; color: #2563eb; background: rgba(37,99,235,0.1); padding: 3px 8px; border-radius: 6px;">Автоадаптація полів</span>
                    </div>

                    <div class="form-group" style="margin-bottom: 14px;">
                        <label style="font-size: 0.88rem; font-weight: 700; color: var(--text-main); margin-bottom: 6px;">Вид та специфіка робіт</label>
                        <select id="serviceKind" class="modern-input" onchange="onServiceKindChanged()">
                            <option value="movers" selected>🚚 Вантажники / Перевезення / Підйом важких речей</option>
                            <option value="repairs">🛠 Ремонтні роботи / Сантехнік / Електрик / Майстер / Клінінг</option>
                            <option value="online">💻 Онлайн / IT / Програмування / Репетиторство / Курсові / Дизайн</option>
                            <option value="beauty">✂️ Краса / Фото / Відео / Здоров'я</option>
                            <option value="other">🤝 Інша студентська допомога або послуга</option>
                        </select>
                    </div>

                    <div class="two-columns">
                        <div class="form-group">
                            <label style="font-size: 0.85rem; font-weight: 700; color: var(--text-main);">Тип тарифікації</label>
                            <select id="serviceRateType" class="modern-input" onchange="onServiceRateTypeChanged()">
                                <option value="hourly" selected>⏱ Погодинна оплата (грн / год)</option>
                                <option value="fixed">🏷 Фіксована ціна за всю роботу (грн)</option>
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

                <!-- БЛОК СТАНУ ДЛЯ ТОВАРІВ -->'''

if old_config:
    content = content[:old_config.start()] + new_config + content[old_config.end():]
    print("Replaced config box in edit.html")

# 3. Replace locationGroup
old_location = re.search(r'<div class="form-group" id="locationGroup">.*?</div>\s*<!-- ФОТОГРАФІЇ', content, re.DOTALL)

new_location = '''<div class="form-group" id="locationGroup">
                    <div style="display: flex; justify-content: space-between; align-items: baseline; margin-bottom: 8px;">
                        <label id="locationLabel" style="font-weight: 700; color: var(--text-main); margin: 0;">📍 Точна адреса на карті</label>
                        <span id="mapHintBadge" style="font-size: 0.8rem; color: var(--primary-light); font-weight: 700;">OpenStreetMap інтерактивна мітка</span>
                    </div>

                    <div id="mapWrapper">
                        <div style="display: flex; gap: 8px; margin-bottom: 8px;">
                            <input type="text" id="mapSearchInput" placeholder="🔍 Введіть вулицю та номер будинку (наприклад: Соборна 15, Кременчук)" class="modern-input" style="flex: 1; margin: 0;" onkeydown="if(event.key==='Enter'){event.preventDefault();searchAddressOnMap();}">
                            <button type="button" onclick="searchAddressOnMap()" class="btn-outline" style="white-space: nowrap; font-weight: 700; padding: 0 16px; border-radius: 8px; cursor: pointer; background: var(--surface); color: var(--text-main); border: 1.5px solid var(--border-color); transition: 0.2s;">Знайти на карті</button>
                            <button type="button" onclick="locateUserPosition()" title="Моє місцезнаходження" class="btn-outline" style="padding: 0 14px; font-size: 1.1rem; border-radius: 8px; cursor: pointer; background: var(--surface); border: 1.5px solid var(--border-color); transition: 0.2s;">📍</button>
                        </div>
                        <div id="map" style="height: 250px; border-radius: 12px; border: 2px solid var(--border-color); margin-bottom: 8px; z-index: 1;"></div>
                    </div>

                    <div id="onlineNoticeBox" style="display: none; padding: 14px 18px; background: rgba(37,99,235,0.08); border: 1.5px dashed #2563eb; border-radius: 12px; margin-bottom: 10px; color: #1d4ed8; font-size: 0.9rem; line-height: 1.4;">
                        🌐 <strong>Дистанційний формат:</strong> Фізична адреса для цієї послуги не потрібна. Клієнти зможуть оформити замовлення онлайн з будь-якого куточка України.
                    </div>

                    <input type="text" id="location" class="modern-input" required placeholder="Точна адреса (вулиця, номер будинку, район або місто)">
                    <div style="font-size: 0.8rem; color: var(--text-muted); margin-top: 5px;">
                        💡 <i>Клікніть або перетягніть мітку на карті для точного визначення, або впишіть назву вулиці та номер будинку вручну.</i>
                    </div>
                </div>

                <!-- ФОТОГРАФІЇ'''

if old_location:
    content = content[:old_location.start()] + new_location + content[old_location.end():]
    print("Replaced location group in edit.html")

with open(target_file, "w", encoding="utf-8") as f:
    f.write(content)
print("Updated edit.html HTML structure")
