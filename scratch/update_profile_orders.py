import re

target_file = r"TradeStorm.Api/wwwroot/profile.html"

with open(target_file, "r", encoding="utf-8") as f:
    content = f.read()

# 1. Update statusMap to include PendingConfirmation
content = content.replace(
    "'Pending': { label: '⏳ Очікує підтвердження', bg: 'rgba(245, 158, 11, 0.12)', color: '#d97706', border: 'rgba(245, 158, 11, 0.3)' },",
    "'Pending': { label: '⏳ Очікує підтвердження', bg: 'rgba(245, 158, 11, 0.12)', color: '#d97706', border: 'rgba(245, 158, 11, 0.3)' },\n                'PendingConfirmation': { label: '⏳ Очікує підтвердження', bg: 'rgba(245, 158, 11, 0.15)', color: '#d97706', border: 'rgba(245, 158, 11, 0.4)' },"
)

# 2. Update condition checks for buttons
content = content.replace(
    "${o.status === 'Pending' ? `",
    "${(o.status === 'Pending' || o.status === 'PendingConfirmation') ? `"
)

with open(target_file, "w", encoding="utf-8") as f:
    f.write(content)
print("Updated profile.html order status handling")
