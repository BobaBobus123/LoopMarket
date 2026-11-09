
    // ==========================================
    // 🚪 НАДІЙНИЙ ПОВНИЙ ВИХІД З АКАУНТА (БЕЗ ЗАЛИШКУ ПРОФІЛЮ)
    // ==========================================
    window.performLoopLogout = async function(redirectPath = '/login.html') {
        document.cookie = "loop_admin_session=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT";
        const currentToken = localStorage.getItem('loopToken') || localStorage.getItem('token');
        const refreshToken = localStorage.getItem('loopRefreshToken');

        if (currentToken && refreshToken) {
            try {
                await fetch('/api/users/revoke', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'Authorization': 'Bearer ' + currentToken
                    },
                    body: JSON.stringify({ refreshToken })
                });
            } catch (e) {}
        }

        // Повне видалення ВСІХ ключів авторизації
        localStorage.removeItem('loopToken');
        localStorage.removeItem('token');
        localStorage.removeItem('loopRefreshToken');
        localStorage.removeItem('loopUserRole');
        localStorage.removeItem('user');
        sessionStorage.clear();

        // Перенаправлення з чистим станом
        window.location.href = redirectPath;
    };
/**
 * LoopMarket Responsive Navigation & Mobile Experience System
 * Supports all viewports: Mobile S/M/L, Tablet, Laptop, Desktop, 2K/4K
 */

(function () {
    'use strict';

    function initResponsiveNav() {
        const header = document.querySelector('header');
        if (!header) return;

        const currentPath = window.location.pathname.toLowerCase();
        const token = localStorage.getItem('token') || localStorage.getItem('loopToken');
        const userJson = localStorage.getItem('loopUser') || localStorage.getItem('user');
        let user = null;
        try {
            if (userJson) user = JSON.parse(userJson);
        } catch (e) { }

        let isAdmin = localStorage.getItem('loopUserRole') === 'Admin';
        if (!isAdmin && token) {
            try {
                const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
                if (payload.role === 'Admin' || payload["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] === 'Admin') {
                    isAdmin = true;
                }
            } catch(e) {}
        }

        // 1. Ensure Header Content has Mobile Hamburger
        let headerContent = header.querySelector('.header-content');
        if (!headerContent) {
            headerContent = header;
        }

        let navActions = header.querySelector('.nav-actions') || header.querySelector('.header-right');

        if (navActions && isAdmin && !document.getElementById('adminPanelNavBtn')) {
            const adminLink = document.createElement('a');
            adminLink.id = 'adminPanelNavBtn';
            adminLink.href = '/admin.html';
            adminLink.style.cssText = 'background: rgba(37,99,235,0.12); color: #2563eb; border: 1.5px solid rgba(37,99,235,0.35); padding: 6px 12px; border-radius: 10px; font-weight: 700; font-size: 0.85rem; text-decoration: none; display: inline-flex; align-items: center; gap: 6px; transition: 0.2s;';
            adminLink.innerHTML = '🛡️ Панель оператора';
            navActions.prepend(adminLink);
        }

        const existingMenuBtn = document.getElementById('mobileMenuBtn') || document.querySelector('.mobile-menu-btn');
        if (!existingMenuBtn && navActions) {
            const menuBtn = document.createElement('button');
            menuBtn.id = 'mobileMenuBtn';
            menuBtn.type = 'button';
            menuBtn.className = 'btn-mobile-menu-toggle mobile-menu-btn';
            menuBtn.setAttribute('aria-label', 'Меню навігації');
            menuBtn.innerHTML = `
                <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.3" stroke-linecap="round" stroke-linejoin="round">
                    <line x1="4" y1="6" x2="20" y2="6"></line>
                    <line x1="4" y1="12" x2="20" y2="12"></line>
                    <line x1="4" y1="18" x2="20" y2="18"></line>
                </svg>
            `;
            navActions.appendChild(menuBtn);
        }

        // 2. Build Slide-out Mobile Navigation Drawer & Overlay
        if (!document.getElementById('mobileNavDrawer')) {
            const overlay = document.createElement('div');
            overlay.id = 'mobileNavOverlay';
            overlay.className = 'mobile-nav-overlay';
            overlay.addEventListener('click', closeMobileDrawer);
            document.body.appendChild(overlay);

            const drawer = document.createElement('aside');
            drawer.id = 'mobileNavDrawer';
            drawer.className = 'mobile-nav-drawer';

            const userName = user ? `${user.firstName || ''} ${user.lastName || ''}`.trim() || 'Користувач' : 'Гість';
            const userEmail = user?.email || 'Авторизуйтесь для покупок';
            const savedLocalAvatar = localStorage.getItem('loopUserAvatar') || (user?.id ? localStorage.getItem(`userAvatar_${user.id}`) : null);
            const userAvatar = savedLocalAvatar || user?.avatarUrl || `https://ui-avatars.com/api/?name=${encodeURIComponent(userName)}&background=10b981&color=fff&bold=true`;

            drawer.innerHTML = `
                <div class="mobile-drawer-header">
                    <a href="/index.html" class="logo" style="text-decoration: none;">
                        <div class="logo-circle" style="width: 34px; height: 34px; font-size: 1rem;">L</div>
                        <span style="font-size: 1.2rem; font-weight: 800; color: var(--text-primary);">Loop<span style="color: #10b981;">Market</span></span>
                    </a>
                    <button class="mobile-drawer-close" id="mobileDrawerCloseBtn" aria-label="Закрити меню">
                        <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
                    </button>
                </div>

                <div class="mobile-user-card" style="display: flex; align-items: center; gap: 12px; padding: 12px; background: var(--bg-subtle); border-radius: 14px; margin-bottom: 16px;">
                    <img src="${userAvatar}" alt="${userName}" style="width: 44px; height: 44px; border-radius: 50%; object-fit: cover; border: 2px solid var(--brand-emerald);">
                    <div style="flex-grow: 1; overflow: hidden;">
                        <div style="font-weight: 700; font-size: 0.95rem; color: var(--text-primary); white-space: nowrap; overflow: hidden; text-overflow: ellipsis;">${userName}</div>
                        <div style="font-size: 0.78rem; color: var(--text-muted); white-space: nowrap; overflow: hidden; text-overflow: ellipsis;">${userEmail}</div>
                    </div>
                </div>

                <div class="mobile-drawer-links">
                    <a href="/index.html" class="mobile-drawer-link ${currentPath === '/index.html' || currentPath === '/' ? 'active' : ''}">
                        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="m3 9 9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/></svg>
                        <span>Головна каталог</span>
                    </a>
                    <a href="/index.html?type=service" class="mobile-drawer-link">
                        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z"/></svg>
                        <span>Послуги та сервіси</span>
                    </a>
                    <a href="/messages.html" class="mobile-drawer-link ${currentPath.includes('messages') ? 'active' : ''}">
                        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg>
                        <span>Повідомлення та чат</span>
                    </a>
                    <a href="/profile.html" class="mobile-drawer-link ${currentPath.includes('profile') ? 'active' : ''}">
                        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>
                        <span>Особистий кабінет</span>
                    </a>
                    <a href="/create.html" class="mobile-drawer-link ${currentPath.includes('create') ? 'active' : ''}" style="color: var(--brand-emerald); font-weight: 700;">
                        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M12 5v14M5 12h14"/></svg>
                        <span>+ Подати оголошення</span>
                    </a>
                    <div style="height: 1px; background: var(--border-subtle); margin: 6px 0;"></div>
                    ${isAdmin ? `
                    <a href="/admin.html" class="mobile-drawer-link ${currentPath.includes('admin') ? 'active' : ''}" style="color: #2563eb; font-weight: 800; background: rgba(37,99,235,0.06); border-radius: 10px;">
                        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/></svg>
                        <span>🛡️ Панель оператора</span>
                    </a>
                    ` : ''}
                    <a href="/safety.html" class="mobile-drawer-link ${currentPath.includes('safety') ? 'active' : ''}">
                        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/></svg>
                        <span>Безпека угод</span>
                    </a>
                    <a href="/about.html" class="mobile-drawer-link ${currentPath.includes('about') ? 'active' : ''}">
                        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="12" x2="12" y1="16" y2="12"/><line x1="12" x2="12.01" y1="8" y2="8"/></svg>
                        <span>Про платформу</span>
                    </a>
                    ${role === 'Admin' ? `
                    <a href="/docs.html" class="mobile-drawer-link ${currentPath.includes('docs') ? 'active' : ''}">
                        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20"/><path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z"/></svg>
                        <span>📚 API Документація</span>
                    </a>
                    ` : ''}
                </div>

                <div style="margin-top: auto; padding-top: 16px; border-top: 1px solid var(--border-subtle); display: flex; flex-direction: column; gap: 10px;">
                    <div style="display: flex; justify-content: space-between; align-items: center; padding: 4px 6px;">
                        <span style="font-size: 0.88rem; font-weight: 600; color: var(--text-secondary);">Тема оформлення</span>
                        <button class="btn btn-sm btn-outline" onclick="window.toggleTheme ? window.toggleTheme() : null" style="gap: 6px;">
                            <span>🌓 Змінити</span>
                        </button>
                    </div>

                    ${token ? `
                        <button class="btn btn-danger" onclick="window.performLoopLogout('/login.html')" style="width: 100%; justify-content: center;">🚪 Вийти з акаунта</button>
                    ` : `
                        <a href="/login.html" class="btn btn-primary" style="width: 100%; justify-content: center; text-decoration: none;">
                            🔑 Увійти / Зареєструватися
                        </a>
                    `}
                </div>
            `;
            document.body.appendChild(drawer);

            document.getElementById('mobileDrawerCloseBtn')?.addEventListener('click', closeMobileDrawer);
        }

        // 3. Inject App-like Mobile Bottom Navigation Bar (< 768px)
        // НА СТОРІНЦІ ЧАТУ (MESSAGES) НІКОЛИ НЕ СТВОРЮЄМО НИЖНЮ ПАНЕЛЬ, ЩОБ ВОНА НЕ ПЕРЕКРИВАЛА ВВЕДЕННЯ ТЕКСТУ
        const isChatPage = currentPath.includes('messages') || 
                           window.location.pathname.toLowerCase().includes('messages') ||
                           document.body.classList.contains('messages-page') ||
                           document.body.classList.contains('in-chat-conversation');

        if (isChatPage) {
            const purge = () => {
                document.querySelectorAll('#mobileBottomBar, .mobile-bottom-bar, .mobile-bottom-app-bar').forEach(el => el.remove());
            };
            purge();
            setTimeout(purge, 100);
            setTimeout(purge, 500);
            return; // На сторінці повідомлень ніколи не створюємо нижню панель
        } else if (!document.getElementById('mobileBottomBar') && !document.getElementById('mobileBottomAppBar')) {
            const bottomBar = document.createElement('nav');
            bottomBar.id = 'mobileBottomBar';
            bottomBar.className = 'mobile-bottom-bar';
            bottomBar.setAttribute('aria-label', 'Мобільна навігація');

            const isHome = currentPath === '/index.html' || currentPath === '/' || currentPath === '';
            const isServices = window.location.hash.includes('services') || window.location.search.includes('service');
            const isChat = currentPath.includes('messages');
            const isProfile = currentPath.includes('profile');
            const isCreate = currentPath.includes('create');

            bottomBar.innerHTML = `
                <div class="mobile-bottom-bar-inner">
                    <a href="/index.html" class="bottom-bar-item ${isHome && !isServices ? 'active' : ''}">
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2"><path d="m3 9 9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/></svg>
                        <span>Каталог</span>
                    </a>
                    <a href="/index.html?type=service" class="bottom-bar-item ${isServices ? 'active' : ''}">
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2"><path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z"/></svg>
                        <span>Послуги</span>
                    </a>
                    <a href="/create.html" class="bottom-bar-item create-fab ${isCreate ? 'active' : ''}">
                        <div class="fab-circle">
                            <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.8"><path d="M12 5v14M5 12h14"/></svg>
                        </div>
                        <span>Додати</span>
                    </a>
                    <a href="/messages.html" class="bottom-bar-item ${isChat ? 'active' : ''}">
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg>
                        <span>Чат</span>
                    </a>
                    <a href="${token ? '/profile.html' : '/login.html'}" class="bottom-bar-item ${isProfile ? 'active' : ''}">
                        ${(typeof savedLocalAvatar !== 'undefined' && savedLocalAvatar) || user?.avatarUrl ? `<img src="${(typeof savedLocalAvatar !== 'undefined' && savedLocalAvatar) || user?.avatarUrl}" style="width: 22px; height: 22px; border-radius: 50%; object-fit: cover; border: 1.5px solid #10b981; margin-bottom: 2px; display: block;" alt="Avatar">` : `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2"><path d="M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>`}
                        <span>Профіль</span>
                    </a>
                </div>
            `;
            document.body.appendChild(bottomBar);

            if (document.body.classList.contains('has-sticky-bar')) {
                bottomBar.style.display = 'none';
            }
        }

        // 4. Mobile Filters Collapsible Toggle for index.html
        
        // Sync user avatar with top header navigation icon
        function syncNavProfileAvatar(avatarUrl) {
            const navProf = document.getElementById('navProfileLink');
            if (!navProf) return;
            const existingImg = navProf.querySelector('.nav-profile-avatar');
            if (avatarUrl && typeof avatarUrl === 'string' && avatarUrl.trim()) {
                if (existingImg) {
                    existingImg.src = avatarUrl.trim();
                } else {
                    const img = document.createElement('img');
                    img.className = 'nav-profile-avatar';
                    img.src = avatarUrl.trim();
                    img.style.cssText = 'width: 20px; height: 20px; border-radius: 50%; object-fit: cover; border: 1.5px solid #10b981; margin-right: 4px; vertical-align: middle; display: inline-block;';
                    const svg = navProf.querySelector('svg');
                    if (svg) svg.replaceWith(img);
                    else navProf.prepend(img);
                }
            } else if (existingImg) {
                existingImg.outerHTML = `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2"><path d="M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>`;
            }
        }

        const initialAv = localStorage.getItem('loopUserAvatar') || (user?.id ? localStorage.getItem(`userAvatar_${user.id}`) : null) || user?.avatarUrl;
        if (initialAv) {
            syncNavProfileAvatar(initialAv);
        }

        if (!window._loopAvatarListenerAttached) {
            window._loopAvatarListenerAttached = true;
            window.addEventListener('loop:avatar-changed', (e) => {
                const newAv = e.detail && e.detail.avatarUrl;
                syncNavProfileAvatar(newAv);
                const drImg = document.querySelector('.mobile-user-card img');
                if (drImg && newAv) drImg.src = newAv;
                const bImg = document.querySelector('#mobileBottomBar a[href*="profile"] img, #mobileBottomAppBar a[href*="profile"] img');
                if (bImg && newAv) bImg.src = newAv;
            });
        }

        setupMobileFilters();
    }

    let lastDrawerToggleTimestamp = 0;

    function toggleMobileDrawer(e) {
        if (e) {
            if (e.preventDefault) e.preventDefault();
            if (e.stopPropagation) e.stopPropagation();
        }
        const now = Date.now();
        if (now - lastDrawerToggleTimestamp < 260) {
            return;
        }
        lastDrawerToggleTimestamp = now;

        let drawer = document.getElementById('mobileNavDrawer');
        let overlay = document.getElementById('mobileNavOverlay') || document.getElementById('mobileDrawerBackdrop');
        if (!drawer) {
            initResponsiveNav();
            drawer = document.getElementById('mobileNavDrawer');
            overlay = document.getElementById('mobileNavOverlay') || document.getElementById('mobileDrawerBackdrop');
        }
        if (!drawer) return;

        const isOpen = drawer.classList.contains('active');
        if (isOpen) {
            closeMobileDrawer();
        } else {
            openMobileDrawer();
        }
    }

    function openMobileDrawer() {
        let drawer = document.getElementById('mobileNavDrawer');
        let overlay = document.getElementById('mobileNavOverlay') || document.getElementById('mobileDrawerBackdrop');
        if (!drawer) {
            initResponsiveNav();
            drawer = document.getElementById('mobileNavDrawer');
            overlay = document.getElementById('mobileNavOverlay') || document.getElementById('mobileDrawerBackdrop');
        }
        if (!drawer) return;

        if (typeof window.renderDrawerUserInfo === 'function') {
            try { window.renderDrawerUserInfo(); } catch(err) {}
        }

        drawer.classList.add('active');
        if (overlay) overlay.classList.add('active');
        document.body.style.overflow = 'hidden';
    }

    function closeMobileDrawer(e) {
        if (e && e.stopPropagation) e.stopPropagation();
        const drawer = document.getElementById('mobileNavDrawer');
        const overlay = document.getElementById('mobileNavOverlay') || document.getElementById('mobileDrawerBackdrop');
        if (drawer) drawer.classList.remove('active');
        if (overlay) overlay.classList.remove('active');
        document.body.style.overflow = '';
    }

    // Expose drawer controls globally
    window.toggleMobileDrawer = toggleMobileDrawer;
    window.openMobileDrawer = openMobileDrawer;
    window.closeMobileDrawer = closeMobileDrawer;
    window.toggleMobileNavDrawer = toggleMobileDrawer;
    window.closeMobileNavDrawer = closeMobileDrawer;

    // Document-level event delegation for all drawer triggers across all pages
    if (!window._loopNavDelegationAttached) {
        window._loopNavDelegationAttached = true;
        const handleDrawerAction = function(e) {
            const toggleBtn = e.target.closest('#mobileMenuBtn, .mobile-menu-btn, .btn-mobile-menu-toggle');
            if (toggleBtn) {
                toggleMobileDrawer(e);
                return;
            }

            const closeBtn = e.target.closest('.mobile-drawer-close, .btn-drawer-close, #mobileDrawerCloseBtn');
            if (closeBtn) {
                closeMobileDrawer(e);
                return;
            }

            const backdrop = e.target.closest('#mobileNavOverlay, #mobileDrawerBackdrop, .mobile-nav-overlay, .mobile-drawer-backdrop');
            if (backdrop) {
                closeMobileDrawer(e);
                return;
            }

            const drawerLink = e.target.closest('#mobileNavDrawer a, .mobile-nav-drawer a');
            if (drawerLink) {
                closeMobileDrawer(e);
            }
        };

        document.addEventListener('click', handleDrawerAction);
        document.addEventListener('touchend', function(e) {
            const toggleBtn = e.target.closest('#mobileMenuBtn, .mobile-menu-btn, .btn-mobile-menu-toggle');
            if (toggleBtn) {
                toggleMobileDrawer(e);
            }
        }, { passive: false });
    }

    function setupMobileFilters() {
        const sidebar = document.querySelector('.sidebar');
        const catalogContainer = document.querySelector('.catalog-container') || document.querySelector('.listings-wrapper') || document.querySelector('.main-layout');

        if (!sidebar || !catalogContainer) return;
        if (document.getElementById('mobileFilterTriggerBar') || document.getElementById('mobileActionBar')) return;

        const filterBar = document.createElement('div');
        filterBar.id = 'mobileFilterTriggerBar';
        filterBar.className = 'mobile-filter-bar';
        filterBar.innerHTML = `
            <button type="button" class="btn-filter-trigger" id="btnToggleMobileFilters">
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2"><line x1="4" x2="20" y1="21" y2="21"/><line x1="4" x2="20" y1="14" y2="14"/><line x1="4" x2="20" y1="7" y2="7"/><circle cx="14" cy="21" r="2"/><circle cx="8" cy="14" r="2"/><circle cx="16" cy="7" r="2"/></svg>
                <span>Фільтри та сортування</span>
            </button>
        `;

        // Insert before catalog listings / main content
        const firstChild = catalogContainer.querySelector('.catalog-header') || catalogContainer.querySelector('.listings-grid') || catalogContainer;
        firstChild.parentNode.insertBefore(filterBar, firstChild);

        document.getElementById('btnToggleMobileFilters')?.addEventListener('click', function () {
            const isVisible = sidebar.classList.contains('mobile-visible');
            if (isVisible) {
                sidebar.classList.remove('mobile-visible');
                this.classList.remove('active');
            } else {
                sidebar.classList.add('mobile-visible');
                this.classList.add('active');
                sidebar.scrollIntoView({ behavior: 'smooth', block: 'start' });
            }
        });
    }

    
    // ==========================================
    // 📢 OLX-STYLE SKYSCRAPER & CATALOG AD BANNER
    // ==========================================
    function injectSkyscraperStyles() {
        if (document.getElementById('loopSkyscraperStyles')) return;
        const style = document.createElement('style');
        style.id = 'loopSkyscraperStyles';
        style.textContent = `
            .loop-olx-skyscraper {
                position: fixed;
                top: 86px;
                width: 240px;
                z-index: 995;
                background: var(--surface, #1e293b);
                border: 1.5px solid var(--border-color, #334155);
                border-radius: 16px;
                box-shadow: 0 16px 36px rgba(0, 0, 0, 0.22);
                overflow: hidden;
                transition: transform 0.25s ease, opacity 0.25s ease;
                animation: loopSkyscraperSlide 0.35s ease;
                display: flex;
                flex-direction: column;
            }
            @keyframes loopSkyscraperSlide {
                from { opacity: 0; transform: translateY(18px); }
                to { opacity: 1; transform: translateY(0); }
            }
            .loop-olx-skyscraper.pos-right {
                right: 20px;
            }
            .loop-olx-skyscraper.pos-left {
                left: 20px;
            }
            .loop-olx-skyscraper.closing {
                opacity: 0;
                transform: scale(0.92);
            }
            .skyscraper-inner {
                display: flex;
                flex-direction: column;
                padding: 12px 14px 14px;
            }
            .skyscraper-top-bar {
                display: flex;
                align-items: center;
                justify-content: space-between;
                margin-bottom: 10px;
                padding-bottom: 6px;
                border-bottom: 1px solid var(--border-color, rgba(255,255,255,0.08));
            }
            .skyscraper-ad-tag {
                font-size: 0.68rem;
                font-weight: 800;
                text-transform: uppercase;
                color: var(--text-muted, #94a3b8);
                display: inline-flex;
                align-items: center;
                gap: 4px;
            }
            .skyscraper-ad-tag .ad-badge {
                background: rgba(16, 185, 129, 0.15);
                color: #10b981;
                padding: 1px 5px;
                border-radius: 4px;
                font-size: 0.62rem;
            }
            .skyscraper-close-btn {
                background: none;
                border: none;
                color: var(--text-muted, #94a3b8);
                font-size: 1.3rem;
                line-height: 1;
                cursor: pointer;
                padding: 2px 6px;
                border-radius: 6px;
                transition: all 0.15s;
            }
            .skyscraper-close-btn:hover {
                color: #ef4444;
                background: rgba(239, 68, 68, 0.12);
            }
            .skyscraper-media img {
                width: 100%;
                max-height: 220px;
                object-fit: cover;
                border-radius: 12px;
                margin-bottom: 10px;
                border: 1px solid var(--border-color, #334155);
            }
            .skyscraper-brand-box {
                background: linear-gradient(135deg, rgba(16, 185, 129, 0.12) 0%, rgba(37, 99, 235, 0.12) 100%);
                border: 1px dashed rgba(16, 185, 129, 0.35);
                border-radius: 12px;
                padding: 18px 10px;
                text-align: center;
                margin-bottom: 12px;
            }
            .skyscraper-brand-logo {
                width: 44px;
                height: 44px;
                margin: 0 auto 6px;
                background: linear-gradient(135deg, #10b981 0%, #2563eb 100%);
                border-radius: 12px;
                color: white;
                font-weight: 900;
                font-size: 1.35rem;
                display: flex;
                align-items: center;
                justify-content: center;
                box-shadow: 0 4px 14px rgba(16, 185, 129, 0.35);
            }
            .skyscraper-title {
                font-size: 0.95rem;
                font-weight: 800;
                color: var(--text-main, #f8fafc);
                line-height: 1.35;
                margin-bottom: 6px;
            }
            .skyscraper-desc {
                font-size: 0.8rem;
                color: var(--text-muted, #94a3b8);
                line-height: 1.45;
                margin-bottom: 14px;
            }
            .skyscraper-action-btn {
                display: flex;
                align-items: center;
                justify-content: center;
                gap: 6px;
                width: 100%;
                padding: 10px 14px;
                background: linear-gradient(135deg, #10b981 0%, #059669 100%);
                color: white !important;
                text-decoration: none;
                font-weight: 800;
                font-size: 0.82rem;
                border-radius: 10px;
                box-shadow: 0 4px 12px rgba(16, 185, 129, 0.3);
                transition: transform 0.2s, box-shadow 0.2s;
            }
            .skyscraper-action-btn:hover {
                transform: translateY(-1px);
                box-shadow: 0 6px 18px rgba(16, 185, 129, 0.45);
            }
            @media (max-width: 1300px) {
                .loop-olx-skyscraper {
                    display: none !important;
                }
            }
        `;
        document.head.appendChild(style);
    }

    function renderOlxSkyscraperAd(item) {
        if (!item || item.placement === 'hidden') return;
        const dismissedKey = 'loop_ad_skyscraper_dismissed_' + (item.id || 'curr');
        if (sessionStorage.getItem(dismissedKey) === 'true') {
            return;
        }

        injectSkyscraperStyles();
        const existing = document.getElementById('loopOlxSkyscraperAd');
        if (existing) existing.remove();

        const isLeft = (item.placement === 'sidebar_left');
        const adWrap = document.createElement('aside');
        adWrap.id = 'loopOlxSkyscraperAd';
        adWrap.className = 'loop-olx-skyscraper ' + (isLeft ? 'pos-left' : 'pos-right');

        const title = (item.title || 'Тут могла бути ваша реклама!').replace(/</g, '&lt;').replace(/>/g, '&gt;');
        const desc = (item.message || 'Залучайте тисячі покупців щодня на LoopMarket.').replace(/</g, '&lt;').replace(/>/g, '&gt;');
        const actionUrl = (item.actionUrl || 'https://t.me/LoopMarketSupport').replace(/"/g, '&quot;');
        const actionText = (item.actionText || 'Розмістити рекламу 📢').replace(/</g, '&lt;').replace(/>/g, '&gt;');
        const imgUrl = item.imageUrl ? item.imageUrl.trim() : '';

        adWrap.innerHTML = `
            <div class="skyscraper-inner">
                <div class="skyscraper-top-bar">
                    <span class="skyscraper-ad-tag">Реклама <span class="ad-badge">AD</span></span>
                    <button type="button" class="skyscraper-close-btn" title="Закрити рекламу" aria-label="Закрити рекламу">&times;</button>
                </div>
                <div class="skyscraper-body">
                    ${imgUrl ? `
                        <div class="skyscraper-media">
                            <img src="${imgUrl}" alt="Рекламний банер" onerror="this.onerror=null; this.parentElement.style.display='none';">
                        </div>
                    ` : `
                        <div class="skyscraper-brand-box">
                            <div class="skyscraper-brand-logo">L</div>
                            <div style="font-size: 0.68rem; font-weight: 800; letter-spacing: 0.8px; color: #10b981;">LOOP ADS</div>
                        </div>
                    `}
                    <div class="skyscraper-title">${title}</div>
                    <div class="skyscraper-desc">${desc}</div>
                </div>
                <div class="skyscraper-footer">
                    <a href="${actionUrl}" target="_blank" rel="noopener noreferrer" class="skyscraper-action-btn">
                        <span>${actionText}</span>
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M5 12h14"/><path d="m12 5 7 7-7 7"/></svg>
                    </a>
                </div>
            </div>
        `;

        const closeBtn = adWrap.querySelector('.skyscraper-close-btn');
        if (closeBtn) {
            closeBtn.onclick = function() {
                sessionStorage.setItem(dismissedKey, 'true');
                adWrap.classList.add('closing');
                setTimeout(() => adWrap.remove(), 250);
            };
        }

        document.body.appendChild(adWrap);
    }
    window.renderOlxSkyscraperAd = renderOlxSkyscraperAd;

    function renderCatalogPromoBanner(item) {
        const slot = document.getElementById('catalogPromoSlot');
        if (!slot) return;
        const dismissedKey = 'loop_catalog_promo_dismissed_' + (item.id || 'curr');
        if (localStorage.getItem(dismissedKey) === 'true') {
            slot.style.display = 'none';
            return;
        }

        slot.style.display = 'block';
        const title = (item.title || '📢 Тут могла бути ваша реклама!').replace(/</g, '&lt;').replace(/>/g, '&gt;');
        const desc = (item.message || 'Залучайте тисячі покупців щодня на LoopMarket. Розміщуйте товари вашого бренду, банери чи акційні пропозиції.').replace(/</g, '&lt;').replace(/>/g, '&gt;');
        const actionUrl = (item.actionUrl || 'https://t.me/LoopMarketSupport').replace(/"/g, '&quot;');
        const actionText = (item.actionText || 'Розмістити рекламу 📢').replace(/</g, '&lt;').replace(/>/g, '&gt;');
        const imgUrl = item.imageUrl ? item.imageUrl.trim() : '';

        slot.innerHTML = `
            <div class="olx-promo-banner" id="olxPromoBanner" style="position: relative;">
                <div class="olx-promo-left" style="display: flex; align-items: center; gap: 14px;">
                    ${imgUrl ? `
                        <img src="${imgUrl}" style="width: 56px; height: 56px; border-radius: 12px; object-fit: cover; border: 1px solid var(--border-color);" alt="Promo">
                    ` : `
                        <div class="olx-promo-logo-box">
                            <span class="olx-promo-logo">L</span>
                            <span class="olx-promo-badge">ADS</span>
                        </div>
                    `}
                    <div class="olx-promo-text">
                        <div class="olx-promo-title">${title}</div>
                        <div class="olx-promo-sub">${desc}</div>
                    </div>
                </div>
                <div class="olx-promo-actions" style="display: flex; align-items: center; gap: 10px;">
                    <a href="${actionUrl}" target="_blank" class="olx-promo-btn">
                        <span>${actionText}</span>
                    </a>
                    <button type="button" class="olx-promo-close" onclick="dismissCustomCatalogPromo(${item.id})" title="Закрити рекламу">&times;</button>
                </div>
            </div>
        `;
    }
    window.renderCatalogPromoBanner = renderCatalogPromoBanner;

    window.dismissCustomCatalogPromo = function(id) {
        const slot = document.getElementById('catalogPromoSlot');
        if (slot) {
            slot.style.transition = 'opacity 0.25s, transform 0.25s';
            slot.style.opacity = '0';
            slot.style.transform = 'translateY(-10px)';
            setTimeout(() => { slot.style.display = 'none'; }, 250);
        }
        localStorage.setItem('loop_catalog_promo_dismissed_' + id, 'true');
        localStorage.setItem('loop_catalog_promo_dismissed', 'true');
    };

    async function checkSiteAnnouncement() {
        try {
            const path = window.location.pathname.toLowerCase();
            let pageKey = 'all';
            if (path.includes('details') || path.includes('product')) pageKey = 'details';
            else if (path.includes('services')) pageKey = 'services';
            else if (path === '/' || path.includes('index')) pageKey = 'home';
            else if (path.includes('create')) pageKey = 'create';

            const res = await fetch('/api/admin/announcement/active?page=' + pageKey);
            if (!res.ok) return;
            const data = await res.json();
            if (!data || !data.hasActive) return;

            const items = data.announcements || (data.message ? [data] : []);

            for (const item of items) {
                if (!item || !item.message) continue;
                const dismissedKey = 'loop_dismissed_announcement_' + item.id;
                if (localStorage.getItem(dismissedKey) === 'true') {
                    continue; // Закрито цим користувачем - більше не показувати
                }

                // 1. TOP HEADER BANNER
                if (!item.placement || item.placement === 'top' || item.placement === 'header') {
                    const bannerId = 'siteAnnouncementBanner_' + item.id;
                    if (document.getElementById(bannerId)) continue;

                    const banner = document.createElement('div');
                    banner.id = bannerId;
                    
                    let bg = 'linear-gradient(90deg, #d97706 0%, #f59e0b 100%)';
                    let icon = '⚠️';
                    if (item.type === 'danger') {
                        bg = 'linear-gradient(90deg, #dc2626 0%, #ef4444 100%)';
                        icon = '🚫';
                    } else if (item.type === 'promo') {
                        bg = 'linear-gradient(90deg, #059669 0%, #10b981 100%)';
                        icon = '🏷️';
                    } else if (item.type === 'info') {
                        bg = 'linear-gradient(90deg, #1d4ed8 0%, #2563eb 100%)';
                        icon = 'ℹ️';
                    }

                    banner.style.cssText = 'background: ' + bg + '; color: #ffffff; padding: 11px 18px; font-size: 0.88rem; font-weight: 600; text-align: center; display: flex; align-items: center; justify-content: center; gap: 10px; z-index: 1000; position: relative; box-shadow: 0 3px 12px rgba(0,0,0,0.18); flex-wrap: wrap;';
                    
                    const actionBtn = (item.actionUrl && item.actionText) 
                        ? `<a href="${item.actionUrl}" target="_blank" style="margin-left: 8px; background: rgba(255,255,255,0.22); color: white; padding: 3px 12px; border-radius: 8px; text-decoration: none; font-weight: 800; font-size: 0.82rem; border: 1px solid rgba(255,255,255,0.4); display: inline-flex; align-items: center; gap: 4px;">${item.actionText} &rarr;</a>` 
                        : '';

                    const titleHtml = item.title ? `<strong style="font-weight: 800; letter-spacing: 0.3px;">${item.title}:</strong> ` : '';

                    banner.innerHTML = `<span>${icon} ${titleHtml}${item.message}</span>${actionBtn}<button class="close-ann-btn" title="Закрити оголошення назавжди" style="background: none; border: none; color: white; cursor: pointer; font-size: 1.35rem; line-height: 1; margin-left: 12px; opacity: 0.9; padding: 2px 6px; border-radius: 4px;">&times;</button>`;

                    const closeBtn = banner.querySelector('.close-ann-btn');
                    if (closeBtn) {
                        closeBtn.onclick = function() {
                            localStorage.setItem(dismissedKey, 'true');
                            banner.remove();
                        };
                    }
                    document.body.prepend(banner);
                }

                // 3. OLX-STYLE SKYSCRAPER SIDEBAR BANNER (RIGHT OR LEFT)
                if (item.placement === 'sidebar_right' || item.placement === 'sidebar_left' || (item.type === 'promo' && item.placement && item.placement.startsWith('sidebar'))) {
                    renderOlxSkyscraperAd(item);
                }

                // 4. CATALOG IN-GRID PROMO BANNER
                if (item.placement === 'catalog') {
                    renderCatalogPromoBanner(item);
                }

                // 2. MODAL POPUP
                if (item.placement === 'modal') {
                    const modalId = 'siteAnnouncementModal_' + item.id;
                    if (document.getElementById(modalId)) continue;

                    const overlay = document.createElement('div');
                    overlay.id = modalId;
                    overlay.style.cssText = 'position: fixed; inset: 0; background: rgba(0,0,0,0.7); backdrop-filter: blur(6px); z-index: 999999; display: flex; align-items: center; justify-content: center; padding: 18px;';
                    
                    overlay.innerHTML = `
                        <div style="background: var(--surface, #1e293b); border: 1.5px solid var(--border-color, #334155); border-radius: 20px; max-width: 500px; width: 100%; padding: 24px; box-shadow: 0 25px 50px rgba(0,0,0,0.5); color: var(--text-main, #f8fafc); position: relative;">
                            <div style="display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 12px;">
                                <h3 style="margin: 0; font-size: 1.2rem; font-weight: 800; display: flex; align-items: center; gap: 8px;">
                                    <span>📢</span>
                                    <span>${item.title || 'Повідомлення платформи'}</span>
                                </h3>
                                <button type="button" class="close-modal-ann" style="background: none; border: none; font-size: 1.5rem; color: var(--text-muted, #94a3b8); cursor: pointer; line-height: 1;">&times;</button>
                            </div>
                            <p style="font-size: 0.92rem; color: var(--text-muted, #cbd5e1); line-height: 1.5; margin-bottom: 18px;">
                                ${item.message}
                            </p>
                            <div style="display: flex; justify-content: flex-end; gap: 10px;">
                                ${item.actionUrl ? `<a href="${item.actionUrl}" target="_blank" style="background: #2563eb; color: white; padding: 9px 18px; border-radius: 10px; font-weight: 700; text-decoration: none; font-size: 0.88rem;">${item.actionText || 'Дізнатися більше'}</a>` : ''}
                                <button type="button" class="close-modal-ann" style="background: rgba(255,255,255,0.08); border: 1px solid var(--border-color, #334155); color: var(--text-main, #fff); padding: 9px 18px; border-radius: 10px; font-weight: 700; cursor: pointer; font-size: 0.88rem;">Закрити</button>
                            </div>
                        </div>
                    `;

                    overlay.querySelectorAll('.close-modal-ann').forEach(btn => {
                        btn.onclick = function() {
                            localStorage.setItem(dismissedKey, 'true');
                            overlay.remove();
                        };
                    });
                    document.body.appendChild(overlay);
                }
            }
        } catch(e) {}
    }

    
    // ==========================================
    // 🔔 REAL-TIME NOTIFICATION CENTER SYSTEM
    // ==========================================
    let globalNotifsList = [];

    function setupNotificationCenter() {
        const token = localStorage.getItem('token') || localStorage.getItem('loopToken');
        if (!token) return;

        // Clean up any duplicate notification buttons if multiple exist
        const existingBtns = Array.from(document.querySelectorAll('#notifBtn, .btn-icon[onclick*="toggleNotifications"], [onclick*="toggleGlobalNotifications"]'));
        if (existingBtns.length > 1) {
            for (let i = 0; i < existingBtns.length - 1; i++) {
                const b = existingBtns[i];
                if (b.parentElement && b.parentElement.firstElementChild === b) {
                    b.remove();
                }
            }
        }

        let notifBtn = document.getElementById('notifBtn') 
                    || document.querySelector('[onclick*="toggleNotifications"]') 
                    || document.querySelector('[onclick*="toggleGlobalNotifications"]')
                    || document.querySelector('.btn-icon[title*="повіщ"]')
                    || document.querySelector('.icon-btn[title*="повіщ"]');

        if (notifBtn) {
            notifBtn.id = 'notifBtn';
        } else {
            const container = document.querySelector('.nav-actions') || document.querySelector('.header-right') || document.getElementById('dynamicIcons');
            if (container) {
                notifBtn = document.createElement('button');
                notifBtn.id = 'notifBtn';
                notifBtn.className = 'btn-icon';
                notifBtn.title = 'Сповіщення';
                notifBtn.style.cssText = 'position: relative; background: var(--surface-hover); border: 1px solid var(--border-color); border-radius: 10px; width: 40px; height: 40px; display: inline-flex; align-items: center; justify-content: center; cursor: pointer; color: var(--text-main); transition: 0.2s;';
                notifBtn.innerHTML = `
                    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9"/><path d="M10.3 21a1.94 1.94 0 0 0 3.4 0"/></svg>
                    <span class="notif-badge" id="notifBadge" style="display: none; position: absolute; top: -4px; right: -4px; background: #ef4444; color: white; border-radius: 10px; font-size: 0.7rem; font-weight: 800; padding: 2px 6px; border: 2px solid var(--surface);"></span>
                `;
                const themeBtn = document.getElementById('themeToggleBtn') || document.getElementById('themeToggle') || container.querySelector('.btn-create-top') || container.querySelector('.btn-primary');
                if (themeBtn) {
                    container.insertBefore(notifBtn, themeBtn);
                } else {
                    container.appendChild(notifBtn);
                }
            }
        }

        let notifDropdown = document.getElementById('notifDropdown');
        if (notifBtn && !notifDropdown) {
            notifDropdown = document.createElement('div');
            notifDropdown.id = 'notifDropdown';
            notifDropdown.className = 'notif-dropdown';
            notifDropdown.style.cssText = 'display: none; position: absolute; top: 62px; right: 20px; width: 340px; max-width: calc(100vw - 30px); background: var(--surface); border: 1.5px solid var(--border-color); border-radius: 16px; box-shadow: 0 16px 40px rgba(0,0,0,0.3); z-index: 2100; overflow: hidden;';
            notifDropdown.innerHTML = `
                <div style="display: flex; justify-content: space-between; align-items: center; padding: 14px 16px; border-bottom: 1px solid var(--border-color); background: var(--bg-color);">
                    <span style="font-weight: 800; font-size: 0.95rem; color: var(--text-main);">🔔 Центр сповіщень</span>
                    <button type="button" id="btnMarkAllNotifsRead" style="background: none; border: none; font-size: 0.78rem; color: #2563eb; cursor: pointer; font-weight: 700; padding: 0;">Позначити всі</button>
                </div>
                <div class="notif-body" id="notifList" style="max-height: 380px; overflow-y: auto; padding: 6px 0;">
                    <p style="padding: 20px; text-align: center; color: var(--text-muted); font-size: 0.85rem;">Завантаження сповіщень...</p>
                </div>
            `;
            document.body.appendChild(notifDropdown);
        }

        if (notifBtn) {
            notifBtn.onclick = function(e) {
                e.stopPropagation();
                toggleGlobalNotifications();
            };
        }

        window.toggleNotifications = toggleGlobalNotifications;

        const markAllBtn = document.getElementById('btnMarkAllNotifsRead');
        if (markAllBtn) {
            markAllBtn.onclick = clearGlobalNotifications;
        }

        window.addEventListener('click', function(e) {
            if (notifDropdown && notifBtn && !notifDropdown.contains(e.target) && !notifBtn.contains(e.target)) {
                notifDropdown.style.display = 'none';
                notifDropdown.classList.remove('active');
            }
        });

        loadGlobalNotifications();
        connectSignalRForNotifications(token);
    }

    function toggleGlobalNotifications() {
        const notifDropdown = document.getElementById('notifDropdown');
        if (!notifDropdown) return;

        const isVisible = notifDropdown.style.display === 'block' || notifDropdown.classList.contains('active');
        if (isVisible) {
            notifDropdown.style.display = 'none';
            notifDropdown.classList.remove('active');
        } else {
            notifDropdown.style.display = 'block';
            notifDropdown.classList.add('active');
            loadGlobalNotifications();
        }
    }

    async function loadGlobalNotifications() {
        const token = localStorage.getItem('token') || localStorage.getItem('loopToken');
        if (!token) return;

        try {
            const res = await fetch('/api/notifications?wrapped=true', {
                headers: { 'Authorization': `Bearer ${token}` }
            });
            if (res.ok) {
                const data = await res.json();
                const items = Array.isArray(data) ? data : (data.items || []);
                const unreadCount = data.unreadCount !== undefined ? data.unreadCount : items.filter(n => !n.isRead).length;
                globalNotifsList = items;
                renderGlobalNotifications(items, unreadCount);
            }
        } catch (e) {
            console.error('Помилка завантаження сповіщень:', e);
        }
    }

    function renderGlobalNotifications(items, unreadCount) {
        const badges = [document.getElementById('notifBadge'), document.getElementById('globalUnreadBadge')].filter(Boolean);
        badges.forEach(badge => {
            if (unreadCount > 0) {
                badge.innerText = unreadCount > 99 ? '99+' : unreadCount;
                badge.style.setProperty('display', 'inline-flex', 'important');
            } else {
                badge.style.setProperty('display', 'none', 'important');
            }
        });

        const list = document.getElementById('notifList');
        if (!list) return;

        if (!items || items.length === 0) {
            list.innerHTML = `
                <div style="padding: 30px 20px; text-align: center; color: var(--text-muted);">
                    <div style="font-size: 2rem; margin-bottom: 8px;">🔕</div>
                    <div style="font-weight: 700; font-size: 0.9rem; color: var(--text-main);">У вас немає нових сповіщень</div>
                    <div style="font-size: 0.8rem; margin-top: 4px;">Тут з'являтимуться оновлення замовлень, повідомлення та відгуки</div>
                </div>
            `;
            return;
        }

        list.innerHTML = items.map(n => {
            const isUnread = !n.isRead;
            const timeAgo = formatTimeAgo(n.createdAt);
            const icon = n.title.includes('замовлення') || n.title.includes('доставк') ? '📦' :
                         (n.title.includes('відгук') ? '⭐' :
                         (n.title.includes('повідомлення') ? '💬' :
                         (n.title.includes('безпек') || n.title.includes('Захист') ? '🛡️' : '🔔')));

            return `
                <a href="${n.link || '#'}" onclick="handleNotifClick(event, ${n.id}, '${n.link || ''}')" style="display: flex; align-items: flex-start; gap: 12px; padding: 12px 16px; text-decoration: none; border-bottom: 1px solid var(--border-color); background: ${isUnread ? 'rgba(16, 185, 129, 0.08)' : 'transparent'}; transition: background 0.2s;">
                    <div style="width: 36px; height: 36px; border-radius: 10px; background: var(--bg-color); border: 1px solid var(--border-color); display: flex; align-items: center; justify-content: center; font-size: 1.1rem; flex-shrink: 0;">
                        ${icon}
                    </div>
                    <div style="flex-grow: 1; min-width: 0;">
                        <div style="display: flex; justify-content: space-between; align-items: baseline; gap: 6px;">
                            <div style="font-weight: 700; font-size: 0.88rem; color: var(--text-main); white-space: nowrap; overflow: hidden; text-overflow: ellipsis;">${n.title}</div>
                            <span style="font-size: 0.72rem; color: var(--text-muted); flex-shrink: 0;">${timeAgo}</span>
                        </div>
                        <div style="font-size: 0.82rem; color: var(--text-muted); margin-top: 2px; line-height: 1.35; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden;">${n.message}</div>
                    </div>
                    ${isUnread ? '<div style="width: 8px; height: 8px; border-radius: 50%; background: #10b981; flex-shrink: 0; margin-top: 6px;"></div>' : ''}
                </a>
            `;
        }).join('');
    }

    window.handleNotifClick = async function(event, notifId, link) {
        const token = localStorage.getItem('token') || localStorage.getItem('loopToken');
        if (token && notifId) {
            fetch(`/api/notifications/${notifId}/read`, {
                method: 'PUT',
                headers: { 'Authorization': `Bearer ${token}` }
            }).catch(() => {});
        }
        if (link && link !== '#') {
            event.preventDefault();
            window.location.href = link;
        }
    };

    async function clearGlobalNotifications() {
        const token = localStorage.getItem('token') || localStorage.getItem('loopToken');
        if (!token) return;

        try {
            await fetch('/api/notifications/mark-read', {
                method: 'POST',
                headers: { 'Authorization': `Bearer ${token}` }
            });
            loadGlobalNotifications();
        } catch (e) {
            console.error(e);
        }
    }

    function formatTimeAgo(dateStr) {
        if (!dateStr) return '';
        const date = new Date(dateStr);
        const now = new Date();
        const diffMs = now - date;
        const diffSec = Math.floor(diffMs / 1000);
        const diffMin = Math.floor(diffSec / 60);
        const diffHours = Math.floor(diffMin / 60);
        const diffDays = Math.floor(diffHours / 24);

        if (diffSec < 45) return 'щойно';
        if (diffMin < 60) return `${diffMin} хв`;
        if (diffHours < 24) return `${diffHours} год`;
        if (diffDays === 1) return 'вчора';
        if (diffDays < 7) return `${diffDays} дн`;
        return date.toLocaleDateString('uk-UA', { day: 'numeric', month: 'short' });
    }

    function connectSignalRForNotifications(token) {
        if (!window.signalR) {
            const script = document.createElement('script');
            script.src = 'https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/7.0.5/signalr.min.js';
            script.onload = () => startHubConnection(token);
            document.head.appendChild(script);
        } else {
            startHubConnection(token);
        }
    }

    let notifHubConnection = null;
    function startHubConnection(token) {
        if (notifHubConnection || !window.signalR) return;

        try {
            notifHubConnection = new signalR.HubConnectionBuilder()
                .withUrl('/hubs/chat', { accessTokenFactory: () => token })
                .withAutomaticReconnect([0, 2000, 5000, 10000])
                .build();

            notifHubConnection.on('ReceiveNotification', (notif) => {
                showInAppToast(notif);
                loadGlobalNotifications();
            });

            notifHubConnection.start().catch(() => {});
        } catch(e) {}
    }

    function showInAppToast(notif) {
        if (window.LoopToast && window.LoopToast.info) {
            window.LoopToast.info(`🔔 ${notif.title}: ${notif.message}`);
            return;
        }

        const toast = document.createElement('div');
        toast.style.cssText = 'position: fixed; bottom: 24px; right: 24px; background: var(--surface); color: var(--text-main); border: 1.5px solid var(--border-color); border-left: 4px solid #10b981; border-radius: 12px; padding: 12px 18px; box-shadow: 0 10px 30px rgba(0,0,0,0.25); z-index: 9999; display: flex; align-items: center; gap: 10px; font-size: 0.9rem; font-weight: 600; cursor: pointer; transition: 0.3s; transform: translateY(20px); opacity: 0;';
        toast.innerHTML = `<span>🔔</span><div><div style="font-size: 0.85rem; font-weight: 800;">${notif.title}</div><div style="font-size: 0.78rem; color: var(--text-muted); font-weight: 400;">${notif.message}</div></div>`;
        document.body.appendChild(toast);

        setTimeout(() => {
            toast.style.transform = 'translateY(0)';
            toast.style.opacity = '1';
        }, 10);

        toast.onclick = () => {
            if (notif.link) window.location.href = notif.link;
            toast.remove();
        };

        setTimeout(() => {
            toast.style.transform = 'translateY(20px)';
            toast.style.opacity = '0';
            setTimeout(() => toast.remove(), 300);
        }, 5000);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            initResponsiveNav();
            checkSiteAnnouncement();
            setupNotificationCenter();
        });
    } else {
        initResponsiveNav();
        checkSiteAnnouncement();
        setupNotificationCenter();
    }

    // Глобальне перехоплення кліків на створення оголошень для незареєстрованих користувачів
    document.addEventListener('click', function(e) {
        const createLink = e.target.closest('a[href*="create.html"], .btn-create-top, .create-fab, .bottom-nav-sell, .drawer-sell-link, .btn-create-mobile');
        if (!createLink) return;
        const currentToken = localStorage.getItem('loopToken') || localStorage.getItem('token');
        if (!currentToken) {
            e.preventDefault();
            e.stopPropagation();
            if (typeof LoopToast !== 'undefined' && LoopToast.info) {
                LoopToast.info('Щоб створити оголошення, будь ласка, увійдіть або зареєструйтесь');
            }
            const targetUrl = createLink.getAttribute('href') || '/create.html';
            window.location.href = '/login.html?returnUrl=' + encodeURIComponent(targetUrl) + '&notice=create_auth_required';
        }
    }, true);

})();
