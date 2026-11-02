import os

path = r'd:\LoopMarket\TradeStorm.Api\wwwroot\details.html'
with open(path, 'r', encoding='utf-8') as f:
    c = f.read()

icons_def = '''
        const ICONS = {
            sun: '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="4"/><path d="M12 2v2"/><path d="M12 20v2"/><path d="m4.93 4.93 1.41 1.41"/><path d="m17.66 17.66 1.41 1.41"/><path d="M2 12h2"/><path d="M20 12h2"/><path d="m6.34 17.66-1.41 1.41"/><path d="m19.07 4.93-1.41 1.41"/></svg>',
            moon: '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z"/></svg>',
            bell: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:4px;"><path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9"/><path d="M10.3 21a1.94 1.94 0 0 0 3.4 0"/></svg>',
            user: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:4px;"><path d="M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>',
            chat: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg>',
            heartOutline: '<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7Z"/></svg>',
            heartFilled: '<svg width="22" height="22" viewBox="0 0 24 24" fill="#ef4444" stroke="#ef4444" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7Z"/></svg>',
            pin: '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:6px;"><path d="M20 10c0 6-8 12-8 12s-8-6-8-12a8 8 0 0 1 16 0Z"/><circle cx="12" cy="10" r="3"/></svg>',
            package: '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:6px;"><path d="m7.5 4.27 9 5.15"/><path d="M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16Z"/><path d="m3.3 7 8.7 5 8.7-5"/><path d="M12 22V12"/></svg>',
            starFilled: '<svg width="15" height="15" viewBox="0 0 24 24" fill="#f59e0b" stroke="#f59e0b" stroke-width="1.5"><polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"/></svg>',
            starEmpty: '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="#cbd5e1" stroke-width="1.5"><polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"/></svg>',
            edit: '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:6px;"><path d="M17 3a2.85 2.83 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5Z"/><path d="m15 5 4 4"/></svg>',
            trash: '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:6px;"><path d="M3 6h18"/><path d="M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6"/><path d="M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2"/></svg>',
            phone: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:8px;"><path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72 12.84 12.84 0 0 0 .7 2.81 2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45 12.84 12.84 0 0 0 2.81.7A2 2 0 0 1 22 16.92z"/></svg>',
            chatBtn: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:8px;"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg>',
            clock: '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:6px;"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>',
            shield: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="var(--primary-light)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:6px;"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/><path d="m9 12 2 2 4-4"/></svg>',
            verifiedStudent: '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="var(--primary-light)" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:4px;"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/><path d="m9 12 2 2 4-4"/></svg>',
            sparkles: '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:6px;"><path d="m12 3-1.9 5.8a2 2 0 0 1-1.3 1.3L3 12l5.8 1.9a2 2 0 0 1 1.3 1.3L12 21l1.9-5.8a2 2 0 0 1 1.3-1.3L21 12l-5.8-1.9a2 2 0 0 1-1.3-1.3Z"/></svg>'
        };

        function renderStars(rating) {
            const count = Math.round(rating) || 0;
            let html = '';
            for (let i = 1; i <= 5; i++) {
                html += (i <= count) ? ICONS.starFilled : ICONS.starEmpty;
            }
            return html;
        }
'''

# Nav links
c = c.replace('<a href="/index.html#vip" id="navHot">🔥 Гаряче</a>',
              '<a href="/index.html#vip" id="navHot"><svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:4px;"><polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"/></svg> Топ-оголошення</a>')

c = c.replace('<a href="/profile.html#favorites" id="navFav">❤️ Обране</a>',
              '<a href="/profile.html#favorites" id="navFav"><svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:4px;"><path d="M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7Z"/></svg> Обране</a>')

c = c.replace('<a href="/messages.html" id="navChat">💬 Чати</a>',
              '<a href="/messages.html" id="navChat"><svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:4px;"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg> Чати</a>')

c = c.replace('<a href="/safety.html" id="navSafety">🛡️ Безпека</a>',
              '<a href="/safety.html" id="navSafety"><svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:4px;"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/></svg> Безпека</a>')

# Theme icon
c = c.replace('<span id="themeIcon">🌙</span>', '<span id="themeIcon"></span>')

# Notif item
c = c.replace('<span>💬</span>',
              '<span><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg></span>')

# Favorite button initial HTML
c = c.replace('<button id="detailFavBtn" class="btn-fav-large" onclick="toggleDetailFavorite()" title="Додати в обране">🤍</button>',
              '<button id="detailFavBtn" class="btn-fav-large" onclick="toggleDetailFavorite()" title="Додати в обране"><svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7Z"/></svg></button>')

# Specs
c = c.replace('<div class="spec-item">📍 <strong id="itemLocation"></strong></div>',
              '<div class="spec-item"><svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:6px;"><path d="M20 10c0 6-8 12-8 12s-8-6-8-12a8 8 0 0 1 16 0Z"/><circle cx="12" cy="10" r="3"/></svg><strong id="itemLocation"></strong></div>')

c = c.replace('<div class="spec-item">✨ <span id="itemCondition"></span></div>',
              '<div class="spec-item"><svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:6px;"><path d="m12 3-1.9 5.8a2 2 0 0 1-1.3 1.3L3 12l5.8 1.9a2 2 0 0 1 1.3 1.3L12 21l1.9-5.8a2 2 0 0 1 1.3-1.3L21 12l-5.8-1.9a2 2 0 0 1-1.3-1.3Z"/></svg><span id="itemCondition"></span></div>')

c = c.replace('<div class="spec-item">🚚 <span id="itemShipping"></span></div>',
              '<div class="spec-item"><svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:6px;"><path d="m7.5 4.27 9 5.15"/><path d="M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16Z"/><path d="m3.3 7 8.7 5 8.7-5"/><path d="M12 22V12"/></svg><span id="itemShipping"></span></div>')

c = c.replace('🕒 <span id="itemDate"></span>',
              '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:6px;"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg><span id="itemDate"></span>')

# Avatar
c = c.replace('<div class="seller-avatar" id="avatarBox">👤</div>',
              '<div class="seller-avatar" id="avatarBox"><svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg></div>')

# Verified badge
c = c.replace('✓ Перевірений студент',
              '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="var(--primary-light)" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:4px;"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/><path d="m9 12 2 2 4-4"/></svg>Перевірений студент')

# Stars initial
c = c.replace('<span id="sellerAvgStars" style="color: #fbbf24; font-size: 1rem; letter-spacing: 1px;">★★★★★</span>',
              '<span id="sellerAvgStars" style="display:inline-flex;align-items:center;gap:2px;"></span>')

# Rate button
c = c.replace('⭐ Оцінити',
              '<svg width="14" height="14" viewBox="0 0 24 24" fill="#f59e0b" stroke="#f59e0b" stroke-width="1.5" style="vertical-align:-2px;margin-right:4px;"><polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"/></svg> Оцінити')

# Safety rule box
c = c.replace('<h4>🛡️️ Правила безпеки</h4>',
              '<h4><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="var(--primary-light)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:6px;"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/><path d="m9 12 2 2 4-4"/></svg>Правила безпеки</h4>')
c = c.replace('<h4>🛡️ Правила безпеки</h4>',
              '<h4><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="var(--primary-light)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:6px;"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/><path d="m9 12 2 2 4-4"/></svg>Правила безпеки</h4>')

# Modal options
c = c.replace('<option value="5">⭐⭐⭐⭐⭐ (5 - Відмінно)</option>', '<option value="5">5 / 5 (Відмінно)</option>')
c = c.replace('<option value="4">⭐⭐⭐⭐ (4 - Добре)</option>', '<option value="4">4 / 5 (Добре)</option>')
c = c.replace('<option value="3">⭐⭐⭐ (3 - Нормально)</option>', '<option value="3">3 / 5 (Нормально)</option>')
c = c.replace('<option value="2">⭐⭐ (2 - Погано)</option>', '<option value="2">2 / 5 (Погано)</option>')
c = c.replace('<option value="1">⭐ (1 - Жахливо)</option>', '<option value="1">1 / 5 (Жахливо)</option>')

# Logout modal icon
c = c.replace('<div class="modal-icon">👋</div>',
              '<div class="modal-icon"><svg width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="var(--primary-light)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><polyline points="16 17 21 12 16 7"/><line x1="21" x2="9" y1="12" y2="12"/></svg></div>')

# JS script updates
c = c.replace("if (iconEl) iconEl.innerText = (theme === 'dark') ? '☀️' : '🌙';",
              "if (iconEl) iconEl.innerHTML = (theme === 'dark') ? ICONS.sun : ICONS.moon;")

c = c.replace("🔔 Сповіщення", "${ICONS.bell} Сповіщення")
c = c.replace("👤 ${userName}", "${ICONS.user} ${userName}")

c = c.replace("document.getElementById('detailFavBtn').innerHTML = favs.includes(currentListingId) ? '❤️' : '🤍';",
              "document.getElementById('detailFavBtn').innerHTML = favs.includes(currentListingId) ? ICONS.heartFilled : ICONS.heartOutline;")

c = c.replace("document.getElementById('detailFavBtn').innerHTML = isFav ? '🤍' : '❤️';",
              "document.getElementById('detailFavBtn').innerHTML = isFav ? ICONS.heartOutline : ICONS.heartFilled;")
c = c.replace("document.getElementById('detailFavBtn').innerHTML = isFav ? '🤍' : '❤️️';",
              "document.getElementById('detailFavBtn').innerHTML = isFav ? ICONS.heartOutline : ICONS.heartFilled;")

c = c.replace("✏️ Редагувати", "${ICONS.edit} Редагувати")
c = c.replace("🗑️ Видалити оголошення", "${ICONS.trash} Видалити оголошення")
c = c.replace("📞 Зателефонувати", "${ICONS.phone} Зателефонувати")
c = c.replace("💬 Написати продавцю", "${ICONS.chatBtn} Написати продавцю")

# Stars rendering in script
c = c.replace("document.getElementById('sellerAvgStars').innerText = '★'.repeat(Math.round(data.averageRating)) + '☆'.repeat(5 - Math.round(data.averageRating));",
              "document.getElementById('sellerAvgStars').innerHTML = renderStars(data.averageRating);")

c = c.replace("<span style=\"color: #fbbf24;\">${'★'.repeat(r.rating)}${'☆'.repeat(5 - r.rating)}</span>",
              "<span style=\"display:inline-flex;align-items:center;gap:2px;\">${renderStars(r.rating)}</span>")

c = c.replace("📦 ${r.listing.title}", "${ICONS.package} ${r.listing.title}")

# Insert ICONS before updateThemeButton
c = c.replace("function updateThemeButton(theme) {", icons_def + "\n        function updateThemeButton(theme) {")

with open(path, 'w', encoding='utf-8') as f:
    f.write(c)

print('Updated details.html successfully')
