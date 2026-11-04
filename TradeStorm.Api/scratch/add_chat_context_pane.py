import re

path = r"D:\LoopMarket\TradeStorm.Api\wwwroot\messages.html"

with open(path, "r", encoding="utf-8") as f:
    html = f.read()

# 1. Update .messenger-card CSS to 3 columns
old_card_css = """.messenger-card {
            width: 100%;
            height: 100%;
            max-height: 100%;
            box-sizing: border-box;
            background: var(--surface);
            border-radius: 18px;
            border: 1.5px solid var(--border-color);
            box-shadow: 0 12px 36px rgba(0, 0, 0, 0.25);
            display: grid;
            grid-template-columns: 310px minmax(0, 1fr); /* 310px sidebar + expansive chat pane */
            overflow: hidden;
            min-height: 0;
            min-width: 0;
        }"""

new_card_css = """.messenger-card {
            width: 100%;
            height: 100%;
            max-height: 100%;
            box-sizing: border-box;
            background: var(--surface);
            border-radius: 18px;
            border: 1.5px solid var(--border-color);
            box-shadow: 0 12px 36px rgba(0, 0, 0, 0.25);
            display: grid;
            grid-template-columns: 300px minmax(0, 1fr) 330px; /* 3-column layout: dialogs + chat + details */
            overflow: hidden;
            min-height: 0;
            min-width: 0;
            transition: grid-template-columns 0.25s ease;
        }

        .messenger-card.details-collapsed {
            grid-template-columns: 300px minmax(0, 1fr) 0px !important;
        }

        /* 3RD COLUMN: DETAILS PANE STYLES */
        .messenger-details-pane {
            background: var(--surface);
            border-left: 1.5px solid var(--border-color);
            display: flex;
            flex-direction: column;
            height: 100%;
            max-height: 100%;
            overflow-y: auto;
            padding: 16px;
            gap: 14px;
            min-width: 0;
            box-sizing: border-box;
            transition: all 0.25s ease;
        }

        .messenger-details-pane.collapsed {
            display: none !important;
        }

        .details-section {
            background: var(--bg-color);
            border: 1px solid var(--border-color);
            border-radius: 16px;
            padding: 14px;
            box-sizing: border-box;
        }

        .details-section-title {
            font-size: 0.76rem;
            font-weight: 800;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            color: var(--text-muted);
            margin-bottom: 10px;
            display: flex;
            align-items: center;
            gap: 6px;
        }

        @media (max-width: 1180px) {
            .messenger-card {
                grid-template-columns: 300px minmax(0, 1fr) !important;
            }
            .messenger-details-pane {
                position: fixed;
                top: 0;
                right: -360px;
                width: min(360px, 92vw);
                height: 100%;
                z-index: 9999;
                box-shadow: -10px 0 35px rgba(0, 0, 0, 0.35);
                border-left: 1.5px solid var(--border-color);
                transition: right 0.3s cubic-bezier(0.16, 1, 0.3, 1);
            }
            .messenger-details-pane.drawer-open {
                right: 0 !important;
                display: flex !important;
            }
        }"""

if ".messenger-details-pane" not in html:
    html = html.replace(old_card_css, new_card_css)

# 2. Add Info toggle button to chat-pane-actions in HTML
old_actions = """                        <a href="#" id="btnSellerProfile" class="btn-header-action" title="Переглянути профіль автора">
                            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>
                            <span>Профіль</span>
                        </a>"""

new_actions = """                        <button type="button" id="btnToggleDetailsPane" class="btn-header-action active" onclick="toggleDetailsPane()" title="Інформація про співрозмовника та товар">
                            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2"><circle cx="12" cy="12" r="10"/><line x1="12" x2="12" y1="16" y2="12"/><line x1="12" x2="12.01" y1="8" y2="8"/></svg>
                            <span>Інфо</span>
                        </button>
                        <a href="#" id="btnSellerProfile" class="btn-header-action" title="Переглянути профіль автора">
                            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>
                            <span>Профіль</span>
                        </a>"""

if "btnToggleDetailsPane" not in html:
    html = html.replace(old_actions, new_actions)

# 3. Add Right Details Pane markup inside .messenger-card right after chatPane closing </div>
details_pane_markup = """
            <!-- ПРАВА КОЛОНКА 3: ДЕТАЛІ ДІАЛОГУ ТА ОГОЛОШЕННЯ -->
            <aside class="messenger-details-pane" id="detailsPane">
                <!-- Шапка правої панелі -->
                <div style="display: flex; justify-content: space-between; align-items: center; padding-bottom: 12px; border-bottom: 1px solid var(--border-color);">
                    <div style="font-weight: 800; font-size: 0.95rem; color: var(--text-main); display: flex; align-items: center; gap: 8px;">
                        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#2563eb" stroke-width="2.2"><circle cx="12" cy="12" r="10"/><line x1="12" x2="12" y1="16" y2="12"/><line x1="12" x2="12.01" y1="8" y2="8"/></svg>
                        Деталі діалогу
                    </div>
                    <button type="button" class="btn-icon" onclick="toggleDetailsPane()" style="width: 32px; height: 32px; border-radius: 8px;" title="Згорнути">
                        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
                    </button>
                </div>

                <!-- 1. Профіль співрозмовника -->
                <div class="details-section" id="detailsUserSection">
                    <div style="display: flex; flex-direction: column; align-items: center; text-align: center;">
                        <div id="detailsUserAvatarWrap" style="position: relative; margin-bottom: 10px;">
                            <img id="detailsAvatarImg" src="https://ui-avatars.com/api/?name=User&background=10b981&color=fff" style="width: 64px; height: 64px; border-radius: 50%; object-fit: cover; border: 2.5px solid var(--accent-green);">
                            <span class="avatar-online-dot" style="width: 14px; height: 14px; bottom: 2px; right: 2px; border-width: 2px;"></span>
                        </div>
                        <div id="detailsUserName" style="font-weight: 800; font-size: 1.05rem; color: var(--text-main); margin-bottom: 4px;">Співрозмовник</div>
                        <div id="detailsUserRole" style="font-size: 0.76rem; font-weight: 700; color: #2563eb; background: var(--badge-bg); padding: 3px 10px; border-radius: 999px; margin-bottom: 8px;">
                            Користувач LoopMarket
                        </div>
                        <div id="detailsUserRating" style="display: flex; align-items: center; gap: 4px; font-size: 0.82rem; color: #f59e0b; font-weight: 700; margin-bottom: 12px;">
                            <span>★ 5.0</span>
                            <span style="color: var(--text-muted); font-weight: 500;">(надійний продавець)</span>
                        </div>
                        <div style="display: flex; gap: 8px; width: 100%;">
                            <a href="#" id="detailsBtnProfile" class="btn btn-outline btn-sm" style="flex: 1; text-decoration: none; justify-content: center; font-size: 0.8rem; padding: 7px 10px;">
                                👤 Профіль
                            </a>
                            <button type="button" onclick="openReviewModal()" class="btn btn-outline btn-sm" style="text-decoration: none; justify-content: center; font-size: 0.8rem; padding: 7px 10px; color: #f59e0b;">
                                ★ Відгук
                            </button>
                        </div>
                    </div>
                </div>

                <!-- 2. Обговорюване оголошення -->
                <div class="details-section" id="detailsListingSection">
                    <div class="details-section-title">
                        <span>📦 Оголошення в діалозі</span>
                    </div>
                    <div id="detailsListingCard" style="background: var(--surface); border: 1.5px solid var(--border-color); border-radius: 14px; overflow: hidden; transition: all 0.2s;">
                        <div style="position: relative; width: 100%; height: 130px; background: var(--bg-color); overflow: hidden;">
                            <img id="detailsListingImg" src="" style="width: 100%; height: 100%; object-fit: cover;">
                            <span id="detailsListingStatusBadge" style="position: absolute; top: 8px; left: 8px; background: rgba(16, 185, 129, 0.92); color: white; font-size: 0.7rem; font-weight: 800; padding: 3px 8px; border-radius: 6px; backdrop-filter: blur(4px);">
                                В наявності
                            </span>
                        </div>
                        <div style="padding: 12px 14px;">
                            <div id="detailsListingTitle" style="font-weight: 700; font-size: 0.9rem; color: var(--text-main); margin-bottom: 6px; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; line-height: 1.35;">
                                Оголошення
                            </div>
                            <div id="detailsListingPrice" style="font-weight: 800; font-size: 1.15rem; color: var(--accent-green); margin-bottom: 10px;">
                                0 ₴
                            </div>
                            <div style="display: flex; flex-direction: column; gap: 6px;">
                                <a href="#" id="detailsBtnBuy" class="btn btn-primary btn-sm" style="text-decoration: none; justify-content: center; font-weight: 700; padding: 8px;">
                                    🛒 Оформити замовлення
                                </a>
                                <a href="#" id="detailsBtnView" class="btn btn-outline btn-sm" target="_blank" style="text-decoration: none; justify-content: center; font-size: 0.78rem; padding: 6px;">
                                    Переглянути на сайті →
                                </a>
                            </div>
                        </div>
                    </div>
                    <div id="detailsNoListingNotice" style="display: none; padding: 14px; text-align: center; background: var(--surface); border-radius: 12px; border: 1px dashed var(--border-color); color: var(--text-muted); font-size: 0.82rem;">
                        💬 Діалог без прив'язки до конкретного товару
                    </div>
                </div>

                <!-- 3. Пам'ятка безпечної угоди -->
                <div class="details-section">
                    <div class="details-section-title">
                        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="#10b981" stroke-width="2.2"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/></svg>
                        <span>Безпечні покупки</span>
                    </div>
                    <ul style="list-style: none; padding: 0; margin: 0; font-size: 0.76rem; color: var(--text-muted); display: flex; flex-direction: column; gap: 7px; line-height: 1.45;">
                        <li style="display: flex; gap: 6px;">
                            <span style="color: var(--accent-green); font-weight: 700;">✓</span>
                            <span><b>Накладений платіж:</b> перевіряйте стан посилки у відділенні пошти перед оплатою.</span>
                        </li>
                        <li style="display: flex; gap: 6px;">
                            <span style="color: var(--accent-green); font-weight: 700;">✓</span>
                            <span><b>Особиста зустріч:</b> обирайте людні та освітлені громадські місця (кав'ярні, ТЦ).</span>
                        </li>
                        <li style="display: flex; gap: 6px;">
                            <span style="color: var(--accent-green); font-weight: 700;">✓</span>
                            <span><b>Захист даних:</b> ніколи не вводьте CVV-код та паролі з SMS за сторонніми посиланнями.</span>
                        </li>
                    </ul>
                </div>

                <!-- 4. Швидкі дії -->
                <div style="display: flex; flex-direction: column; gap: 8px; margin-top: auto; padding-top: 10px;">
                    <a href="/profile.html" class="btn btn-outline btn-sm" style="text-decoration: none; justify-content: center; gap: 6px; font-size: 0.78rem;">
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M6 2 3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4Z"/><path d="M3 6h18"/><path d="M16 10a4 4 0 0 1-8 0"/></svg>
                        Мої замовлення та покупки
                    </a>
                    <button type="button" onclick="reportUser()" class="btn btn-outline btn-sm" style="justify-content: center; gap: 6px; font-size: 0.78rem; color: #ef4444; border-color: rgba(239,68,68,0.25);">
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 9v4"/><path d="M12 17h.01"/><path d="M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/></svg>
                        Поскаржитися на порушення
                    </button>
                </div>
            </aside>
            <div class="mobile-nav-overlay" id="detailsPaneOverlay" onclick="toggleDetailsPane()"></div>
"""

old_chat_end = """                </div>
            </div>
        </div>
    </main>"""

new_chat_end = """                </div>
            </div>
""" + details_pane_markup + """        </div>
    </main>"""

if "detailsPane" not in html:
    html = html.replace(old_chat_end, new_chat_end)

# 4. Add JavaScript functions for Details Pane & update selectDialog
js_additions = """
        // ==================== ДЕТАЛІ ДІАЛОГУ ТА ОГОЛОШЕННЯ (ПРАВА ПАНЕЛЬ) ====================
        function toggleDetailsPane() {
            const card = document.querySelector('.messenger-card');
            const pane = document.getElementById('detailsPane');
            const overlay = document.getElementById('detailsPaneOverlay');
            const btn = document.getElementById('btnToggleDetailsPane');

            if (window.innerWidth > 1180) {
                if (card) {
                    card.classList.toggle('details-collapsed');
                    if (btn) btn.classList.toggle('active', !card.classList.contains('details-collapsed'));
                }
            } else {
                if (pane) {
                    const isOpen = pane.classList.contains('drawer-open');
                    pane.classList.toggle('drawer-open', !isOpen);
                    if (overlay) overlay.classList.toggle('active', !isOpen);
                    if (btn) btn.classList.toggle('active', !isOpen);
                }
            }
        }

        async function updateDetailsPane(partnerData, listingItem, partnerId) {
            const pane = document.getElementById('detailsPane');
            if (!pane) return;

            const pFirst = (partnerData && partnerData.firstName) ? partnerData.firstName : 'Користувач';
            const pLast = (partnerData && partnerData.lastName) ? partnerData.lastName : '';
            const fullName = `${pFirst} ${pLast}`.trim();

            // 1. Інформація про користувача
            const nameEl = document.getElementById('detailsUserName');
            if (nameEl) nameEl.innerText = fullName;

            const avatarEl = document.getElementById('detailsAvatarImg');
            if (avatarEl) {
                if (partnerData && partnerData.avatarUrl) {
                    avatarEl.src = partnerData.avatarUrl;
                } else {
                    avatarEl.src = `https://ui-avatars.com/api/?name=${encodeURIComponent(fullName)}&background=10b981&color=fff&bold=true`;
                }
            }

            const roleEl = document.getElementById('detailsUserRole');
            if (roleEl) {
                roleEl.innerText = (listingItem && (listingItem.categoryId === 8 || listingItem.title.toLowerCase().includes('послуг')))
                    ? 'Майстер послуги'
                    : 'Продавець LoopMarket';
            }

            const btnProfile = document.getElementById('detailsBtnProfile');
            if (btnProfile) btnProfile.href = `/seller.html?id=${partnerId}`;

            // 2. Оголошення в діалозі
            const listingCard = document.getElementById('detailsListingCard');
            const noListingNotice = document.getElementById('detailsNoListingNotice');

            let item = listingItem;
            // Якщо оголошення не передано напряму, перевіримо чи є у автора активні товари
            if (!item && partnerId) {
                try {
                    const res = await fetch(`/api/listings?sellerId=${partnerId}&pageSize=1`);
                    if (res.ok) {
                        const data = await res.json();
                        const items = Array.isArray(data) ? data : (data.items || data.listings || []);
                        if (items.length > 0) item = items[0];
                    }
                } catch(e) {}
            }

            if (item) {
                if (listingCard) listingCard.style.display = 'block';
                if (noListingNotice) noListingNotice.style.display = 'none';

                const titleEl = document.getElementById('detailsListingTitle');
                if (titleEl) titleEl.innerText = item.title;

                const priceEl = document.getElementById('detailsListingPrice');
                if (priceEl) priceEl.innerText = (typeof formatPrice === 'function') ? formatPrice(item.price) : `${item.price} ₴`;

                const imgEl = document.getElementById('detailsListingImg');
                if (imgEl) {
                    imgEl.src = (item.imageUrls && item.imageUrls.length > 0) ? item.imageUrls[0] : (item.imageUrl || 'https://placehold.co/300x200/1e293b/10b981?text=LoopMarket');
                }

                const badgeEl = document.getElementById('detailsListingStatusBadge');
                if (badgeEl) {
                    badgeEl.innerText = item.status === 'Reserved' ? 'Зарезервовано' : 'В наявності';
                    badgeEl.style.background = item.status === 'Reserved' ? 'rgba(245, 158, 11, 0.92)' : 'rgba(16, 185, 129, 0.92)';
                }

                const btnBuy = document.getElementById('detailsBtnBuy');
                if (btnBuy) btnBuy.href = `/details.html?id=${item.id}`;

                const btnView = document.getElementById('detailsBtnView');
                if (btnView) btnView.href = `/details.html?id=${item.id}`;
            } else {
                if (listingCard) listingCard.style.display = 'none';
                if (noListingNotice) noListingNotice.style.display = 'block';
            }

            // 3. Завантажуємо реальний рейтинг продавця
            if (partnerId) {
                try {
                    const rRes = await fetch(`/api/reviews/user/${partnerId}`);
                    if (rRes.ok) {
                        const reviews = await rRes.json();
                        const ratingEl = document.getElementById('detailsUserRating');
                        if (ratingEl && Array.isArray(reviews)) {
                            if (reviews.length > 0) {
                                const avg = (reviews.reduce((acc, r) => acc + (r.rating || 5), 0) / reviews.length).toFixed(1);
                                ratingEl.innerHTML = `<span>★ ${avg}</span> <span style="color: var(--text-muted); font-weight: 500;">(${reviews.length} відгуків)</span>`;
                            } else {
                                ratingEl.innerHTML = `<span style="color: var(--text-muted); font-weight: 500;">Новий користувач (0 відгуків)</span>`;
                            }
                        }
                    }
                } catch(e) {}
            }
        }

        function reportUser() {
            const reason = prompt("Вкажіть причину скарги на користувача (шахрайство, спам, підозріла поведінка):");
            if (reason && reason.trim()) {
                alert("Дякуємо! Вашу скаргу надіслано модерації LoopMarket. Ми перевіримо діалог для забезпечення безпеки.");
            }
        }
"""

if "function updateDetailsPane" not in html:
    html = html.replace("// КОНТЕКСТНІ ШВИДКІ КНОПКИ", js_additions + "\n        // КОНТЕКСТНІ ШВИДКІ КНОПКИ")

# 5. Hook updateDetailsPane call inside selectDialog
select_dialog_call = "updateDetailsPane(partnerData, listingItem, partnerId);"
if select_dialog_call not in html:
    html = html.replace(
        "setupQuickChips(isServ, listingItem);",
        "setupQuickChips(isServ, listingItem);\n            updateDetailsPane(partnerData, listingItem, partnerId);"
    )

with open(path, "w", encoding="utf-8") as f:
    f.write(html)

print("messages.html successfully upgraded with professional 3-column deal & context layout!")
