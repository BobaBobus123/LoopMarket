/**
 * LoopMarket Premium UI Dialogs & Toasts System
 * Elegant, accessible replacement for archaic browser alert/confirm/prompt
 */

(function (window, document) {
    'use strict';

    // Ensure CSS is loaded
    if (!document.querySelector('link[href*="loop-dialogs.css"]')) {
        const link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = '/css/loop-dialogs.css';
        document.head.appendChild(link);
    }

    // ==================== TOAST NOTIFICATION ENGINE ====================
    let toastContainer = null;

    function getToastContainer() {
        if (!toastContainer || !document.body.contains(toastContainer)) {
            toastContainer = document.createElement('div');
            toastContainer.className = 'loop-toast-container';
            document.body.appendChild(toastContainer);
        }
        return toastContainer;
    }

    const LoopToast = {
        show(message, type = 'info', duration = 3500) {
            const container = getToastContainer();
            const toast = document.createElement('div');
            toast.className = `loop-toast ${type}`;

            let iconHtml = `<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><path d="M12 16v-4"/><path d="M12 8h.01"/></svg>`;
            if (type === 'success') {
                iconHtml = `<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="20 6 9 17 4 12"/></svg>`;
            } else if (type === 'error') {
                iconHtml = `<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>`;
            }

            toast.innerHTML = `
                <div class="loop-toast-icon">${iconHtml}</div>
                <div class="loop-toast-message">${escapeHtml(message)}</div>
                <button type="button" class="loop-toast-close" aria-label="Закрити">&times;</button>
                <div class="loop-toast-progress" style="animation-duration: ${duration}ms;"></div>
            `;

            container.appendChild(toast);
            requestAnimationFrame(() => toast.classList.add('show'));

            let timer = setTimeout(dismiss, duration);

            function dismiss() {
                clearTimeout(timer);
                toast.classList.remove('show');
                setTimeout(() => toast.remove(), 320);
            }

            toast.querySelector('.loop-toast-close').addEventListener('click', dismiss);
        },

        success(msg, duration) { this.show(msg, 'success', duration); },
        error(msg, duration) { this.show(msg, 'error', duration); },
        info(msg, duration) { this.show(msg, 'info', duration); }
    };

    // ==================== MODAL DIALOG ENGINE ====================
    let activeOverlay = null;

    function createOverlay() {
        const overlay = document.createElement('div');
        overlay.className = 'loop-dialog-overlay';
        overlay.innerHTML = `<div class="loop-dialog-card" role="dialog" aria-modal="true"></div>`;
        document.body.appendChild(overlay);
        return overlay;
    }

    const LoopDialog = {
        alert(message, options = {}) {
            return new Promise((resolve) => {
                const overlay = createOverlay();
                const card = overlay.querySelector('.loop-dialog-card');
                const opts = (typeof options === 'string') ? { title: options } : (options || {});
                const title = opts.title || 'Повідомлення';
                const confirmText = opts.confirmText || 'Зрозуміло';
                const type = opts.type || 'info';

                let iconHtml = `<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><path d="M12 16v-4"/><path d="M12 8h.01"/></svg>`;
                if (type === 'success') {
                    iconHtml = `<svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.6"><polyline points="20 6 9 17 4 12"/></svg>`;
                } else if (type === 'error' || type === 'danger') {
                    iconHtml = `<svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.6"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>`;
                }

                card.innerHTML = `
                    <div class="loop-dialog-icon ${type}">${iconHtml}</div>
                    <h3 class="loop-dialog-title">${escapeHtml(title)}</h3>
                    <div class="loop-dialog-message">${escapeHtml(message)}</div>
                    <div class="loop-dialog-actions">
                        <button type="button" class="loop-dialog-btn confirm-primary" id="loopDialogConfirmBtn">${escapeHtml(confirmText)}</button>
                    </div>
                `;

                requestAnimationFrame(() => overlay.classList.add('active'));

                const confirmBtn = card.querySelector('#loopDialogConfirmBtn');
                confirmBtn.focus();

                function close() {
                    overlay.classList.remove('active');
                    setTimeout(() => overlay.remove(), 240);
                    resolve(true);
                }

                confirmBtn.addEventListener('click', close);
                overlay.addEventListener('click', (e) => { if (e.target === overlay) close(); });
                window.addEventListener('keydown', function onKey(e) {
                    if (e.key === 'Escape' || e.key === 'Enter') {
                        window.removeEventListener('keydown', onKey);
                        close();
                    }
                });
            });
        },

        confirm(message, options = {}) {
            return new Promise((resolve) => {
                const overlay = createOverlay();
                const card = overlay.querySelector('.loop-dialog-card');
                const opts = (typeof options === 'string') ? { title: options } : (options || {});
                const title = opts.title || 'Підтвердження дії';
                const confirmText = opts.confirmText || 'Підтвердити';
                const cancelText = opts.cancelText || 'Скасувати';
                const isDanger = opts.danger !== false;

                const iconHtml = `<svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4"><path d="M12 9v4"/><path d="M12 17h.01"/><path d="M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/></svg>`;

                card.innerHTML = `
                    <div class="loop-dialog-icon ${isDanger ? 'danger' : 'info'}">${iconHtml}</div>
                    <h3 class="loop-dialog-title">${escapeHtml(title)}</h3>
                    <div class="loop-dialog-message">${escapeHtml(message)}</div>
                    <div class="loop-dialog-actions">
                        <button type="button" class="loop-dialog-btn cancel" id="loopDialogCancelBtn">${escapeHtml(cancelText)}</button>
                        <button type="button" class="loop-dialog-btn ${isDanger ? 'confirm-danger' : 'confirm-primary'}" id="loopDialogConfirmBtn">${escapeHtml(confirmText)}</button>
                    </div>
                `;

                requestAnimationFrame(() => overlay.classList.add('active'));

                const confirmBtn = card.querySelector('#loopDialogConfirmBtn');
                const cancelBtn = card.querySelector('#loopDialogCancelBtn');
                confirmBtn.focus();

                function finish(result) {
                    overlay.classList.remove('active');
                    setTimeout(() => overlay.remove(), 240);
                    resolve(result);
                }

                confirmBtn.addEventListener('click', () => finish(true));
                cancelBtn.addEventListener('click', () => finish(false));
                overlay.addEventListener('click', (e) => { if (e.target === overlay) finish(false); });

                window.addEventListener('keydown', function onKey(e) {
                    if (e.key === 'Escape') {
                        window.removeEventListener('keydown', onKey);
                        finish(false);
                    } else if (e.key === 'Enter') {
                        window.removeEventListener('keydown', onKey);
                        finish(true);
                    }
                });
            });
        },

        prompt(message, defaultValueOrOptions = '', titleArg = '') {
            return new Promise((resolve) => {
                const overlay = createOverlay();
                const card = overlay.querySelector('.loop-dialog-card');
                let opts = {};
                if (typeof defaultValueOrOptions === 'object' && defaultValueOrOptions !== null) {
                    opts = defaultValueOrOptions;
                } else {
                    opts = {
                        defaultValue: String(defaultValueOrOptions || ''),
                        title: titleArg || 'Вкажіть дані'
                    };
                }
                const title = opts.title || 'Вкажіть дані';
                const placeholder = opts.placeholder || '';
                const defaultValue = opts.defaultValue || '';
                const confirmText = opts.confirmText || 'Підтвердити';
                const cancelText = opts.cancelText || 'Скасувати';

                const iconHtml = `<svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4"><path d="M12 20h9"/><path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z"/></svg>`;

                card.innerHTML = `
                    <div class="loop-dialog-icon prompt">${iconHtml}</div>
                    <h3 class="loop-dialog-title">${escapeHtml(title)}</h3>
                    <div class="loop-dialog-message">${escapeHtml(message)}</div>
                    <input type="text" class="loop-dialog-input" id="loopDialogPromptInput" placeholder="${escapeHtml(placeholder)}" value="${escapeHtml(defaultValue)}">
                    <div class="loop-dialog-actions">
                        <button type="button" class="loop-dialog-btn cancel" id="loopDialogCancelBtn">${escapeHtml(cancelText)}</button>
                        <button type="button" class="loop-dialog-btn confirm-primary" id="loopDialogConfirmBtn">${escapeHtml(confirmText)}</button>
                    </div>
                `;

                requestAnimationFrame(() => overlay.classList.add('active'));

                const input = card.querySelector('#loopDialogPromptInput');
                const confirmBtn = card.querySelector('#loopDialogConfirmBtn');
                const cancelBtn = card.querySelector('#loopDialogCancelBtn');

                setTimeout(() => input.focus(), 100);

                function finish(val) {
                    overlay.classList.remove('active');
                    setTimeout(() => overlay.remove(), 240);
                    resolve(val);
                }

                confirmBtn.addEventListener('click', () => finish(input.value.trim()));
                cancelBtn.addEventListener('click', () => finish(null));
                overlay.addEventListener('click', (e) => { if (e.target === overlay) finish(null); });

                input.addEventListener('keydown', (e) => {
                    if (e.key === 'Enter') finish(input.value.trim());
                    if (e.key === 'Escape') finish(null);
                });
            });
        },

        // Dedicated Beautiful Complaint Modal
        complaint(partnerName = 'користувача') {
            return new Promise((resolve) => {
                const overlay = createOverlay();
                const card = overlay.querySelector('.loop-dialog-card');
                card.style.maxWidth = '480px';

                card.innerHTML = `
                    <div class="loop-dialog-icon danger" style="margin-bottom: 12px;">
                        <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4"><path d="M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>
                    </div>
                    <h3 class="loop-dialog-title">Поскаржитися на порушення</h3>
                    <div class="loop-dialog-message" style="margin-bottom: 14px;">
                        Оберіть причину скарги на ${escapeHtml(partnerName)}. Модерація LoopMarket перевірить звернення для забезпечення безпеки платформи.
                    </div>

                    <div class="loop-dialog-radio-group">
                        <label class="loop-dialog-radio-item selected">
                            <input type="radio" name="complaintReason" value="Шахрайство або фішинг" checked style="accent-color: #10b981;">
                            <span style="display:inline-flex;align-items:center;gap:8px;"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#ef4444" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><path d="m4.9 4.9 14.2 14.2"/></svg> Шахрайство або підозріла оплата</span>
                        </label>
                        <label class="loop-dialog-radio-item">
                            <input type="radio" name="complaintReason" value="Спам або нав'язлива реклама" style="accent-color: #10b981;">
                            <span style="display:inline-flex;align-items:center;gap:8px;"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#f59e0b" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="m3 11 18-5v12L3 13v-2z"/><path d="M11.6 16.8a3 3 0 1 1-5.8-1.6"/></svg> Спам або реклама</span>
                        </label>
                        <label class="loop-dialog-radio-item">
                            <input type="radio" name="complaintReason" value="Некоректна поведінка" style="accent-color: #10b981;">
                            <span style="display:inline-flex;align-items:center;gap:8px;"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#f97316" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z"/><path d="M12 9v4M12 17h.01"/></svg> Образи або груба поведінка</span>
                        </label>
                        <label class="loop-dialog-radio-item">
                            <input type="radio" name="complaintReason" value="Невідповідність товару опису" style="accent-color: #10b981;">
                            <span style="display:inline-flex;align-items:center;gap:8px;"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#3b82f6" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16Z"/><path d="m3.3 7 8.7 5 8.7-5"/><path d="M12 22V12"/></svg> Невідповідність товару / послуги</span>
                        </label>
                    </div>

                    <input type="text" class="loop-dialog-input" id="complaintDetailsInput" placeholder="Коментар (необов'язково)..." style="margin-bottom: 20px;">

                    <div class="loop-dialog-actions">
                        <button type="button" class="loop-dialog-btn cancel" id="complaintCancelBtn">Скасувати</button>
                        <button type="button" class="loop-dialog-btn confirm-danger" id="complaintSubmitBtn">Надіслати скаргу</button>
                    </div>
                `;

                requestAnimationFrame(() => overlay.classList.add('active'));

                // Handle radio item styles
                card.querySelectorAll('.loop-dialog-radio-item').forEach(item => {
                    item.addEventListener('click', () => {
                        card.querySelectorAll('.loop-dialog-radio-item').forEach(i => i.classList.remove('selected'));
                        item.classList.add('selected');
                    });
                });

                function finish(res) {
                    overlay.classList.remove('active');
                    setTimeout(() => overlay.remove(), 240);
                    resolve(res);
                }

                card.querySelector('#complaintCancelBtn').addEventListener('click', () => finish(null));
                card.querySelector('#complaintSubmitBtn').addEventListener('click', () => {
                    const checkedRadio = card.querySelector('input[name="complaintReason"]:checked');
                    const reason = checkedRadio ? checkedRadio.value : 'Порушення правил';
                    const details = card.querySelector('#complaintDetailsInput').value.trim();
                    finish({ reason, details });
                });
                overlay.addEventListener('click', (e) => { if (e.target === overlay) finish(null); });
            });
        }
    };

    function escapeHtml(str) {
        if (!str) return '';
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    // Expose to global window
    window.LoopToast = LoopToast;
    window.LoopDialog = LoopDialog;

    // Polyfill / Bridge for seamless integration with existing code:
    // If a script calls alert(msg), use LoopToast.info or LoopDialog.alert
    const nativeAlert = window.alert;
    window.alert = function (msg) {
        if (typeof msg === 'string' && (msg.toLowerCase().includes('успішно') || msg.toLowerCase().includes('дякуємо') || msg.toLowerCase().includes('збережено') || msg.toLowerCase().includes('скопійовано'))) {
            LoopToast.success(msg);
        } else if (typeof msg === 'string' && (msg.toLowerCase().includes('помилка') || msg.toLowerCase().includes('не знайдено') || msg.toLowerCase().includes('не можете') || msg.toLowerCase().includes('вичерпано'))) {
            LoopToast.error(msg);
        } else {
            LoopDialog.alert(String(msg));
        }
    };

})(window, document);
