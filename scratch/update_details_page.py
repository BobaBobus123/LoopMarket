import re

target_file = r"TradeStorm.Api/wwwroot/details.html"

with open(target_file, "r", encoding="utf-8") as f:
    content = f.read()

# 1. Add Leaflet CSS and JS to <head> if missing
if "leaflet.css" not in content:
    leaflet_tags = '''    <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css" />
    <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
'''
    content = content.replace("<title>Деталі товару | Loop Market</title>", "<title>Деталі товару | Loop Market</title>\n" + leaflet_tags)
    print("Added Leaflet tags to details.html")

# 2. Update Location section in serviceOrderModal
old_loc_section = re.search(r'<!-- 3\. Адреса локації -->.*?</div>\s*</div>\s*<!-- ПРАВА КОЛОНКА', content, re.DOTALL)

new_loc_section = '''<!-- 3. Адреса локації -->
                        <div>
                            <div style="font-size: 0.88rem; font-weight: 800; text-transform: uppercase; color: var(--text-muted); letter-spacing: 0.5px; margin-bottom: 10px; display: flex; align-items: center; justify-content: space-between;">
                                <span style="display: flex; align-items: center; gap: 8px;">
                                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z"/><circle cx="12" cy="10" r="3"/></svg>
                                    3. Адреса виконання послуги
                                </span>
                                <span id="serviceOrderMapBadge" style="font-size: 0.74rem; color: #2563eb; font-weight: 700;">Інтерактивна карта</span>
                            </div>

                            <!-- Інтерактивна карта для точного вибору адреси -->
                            <div id="serviceOrderMapBox" style="margin-bottom: 10px;">
                                <div style="display: flex; gap: 6px; margin-bottom: 6px;">
                                    <input type="text" id="serviceMapSearchInput" placeholder="🔍 Введіть вашу вулицю та номер будинку" style="flex: 1; box-sizing: border-box; background: var(--bg-color); border: 1.5px solid var(--border-color); border-radius: 8px; padding: 8px 12px; color: var(--text-main); font-size: 0.85rem; outline: none;" onkeydown="if(event.key==='Enter'){event.preventDefault();searchServiceOrderAddress();}">
                                    <button type="button" onclick="searchServiceOrderAddress()" style="background: var(--surface); border: 1.5px solid var(--border-color); border-radius: 8px; padding: 0 14px; font-size: 0.82rem; font-weight: 700; color: var(--text-main); cursor: pointer; transition: 0.2s;">Знайти</button>
                                </div>
                                <div id="serviceMap" style="height: 170px; border-radius: 10px; border: 1.5px solid var(--border-color); margin-bottom: 8px; z-index: 1;"></div>
                            </div>

                            <div style="display: grid; grid-template-columns: 1fr 1.5fr; gap: 10px; margin-bottom: 8px;">
                                <input type="text" id="serviceCity" required placeholder="Місто (напр. Кременчук)" style="width: 100%; box-sizing: border-box; background: var(--bg-color); border: 1.5px solid var(--border-color); border-radius: 10px; padding: 10px 14px; color: var(--text-main); font-size: 0.9rem; outline: none;">
                                <input type="text" id="serviceAddress" required placeholder="Вулиця, номер будинку" style="width: 100%; box-sizing: border-box; background: var(--bg-color); border: 1.5px solid var(--border-color); border-radius: 10px; padding: 10px 14px; color: var(--text-main); font-size: 0.9rem; outline: none;">
                            </div>
                            <input type="text" id="serviceFloorInfo" placeholder="Поверх, під'їзд, наявність вантажного ліфта (якщо є)" style="width: 100%; box-sizing: border-box; background: var(--bg-color); border: 1.5px solid var(--border-color); border-radius: 10px; padding: 9px 14px; color: var(--text-main); font-size: 0.88rem; outline: none;">
                        </div>
                    </div>

                    <!-- ПРАВА КОЛОНКА'''

if old_loc_section:
    content = content[:old_loc_section.start()] + new_loc_section + content[old_loc_section.end():]
    print("Replaced location section in details.html")

# 3. Add JS map support for serviceOrderModal
js_map_helpers = '''
        let serviceOrderMap = null;
        let serviceOrderMarker = null;

        async function reverseGeocodeServiceOrder(lat, lng) {
            try {
                const res = await fetch(`https://nominatim.openstreetmap.org/reverse?format=json&lat=${lat}&lon=${lng}&accept-language=uk`);
                if (!res.ok) return;
                const data = await res.json();
                if (data && data.address) {
                    const a = data.address;
                    const city = a.city || a.town || a.village || a.county || "Кременчук";
                    const road = a.road || a.pedestrian || a.street || "";
                    const house = a.house_number || "";
                    
                    document.getElementById('serviceCity').value = city;
                    document.getElementById('serviceAddress').value = road + (house ? ` ${house}` : '');
                    const sInp = document.getElementById('serviceMapSearchInput');
                    if (sInp) sInp.value = road ? `${road} ${house}, ${city}`.trim() : city;
                }
            } catch(e) {}
        }

        window.searchServiceOrderAddress = async function() {
            const q = (document.getElementById('serviceMapSearchInput')?.value || '').trim();
            if (!q) return;
            try {
                const res = await fetch(`https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(q)}&limit=1&accept-language=uk`);
                const data = await res.json();
                if (data && data.length > 0) {
                    const lat = parseFloat(data[0].lat);
                    const lon = parseFloat(data[0].lon);
                    if (serviceOrderMap) serviceOrderMap.setView([lat, lon], 16);
                    if (serviceOrderMarker) serviceOrderMarker.setLatLng([lat, lon]);
                    reverseGeocodeServiceOrder(lat, lon);
                } else {
                    alert("Адресу не знайдено. Спробуйте вказати місто або точнішу вулицю.");
                }
            } catch(e) {
                alert("Помилка пошуку адреси");
            }
        };

        function initServiceOrderMap() {
            try {
                if (typeof L === 'undefined' || !document.getElementById('serviceMap')) return;
                if (!serviceOrderMap) {
                    const defLat = 49.0629;
                    const defLng = 33.4042;
                    serviceOrderMap = L.map('serviceMap').setView([defLat, defLng], 13);
                    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                        attribution: '&copy; OpenStreetMap'
                    }).addTo(serviceOrderMap);

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
                setTimeout(() => { if (serviceOrderMap) serviceOrderMap.invalidateSize(); }, 250);
            } catch(err) {
                console.warn("Service map init error:", err);
            }
        }
'''

if "function initServiceOrderMap" not in content:
    content = content.replace("window.openServiceOrderModal = function() {", js_map_helpers + "\n        window.openServiceOrderModal = function() {")
    print("Injected map helpers before openServiceOrderModal in details.html")

# In openServiceOrderModal, call initServiceOrderMap or hide if online
old_open_modal = re.search(r'modal\.style\.display = \'flex\';', content)
if old_open_modal:
    replacement = '''modal.style.display = 'flex';
                const mapBox = document.getElementById('serviceOrderMapBox');
                const badge = document.getElementById('serviceOrderMapBadge');
                if (isServiceOnlineFormat) {
                    if (mapBox) mapBox.style.display = 'none';
                    if (badge) badge.innerText = 'Онлайн формат';
                } else {
                    if (mapBox) mapBox.style.display = 'block';
                    if (badge) badge.innerText = 'Інтерактивна карта';
                    initServiceOrderMap();
                }'''
    content = content[:old_open_modal.start()] + replacement + content[old_open_modal.end():]
    print("Updated openServiceOrderModal to init map")

with open(target_file, "w", encoding="utf-8") as f:
    f.write(content)
print("Updated details.html successfully")
