import os

icons_def = '''
        const ICONS = {
            sun: '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="4"/><path d="M12 2v2"/><path d="M12 20v2"/><path d="m4.93 4.93 1.41 1.41"/><path d="m17.66 17.66 1.41 1.41"/><path d="M2 12h2"/><path d="M20 12h2"/><path d="m6.34 17.66-1.41 1.41"/><path d="m19.07 4.93-1.41 1.41"/></svg>',
            moon: '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z"/></svg>',
            bell: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:4px;"><path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9"/><path d="M10.3 21a1.94 1.94 0 0 0 3.4 0"/></svg>',
            user: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:4px;"><path d="M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>',
            chat: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg>',
            camera: '<svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="var(--primary-light)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M23 19a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4l2-3h6l2 3h4a2 2 0 0 1 2 2z"/><circle cx="12" cy="13" r="4"/></svg>',
            cancel: '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:4px;"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>'
        };
'''

for fname in ['create.html', 'edit.html']:
    path = os.path.join(r'd:\LoopMarket\TradeStorm.Api\wwwroot', fname)
    with open(path, 'r', encoding='utf-8') as f:
        c = f.read()

    # Clean comments
    c = c.replace('<!-- 🔥 ', '<!-- ')

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

    # Info tips header
    c = c.replace('<h3>💡 Як правильно створити успішне оголошення:</h3>',
                  '<h3><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="var(--primary-light)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-3px;margin-right:6px;"><line x1="9" x2="15" y1="18" y2="18"/><line x1="10" x2="14" y1="22" y2="22"/><path d="M15.09 14c.18-.98.65-1.74 1.41-2.5A4.65 4.65 0 0 0 18 8 6 6 0 0 0 6 8c0 1 .23 2.23 1.5 3.5A4.61 4.61 0 0 1 8.91 14"/></svg>Рекомендації для успішної публікації:</h3>')

    # Photo upload icon
    c = c.replace('<span style="font-size: 2rem;">📸</span>',
                  '<span style="display:inline-block;margin-bottom:6px;"><svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="var(--primary-light)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M23 19a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4l2-3h6l2 3h4a2 2 0 0 1 2 2z"/><circle cx="12" cy="13" r="4"/></svg></span>')

    # Cancel button icon
    c = c.replace('✕ Скасувати', '${ICONS.cancel} Скасувати')

    # Logout modal icon
    c = c.replace('<div class="modal-icon">👋</div>',
                  '<div class="modal-icon"><svg width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="var(--primary-light)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><polyline points="16 17 21 12 16 7"/><line x1="21" x2="9" y1="12" y2="12"/></svg></div>')

    # JS updates
    c = c.replace("if (iconEl) iconEl.innerText = (theme === 'dark') ? '☀️' : '🌙';",
                  "if (iconEl) iconEl.innerHTML = (theme === 'dark') ? ICONS.sun : ICONS.moon;")

    c = c.replace("🔔 Сповіщення", "${ICONS.bell} Сповіщення")
    c = c.replace("👤 ${userName}", "${ICONS.user} ${userName}")
    c = c.replace("<span>💬</span>", "<span>${ICONS.chat}</span>")

    # Insert ICONS before updateThemeButton
    c = c.replace("function updateThemeButton(theme) {", icons_def + "\n        function updateThemeButton(theme) {")

    with open(path, 'w', encoding='utf-8') as f:
        f.write(c)

    print(f'Updated {fname} successfully')
