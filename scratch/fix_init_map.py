import re

with open(r"TradeStorm.Api/wwwroot/details.html", "r", encoding="utf-8") as f:
    text = f.read()

old_func_pattern = re.search(r'function initServiceOrderMap\(targetAddress = \'\'\) \{.*?window\.toggleEditAddressField = function\(\) \{', text, re.DOTALL)

clean_func = '''function initServiceOrderMap(targetAddress = '') {
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
                if (targetAddress && !targetAddress.toLowerCase().includes('онлайн')) {
                    fetch(`https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(targetAddress)}&limit=1&accept-language=uk`)
                        .then(r => r.json())
                        .then(data => {
                            if (data && data.length > 0) {
                                const lat = parseFloat(data[0].lat);
                                const lon = parseFloat(data[0].lon);
                                if (serviceOrderMap) serviceOrderMap.setView([lat, lon], 15);
                                if (serviceOrderMarker) serviceOrderMarker.setLatLng([lat, lon]);
                            }
                        }).catch(() => {});
                }
                setTimeout(() => { if (serviceOrderMap) serviceOrderMap.invalidateSize(); }, 250);
            } catch(err) {
                console.warn("Service map init error:", err);
            }
        }

        window.toggleEditAddressField = function() {'''

if old_func_pattern:
    text = text[:old_func_pattern.start()] + clean_func + text[old_func_pattern.end():]
    with open(r"TradeStorm.Api/wwwroot/details.html", "w", encoding="utf-8") as f:
        f.write(text)
    print("Fixed initServiceOrderMap in details.html")
else:
    print("Could not find pattern!")
