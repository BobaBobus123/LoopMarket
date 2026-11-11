/**
 * LoopMarket Push Notifications System
 * Modern in-app Toast / Push alerts with audio chimes & smooth physics animations
 */

(function () {
    function getDismissedNotificationIds() {
        try {
            return new Set(JSON.parse(localStorage.getItem('loop_dismissed_notifications') || '[]'));
        } catch(e) {
            return new Set();
        }
    }

    function markNotificationDismissed(id) {
        if (!id) return;
        try {
            const list = JSON.parse(localStorage.getItem('loop_dismissed_notifications') || '[]');
            if (!list.includes(id)) {
                list.push(id);
                if (list.length > 250) list.shift();
                localStorage.setItem('loop_dismissed_notifications', JSON.stringify(list));
            }
        } catch(e) {}

        const token = localStorage.getItem('loopToken') || localStorage.getItem('token');
        if (token && (typeof id === 'number' || (typeof id === 'string' && !id.startsWith('notif_')))) {
            fetch('/api/notifications/' + id + '/read', {
                method: 'PUT',
                headers: { 'Authorization': 'Bearer ' + token }
            }).catch(() => {});
        }
    }

    if (window.LoopPushNotifications) return;

    // 1. Inject Styles
    const style = document.createElement('style');
    style.id = 'loopPushStyles';
    style.innerHTML = `
        #loopPushContainer {
            position: fixed;
            top: 24px;
            right: 24px;
            z-index: 999999;
            display: flex;
            flex-direction: column;
            gap: 12px;
            pointer-events: none;
            max-width: 400px;
            width: calc(100vw - 36px);
            font-family: 'Plus Jakarta Sans', system-ui, -apple-system, sans-serif;
        }

        .loop-push-toast {
            pointer-events: auto;
            position: relative;
            background: rgba(18, 25, 41, 0.96);
            backdrop-filter: blur(20px);
            -webkit-backdrop-filter: blur(20px);
            border: 1.5px solid rgba(59, 130, 246, 0.35);
            border-radius: 16px;
            box-shadow: 0 16px 36px -4px rgba(0, 0, 0, 0.6), 0 0 25px rgba(37, 99, 235, 0.18);
            padding: 14px 16px 16px;
            display: flex;
            align-items: flex-start;
            gap: 14px;
            cursor: pointer;
            overflow: hidden;
            transform: translateX(110%);
            opacity: 0;
            animation: loopPushIn 0.38s cubic-bezier(0.16, 1, 0.3, 1) forwards;
            transition: transform 0.25s ease, opacity 0.25s ease, box-shadow 0.2s ease;
        }

        .loop-push-toast:hover {
            transform: translateY(-2px) scale(1.01);
            box-shadow: 0 20px 42px -4px rgba(0, 0, 0, 0.7), 0 0 30px rgba(59, 130, 246, 0.3);
            border-color: rgba(96, 165, 250, 0.6);
        }

        .loop-push-toast.hiding {
            animation: loopPushOut 0.3s cubic-bezier(0.7, 0, 0.84, 0) forwards;
        }

        @keyframes loopPushIn {
            0% { transform: translateX(110%) scale(0.92); opacity: 0; }
            100% { transform: translateX(0) scale(1); opacity: 1; }
        }

        @keyframes loopPushOut {
            0% { transform: translateX(0) scale(1); opacity: 1; }
            100% { transform: translateX(120%) scale(0.9); opacity: 0; }
        }

        .loop-push-icon {
            width: 44px;
            height: 44px;
            border-radius: 12px;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 1.35rem;
            flex-shrink: 0;
            box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
            position: relative;
        }

        .loop-push-icon.message {
            background: linear-gradient(135deg, #2563eb 0%, #1d4ed8 100%);
            border: 1px solid rgba(96, 165, 250, 0.4);
        }

        .loop-push-icon.order {
            background: linear-gradient(135deg, #10b981 0%, #059669 100%);
            border: 1px solid rgba(52, 211, 153, 0.4);
        }

        .loop-push-icon.delivery {
            background: linear-gradient(135deg, #f59e0b 0%, #d97706 100%);
            border: 1px solid rgba(251, 191, 36, 0.4);
        }

        .loop-push-icon.system {
            background: linear-gradient(135deg, #6366f1 0%, #4f46e5 100%);
            border: 1px solid rgba(165, 180, 252, 0.4);
        }

        .loop-push-content {
            flex: 1;
            min-width: 0;
        }

        .loop-push-header {
            display: flex;
            align-items: center;
            justify-content: space-between;
            margin-bottom: 4px;
        }

        .loop-push-badge {
            font-size: 0.72rem;
            font-weight: 800;
            letter-spacing: 0.5px;
            text-transform: uppercase;
            color: #60a5fa;
            display: flex;
            align-items: center;
            gap: 5px;
        }

        .loop-push-badge::before {
            content: '';
            width: 6px;
            height: 6px;
            border-radius: 50%;
            background: #60a5fa;
            box-shadow: 0 0 8px #60a5fa;
            animation: pushDotPulse 1.8s infinite;
        }

        @keyframes pushDotPulse {
            0% { transform: scale(0.9); opacity: 0.6; }
            50% { transform: scale(1.3); opacity: 1; }
            100% { transform: scale(0.9); opacity: 0.6; }
        }

        .loop-push-close {
            background: transparent;
            border: none;
            color: #94a3b8;
            font-size: 1.15rem;
            cursor: pointer;
            padding: 0 4px;
            line-height: 1;
            border-radius: 6px;
            transition: color 0.15s;
        }

        .loop-push-close:hover {
            color: #f8fafc;
            background: rgba(255, 255, 255, 0.1);
        }

        .loop-push-title {
            font-size: 0.94rem;
            font-weight: 800;
            color: #f8fafc;
            margin: 0 0 3px 0;
            line-height: 1.3;
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
        }

        .loop-push-message {
            font-size: 0.82rem;
            color: #cbd5e1;
            margin: 0;
            line-height: 1.4;
            display: -webkit-box;
            -webkit-line-clamp: 2;
            -webkit-box-orient: vertical;
            overflow: hidden;
        }

        .loop-push-progress {
            position: absolute;
            bottom: 0;
            left: 0;
            height: 3px;
            background: linear-gradient(90deg, #3b82f6, #10b981);
            width: 100%;
            transform-origin: left;
            animation: pushProgressLinear 5s linear forwards;
        }

        @keyframes pushProgressLinear {
            0% { transform: scaleX(1); }
            100% { transform: scaleX(0); }
        }
    `;
    document.head.appendChild(style);

    // 2. Synthesized Modern Web Audio Chime (No external audio file required)
    function playChime() {
        try {
            const AudioCtx = window.AudioContext || window.webkitAudioContext;
            if (!AudioCtx) return;
            const ctx = new AudioCtx();
            const now = ctx.currentTime;

            // Tone 1: Bright harmonic (E5 - 659Hz)
            const osc1 = ctx.createOscillator();
            const gain1 = ctx.createGain();
            osc1.type = 'sine';
            osc1.frequency.setValueAtTime(659.25, now);
            gain1.gain.setValueAtTime(0.12, now);
            gain1.gain.exponentialRampToValueAtTime(0.0001, now + 0.35);
            osc1.connect(gain1);
            gain1.connect(ctx.destination);
            osc1.start(now);
            osc1.stop(now + 0.35);

            // Tone 2: Warm upper chime (A5 - 880Hz)
            const osc2 = ctx.createOscillator();
            const gain2 = ctx.createGain();
            osc2.type = 'sine';
            osc2.frequency.setValueAtTime(880, now + 0.09);
            gain2.gain.setValueAtTime(0.16, now + 0.09);
            gain2.gain.exponentialRampToValueAtTime(0.0001, now + 0.5);
            osc2.connect(gain2);
            gain2.connect(ctx.destination);
            osc2.start(now + 0.09);
            osc2.stop(now + 0.5);
        } catch (e) {
            // Autoplay blocked by browser policy until first click
        }
    }

    // 3. Container Element
    function getContainer() {
        let container = document.getElementById('loopPushContainer');
        if (!container) {
            container = document.createElement('div');
            container.id = 'loopPushContainer';
            document.body.appendChild(container);
        }
        return container;
    }

    // 4. Core Toast Function
    function showToast(options) {
        const {
            id = 'notif_' + Date.now(),
            title = 'Нове сповіщення',
            message = '',
            link = null,
            duration = 5000,
            sound = true
        } = options;

        const container = getContainer();

        // Determine icon & category
        let iconHtml = '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9"/><path d="M10.3 21a1.94 1.94 0 0 0 3.4 0"/></svg>';
        let iconType = 'system';
        const lowerT = (title + ' ' + message).toLowerCase();

        if (lowerT.includes('повідомлен') || lowerT.includes('написав')) {
            iconHtml = '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg>';
            iconType = 'message';
        } else if (lowerT.includes('замовленн') || lowerT.includes('купівл')) {
            iconHtml = '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16Z"/><path d="m3.3 7 8.7 5 8.7-5"/><path d="M12 22V12"/></svg>';
            iconType = 'order';
        } else if (lowerT.includes('відправлен') || lowerT.includes('ттн') || lowerT.includes('пошт')) {
            iconHtml = '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 18V6a2 2 0 0 0-2-2H4a2 2 0 0 0-2 2v11a1 1 0 0 0 1 1h2"/><path d="M15 18H9"/><path d="M19 18h2a1 1 0 0 0 1-1v-3.65a1 1 0 0 0-.22-.624l-3.48-4.35A1 1 0 0 0 17.52 8H14"/><circle cx="17" cy="18" r="2"/><circle cx="7" cy="18" r="2"/></svg>';
            iconType = 'delivery';
        }

        const toast = document.createElement('div');
        toast.className = 'loop-push-toast';
        toast.setAttribute('data-id', id);

        toast.innerHTML = `
            <div class="loop-push-icon ${iconType}">${iconHtml}</div>
            <div class="loop-push-content">
                <div class="loop-push-header">
                    <span class="loop-push-badge">LoopMarket • Сповіщення</span>
                    <button type="button" class="loop-push-close" title="Закрити">&times;</button>
                </div>
                <h4 class="loop-push-title">${title}</h4>
                <p class="loop-push-message">${message}</p>
            </div>
            <div class="loop-push-progress" style="animation-duration: ${duration}ms;"></div>
        `;

        if (sound) {
            playChime();
        }

        // Close logic
                function dismiss() {
            if (toast.classList.contains('hiding')) return;
            toast.classList.add('hiding');
            if (id) {
                markNotificationDismissed(id);
            }
            setTimeout(() => {
                if (toast.parentNode) toast.parentNode.removeChild(toast);
            }, 300);
        }

        // Click close button
        const closeBtn = toast.querySelector('.loop-push-close');
        if (closeBtn) {
            closeBtn.onclick = (e) => {
                e.stopPropagation();
                dismiss();
            };
        }

        // Click card body
        toast.onclick = () => {
            dismiss();
            if (link && link !== '#') {
                window.location.href = link;
            }
        };

        container.appendChild(toast);

        // Auto-dismiss timer
        let autoTimer = setTimeout(dismiss, duration);
        toast.onmouseenter = () => {
            clearTimeout(autoTimer);
            const pb = toast.querySelector('.loop-push-progress');
            if (pb) pb.style.animationPlayState = 'paused';
        };
        toast.onmouseleave = () => {
            autoTimer = setTimeout(dismiss, 2500);
            const pb = toast.querySelector('.loop-push-progress');
            if (pb) pb.style.animationPlayState = 'running';
        };
    }

        // Helper: Get current user info from JWT
    function getCurrentUser() {
        try {
            const token = localStorage.getItem('loopToken');
            if (!token) return null;
            const parts = token.split('.');
            if (parts.length < 2) return null;
            const payload = JSON.parse(atob(parts[1]));
            const uid = parseInt(payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] || payload.id || payload.sub || 0);
            const name = payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'] || payload.name || payload.unique_name || '';
            return { id: uid, name: name };
        } catch(e) {
            return null;
        }
    }

    // 5. Automatic In-App Polling & Real-time Delivery
    const seenNotifications = new Set();
    let isInitialLoad = true;

    async function checkNotifications() {
        const token = localStorage.getItem('loopToken');
        if (!token) return;

        try {
            const res = await fetch('/api/notifications', {
                headers: { 'Authorization': 'Bearer ' + token }
            });
            if (!res.ok) return;

            const notifs = await res.json();
            if (!Array.isArray(notifs)) return;

            // Update header badge if present
            const unreadCount = notifs.filter(n => !n.isRead).length;
            const badge = document.getElementById('globalUnreadBadge');
            if (badge) {
                if (unreadCount > 0) {
                    badge.innerText = unreadCount;
                    badge.style.display = 'inline-block';
                } else {
                    badge.style.display = 'none';
                }
            }

            // On first page load, register existing IDs without popping all historic toasts
            if (isInitialLoad) {
                notifs.forEach(n => seenNotifications.add(n.id));
                isInitialLoad = false;
                return;
            }

            const me = getCurrentUser();

            // When new notifications arrive while user is sitting on the site:
                        const dismissedIds = getDismissedNotificationIds();
            notifs.forEach(n => {
                if (!seenNotifications.has(n.id)) {
                    seenNotifications.add(n.id);
                    if (!n.isRead && !dismissedIds.has(n.id)) {
                        // Double safeguard: don't show self-notifications
                        if (me && me.name && n.message && n.message.includes(`Вам написав(ла) ${me.name}`)) {
                            return;
                        }

                        // If user is already on messages.html chatting with this exact partner, don't obstruct screen
                        const isMessagesPage = window.location.pathname.includes('messages.html');
                        const activePartner = (typeof activeReceiverId !== 'undefined') ? activeReceiverId : null;
                        if (isMessagesPage && activePartner && n.link && n.link.includes(`partner=${activePartner}`)) {
                            // User is already reading this chat in real time
                            return;
                        }

                        showToast({
                            id: n.id,
                            title: n.title,
                            message: n.message,
                            link: n.link,
                            sound: true
                        });
                    }
                }
            });
        } catch (err) {
            console.debug('Push polling error', err);
        }
    }

    // Start background check every 4.5 seconds
    setInterval(checkNotifications, 4500);
    setTimeout(checkNotifications, 600);

    // Export API
    window.LoopPushNotifications = {
        show: showToast,
        playChime: playChime,
        checkNow: checkNotifications,
        test: function () {
            showToast({
                title: 'Нове сповіщення LoopMarket',
                message: 'Ваше замовлення #108 успішно підтверджено продавцем! Посилка готова до відправлення.',
                link: '/profile.html?tab=orders',
                duration: 5000,
                sound: true
            });
        }
    };
})();
