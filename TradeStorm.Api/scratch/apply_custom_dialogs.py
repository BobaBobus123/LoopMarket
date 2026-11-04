import os
import re

wwwroot = r"D:\LoopMarket\TradeStorm.Api\wwwroot"

html_files = [
    "messages.html",
    "details.html",
    "profile.html",
    "seller.html",
    "create.html",
    "edit.html",
    "index.html",
    "about.html",
    "safety.html",
    "terms.html",
    "docs.html"
]

print("Integrating LoopDialogs & LoopToasts into all pages...")

for fname in html_files:
    fpath = os.path.join(wwwroot, fname)
    if not os.path.exists(fpath):
        continue

    with open(fpath, "r", encoding="utf-8") as f:
        html = f.read()

    # 1. Ensure loop-dialogs.css is linked
    if 'loop-dialogs.css' not in html:
        html = html.replace('</head>', '    <link rel="stylesheet" href="/css/loop-dialogs.css">\n</head>')

    # 2. Ensure loop-dialogs.js is loaded
    if 'loop-dialogs.js' not in html:
        html = html.replace('</body>', '    <script src="/js/loop-dialogs.js"></script>\n</body>')

    with open(fpath, "w", encoding="utf-8") as f:
        f.write(html)
    print(f"  [OK] Linked dialog assets in {fname}")

# Specific update for messages.html reportUser & deleteChat
messages_path = os.path.join(wwwroot, "messages.html")
with open(messages_path, "r", encoding="utf-8") as f:
    m_html = f.read()

old_report_user = """        function reportUser() {
            const reason = prompt("Вкажіть причину скарги на користувача (шахрайство, спам, підозріла поведінка):");
            if (reason && reason.trim()) {
                alert("Дякуємо! Вашу скаргу надіслано модерації LoopMarket. Ми перевіримо діалог для забезпечення безпеки.");
            }
        }"""

new_report_user = """        async function reportUser() {
            const partnerName = document.getElementById('activeChatName')?.innerText || 'користувача';
            const result = await LoopDialog.complaint(partnerName);
            if (result) {
                LoopToast.success("Дякуємо! Вашу скаргу надіслано модерації LoopMarket. Ми перевіримо звернення найближчим часом.");
            }
        }"""

if old_report_user in m_html:
    m_html = m_html.replace(old_report_user, new_report_user)

old_delete_chat = """        async function deleteChat() {
            if (!activeReceiverId) return;
            if (!confirm("Ви дійсно бажаєте видалити всю історію цього листування?")) return;"""

new_delete_chat = """        async function deleteChat() {
            if (!activeReceiverId) return;
            const confirmed = await LoopDialog.confirm("Ви дійсно бажаєте видалити всю історію цього листування? Цю дію неможливо буде скасувати.", {
                title: "Видалення листування",
                confirmText: "Видалити чат",
                cancelText: "Скасувати",
                danger: true
            });
            if (!confirmed) return;"""

if old_delete_chat in m_html:
    m_html = m_html.replace(old_delete_chat, new_delete_chat)

with open(messages_path, "w", encoding="utf-8") as f:
    f.write(m_html)

print("messages.html dialogs successfully upgraded to custom beautiful modals!")
