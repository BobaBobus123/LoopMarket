import re

target_file = r"TradeStorm.Api/wwwroot/create.html"

with open(target_file, "r", encoding="utf-8") as f:
    content = f.read()

# Replace script starting from let currentListingType = 'product'; to end of <script>
old_script_block = re.search(r'let currentListingType = \'product\';.*?</script>', content, re.DOTALL)

new_script = '''let currentListingType = 'product';
        let leafletMap = null;
        let leafletMarker = null;

        function setListingType(type) {
            currentListingType = type;
            const btnProd = document.getElementById('btnTypeProduct');
            const btnServ = document.getElementById('btnTypeService');
            const servBox = document.getElementById('serviceConfigBox');
            const prodBox = document.getElementById('productConditionBox');
            const heading = document.getElementById('pageHeading');
            const titleLabel = document.getElementById('titleLabel');
            const titleInput = document.getElementById('title');
            const priceLabel = document.getElementById('priceLabel');
            const descInput = document.getElementById('description');
            const catSelect = document.getElementById('categoryId');
            const shippingLabel = document.getElementById('shippingLabel');
            const guideTitle = document.getElementById('guideTitle');
            const guideList = document.getElementById('guideList');

            if (type === 'service') {
                if (btnServ) {
                    btnServ.style.border = '2px solid #2563eb';
                    btnServ.style.background = 'rgba(37, 99, 235, 0.12)';
                    btnServ.style.color = '#2563eb';
                }
                if (btnProd) {
                    btnProd.style.border = '2px solid var(--border-color)';
                    btnProd.style.background = 'var(--surface)';
                    btnProd.style.color = 'var(--text-muted)';
                }

                if (servBox) servBox.style.display = 'block';
                if (prodBox) prodBox.style.display = 'none';

                if (heading) heading.innerText = 'Створення оголошення: Послуга або робота';
                if (titleLabel) titleLabel.innerText = 'Назва послуги';
                if (titleInput) titleInput.placeholder = 'Наприклад: Вантажні роботи / Репетитор з математики / Майстер';
                if (descInput) descInput.placeholder = 'Опишіть ваш досвід, кваліфікацію, умови співпраці та що входить у вартість...';
                if (shippingLabel) shippingLabel.innerText = 'Можливий терміновий виїзд за адресою замовника';

                if (catSelect && catSelect.options) {
                    for (let i = 0; i < catSelect.options.length; i++) {
                        const opt = catSelect.options[i];
                        if (opt.value === '8' || (opt.text && opt.text.toLowerCase().includes('послуг'))) {
                            catSelect.value = opt.value;
                            break;
                        }
                    }
                }

                if (guideTitle) guideTitle.innerText = 'Рекомендації для публікації послуги:';
                if (guideList) {
                    guideList.innerHTML = `
                        <li><strong>Точна адреса:</strong> Оберіть точку на карті де надаєте послугу або куди готові виїжджати (або оберіть онлайн-формат).</li>
                        <li><strong>Зручний тариф:</strong> Вкажіть погодинну ставку або фіксовану вартість за все завдання.</li>
                        <li><strong>Специфікація:</strong> Виберіть вид послуги для автоматичного налаштування потрібних параметрів.</li>
                        <li><strong>Швидкість:</strong> Своєчасно підтверджуйте замовлення клієнтів для отримання високого рейтингу.</li>
                    `;
                }

                onServiceKindChanged();
                onServiceRateTypeChanged();
                onServiceFormatChanged();
            } else {
                if (btnProd) {
                    btnProd.style.border = '2px solid var(--primary-light)';
                    btnProd.style.background = 'var(--primary-soft)';
                    btnProd.style.color = 'var(--primary-light)';
                }
                if (btnServ) {
                    btnServ.style.border = '2px solid var(--border-color)';
                    btnServ.style.background = 'var(--surface)';
                    btnServ.style.color = 'var(--text-muted)';
                }

                if (servBox) servBox.style.display = 'none';
                if (prodBox) prodBox.style.display = 'grid';

                if (heading) heading.innerText = 'Що будемо продавати?';
                if (titleLabel) titleLabel.innerText = 'Назва товару';
                if (titleInput) titleInput.placeholder = 'Наприклад: Калькулятор науковий Casio';
                if (priceLabel) priceLabel.innerText = 'Ціна (грн)';
                if (descInput) descInput.placeholder = 'Розкажіть про особливості товару, стан, чому продаєте тощо...';
                if (shippingLabel) shippingLabel.innerText = 'Відправка поштою';

                if (catSelect && catSelect.value === '8') {
                    catSelect.value = '1';
                }

                toggleOnlineMode(false);

                if (guideTitle) guideTitle.innerText = 'Рекомендації для публікації товару:';
                if (guideList) {
                    guideList.innerHTML = `
                        <li><strong>Чітка назва:</strong> Вказуйте точну модель або назву предмета (наприклад, <i>«Підручник з вищої математики»</i>).</li>
                        <li><strong>Чесна ціна та опис:</strong> Опишіть реальний стан речі, наявність нюансів чи дефектів.</li>
                        <li><strong>Якісні фото:</strong> Завантажте до 5 чітких фотографій при хорошому освітленні.</li>
                        <li><strong>Точна адреса:</strong> Вкажіть точну адресу або район на інтерактивній карті чи текстом.</li>
                    `;
                }
            }

            if (leafletMap) {
                setTimeout(() => { leafletMap.invalidateSize(); }, 200);
            }
        }
        window.setListingType = setListingType;

        function onServiceKindChanged() {
            const kind = document.getElementById('serviceKind')?.value || 'movers';
            const moversBox = document.getElementById('moversSpecBox');
            const repairsBox = document.getElementById('repairsSpecBox');
            const onlineBox = document.getElementById('onlineSpecBox');
            const titleInput = document.getElementById('title');
            const isOnlineCheckbox = document.getElementById('serviceIsOnlineOnly');
            const formatSelect = document.getElementById('serviceFormat');

            if (moversBox) moversBox.style.display = (kind === 'movers') ? 'block' : 'none';
            if (repairsBox) repairsBox.style.display = (kind === 'repairs') ? 'block' : 'none';
            if (onlineBox) onlineBox.style.display = (kind === 'online') ? 'block' : 'none';

            if (kind === 'online') {
                if (isOnlineCheckbox) isOnlineCheckbox.checked = true;
                if (formatSelect) formatSelect.value = 'online';
                toggleOnlineMode(true);
                if (titleInput && (!titleInput.value || titleInput.value.includes('Вантаж') || titleInput.value.includes('Ремонт'))) {
                    titleInput.placeholder = 'Наприклад: Репетиторство з програмування (Python / C#) або англійської';
                }
            } else {
                if (isOnlineCheckbox) isOnlineCheckbox.checked = false;
                if (formatSelect && formatSelect.value === 'online') formatSelect.value = 'visit';
                toggleOnlineMode(false);
                if (kind === 'movers') {
                    if (titleInput && (!titleInput.value || titleInput.value.includes('Репетитор') || titleInput.value.includes('Ремонт'))) {
                        titleInput.placeholder = 'Наприклад: Вантажні роботи / Переїзд / Підйом меблів на поверх';
                    }
                } else if (kind === 'repairs') {
                    if (titleInput && (!titleInput.value || titleInput.value.includes('Вантаж') || titleInput.value.includes('Репетитор'))) {
                        titleInput.placeholder = 'Наприклад: Майстер сантехнік / Ремонт розеток / Клінінг';
                    }
                }
            }
        }
        window.onServiceKindChanged = onServiceKindChanged;

        function toggleOnlineMode(isOnline) {
            const mapWrapper = document.getElementById('mapWrapper');
            const onlineBox = document.getElementById('onlineNoticeBox');
            const locInput = document.getElementById('location');
            const locLabel = document.getElementById('locationLabel');
            const badge = document.getElementById('mapHintBadge');

            if (isOnline) {
                if (mapWrapper) mapWrapper.style.display = 'none';
                if (onlineBox) onlineBox.style.display = 'block';
                if (locInput) locInput.value = 'Онлайн (дистанційно)';
                if (locLabel) locLabel.innerText = '🌐 Формат надання: Онлайн (дистанційно)';
                if (badge) badge.innerText = 'Вся Україна';
            } else {
                if (mapWrapper) mapWrapper.style.display = 'block';
                if (onlineBox) onlineBox.style.display = 'none';
                if (locInput && locInput.value === 'Онлайн (дистанційно)') locInput.value = 'Кременчук';
                if (locLabel) locLabel.innerText = '📍 Точна адреса на карті';
                if (badge) badge.innerText = 'OpenStreetMap інтерактивна мітка';
                if (leafletMap) setTimeout(() => { leafletMap.invalidateSize(); }, 200);
            }
        }
        window.toggleOnlineMode = toggleOnlineMode;

        function onServiceRateTypeChanged() {
            const rateSelect = document.getElementById('serviceRateType');
            const rateType = rateSelect ? rateSelect.value : 'hourly';
            const priceLabel = document.getElementById('priceLabel');
            const priceInput = document.getElementById('price');
            if (!priceLabel || !priceInput) return;
            if (rateType === 'hourly') {
                priceLabel.innerText = 'Тариф (грн / год)';
                priceInput.placeholder = '150';
            } else if (rateType === 'fixed') {
                priceLabel.innerText = 'Вартість робіт (грн)';
                priceInput.placeholder = '500';
            } else {
                priceLabel.innerText = 'Орієнтовна вартість від (грн)';
                priceInput.placeholder = '100';
            }
        }
        window.onServiceRateTypeChanged = onServiceRateTypeChanged;

        function onServiceFormatChanged() {
            const formatSelect = document.getElementById('serviceFormat');
            const format = formatSelect ? formatSelect.value : 'visit';
            const isOnline = (format === 'online');
            const isOnlineCheckbox = document.getElementById('serviceIsOnlineOnly');
            if (isOnlineCheckbox) isOnlineCheckbox.checked = isOnline;
            toggleOnlineMode(isOnline);
        }
        window.onServiceFormatChanged = onServiceFormatChanged;

        function onCategoryChanged() {
            const catSelect = document.getElementById('categoryId');
            if (!catSelect) return;
            const selectedText = catSelect.options[catSelect.selectedIndex]?.text.toLowerCase() || '';
            if (catSelect.value === '8' || selectedText.includes('послуг')) {
                setListingType('service');
            } else if (currentListingType === 'service') {
                setListingType('product');
            }
        }
        window.onCategoryChanged = onCategoryChanged;

        // ==================== 2. КАРТОГРАФІЧНІ ФУНКЦІЇ (Leaflet + Nominatim) ====================
        async function reverseGeocodeCoords(lat, lng) {
            try {
                const res = await fetch(`https://nominatim.openstreetmap.org/reverse?format=json&lat=${lat}&lon=${lng}&accept-language=uk`);
                if (!res.ok) return;
                const data = await res.json();
                if (data && data.address) {
                    const a = data.address;
                    const city = a.city || a.town || a.village || a.county || "Кременчук";
                    const road = a.road || a.pedestrian || a.street || "";
                    const house = a.house_number || "";
                    const suburb = a.suburb || a.neighbourhood || "";
                    
                    let fullAddr = "";
                    if (road) {
                        fullAddr = road + (house ? ` ${house}` : '');
                        if (suburb && !road.includes(suburb)) fullAddr += ` (${suburb})`;
                        fullAddr += `, ${city}`;
                    } else {
                        fullAddr = suburb ? `${suburb}, ${city}` : city;
                    }

                    const locField = document.getElementById('location');
                    if (locField) locField.value = fullAddr;
                    const searchInp = document.getElementById('mapSearchInput');
                    if (searchInp) searchInp.value = fullAddr;
                }
            } catch(e) {
                console.warn("Geocoding failed:", e);
            }
        }

        window.searchAddressOnMap = async function() {
            const q = (document.getElementById('mapSearchInput')?.value || document.getElementById('location')?.value || '').trim();
            if (!q) {
                alert("Введіть назву вулиці та місто для пошуку");
                return;
            }
            try {
                const res = await fetch(`https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(q)}&limit=1&accept-language=uk`);
                const data = await res.json();
                if (data && data.length > 0) {
                    const lat = parseFloat(data[0].lat);
                    const lon = parseFloat(data[0].lon);
                    if (leafletMap) leafletMap.setView([lat, lon], 16);
                    if (leafletMarker) leafletMarker.setLatLng([lat, lon]);
                    else if (leafletMap) leafletMarker = L.marker([lat, lon], { draggable: true }).addTo(leafletMap);
                    
                    const cleanName = data[0].display_name.split(',').slice(0, 3).join(',').trim();
                    document.getElementById('location').value = cleanName || q;
                } else {
                    alert("Адресу не знайдено на карті. Спробуйте вказати місто або точнішу вулицю.");
                }
            } catch(e) {
                alert("Помилка пошуку адреси на карті");
            }
        };

        window.locateUserPosition = function() {
            if (!navigator.geolocation) {
                alert("Геолокація не підтримується браузером");
                return;
            }
            navigator.geolocation.getCurrentPosition(pos => {
                const lat = pos.coords.latitude;
                const lon = pos.coords.longitude;
                if (leafletMap) leafletMap.setView([lat, lon], 16);
                if (leafletMarker) leafletMarker.setLatLng([lat, lon]);
                reverseGeocodeCoords(lat, lon);
            }, () => {
                alert("Не вдалося отримати поточне місцезнаходження. Введіть адресу вручну.");
            });
        };

        function initMap() {
            try {
                if (typeof L !== 'undefined' && document.getElementById('map')) {
                    const startLat = 49.0629;
                    const startLng = 33.4042;
                    leafletMap = L.map('map').setView([startLat, startLng], 13);
                    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                        attribution: '&copy; OpenStreetMap'
                    }).addTo(leafletMap);

                    leafletMarker = L.marker([startLat, startLng], { draggable: true }).addTo(leafletMap);

                    leafletMap.on('click', function(e) {
                        const lat = e.latlng.lat;
                        const lng = e.latlng.lng;
                        leafletMarker.setLatLng([lat, lng]);
                        reverseGeocodeCoords(lat, lng);
                    });

                    leafletMarker.on('dragend', function(e) {
                        const pos = leafletMarker.getLatLng();
                        reverseGeocodeCoords(pos.lat, pos.lng);
                    });
                }
            } catch (err) {
                console.warn("Leaflet error:", err);
            }
        }

        // ==================== 3. СИСТЕМНІ ФУНКЦІЇ ТА АВТОРИЗАЦІЯ ====================
        const ICONS = {
            sun: '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="4"/><path d="M12 2v2"/><path d="M12 20v2"/><path d="m4.93 4.93 1.41 1.41"/><path d="m17.66 17.66 1.41 1.41"/><path d="M2 12h2"/><path d="M20 12h2"/><path d="m6.34 17.66-1.41 1.41"/><path d="m19.07 4.93-1.41 1.41"/></svg>',
            moon: '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z"/></svg>',
            bell: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:4px;"><path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9"/><path d="M10.3 21a1.94 1.94 0 0 0 3.4 0"/></svg>',
            user: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:4px;"><path d="M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>',
            chat: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg>',
            camera: '<svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="var(--primary-light)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M23 19a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4l2-3h6l2 3h4a2 2 0 0 1 2 2z"/><circle cx="12" cy="13" r="4"/></svg>',
            cancel: '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:4px;"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>'
        };

        function updateThemeButton(theme) {
            const iconEl = document.getElementById('themeIcon');
            if (iconEl) iconEl.innerHTML = (theme === 'dark') ? ICONS.sun : ICONS.moon;
        }
        updateThemeButton(localStorage.getItem('loopTheme') || 'light');

        const themeToggle = document.getElementById('themeToggleBtn');
        if (themeToggle) {
            themeToggle.addEventListener('click', () => {
                const currentTheme = document.documentElement.getAttribute('data-theme');
                const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
                document.documentElement.setAttribute('data-theme', newTheme);
                localStorage.setItem('loopTheme', newTheme);
                updateThemeButton(newTheme);
            });
        }

        function parseJwt(token) {
            try {
                return JSON.parse(decodeURIComponent(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')).split('').map(c => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2)).join('')));
            } catch (e) {
                return null;
            }
        }

        const token = localStorage.getItem('loopToken');
        const headerBtns = document.getElementById('headerButtons');
        const dynamicIcons = document.getElementById('dynamicIcons');

        if (token) {
            let userName = "Студент";
            try {
                const user = parseJwt(token);
                if (user && user["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"]) {
                    userName = user["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"];
                }
            } catch(e) {}

            if (dynamicIcons) {
                dynamicIcons.innerHTML = `
                    <button class="nav-pill" id="notifBtn" onclick="toggleNotifications()">
                        ${ICONS.bell} Сповіщення <span style="background: #ef4444; color: white; font-size: 0.65rem; padding: 1px 5px; border-radius: 10px; font-weight: bold; display:none;" id="globalUnreadBadge">0</span>
                    </button>
                `;
            }

            if (headerBtns) {
                headerBtns.innerHTML = `
                    <a href="/profile.html" class="nav-pill profile-highlight" title="Мій профіль">${ICONS.user} ${userName}</a>
                    <button class="btn-logout-header" onclick="showLogoutModal()">Вийти</button>
                    <a href="/index.html" class="btn-outline" style="text-decoration:none; display:inline-flex; align-items:center; padding: 8px 14px; border-radius: 8px; font-weight: 700; font-size: 0.9rem;">${ICONS.cancel} Скасувати</a>
                `;
            }
        }

        function toggleNotifications() {
            const dropdown = document.getElementById('notifDropdown');
            if (dropdown) {
                dropdown.classList.toggle('active');
                if (dropdown.classList.contains('active')) loadNotifications();
            }
        }

        async function loadNotifications() {
            if (!token) return;
            try {
                const res = await fetch('/api/notifications', { headers: { 'Authorization': `Bearer ${token}` } });
                if (res.ok) renderNotifications(await res.json());
            } catch (err) {}
        }

        function renderNotifications(notifs) {
            const listContainer = document.getElementById('notifList');
            const badge = document.getElementById('globalUnreadBadge');
            if (!listContainer) return;
            const unreadCount = notifs.filter(n => !n.isRead).length;
            if (badge) {
                badge.innerText = unreadCount;
                badge.style.display = unreadCount > 0 ? 'inline-block' : 'none';
            }
            if (notifs.length === 0) {
                listContainer.innerHTML = `<p style="padding: 15px; text-align: center; color: var(--text-muted); font-size: 0.85rem;">Нових сповіщень немає</p>`;
                return;
            }
            listContainer.innerHTML = notifs.map(n => `
                <a href="${n.link || '#'}" class="notif-item ${n.isRead ? '' : 'unread'}">
                    <span>${ICONS.chat}</span>
                    <div><b>${n.title}</b><p style="font-size: 0.8rem; color: var(--text-muted);">${n.message}</p></div>
                </a>
            `).join('');
        }

        function clearNotifications() {
            if (!token) return;
            fetch('/api/notifications/mark-read', { method: 'POST', headers: { 'Authorization': `Bearer ${token}` } })
                .then(() => loadNotifications());
        }

        window.addEventListener('click', function(e) {
            const dropdown = document.getElementById('notifDropdown');
            const btn = document.getElementById('notifBtn');
            if (dropdown && btn && !dropdown.contains(e.target) && !btn.contains(e.target)) {
                dropdown.classList.remove('active');
            }
        });

        function showLogoutModal() { document.getElementById('logoutModal')?.classList.add('active'); }
        function closeLogoutModal() { document.getElementById('logoutModal')?.classList.remove('active'); }
        function confirmLogoutAction() {
            localStorage.removeItem('loopToken');
            window.location.reload();
        }

        async function loadCategories() {
            try {
                const res = await fetch('/api/listings/categories');
                if (res.ok) {
                    const categories = await res.json();
                    const select = document.getElementById('categoryId');
                    if (select) {
                        select.innerHTML = '<option value="" disabled selected>Оберіть категорію...</option>';
                        categories.forEach(c => select.innerHTML += `<option value="${c.id}">${c.name}</option>`);
                        if (currentListingType === 'service') {
                            for (let opt of select.options) {
                                if (opt.value === '8' || (opt.text && opt.text.toLowerCase().includes('послуг'))) {
                                    select.value = opt.value;
                                    break;
                                }
                            }
                        }
                    }
                }
            } catch (e) {}
        }

        // ==================== 4. ІНІЦІАЛІЗАЦІЯ ПІСЛЯ ЗАВАНТАЖЕННЯ DOM ====================
        document.addEventListener('DOMContentLoaded', () => {
            const pBtn = document.getElementById('btnTypeProduct');
            const sBtn = document.getElementById('btnTypeService');
            if (pBtn) pBtn.addEventListener('click', (e) => { e.preventDefault(); setListingType('product'); });
            if (sBtn) sBtn.addEventListener('click', (e) => { e.preventDefault(); setListingType('service'); });

            loadCategories();
            if (token) {
                loadNotifications();
                setInterval(loadNotifications, 15000);
            }

            initMap();

            const urlParams = new URLSearchParams(window.location.search);
            if (urlParams.get('type') === 'service') {
                setListingType('service');
            }
        });

        // ==================== 5. ФОТО ТА НАДСИЛАННЯ ====================
        const imageFileInput = document.getElementById('imageFile');
        const previewGrid = document.getElementById('previewGrid');
        let selectedFiles = [];
        if (imageFileInput) {
            imageFileInput.addEventListener('change', function () {
                const newFiles = Array.from(this.files);
                for (let file of newFiles) { if (selectedFiles.length < 5) selectedFiles.push(file); }
                this.value = ''; renderPreviews();
            });
        }

        function renderPreviews() {
            if (!previewGrid) return;
            previewGrid.innerHTML = '';
            selectedFiles.forEach((file, index) => {
                const reader = new FileReader();
                reader.onload = e => {
                    previewGrid.innerHTML += `<div style="position: relative; display: inline-block;"><img src="${e.target.result}" class="preview-item"><span onclick="removePhoto(${index})" style="position: absolute; top: -5px; right: -5px; background: #ef4444; color: white; border-radius: 50%; width: 20px; height: 20px; text-align: center; cursor: pointer; font-size: 14px; line-height: 18px;">×</span></div>`;
                };
                reader.readAsDataURL(file);
            });
        }
        window.removePhoto = function (index) { selectedFiles.splice(index, 1); renderPreviews(); };

        const createFormEl = document.getElementById('createForm');
        if (createFormEl) {
            createFormEl.addEventListener('submit', async (e) => {
                e.preventDefault();
                const btn = document.querySelector('.btn-publish');
                const statusDiv = document.getElementById('statusMsg');
                if (btn) { btn.innerText = "Завантаження..."; btn.disabled = true; }
                if (statusDiv) statusDiv.innerText = "";

                try {
                    let finalImageUrls = [];
                    if (selectedFiles.length > 0) {
                        const formData = new FormData();
                        selectedFiles.forEach(f => formData.append('files', f));
                        const uploadRes = await fetch('/api/listings/upload-multiple', {
                            method: 'POST',
                            headers: token ? { 'Authorization': `Bearer ${token}` } : {},
                            body: formData
                        });
                        if (uploadRes.ok) finalImageUrls = (await uploadRes.json()).imageUrls;
                        else throw new Error("Помилка завантаження фотографій.");
                    }

                    let conditionValue = document.getElementById('condition')?.value || 'Вживане';
                    let shippingAvailable = document.getElementById('isShippingAvailable')?.checked || false;
                    let descVal = document.getElementById('description')?.value || '';
                    let locationVal = document.getElementById('location')?.value || 'Кременчук';

                    if (currentListingType === 'service') {
                        const kind = document.getElementById('serviceKind')?.value || 'movers';
                        const rateSelect = document.getElementById('serviceRateType');
                        const formatSelect = document.getElementById('serviceFormat');
                        const isOnline = document.getElementById('serviceIsOnlineOnly')?.checked || (formatSelect?.value === 'online');

                        const rateText = rateSelect?.options[rateSelect.selectedIndex]?.text.split('(')[0].trim() || 'Погодинно';
                        const formatText = isOnline ? 'Онлайн' : (formatSelect?.options[formatSelect.selectedIndex]?.text.split('(')[0].trim() || 'Виїзд');

                        conditionValue = `${rateText} • ${formatText}`;
                        shippingAvailable = !isOnline;

                        if (isOnline) {
                            locationVal = 'Онлайн (дистанційно)';
                        }

                        // Збираємо специфікації послуги
                        let specs = [];
                        if (kind === 'movers') {
                            const floor = document.getElementById('specFloor')?.value || 'За домовленістю';
                            const elevator = document.getElementById('specElevator')?.value || 'є';
                            const workers = document.getElementById('specWorkers')?.value || '2 вантажники';
                            const transport = document.getElementById('specTransport')?.value || 'Тільки вантажники';
                            specs.push(`Поверх: ${floor}`);
                            specs.push(`Вантажний ліфт: ${elevator}`);
                            specs.push(`Кількість працівників: ${workers}`);
                            specs.push(`Транспорт: ${transport}`);
                        } else if (kind === 'repairs') {
                            const premises = document.getElementById('specPremises')?.value || 'Квартира';
                            const tools = document.getElementById('specTools')?.value || 'З власним інструментом';
                            specs.push(`Тип приміщення: ${premises}`);
                            specs.push(`Інструменти: ${tools}`);
                        } else if (kind === 'online') {
                            const platform = document.getElementById('specOnlinePlatform')?.value || 'Zoom / Meet';
                            const duration = document.getElementById('specOnlineDuration')?.value || '60 хв';
                            specs.push(`Платформа: ${platform}`);
                            specs.push(`Тривалість: ${duration}`);
                        }

                        if (specs.length > 0) {
                            descVal += `\\n\\n📋 Параметри послуги:\\n• ` + specs.join('\\n• ');
                        }
                    }

                    const payload = {
                        title: document.getElementById('title')?.value || '',
                        description: descVal,
                        price: parseFloat(document.getElementById('price')?.value || '0'),
                        categoryId: parseInt(document.getElementById('categoryId')?.value || '8'),
                        imageUrls: finalImageUrls,
                        condition: conditionValue,
                        location: locationVal,
                        isShippingAvailable: shippingAvailable
                    };

                    const response = await fetch('/api/listings', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                            ...(token ? { 'Authorization': `Bearer ${token}` } : {})
                        },
                        body: JSON.stringify(payload)
                    });

                    if (response.ok) {
                        if (statusDiv) { statusDiv.innerText = "Опубліковано успішно!"; statusDiv.style.color = "green"; }
                        setTimeout(() => { window.location.href = '/index.html'; }, 1000);
                    } else {
                        const err = await response.json();
                        throw new Error(err.error || "Помилка при збереженні");
                    }
                } catch (err) {
                    if (statusDiv) { statusDiv.innerText = err.message || "Помилка при створенні"; statusDiv.style.color = "red"; }
                    if (btn) { btn.innerText = "Опублікувати оголошення"; btn.disabled = false; }
                }
            });
        }
    </script>'''

if old_script_block:
    content = content[:old_script_block.start()] + new_script + content[old_script_block.end():]
    print("Replaced script successfully in create.html")

with open(target_file, "w", encoding="utf-8") as f:
    f.write(content)
print("Saved create.html successfully")
