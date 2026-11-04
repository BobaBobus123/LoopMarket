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

        if (navActions && !document.getElementById('mobileMenuBtn')) {
            const menuBtn = document.createElement('button');
            menuBtn.id = 'mobileMenuBtn';
            menuBtn.className = 'btn-mobile-menu-toggle';
            menuBtn.setAttribute('aria-label', 'Меню навігації');
            menuBtn.innerHTML = `
                <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
                    <line x1="3" y1="12" x2="21" y2="12"></line>
                    <line x1="3" y1="6" x2="21" y2="6"></line>
                    <line x1="3" y1="18" x2="21" y2="18"></line>
                </svg>
            `;
            navActions.appendChild(menuBtn);

            menuBtn.addEventListener('click', toggleMobileDrawer);
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
            const userAvatar = user?.avatarUrl || `https://ui-avatars.com/api/?name=${encodeURIComponent(userName)}&background=10b981&color=fff&bold=true`;

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
                    <a href="/docs.html" class="mobile-drawer-link ${currentPath.includes('docs') ? 'active' : ''}">
                        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20"/><path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z"/></svg>
                        <span>API Документація</span>
                    </a>
                </div>

                <div style="margin-top: auto; padding-top: 16px; border-top: 1px solid var(--border-subtle); display: flex; flex-direction: column; gap: 10px;">
                    <div style="display: flex; justify-content: space-between; align-items: center; padding: 4px 6px;">
                        <span style="font-size: 0.88rem; font-weight: 600; color: var(--text-secondary);">Тема оформлення</span>
                        <button class="btn btn-sm btn-outline" onclick="window.toggleTheme ? window.toggleTheme() : null" style="gap: 6px;">
                            <span>🌓 Змінити</span>
                        </button>
                    </div>

                    ${token ? `
                        <button class="btn btn-danger" onclick="window.showLogoutModal ? window.showLogoutModal() : (localStorage.clear(), window.location.href='/login.html')" style="width: 100%; justify-content: center;">
                            🚪 Вийти з акаунта
                        </button>
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
        if (!document.getElementById('mobileBottomBar')) {
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
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2"><path d="M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>
                        <span>Профіль</span>
                    </a>
                </div>
            `;
            document.body.appendChild(bottomBar);
        }

        // 4. Mobile Filters Collapsible Toggle for index.html
        setupMobileFilters();
    }

    function toggleMobileDrawer() {
        const drawer = document.getElementById('mobileNavDrawer');
        const overlay = document.getElementById('mobileNavOverlay');
        if (!drawer || !overlay) return;

        const isOpen = drawer.classList.contains('active');
        if (isOpen) {
            closeMobileDrawer();
        } else {
            drawer.classList.add('active');
            overlay.classList.add('active');
            document.body.style.overflow = 'hidden';
        }
    }

    function closeMobileDrawer() {
        const drawer = document.getElementById('mobileNavDrawer');
        const overlay = document.getElementById('mobileNavOverlay');
        if (drawer) drawer.classList.remove('active');
        if (overlay) overlay.classList.remove('active');
        document.body.style.overflow = '';
    }

    function setupMobileFilters() {
        const sidebar = document.querySelector('.sidebar');
        const catalogContainer = document.querySelector('.catalog-container') || document.querySelector('.listings-wrapper') || document.querySelector('.main-layout');

        if (!sidebar || !catalogContainer) return;
        if (document.getElementById('mobileFilterTriggerBar')) return;

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

    async function checkSiteAnnouncement() {
        try {
            const res = await fetch('/api/admin/announcement/active');
            if (!res.ok) return;
            const data = await res.json();
            if (data && data.hasActive && data.message) {
                if (document.getElementById('siteAnnouncementBanner')) return;
                const banner = document.createElement('div');
                banner.id = 'siteAnnouncementBanner';
                const bg = data.type === 'danger' ? '#ef4444' : (data.type === 'info' ? '#2563eb' : '#f59e0b');
                banner.style.cssText = 'background: ' + bg + '; color: #ffffff; padding: 10px 16px; font-size: 0.88rem; font-weight: 700; text-align: center; display: flex; align-items: center; justify-content: center; gap: 8px; z-index: 1000; position: relative; box-shadow: 0 2px 8px rgba(0,0,0,0.15);';
                banner.innerHTML = '<span>📢 ' + data.message + '</span><button onclick="this.parentElement.remove()" style="background: none; border: none; color: white; cursor: pointer; font-size: 1.2rem; line-height: 1; margin-left: 12px; opacity: 0.85;">&times;</button>';
                document.body.prepend(banner);
            }
        } catch(e) {}
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            initResponsiveNav();
            checkSiteAnnouncement();
        });
    } else {
        initResponsiveNav();
        checkSiteAnnouncement();
    }
})();
