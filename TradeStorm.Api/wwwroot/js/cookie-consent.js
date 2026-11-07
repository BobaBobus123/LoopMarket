(function () {
    const COOKIE_CONSENT_KEY = 'loop_cookie_consent_accepted';
    
    // Check if user already made a decision
    if (localStorage.getItem(COOKIE_CONSENT_KEY)) {
        return;
    }

    // Create banner elements
    const banner = document.createElement('div');
    banner.id = 'loopCookieConsentBanner';
    banner.style.position = 'fixed';
    banner.style.bottom = '20px';
    banner.style.left = '50%';
    banner.style.transform = 'translateX(-50%)';
    banner.style.width = 'calc(100% - 32px)';
    banner.style.maxWidth = '580px';
    banner.style.background = 'var(--surface, #ffffff)';
    banner.style.color = 'var(--text-main, #0f172a)';
    banner.style.border = '1px solid var(--border-color, #e2e8f0)';
    banner.style.borderRadius = '14px';
    banner.style.padding = '16px 20px';
    banner.style.boxShadow = '0 12px 32px rgba(0, 0, 0, 0.15)';
    banner.style.zIndex = '999999';
    banner.style.display = 'flex';
    banner.style.alignItems = 'center';
    banner.style.justifyContent = 'space-between';
    banner.style.gap = '16px';
    banner.style.fontSize = '0.9rem';
    banner.style.lineHeight = '1.45';

    banner.innerHTML = `
        <div style="flex: 1;">
            <div style="font-weight: 700; margin-bottom: 4px; display: flex; align-items: center; gap: 6px;">
                <span>🍪 Ми цінуємо вашу приватність</span>
            </div>
            <div style="color: var(--text-muted, #64748b); font-size: 0.85rem;">
                Loop Market використовує виключно захищені сесійні файли cookies для збереження вашої авторизації та налаштувань.
                <a href="/privacy.html" style="color: var(--primary-light, #10b981); text-decoration: underline; font-weight: 600;">Детальніше</a>
            </div>
        </div>
        <div style="display: flex; gap: 8px;">
            <button id="btnAcceptCookies" style="padding: 8px 16px; background: var(--primary-light, #10b981); color: white; border: none; border-radius: 8px; font-weight: 700; cursor: pointer; font-size: 0.85rem; transition: 0.2s;">
                Зрозуміло
            </button>
        </div>
    `;

    document.addEventListener('DOMContentLoaded', () => {
        document.body.appendChild(banner);
        const btn = document.getElementById('btnAcceptCookies');
        if (btn) {
            btn.addEventListener('click', () => {
                localStorage.setItem(COOKIE_CONSENT_KEY, 'true');
                banner.remove();
            });
        }
    });

    if (document.readyState === 'complete' || document.readyState === 'interactive') {
        if (!document.getElementById('loopCookieConsentBanner')) {
            document.body.appendChild(banner);
            const btn = document.getElementById('btnAcceptCookies');
            if (btn) {
                btn.addEventListener('click', () => {
                    localStorage.setItem(COOKIE_CONSENT_KEY, 'true');
                    banner.remove();
                });
            }
        }
    }
})();
