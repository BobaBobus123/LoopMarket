import os

path = r'd:\LoopMarket\TradeStorm.Api\wwwroot\profile.html'
with open(path, 'r', encoding='utf-8') as f:
    c = f.read()

icons_def = '''
        const ICONS = {
            sun: '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="4"/><path d="M12 2v2"/><path d="M12 20v2"/><path d="m4.93 4.93 1.41 1.41"/><path d="m17.66 17.66 1.41 1.41"/><path d="M2 12h2"/><path d="M20 12h2"/><path d="m6.34 17.66-1.41 1.41"/><path d="m19.07 4.93-1.41 1.41"/></svg>',
            moon: '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z"/></svg>',
            bell: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:4px;"><path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9"/><path d="M10.3 21a1.94 1.94 0 0 0 3.4 0"/></svg>',
            user: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:4px;"><path d="M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>',
            chat: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg>',
            edit: '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:4px;"><path d="M17 3a2.85 2.83 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5Z"/><path d="m15 5 4 4"/></svg>',
            trash: '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:4px;"><path d="M3 6h18"/><path d="M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6"/><path d="M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2"/></svg>',
            starFilled: '<svg width="14" height="14" viewBox="0 0 24 24" fill="#f59e0b" stroke="#f59e0b" stroke-width="1.5"><polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"/></svg>',
            starEmpty: '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#cbd5e1" stroke-width="1.5"><polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"/></svg>',
            heartFilled: '<svg width="16" height="16" viewBox="0 0 24 24" fill="#ef4444" stroke="#ef4444" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7Z"/></svg>',
            package: '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:4px;"><path d="m7.5 4.27 9 5.15"/><path d="M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16Z"/><path d="m3.3 7 8.7 5 8.7-5"/><path d="M12 22V12"/></svg>'
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

# Clean code comments
c = c.replace('/* 🚀 ', '/* ')
c = c.replace('<!-- 🚀 ', '<!-- ')
c = c.replace('// 🚀 ', '// ')

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

# Avatar
c = c.replace('<div class="avatar" id="userAvatar">🧑‍🎓</div>',
              '<div class="avatar" id="userAvatar"><svg width="44" height="44" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg></div>')

# Avatar camera icon
c = c.replace('<label class="avatar-upload-btn" title="Змінити фото (ліміт: 3 рази на місяць)">\n                    📷',
              '<label class="avatar-upload-btn" title="Змінити фото (ліміт: 3 рази на місяць)">\n                    <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M23 19a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4l2-3h6l2 3h4a2 2 0 0 1 2 2z"/><circle cx="12" cy="13" r="4"/></svg>')

# Sidebar stars
c = c.replace('<div style="color: #fbbf24; font-size: 0.85rem;" id="sidebarStars">★★★★★</div>',
              '<div id="sidebarStars" style="display:inline-flex;align-items:center;gap:2px;"></div>')

# Tabs
c = c.replace('<div class="tab active" id="tabMyListings" onclick="switchTab(\'myListings\')">📝 Мої оголошення</div>',
              '<div class="tab active" id="tabMyListings" onclick="switchTab(\'myListings\')"><svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:6px;"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" x2="8" y1="13" y2="13"/><line x1="16" x2="8" y1="17" y2="17"/></svg> Мої оголошення</div>')

c = c.replace('<div class="tab" id="tabFavorites" onclick="switchTab(\'favorites\')">❤️ Обране</div>',
              '<div class="tab" id="tabFavorites" onclick="switchTab(\'favorites\')"><svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:6px;"><path d="M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7Z"/></svg> Обране</div>')

c = c.replace('<div class="tab" id="tabReviews" onclick="switchTab(\'reviews\')">⭐ Відгуки</div>',
              '<div class="tab" id="tabReviews" onclick="switchTab(\'reviews\')"><svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:6px;"><polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"/></svg> Відгуки</div>')

c = c.replace('<div class="tab" id="tabSettings" onclick="switchTab(\'settings\')">⚙️ Налаштування</div>',
              '<div class="tab" id="tabSettings" onclick="switchTab(\'settings\')"><svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:6px;"><path d="M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z"/><circle cx="12" cy="12" r="3"/></svg> Налаштування</div>')

# Telegram block
c = c.replace('✅ Акаунт успішно підключено та верифіковано!',
              '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="var(--primary-light)" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:6px;"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/><path d="m9 12 2 2 4-4"/></svg> Акаунт успішно підключено та верифіковано!')

c = c.replace('🤖 Відкрити @LoopMarket_Verify_bot',
              '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:6px;"><path d="m22 2-7 20-4-9-9-4Z"/><path d="M22 2 11 13"/></svg> Відкрити @LoopMarket_Verify_bot')

# Logout modal icon
c = c.replace('<div class="modal-icon">👋</div>',
              '<div class="modal-icon"><svg width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="var(--primary-light)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><polyline points="16 17 21 12 16 7"/><line x1="21" x2="9" y1="12" y2="12"/></svg></div>')

# Alert
c = c.replace("alert('⚠️ Ліміт вичерпано!", "alert('Ліміт вичерпано!")

# JS updates
c = c.replace("if (iconEl) iconEl.innerText = (theme === 'dark') ? '☀️' : '🌙';",
              "if (iconEl) iconEl.innerHTML = (theme === 'dark') ? ICONS.sun : ICONS.moon;")

c = c.replace("🔔 Сповіщення", "${ICONS.bell} Сповіщення")
c = c.replace("👤 ${userName}", "${ICONS.user} ${userName}")

c = c.replace("document.getElementById('sidebarStars').innerText = '★'.repeat(Math.round(data.averageRating)) + '☆'.repeat(5 - Math.round(data.averageRating));",
              "document.getElementById('sidebarStars').innerHTML = renderStars(data.averageRating);")

c = c.replace("<span style=\"color: #fbbf24; font-size: 0.9rem;\">${'★'.repeat(r.rating)}${'☆'.repeat(5 - r.rating)}</span>",
              "<span style=\"display:inline-flex;align-items:center;gap:2px;\">${renderStars(r.rating)}</span>")

c = c.replace("📦 ${r.listing.title}", "${ICONS.package} ${r.listing.title}")

c = c.replace("✏️ Змінити", "${ICONS.edit} Змінити")
c = c.replace("🗑️ Видалити", "${ICONS.trash} Видалити")
c = c.replace("🗑 Видалити", "${ICONS.trash} Видалити")

c = c.replace('<button onclick="toggleFavorite(${item.id}, event)" class="btn-fav">❤️</button>',
              '<button onclick="toggleFavorite(${item.id}, event)" class="btn-fav" title="Видалити з обраного">${ICONS.heartFilled}</button>')

# Insert ICONS before updateThemeButton
c = c.replace("function updateThemeButton(theme) {", icons_def + "\n        function updateThemeButton(theme) {")

with open(path, 'w', encoding='utf-8') as f:
    f.write(c)

print('Updated profile.html successfully')
