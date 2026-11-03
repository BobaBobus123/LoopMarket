import urllib.request
import urllib.parse
import json
import base64
import sys

BASE_URL = "http://localhost:65083"

def make_token(uid, name, email="user@test.com", role="User"):
    h = json.dumps({"alg":"HS256","typ":"JWT"}).encode()
    p = json.dumps({
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier": str(uid),
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name": name,
        "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": role,
        "email": email
    }).encode()
    return f"{base64.urlsafe_b64encode(h).decode().rstrip('=')}.{base64.urlsafe_b64encode(p).decode().rstrip('=')}.mock_sig"

token_nikita = make_token(1, "Нікіта", "nikitazhidenko@gmail.com", "Admin")
token_vova = make_token(3, "Вова Вакуленко", "vovavakulenko@gmail.com", "User")

def req(url, method="GET", payload=None, token=None):
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    data = json.dumps(payload).encode('utf-8') if payload else None
    request = urllib.request.Request(f"{BASE_URL}{url}", data=data, headers=headers, method=method)
    with urllib.request.urlopen(request) as resp:
        return resp.status, json.loads(resp.read().decode('utf-8'))

print("=== STARTING END-TO-END VERIFICATION ===")

# 1. User 3 creates a service listing with physical street address
create_payload = {
    "title": "🚚 Вантажники та перевезення по Кременчуку",
    "description": "Акуратне навантаження та вивантаження меблів і техніки.\\n\\n📋 Параметри послуги:\\n• Поверх: Будь-який\\n• Вантажний ліфт: є / немає\\n• Кількість працівників: 2 вантажники",
    "price": 180,
    "categoryId": 8,
    "condition": "Погодинно • Виїзд",
    "location": "вул. Соборна 15, Кременчук",
    "isShippingAvailable": True
}
status, listing_res = req("/api/listings", "POST", create_payload, token_vova)
print(f"1. Listing Created: Status {status}, ID: {listing_res.get('id')}")
listing_id = listing_res.get("id")

# 2. Check initial services stats for User 3
status, stats_before = req("/api/users/me/services-stats", "GET", None, token_vova)
initial_earnings = stats_before.get("totalEarnings", 0)
print(f"2. Initial User 3 Earnings: {initial_earnings} грн")

# 3. User 1 (Нікіта) orders the service at a specific address
order_payload = {
    "listingId": listing_id,
    "sellerId": 3,
    "buyerName": "Нікіта Замовник",
    "buyerPhone": "+380671112233",
    "deliveryCity": "вул. Першотравнева 20, Кременчук (3 поверх, ліфт є)",
    "deliveryBranch": "Підйом та спуск меблів | Дата: 2026-11-05 о 14:00 | Обсяг: 2 год, 2 осіб",
    "paymentMethod": "Після виконання робіт",
    "comment": "Потрібно занести диван та стіл",
    "customTotalPrice": 360
}
status, order_res = req("/api/orders", "POST", order_payload, token_nikita)
order_id = order_res.get("id")
print(f"3. Order Created: Status {status}, ID: {order_id}, Order Status: {order_res.get('status')}")
assert order_res.get("status") == "PendingConfirmation"

# 4. User 3 checks my-sales: order must be in PendingConfirmation
status, sales = req("/api/orders/my-sales", "GET", None, token_vova)
order_in_sales = next((o for o in sales if o["id"] == order_id), None)
print(f"4. Order in User 3 sales: Found={order_in_sales is not None}, Status={order_in_sales['status'] if order_in_sales else None}")
assert order_in_sales is not None
assert order_in_sales["status"] == "PendingConfirmation"

# 5. User 3 CONFIRMS the order on the site
status, confirm_res = req(f"/api/orders/{order_id}/status", "PATCH", {"status": "Confirmed"}, token_vova)
print(f"5. User 3 Confirmed Order: Status {status}, New Order Status: {confirm_res.get('status')}")
assert confirm_res.get("status") == "Confirmed"

# 6. User 1 checks my-purchases: order is Confirmed
status, purchases = req("/api/orders/my-purchases", "GET", None, token_nikita)
order_in_purchases = next((o for o in purchases if o["id"] == order_id), None)
print(f"6. Order in User 1 purchases: Found={order_in_purchases is not None}, Status={order_in_purchases['status'] if order_in_purchases else None}")
assert order_in_purchases is not None
assert order_in_purchases["status"] == "Confirmed"

# 7. User 3 COMPLETES the order on the site
status, complete_res = req(f"/api/orders/{order_id}/status", "PATCH", {"status": "Completed"}, token_vova)
print(f"7. User 3 Completed Order: Status {status}, New Order Status: {complete_res.get('status')}")
assert complete_res.get("status") == "Completed"

# 8. User 3 Earnings auto-recalculated!
status, stats_after = req("/api/users/me/services-stats", "GET", None, token_vova)
final_earnings = stats_after.get("totalEarnings", 0)
print(f"8. Final User 3 Earnings: {final_earnings} грн (Difference: +{final_earnings - initial_earnings} грн)")
assert final_earnings == initial_earnings + 360

print("\n🎉 ALL E2E VERIFICATION TESTS PASSED SUCCESSFULLY!")
