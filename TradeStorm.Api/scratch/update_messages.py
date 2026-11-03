import os

path = r'd:\LoopMarket\TradeStorm.Api\wwwroot\messages.html'
with open(path, 'r', encoding='utf-8') as f:
    c = f.read()

icons_def = '''
        const ICONS = {
            sun: '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="4"/><path d="M12 2v2"/><path d="M12 20v2"/><path d="m4.93 4.93 1.41 1.41"/><path d="m17.66 17.66 1.41 1.41"/><path d="M2 12h2"/><path d="M20 12h2"/><path d="m6.34 17.66-1.41 1.41"/><path d="m19.07 4.93-1.41 1.41"/></svg>',
            moon: '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z"/></svg>',
            bell: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:4px;"><path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9"/><path d="M10.3 21a1.94 1.94 0 0 0 3.4 0"/></svg>',
            user: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:4px;"><path d="M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>',
            chat: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg>',
            trash: '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:6px;"><path d="M3 6h18"/><path d="M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6"/><path d="M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2"/></svg>',
            camera: '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M23 19a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4l2-3h6l2 3h4a2 2 0 0 1 2 2z"/><circle cx="12" cy="13" r="4"/></svg>',
            send: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><line x1="22" y1="2" x2="11" y2="13"/><polygon points="22 2 15 22 11 13 2 9 22 2"/></svg>',
            starFilled: '<svg width="15" height="15" viewBox="0 0 24 24" fill="#f59e0b" stroke="#f59e0b" stroke-width="1.5"><polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"/></svg>',
            starEmpty: '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="#cbd5e1" stroke-width="1.5"><polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"/></svg>',
            checkSingle: '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;"><path d="M20 6 9 17l-5-5"/></svg>',
            checkDouble: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="var(--primary-light)" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;"><path d="M18 6 7 17l-5-5"/><path d="m22 10-7.5 7.5L13 16"/></svg>',
            refresh: '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:6px;"><path d="M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8"/><path d="M21 3v5h-5"/><path d="M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16"/><path d="M3 21v-5h5"/></svg>'
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

c = c.replace('<a href="/messages.html" id="navChat" class="active">💬 Чати</a>',
              '<a href="/messages.html" id="navChat" class="active"><svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:4px;"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg> Чати</a>')

c = c.replace('<a href="/safety.html" id="navSafety">🛡️ Безпека</a>',
              '<a href="/safety.html" id="navSafety"><svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:4px;"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/></svg> Безпека</a>')

# Theme icon
c = c.replace('<span id="themeIcon">🌙</span>', '<span id="themeIcon"></span>')

# Left side-cards
c = c.replace('<div class="card-title">⚡ Корисні розділи</div>',
              '<div class="card-title"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="var(--primary-light)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:6px;"><path d="m13 2-2 10h5l-2 10 9-12h-5l2-8z"/></svg> Корисні розділи</div>')

c = c.replace('<span>🛡️ Правила безпеки</span>',
              '<span><svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:4px;"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/><path d="m9 12 2 2 4-4"/></svg> Правила безпеки</span>')

c = c.replace('<span>📄 Умови користування</span>',
              '<span><svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:4px;"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" x2="8" y1="13" y2="13"/><line x1="16" x2="8" y1="17" y2="17"/></svg> Умови користування</span>')

c = c.replace('<span>ℹ️ Про розробників</span>',
              '<span><svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:4px;"><circle cx="12" cy="12" r="10"/><line x1="12" x2="12" y1="16" y2="12"/><line x1="12" x2="12.01" y1="8" y2="8"/></svg> Про розробників</span>')

c = c.replace('<div class="card-title" style="justify-content: center;">⏳ Час на кампусі</div>',
              '<div class="card-title" style="justify-content: center;"><svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:6px;"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg> Час на кампусі</div>')

# Search icon
c = c.replace('<span class="search-icon">🔍</span>',
              '<span class="search-icon"><svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/></svg></span>')

# Delete chat button
c = c.replace('<button id="btnDeleteChat" class="btn-delete-chat" onclick="deleteChat()">🗑️ Видалити чат</button>',
              '<button id="btnDeleteChat" class="btn-delete-chat" onclick="deleteChat()"><svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:4px;"><path d="M3 6h18"/><path d="M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6"/><path d="M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2"/></svg> Видалити чат</button>')

# Chat area placeholder
c = c.replace('Оберіть співрозмовника зліва, щоб розпочати живе спілкування 🚀',
              'Оберіть співрозмовника зі списку ліворуч, щоб переглянути листування')

# Review stars and button
c = c.replace('<span id="averageRatingStars" style="color: #fbbf24;">★★★★★</span>',
              '<span id="averageRatingStars" style="display:inline-flex;align-items:center;gap:2px;"></span>')

c = c.replace('⭐ Оцінити',
              '<svg width="14" height="14" viewBox="0 0 24 24" fill="#f59e0b" stroke="#f59e0b" stroke-width="1.5" style="vertical-align:-2px;margin-right:4px;"><polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"/></svg> Оцінити')

# Quick chips
c = c.replace('👋 Актуально?', 'Актуально?')
c = c.replace('🏷️ Можна торг?', 'Можливий торг?')
c = c.replace('📍 Де зустрінемося?', 'Де зустрінемось?')
c = c.replace('📦 Відправите поштою?', 'Відправка поштою?')

# Input buttons
c = c.replace('<button class="attach-btn" title="Додати фото" onclick="document.getElementById(\'photoInput\').click()">📷</button>',
              '<button class="attach-btn" title="Додати фото" onclick="document.getElementById(\'photoInput\').click()"><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M23 19a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4l2-3h6l2 3h4a2 2 0 0 1 2 2z"/><circle cx="12" cy="13" r="4"/></svg></button>')

c = c.replace('<button class="btn-send" onclick="sendMessage()">➤</button>',
              '<button class="btn-send" onclick="sendMessage()"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><line x1="22" y1="2" x2="11" y2="13"/><polygon points="22 2 15 22 11 13 2 9 22 2"/></svg></button>')

# Right side cards
c = c.replace('<div class="card-title" style="color: #fbbf24; font-size: 1.15rem; margin-bottom: 12px; justify-content: center;">🔥 Цікаве на кампусі</div>',
              '<div class="card-title" style="color: #fbbf24; font-size: 1.05rem; margin-bottom: 12px; justify-content: center;"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#f59e0b" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:6px;"><polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"/></svg> Популярне на кампусі</div>')

c = c.replace('<div class="card-title" style="justify-content: center;">💡 Студентський муд</div>',
              '<div class="card-title" style="justify-content: center;"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="var(--primary-light)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:6px;"><line x1="9" x2="15" y1="18" y2="18"/><line x1="10" x2="14" y1="22" y2="22"/><path d="M15.09 14c.18-.98.65-1.74 1.41-2.5A4.65 4.65 0 0 0 18 8 6 6 0 0 0 6 8c0 1 .23 2.23 1.5 3.5A4.61 4.61 0 0 1 8.91 14"/></svg> Студентська думка</div>')

c = c.replace('🔄 Інший факт',
              '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:6px;"><path d="M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8"/><path d="M21 3v5h-5"/><path d="M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16"/><path d="M3 21v-5h5"/></svg> Наступна думка')

# Logout modal icon
c = c.replace('<div class="modal-icon">👋</div>',
              '<div class="modal-icon"><svg width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="var(--primary-light)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><polyline points="16 17 21 12 16 7"/><line x1="21" x2="9" y1="12" y2="12"/></svg></div>')

# Modal options
c = c.replace('<option value="5">⭐⭐⭐⭐⭐ (5 - Відмінно)</option>', '<option value="5">5 / 5 (Відмінно)</option>')
c = c.replace('<option value="4">⭐⭐⭐⭐ (4 - Добре)</option>', '<option value="4">4 / 5 (Добре)</option>')
c = c.replace('<option value="3">⭐⭐⭐ (3 - Нормально)</option>', '<option value="3">3 / 5 (Нормально)</option>')
c = c.replace('<option value="2">⭐⭐ (2 - Погано)</option>', '<option value="2">2 / 5 (Погано)</option>')
c = c.replace('<option value="1">⭐ (1 - Жахливо)</option>', '<option value="1">1 / 5 (Жахливо)</option>')

# JS updates
c = c.replace("if (iconEl) iconEl.innerText = (theme === 'dark') ? '☀️' : '🌙';",
              "if (iconEl) iconEl.innerHTML = (theme === 'dark') ? ICONS.sun : ICONS.moon;")

c = c.replace("🔔 Сповіщення", "${ICONS.bell} Сповіщення")
c = c.replace("👤 ${name}", "${ICONS.user} ${name}")
c = c.replace("<span>💬</span>", "<span>${ICONS.chat}</span>")

c = c.replace("const stars = '★'.repeat(Math.round(data.averageRating)) + '☆'.repeat(5 - Math.round(data.averageRating));",
              "const stars = renderStars(data.averageRating);")
c = c.replace("document.getElementById('averageRatingStars').innerText = stars;",
              "document.getElementById('averageRatingStars').innerHTML = stars;")

c = c.replace("<span style=\"color: #fbbf24;\">${'★'.repeat(r.rating)}${'☆'.repeat(5 - r.rating)}</span>",
              "<span style=\"display:inline-flex;align-items:center;gap:2px;\">${renderStars(r.rating)}</span>")

c = c.replace("Історія порожня. Напишіть перше повідомлення або оберіть підказку нижче 👇",
              "Історія листування порожня. Надішліть перше повідомлення")

c = c.replace("statusHtml = `<span class=\"msg-status\">${isRead ? '✓✓' : '✓'}</span>`;",
              "statusHtml = `<span class=\"msg-status\" title=\"${isRead ? 'Прочитано' : 'Надіслано'}\">${isRead ? ICONS.checkDouble : ICONS.checkSingle}</span>`;")

# Insert ICONS before updateThemeButton
c = c.replace("function updateThemeButton(theme) {", icons_def + "\n        function updateThemeButton(theme) {")

with open(path, 'w', encoding='utf-8') as f:
    f.write(c)

print('Updated messages.html successfully')
