import os
import re

edit_path = r"D:\LoopMarket\TradeStorm.Api\wwwroot\edit.html"

with open(edit_path, "r", encoding="utf-8") as f:
    html = f.read()

# Replace the script section from line 925 to 1380 with the upgraded, rock-solid script
new_script = """    <script>
        // ==================== 1. ТИП ОГОЛОШЕННЯ: ТОВАР / ПОСЛУГА ====================
        let isCurrentService = false;

        function setEditListingType(type, isUserClick = true) {
            isCurrentService = (type === 'service');
            const btnProd = document.getElementById('btnTypeProduct');
            const btnServ = document.getElementById('btnTypeService');
            const servBox = document.getElementById('serviceConfigBox');
            const prodBox = document.getElementById('productConditionBox');
            const heading = document.getElementById('pageHeading');
            const titleLabel = document.getElementById('titleLabel');
            const priceLabel = document.getElementById('priceLabel');
            const shippingLabel = document.getElementById('shippingLabel');
            const catSelect = document.getElementById('categoryId');

            if (isCurrentService) {
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

                if (heading) heading.innerText = 'Редагувати послугу';
                if (titleLabel) titleLabel.innerText = 'Назва послуги';
                if (shippingLabel) shippingLabel.innerText = 'Можливий терміновий виїзд / на локацію замовника';

                if (isUserClick && catSelect && catSelect.options) {
                    for (let opt of catSelect.options) {
                        if (opt.value === '8' || (opt.text && opt.text.toLowerCase().includes('послуг'))) {
                            catSelect.value = opt.value;
                            break;
                        }
                    }
                    onServiceRateTypeChanged();
                    onServiceFormatChanged();
                }
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

                if (heading) heading.innerText = 'Редагувати оголошення';
                if (titleLabel) titleLabel.innerText = 'Назва товару';
                if (priceLabel) priceLabel.innerText = 'Ціна (грн)';
                if (shippingLabel) shippingLabel.innerText = 'Відправка поштою';
            }
        }

        window.onCategoryChanged = function() {
            const catSelect = document.getElementById('categoryId');
            const isService = catSelect.value === '8' || (catSelect.options[catSelect.selectedIndex]?.text.toLowerCase().includes('послуг'));
            setEditListingType(isService ? 'service' : 'product', true);
        };

        window.onServiceRateTypeChanged = function() {
            const rateType = document.getElementById('serviceRateType').value;
            const priceLabel = document.getElementById('priceLabel');
            if (rateType === 'hourly') {
                priceLabel.innerText = 'Тариф (грн / год)';
            } else if (rateType === 'fixed') {
                priceLabel.innerText = 'Вартість послуги (грн)';
            } else {
                priceLabel.innerText = 'Орієнтовна вартість від (грн)';
            }
        };

        window.onServiceFormatChanged = function() {
            const format = document.getElementById('serviceFormat').value;
            if (format === 'online') {
                toggleOnlineMode(true);
            } else {
                toggleOnlineMode(false);
            }
        };

        // ==================== ІНТЕРАКТИВНА КАРТА ТА ГЕОЛОКАЦІЯ ====================
        let map = null;
        let mapMarker = null;

        function initEditMap(defaultAddress = '') {
            try {
                const mapEl = document.getElementById('map');
                if (!mapEl || typeof L === 'undefined') return;
                if (!map) {
                    map = L.map('map').setView([49.0629, 33.4042], 13);
                    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                        attribution: '&copy; OpenStreetMap'
                    }).addTo(map);

                    map.on('click', function (e) {
                        placeMapMarker(e.latlng.lat, e.latlng.lng, true);
                    });
                }

                if (defaultAddress && !defaultAddress.toLowerCase().includes('онлайн')) {
                    geocodeAndPlaceMarker(defaultAddress);
                }
                setTimeout(() => { if (map) map.invalidateSize(); }, 300);
            } catch (e) {
                console.warn('Map init skipped:', e);
            }
        }

        function placeMapMarker(lat, lng, doReverseGeocode = false) {
            if (!map) return;
            if (mapMarker) map.removeLayer(mapMarker);
            mapMarker = L.marker([lat, lng], { draggable: true }).addTo(map);

            mapMarker.on('dragend', function () {
                const pos = mapMarker.getLatLng();
                reverseGeocodeLocation(pos.lat, pos.lng);
            });

            if (doReverseGeocode) {
                reverseGeocodeLocation(lat, lng);
            }
        }

        async function reverseGeocodeLocation(lat, lng) {
            try {
                const res = await fetch(`https://nominatim.openstreetmap.org/reverse?format=json&lat=${lat}&lon=${lng}&accept-language=uk`);
                if (res.ok) {
                    const data = await res.json();
                    if (data.address) {
                        const city = data.address.city || data.address.town || data.address.village || "Кременчук";
                        const road = data.address.road || data.address.suburb || "";
                        const house = data.address.house_number || "";
                        const fullAddr = [road, house].filter(Boolean).join(' ') + (road ? `, ${city}` : city);
                        document.getElementById('location').value = fullAddr;
                        document.getElementById('mapSearchInput').value = fullAddr;
                        window.savedPhysicalLocation = fullAddr;
                    }
                }
            } catch (e) {}
        }

        async function geocodeAndPlaceMarker(addressText) {
            if (!map || !addressText) return;
            try {
                const res = await fetch(`https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(addressText)}&limit=1&accept-language=uk`);
                if (res.ok) {
                    const data = await res.json();
                    if (data && data.length > 0) {
                        const lat = parseFloat(data[0].lat);
                        const lon = parseFloat(data[0].lon);
                        map.setView([lat, lon], 15);
                        placeMapMarker(lat, lon, false);
                    }
                }
            } catch (e) {}
        }

        window.searchAddressOnMap = async function () {
            const q = document.getElementById('mapSearchInput').value.trim() || document.getElementById('location').value.trim();
            if (!q) {
                LoopToast.info("Введіть адресу для пошуку на карті");
                return;
            }
            try {
                const res = await fetch(`https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(q)}&limit=1&accept-language=uk`);
                if (res.ok) {
                    const data = await res.json();
                    if (data && data.length > 0) {
                        const lat = parseFloat(data[0].lat);
                        const lon = parseFloat(data[0].lon);
                        if (map) {
                            map.setView([lat, lon], 16);
                            placeMapMarker(lat, lon, false);
                        }
                        const formatted = data[0].display_name ? data[0].display_name.split(',').slice(0, 3).join(', ') : q;
                        document.getElementById('location').value = formatted;
                        window.savedPhysicalLocation = formatted;
                        LoopToast.success("Адресу знайдено та позначено на карті!");
                    } else {
                        LoopToast.warning("Адресу не знайдено на карті. Вкажіть місто або точнішу вулицю.");
                    }
                }
            } catch (e) {
                LoopToast.error("Помилка геокодування");
            }
        };

        window.locateUserPosition = function () {
            if (!navigator.geolocation) {
                LoopToast.warning("Геолокація не підтримується браузером.");
                return;
            }
            LoopToast.info("Визначення координат...");
            navigator.geolocation.getCurrentPosition(
                (pos) => {
                    const lat = pos.coords.latitude;
                    const lng = pos.coords.longitude;
                    if (map) {
                        map.setView([lat, lng], 16);
                        placeMapMarker(lat, lng, true);
                    }
                    LoopToast.success("Місцезнаходження визначено!");
                },
                () => {
                    LoopToast.warning("Не вдалося отримати доступ до геолокації.");
                },
                { timeout: 7000 }
            );
        };

        window.toggleOnlineMode = function (isOnline) {
            const mapWrap = document.getElementById('mapWrapper');
            const noticeBox = document.getElementById('onlineNoticeBox');
            const locInput = document.getElementById('location');
            const locLabel = document.getElementById('locationLabel');
            const chk = document.getElementById('serviceIsOnlineOnly');
            if (chk) chk.checked = !!isOnline;

            if (isOnline) {
                if (mapWrap) mapWrap.style.display = 'none';
                if (noticeBox) noticeBox.style.display = 'block';
                if (locInput) locInput.value = 'Онлайн / Дистанційно';
                if (locLabel) locLabel.innerText = 'Формат надання: Дистанційно (карта не потрібна)';
            } else {
                if (mapWrap) mapWrap.style.display = 'block';
                if (noticeBox) noticeBox.style.display = 'none';
                if (locInput && locInput.value === 'Онлайн / Дистанційно') {
                    locInput.value = window.savedPhysicalLocation || 'Кременчук';
                }
                if (locLabel) locLabel.innerText = '📍 Точна адреса на карті';
                setTimeout(() => { if (map) map.invalidateSize(); }, 250);
            }
        };

        const ICONS = {
            sun: '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="4"/><path d="M12 2v2"/><path d="M12 20v2"/><path d="m4.93 4.93 1.41 1.41"/><path d="m17.66 17.66 1.41 1.41"/><path d="M2 12h2"/><path d="M20 12h2"/><path d="m6.34 17.66-1.41 1.41"/><path d="m19.07 4.93-1.41 1.41"/></svg>',
            moon: '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z"/></svg>',
            bell: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:4px;"><path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9"/><path d="M10.3 21a1.94 1.94 0 0 0 3.4 0"/></svg>',
            user: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:4px;"><path d="M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>',
            cancel: '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:4px;"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>'
        };

        function parseJwt(tk) {
            try {
                const base64Url = tk.split('.')[1];
                const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
                const jsonPayload = decodeURIComponent(window.atob(base64).split('').map(function(c) {
                    return '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2);
                }).join(''));
                return JSON.parse(jsonPayload);
            } catch (e) {
                return {};
            }
        }

        function updateThemeButton(theme) {
            const iconEl = document.getElementById('themeIcon');
            if (iconEl) iconEl.innerHTML = (theme === 'dark') ? ICONS.sun : ICONS.moon;
        }
        updateThemeButton(localStorage.getItem('loopTheme') || 'light');

        document.getElementById('themeToggleBtn')?.addEventListener('click', () => {
            const currentTheme = document.documentElement.getAttribute('data-theme');
            const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
            document.documentElement.setAttribute('data-theme', newTheme);
            localStorage.setItem('loopTheme', newTheme);
            updateThemeButton(newTheme);
        });

        const listingId = new URLSearchParams(window.location.search).get('id');
        if (!listingId) {
            window.location.href = '/index.html';
        }

        const token = localStorage.getItem('loopToken');
        const headerBtns = document.getElementById('headerButtons');
        const dynamicIcons = document.getElementById('dynamicIcons');

        if (token) {
            const user = parseJwt(token);
            const userName = user ? (user["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"] || user["name"] || "Користувач") : "Користувач";

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
                    <a href="/details.html?id=${listingId}" class="btn-outline" style="text-decoration:none; display:inline-flex; align-items:center;">${ICONS.cancel} Скасувати</a>
                `;
            }
        } else {
            window.location.href = `/login.html?returnUrl=/edit.html?id=${encodeURIComponent(listingId)}`;
        }

        function toggleNotifications() {
            document.getElementById('notifDropdown')?.classList.toggle('active');
        }

        function showLogoutModal() { document.getElementById('logoutModal')?.classList.add('active'); }
        function closeLogoutModal() { document.getElementById('logoutModal')?.classList.remove('active'); }
        function confirmLogoutAction() {
            localStorage.removeItem('loopToken');
            window.location.reload();
        }

        async function loadCategories() {
            try {
                const res = await fetch('/api/categories');
                if (res.ok) {
                    const categories = await res.json();
                    const select = document.getElementById('categoryId');
                    if (select) {
                        select.innerHTML = categories.map(c => `<option value="${c.id}">${c.name}</option>`).join('');
                    }
                }
            } catch (e) {
                console.warn('Categories load error:', e);
            }
        }

        // ==================== 2. КЕРУВАННЯ ФОТОГРАФІЯМИ ====================
        let existingPhotos = [];
        let newSelectedFiles = [];
        const previewGrid = document.getElementById('previewGrid');
        const imageFileInput = document.getElementById('imageFile');

        if (imageFileInput) {
            imageFileInput.addEventListener('change', function () {
                const newFiles = Array.from(this.files);
                for (let file of newFiles) {
                    if (existingPhotos.length + newSelectedFiles.length < 5) {
                        newSelectedFiles.push(file);
                    }
                }
                this.value = '';
                renderAllPhotos();
            });
        }

        function renderAllPhotos() {
            if (!previewGrid) return;
            previewGrid.innerHTML = '';

            const badge = document.getElementById('photoCountBadge');
            const totalCount = existingPhotos.length + newSelectedFiles.length;
            if (badge) badge.innerText = `${totalCount} / 5`;

            // 1. Поточні збережені фото
            existingPhotos.forEach((url, index) => {
                const div = document.createElement('div');
                div.style.cssText = 'position: relative; display: inline-block; width: 100px; height: 100px;';
                div.innerHTML = `
                    <img src="${url}" class="preview-item" style="width: 100px; height: 100px; object-fit: cover; border-radius: 8px; border: 2px solid var(--border-color);" onerror="this.onerror=null; this.src='https://placehold.co/100x100?text=Loop';">
                    <span style="position: absolute; bottom: 4px; left: 4px; background: rgba(0,0,0,0.65); color: white; font-size: 0.65rem; padding: 1px 5px; border-radius: 4px;">Збережене</span>
                    <button type="button" onclick="removeExistingPhoto(${index})" title="Видалити це фото" style="position: absolute; top: -6px; right: -6px; background: #ef4444; color: white; border: none; border-radius: 50%; width: 22px; height: 22px; text-align: center; cursor: pointer; font-size: 14px; font-weight: bold; line-height: 20px; box-shadow: 0 2px 4px rgba(0,0,0,0.2);">×</button>
                `;
                previewGrid.appendChild(div);
            });

            // 2. Нові додані фото
            newSelectedFiles.forEach((file, index) => {
                const reader = new FileReader();
                reader.onload = e => {
                    const div = document.createElement('div');
                    div.style.cssText = 'position: relative; display: inline-block; width: 100px; height: 100px;';
                    div.innerHTML = `
                        <img src="${e.target.result}" class="preview-item" style="width: 100px; height: 100px; object-fit: cover; border-radius: 8px; border: 2px solid var(--primary-light);">
                        <span style="position: absolute; bottom: 4px; left: 4px; background: var(--primary-light); color: white; font-size: 0.65rem; padding: 1px 5px; border-radius: 4px; font-weight: bold;">Нове</span>
                        <button type="button" onclick="removeNewPhoto(${index})" title="Видалити це нове фото" style="position: absolute; top: -6px; right: -6px; background: #ef4444; color: white; border: none; border-radius: 50%; width: 22px; height: 22px; text-align: center; cursor: pointer; font-size: 14px; font-weight: bold; line-height: 20px; box-shadow: 0 2px 4px rgba(0,0,0,0.2);">×</button>
                    `;
                    previewGrid.appendChild(div);
                };
                reader.readAsDataURL(file);
            });

            if (totalCount === 0) {
                previewGrid.innerHTML = '<p style="grid-column: 1/-1; color: var(--text-muted); font-size: 0.85rem; padding: 10px 0;">Фотографій ще немає. Натисніть кнопку нижче, щоб додати.</p>';
            }
        }

        window.removeExistingPhoto = function(index) {
            existingPhotos.splice(index, 1);
            renderAllPhotos();
            LoopToast.info("Фото видалено зі списку збережених");
        };

        window.removeNewPhoto = function(index) {
            newSelectedFiles.splice(index, 1);
            renderAllPhotos();
        };

        // ==================== 3. ПОВНЕ АВТОМАТИЧНЕ ЗАВАНТАЖЕННЯ ДАНИХ ====================
        async function loadListingData() {
            try {
                const res = await fetch(`/api/listings/${listingId}`);
                if (!res.ok) throw new Error("Оголошення не знайдено на сервері");
                const item = await res.json();
                window.currentListing = item;
                window.savedPhysicalLocation = item.location && !item.location.toLowerCase().includes('онлайн') ? item.location : 'Кременчук';

                // 1. Заповнюємо базові текстові поля
                document.getElementById('title').value = item.title || "";
                document.getElementById('price').value = item.price || 0;
                document.getElementById('description').value = item.description || "";
                document.getElementById('location').value = item.location || "Кременчук";
                document.getElementById('mapSearchInput').value = item.location && !item.location.toLowerCase().includes('онлайн') ? item.location : "";

                // 2. Встановлюємо категорію
                const catSelect = document.getElementById('categoryId');
                if (catSelect && item.categoryId) {
                    catSelect.value = String(item.categoryId);
                }

                // 3. Стан та доставка для товару
                const condEl = document.getElementById('condition');
                if (condEl && (item.condition === 'Нове' || item.condition === 'Вживане')) {
                    condEl.value = item.condition;
                }
                document.getElementById('isShippingAvailable').checked = !!item.isShippingAvailable;

                // 4. Завантажуємо фотографії
                const rawImgs = item.imageUrls || (item.imageUrl ? item.imageUrl.split(';') : []);
                existingPhotos = rawImgs.map(u => u.trim()).filter(Boolean);
                renderAllPhotos();

                // 5. Визначення послуги чи товару
                const isService = item.categoryId === 8 ||
                    (item.categoryName && item.categoryName.toLowerCase().includes('послуг')) ||
                    (item.title && item.title.toLowerCase().includes('послуг')) ||
                    (item.condition && (item.condition.includes('Послуга') || item.condition.includes('•') || item.condition.includes('Погодинно') || item.condition.includes('Фіксован')));

                setEditListingType(isService ? 'service' : 'product', false);

                if (isService) {
                    const cond = item.condition || '';
                    if (cond.includes('Фіксован')) {
                        document.getElementById('serviceRateType').value = 'fixed';
                    } else if (cond.includes('домовленістю')) {
                        document.getElementById('serviceRateType').value = 'negotiated';
                    } else {
                        document.getElementById('serviceRateType').value = 'hourly';
                    }

                    const isOnline = cond.includes('Онлайн') || (item.location && item.location.toLowerCase().includes('онлайн'));
                    if (isOnline) {
                        document.getElementById('serviceFormat').value = 'online';
                        toggleOnlineMode(true);
                    } else if (cond.includes('майстерн') || cond.includes('офіс')) {
                        document.getElementById('serviceFormat').value = 'on_site';
                        toggleOnlineMode(false);
                    } else {
                        document.getElementById('serviceFormat').value = 'visit';
                        toggleOnlineMode(false);
                    }

                    onServiceRateTypeChanged();
                } else {
                    toggleOnlineMode(false);
                }

                // 6. Ініціалізуємо карту та ставимо мітку на поточну адресу
                if (!isService || document.getElementById('serviceFormat').value !== 'online') {
                    initEditMap(item.location || 'Кременчук');
                }
            } catch (e) {
                console.error("Помилка завантаження оголошення:", e);
                LoopToast.error(e.message || "Помилка завантаження оголошення");
                setTimeout(() => { window.location.href = '/index.html'; }, 1500);
            }
        }

        async function initPage() {
            await loadCategories();
            await loadListingData();
        }
        initPage();

        // ==================== 4. ЗБЕРЕЖЕННЯ ЗМІН В 1 КЛІК ====================
        document.getElementById('editForm').addEventListener('submit', async (e) => {
            e.preventDefault();
            const btn = document.querySelector('.btn-publish');
            const statusDiv = document.getElementById('statusMsg');
            btn.innerText = "Збереження змін...";
            btn.disabled = true;
            statusDiv.innerText = "";

            try {
                let newlyUploadedUrls = [];
                if (newSelectedFiles.length > 0) {
                    const formData = new FormData();
                    newSelectedFiles.forEach(f => formData.append('files', f));
                    const uploadRes = await fetch('/api/listings/upload-multiple', {
                        method: 'POST',
                        headers: { 'Authorization': `Bearer ${token}` },
                        body: formData
                    });
                    if (uploadRes.ok) {
                        newlyUploadedUrls = (await uploadRes.json()).imageUrls;
                    } else {
                        throw new Error("Помилка завантаження нових фотографій.");
                    }
                }

                let finalImageUrls = [...existingPhotos, ...newlyUploadedUrls];
                if (finalImageUrls.length === 0) {
                    finalImageUrls = [isCurrentService ? 'https://placehold.co/600x400/e2e8f0/64748b?text=Послуга' : 'https://placehold.co/600x400/e2e8f0/64748b?text=Loop+Market'];
                }

                let conditionValue = document.getElementById('condition').value;
                let shippingAvailable = document.getElementById('isShippingAvailable').checked;

                if (isCurrentService) {
                    const rateSelect = document.getElementById('serviceRateType');
                    const formatSelect = document.getElementById('serviceFormat');
                    const rateText = rateSelect.options[rateSelect.selectedIndex]?.text.split('(')[0].trim() || 'Погодинно';
                    const formatText = formatSelect.options[formatSelect.selectedIndex]?.text.split('(')[0].trim() || 'Онлайн';
                    conditionValue = `${rateText} • ${formatText}`;
                    shippingAvailable = (formatSelect.value !== 'online');
                }

                const payload = {
                    title: document.getElementById('title').value.trim(),
                    description: document.getElementById('description').value.trim(),
                    price: parseFloat(document.getElementById('price').value) || 0,
                    categoryId: parseInt(document.getElementById('categoryId').value),
                    imageUrls: finalImageUrls,
                    condition: conditionValue,
                    location: document.getElementById('location').value.trim() || 'Кременчук',
                    isShippingAvailable: shippingAvailable
                };

                const response = await fetch(`/api/listings/${listingId}`, {
                    method: 'PUT',
                    headers: {
                        'Content-Type': 'application/json',
                        'Authorization': `Bearer ${token}`
                    },
                    body: JSON.stringify(payload)
                });

                if (response.ok) {
                    LoopToast.success("Зміни успішно збережено!");
                    statusDiv.innerText = "Оголошення успішно збережено!";
                    statusDiv.style.color = "var(--primary-light)";
                    setTimeout(() => { window.location.href = `/details.html?id=${listingId}`; }, 700);
                } else {
                    const errorData = await response.json().catch(() => ({}));
                    throw new Error(errorData.error || "Помилка сервера при оновленні");
                }
            } catch (err) {
                LoopToast.error(err.message || "Помилка збереження");
                statusDiv.innerText = err.message;
                statusDiv.style.color = "#ef4444";
                btn.innerText = "Зберегти зміни";
                btn.disabled = false;
            }
        });
    </script>
"""

# Find where <script> starts around line 925 and ends before <script src="/js/responsive-nav.js"
pattern = r"<script>\s*// ==================== 1\.[\s\S]*?<\/script>\s*<script>\s*// Header Auth Auto-fill[\s\S]*?<\/script>"
if re.search(pattern, html):
    html = re.sub(pattern, new_script, html)
    with open(edit_path, "w", encoding="utf-8") as f:
        f.write(html)
    print("SUCCESS: Upgraded edit.html script cleanly!")
else:
    print("Pattern not matched directly, trying broader script replacement...")
    start_marker = "    <script>\n        // ==================== 1."
    end_marker = '    <script src="/js/responsive-nav.js"'
    if start_marker in html and end_marker in html:
        before = html[:html.find(start_marker)]
        after = html[html.find(end_marker):]
        html = before + new_script + "\n" + after
        with open(edit_path, "w", encoding="utf-8") as f:
            f.write(html)
        print("SUCCESS: Upgraded edit.html via markers!")
    else:
        print("Failed to locate script block in edit.html")
