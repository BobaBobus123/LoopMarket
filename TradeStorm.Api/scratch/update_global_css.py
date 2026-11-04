import os

path = r"D:\LoopMarket\TradeStorm.Api\wwwroot\css\global.css"
with open(path, "r", encoding="utf-8") as f:
    css = f.read()

# 1. Update :root variables
root_addon = """    /* Theme Glows & Header Glass */
    --glow-blue: rgba(37, 99, 235, 0.05);
    --glow-emerald: rgba(16, 185, 129, 0.04);
    --header-glass: rgba(255, 255, 255, 0.92);
"""
if "--glow-blue" not in css:
    css = css.replace("    --trust-bg: var(--bg-subtle);\n}", "    --trust-bg: var(--bg-subtle);\n" + root_addon + "}")

# 2. Update dark theme variables
dark_addon = """    /* Theme Glows & Header Glass (Dark) */
    --glow-blue: rgba(37, 99, 235, 0.14);
    --glow-emerald: rgba(16, 185, 129, 0.08);
    --header-glass: rgba(11, 15, 25, 0.92);
"""
if "--header-glass: rgba(11" not in css:
    target_dark = "    --trust-bg: var(--bg-subtle);\n}"
    parts = css.split(target_dark)
    if len(parts) >= 3:
        css = parts[0] + target_dark + parts[1] + "    --trust-bg: var(--bg-subtle);\n" + dark_addon + "}" + "".join(parts[2:])

# 3. Update body style for constellation background & responsive height
old_body = """body {
    font-family: 'Plus Jakarta Sans', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
    font-size: 0.9375rem; /* 15px - modern enterprise standard */
    line-height: 1.55;
    color: var(--text-primary);
    background-color: var(--bg-canvas);
    min-height: 100vh;
    display: flex;
    flex-direction: column;
    text-rendering: optimizeLegibility;
    -webkit-font-smoothing: antialiased;
    -moz-osx-font-smoothing: grayscale;
    transition: background-color var(--transition-base), color var(--transition-base);
}"""

new_body = """body {
    font-family: 'Plus Jakarta Sans', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
    font-size: 0.9375rem; /* 15px - modern enterprise standard */
    line-height: 1.55;
    color: var(--text-primary);
    background-color: var(--bg-canvas);
    background-image: 
        radial-gradient(ellipse 80% 50% at 50% 0%, var(--glow-blue), transparent 65%),
        radial-gradient(ellipse 60% 40% at 100% 100%, var(--glow-emerald), transparent 55%),
        url('/images/bg_tech_constellation.svg');
    background-repeat: repeat;
    background-size: 800px auto;
    background-attachment: fixed;
    min-height: 100vh;
    min-height: 100dvh;
    display: flex;
    flex-direction: column;
    text-rendering: optimizeLegibility;
    -webkit-font-smoothing: antialiased;
    -moz-osx-font-smoothing: grayscale;
    transition: background-color var(--transition-base), color var(--transition-base);
    overflow-x: hidden;
}"""

if "min-height: 100dvh;" not in css:
    css = css.replace(old_body.replace('\r\n', '\n'), new_body)

# 4. Append comprehensive responsive sections if not present
responsive_sections = """
/* ==========================================================================
   10. UNIVERSAL ADAPTIVE VIEWPORTS & RESPONSIVE LAYOUT SYSTEM
   ========================================================================== */

/* Universal Fluid Container */
.container {
    width: 100%;
    max-width: 1440px;
    margin-left: auto;
    margin-right: auto;
    padding-left: 20px;
    padding-right: 20px;
}

@media (max-width: 768px) {
    .container {
        padding-left: 12px;
        padding-right: 12px;
    }
}

/* ==========================================================================
   11. UNIFIED RESPONSIVE HEADER & MOBILE DRAWER NAVIGATION
   ========================================================================== */

header {
    background-color: var(--surface) !important;
    border-bottom: 1px solid var(--border-color) !important;
    position: sticky !important;
    top: 0 !important;
    z-index: 1000 !important;
    box-shadow: 0 2px 10px rgba(0, 0, 0, 0.04) !important;
    transition: background-color 0.3s ease, border-color 0.3s ease !important;
}

.header-content {
    width: 100%;
    max-width: 1440px;
    margin: 0 auto;
    display: flex;
    justify-content: space-between;
    align-items: center;
    height: 64px;
    padding: 0 20px;
    gap: 12px;
}

/* Mobile Hamburger Button */
.btn-mobile-menu-toggle {
    display: none;
    align-items: center;
    justify-content: center;
    width: 40px;
    height: 40px;
    border-radius: 10px;
    background: var(--surface);
    border: 1px solid var(--border-color);
    color: var(--text-primary);
    cursor: pointer;
    transition: all 0.2s;
}

.btn-mobile-menu-toggle:hover {
    border-color: var(--brand-emerald);
    color: var(--brand-emerald);
}

/* Slide-out Mobile Navigation Drawer */
.mobile-nav-overlay {
    position: fixed;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    background: rgba(0, 0, 0, 0.6);
    backdrop-filter: blur(6px);
    -webkit-backdrop-filter: blur(6px);
    z-index: 9998;
    opacity: 0;
    pointer-events: none;
    transition: opacity 0.3s ease;
}

.mobile-nav-overlay.active {
    opacity: 1;
    pointer-events: auto;
}

.mobile-nav-drawer {
    position: fixed;
    top: 0;
    right: -320px;
    width: min(320px, 86vw);
    height: 100%;
    background: var(--surface-card);
    border-left: 1px solid var(--border-default);
    box-shadow: -10px 0 30px rgba(0, 0, 0, 0.25);
    z-index: 9999;
    display: flex;
    flex-direction: column;
    transition: right 0.32s cubic-bezier(0.16, 1, 0.3, 1);
    padding: 20px;
    overflow-y: auto;
}

.mobile-nav-drawer.active {
    right: 0;
}

.mobile-drawer-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding-bottom: 16px;
    border-bottom: 1px solid var(--border-subtle);
    margin-bottom: 16px;
}

.mobile-drawer-close {
    background: transparent;
    border: none;
    color: var(--text-muted);
    cursor: pointer;
    padding: 6px;
    border-radius: 8px;
    display: flex;
    align-items: center;
    justify-content: center;
}

.mobile-drawer-links {
    display: flex;
    flex-direction: column;
    gap: 8px;
    flex-grow: 1;
}

.mobile-drawer-link {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 12px 14px;
    border-radius: 12px;
    text-decoration: none;
    color: var(--text-primary);
    font-weight: 600;
    font-size: 0.95rem;
    background: var(--bg-subtle);
    border: 1px solid transparent;
    transition: all 0.2s;
}

.mobile-drawer-link:hover, .mobile-drawer-link.active {
    background: var(--brand-emerald-soft);
    border-color: var(--brand-emerald);
    color: var(--brand-emerald);
}

.mobile-drawer-link svg {
    flex-shrink: 0;
}

/* Header Responsiveness (< 992px) */
@media (max-width: 992px) {
    .header-content {
        height: 60px !important;
        padding: 0 14px !important;
    }
    
    .nav-actions .nav-link {
        display: none !important;
    }
    
    .btn-mobile-menu-toggle {
        display: inline-flex !important;
    }

    .btn-create-top {
        padding: 7px 12px !important;
        font-size: 0.82rem !important;
    }

    .notif-dropdown {
        right: 14px !important;
        width: min(340px, 92vw) !important;
    }
}

/* ==========================================================================
   12. MOBILE APP-LIKE BOTTOM NAVIGATION BAR (< 768px)
   ========================================================================== */

.mobile-bottom-bar {
    display: none;
    position: fixed;
    bottom: 0;
    left: 0;
    right: 0;
    height: calc(60px + env(safe-area-inset-bottom, 0px));
    padding-bottom: env(safe-area-inset-bottom, 0px);
    background: var(--surface-glass);
    backdrop-filter: blur(18px);
    -webkit-backdrop-filter: blur(18px);
    border-top: 1px solid var(--border-default);
    z-index: 9990;
    box-shadow: 0 -4px 20px rgba(0, 0, 0, 0.08);
}

.mobile-bottom-bar-inner {
    height: 60px;
    max-width: 600px;
    margin: 0 auto;
    display: flex;
    align-items: center;
    justify-content: space-around;
    padding: 0 8px;
}

.bottom-bar-item {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 3px;
    text-decoration: none;
    color: var(--text-muted);
    font-size: 0.68rem;
    font-weight: 700;
    flex: 1;
    height: 100%;
    position: relative;
    transition: all 0.2s;
}

.bottom-bar-item svg {
    width: 20px;
    height: 20px;
}

.bottom-bar-item:hover, .bottom-bar-item.active {
    color: var(--brand-emerald);
}

.bottom-bar-item.create-fab {
    margin-top: -16px;
}

.bottom-bar-item.create-fab .fab-circle {
    width: 44px;
    height: 44px;
    border-radius: 50%;
    background: linear-gradient(135deg, #10b981 0%, #059669 100%);
    color: #ffffff;
    display: flex;
    align-items: center;
    justify-content: center;
    box-shadow: 0 6px 16px rgba(16, 185, 129, 0.4);
    transition: transform 0.2s, box-shadow 0.2s;
}

.bottom-bar-item.create-fab:active .fab-circle {
    transform: scale(0.92);
}

@media (max-width: 768px) {
    .mobile-bottom-bar {
        display: block !important;
    }
    
    /* Ensure content is not hidden behind the fixed bottom bar on phones */
    body {
        padding-bottom: calc(72px + env(safe-area-inset-bottom, 0px)) !important;
    }
}

/* ==========================================================================
   13. RESPONSIVE CATALOG LAYOUT & MOBILE FILTER TOGGLE (index.html)
   ========================================================================== */

/* Category Quick Horizontal Scroller */
.categories-scroll-wrapper {
    width: 100%;
    overflow-x: auto;
    -webkit-overflow-scrolling: touch;
    scrollbar-width: none;
    padding: 10px 0 6px 0;
    margin-bottom: 12px;
}

.categories-scroll-wrapper::-webkit-scrollbar {
    display: none;
}

.categories-scroll-list {
    display: flex;
    gap: 8px;
    padding: 0 4px;
    width: max-content;
}

.cat-pill {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    padding: 8px 16px;
    border-radius: 999px;
    background: var(--surface-card);
    border: 1px solid var(--border-default);
    color: var(--text-primary);
    font-size: 0.84rem;
    font-weight: 600;
    text-decoration: none;
    white-space: nowrap;
    cursor: pointer;
    transition: all 0.2s;
    user-select: none;
}

.cat-pill:hover, .cat-pill.active {
    background: var(--brand-emerald-soft);
    border-color: var(--brand-emerald);
    color: var(--brand-emerald);
    transform: translateY(-1px);
}

/* Mobile Filter Bar & Trigger */
.mobile-filter-bar {
    display: none;
    margin-bottom: 14px;
    gap: 10px;
}

.btn-filter-trigger {
    flex: 1;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    gap: 8px;
    padding: 10px 16px;
    border-radius: 12px;
    background: var(--surface-card);
    border: 1px solid var(--border-default);
    color: var(--text-primary);
    font-weight: 700;
    font-size: 0.88rem;
    cursor: pointer;
    box-shadow: var(--shadow-xs);
    transition: all 0.2s;
}

.btn-filter-trigger:hover, .btn-filter-trigger.active {
    border-color: var(--brand-emerald);
    color: var(--brand-emerald);
    background: var(--brand-emerald-soft);
}

@media (max-width: 960px) {
    .mobile-filter-bar {
        display: flex !important;
    }

    .main-layout {
        display: flex !important;
        flex-direction: column !important;
        padding: 12px 14px !important;
        gap: 14px !important;
    }

    .sidebar {
        display: none !important; /* Hidden by default on mobile, toggled with .mobile-visible */
        width: 100% !important;
        position: static !important;
        margin-bottom: 12px !important;
        border-radius: 16px !important;
        box-shadow: var(--shadow-md) !important;
    }

    .sidebar.mobile-visible {
        display: block !important;
        animation: fadeInSlide 0.25s ease-out;
    }
}

@keyframes fadeInSlide {
    from {
        opacity: 0;
        transform: translateY(-8px);
    }
    to {
        opacity: 1;
        transform: translateY(0);
    }
}

/* Responsive Catalog Grid across All Screen Sizes */
@media (min-width: 1441px) {
    .listings-grid {
        grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)) !important;
        gap: 20px !important;
    }
}

@media (max-width: 1440px) and (min-width: 961px) {
    .listings-grid {
        grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)) !important;
        gap: 16px !important;
    }
}

@media (max-width: 960px) and (min-width: 601px) {
    .listings-grid {
        grid-template-columns: repeat(3, minmax(0, 1fr)) !important;
        gap: 14px !important;
    }
}

@media (max-width: 600px) and (min-width: 361px) {
    .listings-grid {
        grid-template-columns: repeat(2, minmax(0, 1fr)) !important;
        gap: 10px !important;
    }
    
    .card-img-wrapper {
        padding-top: 75% !important;
    }
    
    .card-content {
        padding: 10px 10px 12px 10px !important;
    }
    
    .card-title {
        font-size: 0.88rem !important;
        min-height: 2.4em !important;
    }
    
    .card-price {
        font-size: 1.05rem !important;
    }
}

@media (max-width: 360px) {
    .listings-grid {
        grid-template-columns: 1fr !important;
        gap: 12px !important;
    }
}

/* ==========================================================================
   14. RESPONSIVE PRODUCT DETAILS & ORDER MODAL (details.html)
   ========================================================================== */

@media (max-width: 992px) {
    .product-layout {
        grid-template-columns: 1fr !important;
        gap: 18px !important;
        padding: 14px !important;
    }
    
    .image-showcase {
        max-height: 400px !important;
    }

    .main-image {
        max-height: 380px !important;
    }
}

@media (max-width: 768px) {
    .order-modal-container {
        width: 100% !important;
        max-width: 100% !important;
        height: auto !important;
        max-height: 92vh !important;
        border-radius: 20px 20px 0 0 !important;
        position: fixed !important;
        bottom: 0 !important;
        padding: 20px 16px 30px 16px !important;
        margin: 0 !important;
        overflow-y: auto !important;
    }

    .order-modal-grid {
        grid-template-columns: 1fr !important;
        gap: 18px !important;
    }
}

/* ==========================================================================
   15. ACCESSIBILITY, TOUCH TARGETS & FORM INPUT SIZES
   ========================================================================== */

@media (hover: none) and (pointer: coarse) {
    /* Touch devices: ensure minimum comfortable tap area */
    button, .btn, .btn-icon, .nav-link, .cat-pill, .bottom-bar-item {
        min-height: 44px;
    }

    input, select, textarea {
        font-size: 16px !important; /* Prevents auto-zoom in mobile Safari */
        min-height: 44px;
    }
}
"""

if "UNIVERSAL ADAPTIVE VIEWPORTS" not in css:
    css = css.rstrip() + "\n" + responsive_sections

with open(path, "w", encoding="utf-8") as f:
    f.write(css)

print("global.css successfully enhanced with full responsive architecture!")
