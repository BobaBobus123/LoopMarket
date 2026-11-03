import os

path = r'd:\LoopMarket\TradeStorm.Api\wwwroot\login.html'
with open(path, 'r', encoding='utf-8') as f:
    c = f.read()

icons_def = '''
        const ICONS = {
            sun: '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="4"/><path d="M12 2v2"/><path d="M12 20v2"/><path d="m4.93 4.93 1.41 1.41"/><path d="m17.66 17.66 1.41 1.41"/><path d="M2 12h2"/><path d="M20 12h2"/><path d="m6.34 17.66-1.41 1.41"/><path d="m19.07 4.93-1.41 1.41"/></svg>',
            moon: '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z"/></svg>'
        };
'''

# Theme icon
c = c.replace('<span id="themeIcon">🌙</span>', '<span id="themeIcon"></span>')

# Feature items
c = c.replace('<div class="feature-icon">🛡️</div>',
              '<div class="feature-icon"><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="var(--primary-light)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/><path d="m9 12 2 2 4-4"/></svg></div>')

c = c.replace('<div class="feature-icon">💸</div>',
              '<div class="feature-icon"><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="var(--primary-light)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="20" height="14" x="2" y="5" rx="2"/><line x1="2" x2="22" y1="10" y2="10"/></svg></div>')

c = c.replace('<div class="feature-icon">🤝</div>',
              '<div class="feature-icon"><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="var(--primary-light)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M22 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg></div>')

c = c.replace('<div class="feature-icon">🤖</div>',
              '<div class="feature-icon"><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="var(--primary-light)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m22 2-7 20-4-9-9-4Z"/><path d="M22 2 11 13"/></svg></div>')

# Success icon
c = c.replace('<div style="font-size: 3.5rem; text-align: center; margin-bottom: 10px;">🎉</div>',
              '<div style="text-align: center; margin-bottom: 14px; color: var(--primary-light);"><svg width="52" height="52" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/><path d="m9 12 2 2 4-4"/></svg></div>')

# JS theme update
c = c.replace("if (iconEl) iconEl.innerText = (theme === 'dark') ? '☀️' : '🌙';",
              "if (iconEl) iconEl.innerHTML = (theme === 'dark') ? ICONS.sun : ICONS.moon;")

# JS verification status texts
c = c.replace('errorDiv.innerText = "⏳ Перевіряємо статус у Telegram...";',
              'errorDiv.innerText = "Перевіряємо статус верифікації у Telegram...";')

c = c.replace('errorDiv.innerText = "❌ Ви ще не підтвердили номер. Перейдіть у Telegram-бот та поділіться контактом!";',
              'errorDiv.innerText = "Номер ще не підтверджено. Будь ласка, перейдіть у Telegram-бот та надішліть контакт.";')

# Insert ICONS before updateThemeButton
c = c.replace("function updateThemeButton(theme) {", icons_def + "\n        function updateThemeButton(theme) {")

with open(path, 'w', encoding='utf-8') as f:
    f.write(c)

print('Updated login.html successfully')
