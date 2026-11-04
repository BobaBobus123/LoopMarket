import os
import re

wwwroot = r"D:\LoopMarket\TradeStorm.Api\wwwroot"

html_files = [
    "index.html",
    "details.html",
    "create.html",
    "edit.html",
    "profile.html",
    "messages.html",
    "login.html",
    "seller.html",
    "about.html",
    "safety.html",
    "terms.html",
    "docs.html"
]

print("Applying modern responsive design system across all pages...")

for fname in html_files:
    fpath = os.path.join(wwwroot, fname)
    if not os.path.exists(fpath):
        continue

    with open(fpath, "r", encoding="utf-8") as f:
        html = f.read()

    # 1. Ensure viewport meta is present with viewport-fit=cover
    if 'name="viewport"' in html:
        html = re.sub(
            r'<meta name="viewport" content="[^"]*">',
            '<meta name="viewport" content="width=device-width, initial-scale=1.0, viewport-fit=cover">',
            html
        )
    else:
        html = html.replace("<head>", '<head>\n    <meta name="viewport" content="width=device-width, initial-scale=1.0, viewport-fit=cover">')

    # 2. Link global.css in head if not already linked
    if 'href="/css/global.css"' not in html and 'href="css/global.css"' not in html:
        # insert before </head>
        html = html.replace('</head>', '    <link rel="stylesheet" href="/css/global.css">\n</head>')

    # 3. Add responsive-nav.js script before </body> if not present
    if 'src="/js/responsive-nav.js"' not in html and 'src="js/responsive-nav.js"' not in html:
        html = html.replace('</body>', '    <script src="/js/responsive-nav.js" defer></script>\n</body>')

    # 4. Fix hardcoded background-color: #0b0f19 on body
    # Replace background-color: #0b0f19; in body style with background-color: var(--bg-color);
    html = re.sub(r'body\s*\{\s*background-color:\s*#0b0f19;', 'body {\n            background-color: var(--bg-color);', html)

    # 5. Page-specific enhancements
    if fname == "index.html":
        # Add categories scrollbar if not already present
        scroll_bar_html = """
    <!-- ШВИДКИЙ МОБІЛЬНИЙ СКРОЛЕР КАТЕГОРІЙ -->
    <div class="container" style="padding-top: 10px; padding-bottom: 4px;">
        <div class="categories-scroll-wrapper" id="categoriesScrollWrapper">
            <div class="categories-scroll-list" id="categoryPillsList">
                <button type="button" class="cat-pill active" onclick="filterByCategory('', this)">✨ Всі товари</button>
            </div>
        </div>
    </div>
"""
        if 'id="categoriesScrollWrapper"' not in html:
            html = html.replace('<div class="main-layout">', scroll_bar_html + '\n    <div class="main-layout">')

        # Update loadCategories in index.html to populate categoryPillsList
        old_load_cat = """        async function loadCategories() {
            try {
                const res = await fetch('/api/listings/categories');
                const categories = await res.json();
                const list = document.getElementById('categoryList');
                categories.forEach(c => {
                    list.innerHTML += `<li class="category-item" onclick="filterByCategory('${c.name}', this)">${c.name}</li>`;
                });
            } catch (e) { }
        }"""
        
        new_load_cat = """        async function loadCategories() {
            try {
                const res = await fetch('/api/listings/categories');
                const categories = await res.json();
                const list = document.getElementById('categoryList');
                const pillsList = document.getElementById('categoryPillsList');
                if (pillsList) {
                    pillsList.innerHTML = `<button type="button" class="cat-pill active" onclick="filterByCategory('', this)">✨ Всі товари</button>`;
                }
                const ICONS = {
                    'Електроніка': '📱', 'Одяг': '👕', 'Навчальні': '📚', 'Товари для дому': '🏠',
                    'Спорт': '⚽', 'Зоотовари': '🐾', 'Транспорт': '🚲', 'Послуги': '💼',
                    'Безкоштовно': '🎁', 'Інше': '📦'
                };
                categories.forEach(c => {
                    if (list) list.innerHTML += `<li class="category-item" onclick="filterByCategory('${c.name}', this)">${c.name}</li>`;
                    if (pillsList) {
                        let icon = '🏷️';
                        for (let k in ICONS) { if (c.name.includes(k)) { icon = ICONS[k]; break; } }
                        pillsList.innerHTML += `<button type="button" class="cat-pill" onclick="filterByCategory('${c.name}', this)">${icon} ${c.name}</button>`;
                    }
                });
            } catch (e) { }
        }"""
        if old_load_cat in html:
            html = html.replace(old_load_cat, new_load_cat)

        # Update filterByCategory to synchronize pills active state
        old_filter_cat = """        function filterByCategory(cat, el) {
            currentCategory = cat;
            document.querySelectorAll('.category-item').forEach(i => i.classList.remove('active'));
            el.classList.add('active');
            applyFilters();
        }"""
        new_filter_cat = """        function filterByCategory(cat, el) {
            currentCategory = cat;
            document.querySelectorAll('.category-item').forEach(i => i.classList.remove('active'));
            document.querySelectorAll('.cat-pill').forEach(i => i.classList.remove('active'));
            if (el) el.classList.add('active');
            // Sync with sidebar if clicked from pill
            document.querySelectorAll('.category-item').forEach(i => {
                if (cat && i.textContent.trim() === cat.trim()) i.classList.add('active');
            });
            applyFilters();
        }"""
        if old_filter_cat in html:
            html = html.replace(old_filter_cat, new_filter_cat)

    with open(fpath, "w", encoding="utf-8") as f:
        f.write(html)
    print(f"  [OK] Processed {fname}")

print("All HTML pages successfully upgraded with adaptive design system!")
