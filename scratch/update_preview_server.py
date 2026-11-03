import re

target_file = r"C:\Users\nikit\.gemini\antigravity\brain\8485f418-e191-4d0f-84bd-3c398ead35b3\scratch\preview_server.py"

with open(target_file, "r", encoding="utf-8") as f:
    content = f.read()

# 1. Add Telegram helper near imports
telegram_helper = '''
TELEGRAM_BOT_TOKEN = "8990313504:AAGUpi64ddFbrAEa8KxR97lJJ2Y-1kz14uY"

def send_telegram_notification(chat_id, text):
    if not chat_id or int(chat_id) <= 0:
        return
    def _send():
        try:
            url = f"https://api.telegram.org/bot{TELEGRAM_BOT_TOKEN}/sendMessage"
            data = urllib.parse.urlencode({
                "chat_id": int(chat_id),
                "text": text,
                "parse_mode": "HTML"
            }).encode('utf-8')
            req = urllib.request.Request(url, data=data, headers={"User-Agent": "LoopMarket/2.0"})
            with urllib.request.urlopen(req, timeout=6) as resp:
                print(f"[Telegram Notification] Sent successfully to ChatId {chat_id}")
        except Exception as ex:
            print(f"[Telegram Notification Error] Failed to send to ChatId {chat_id}: {ex}")
    
    import threading
    t = threading.Thread(target=_send, daemon=True)
    t.start()
'''

if "def send_telegram_notification" not in content:
    content = content.replace("def get_db():", telegram_helper + "\ndef get_db():", 1)

# Ensure urllib.request is imported
if "import urllib.request" not in content:
    content = "import urllib.request\n" + content

# 2. Replace POST /api/orders
old_orders_post = re.search(r"if path == '/api/orders':.*?return self\.send_json\(.*?\n\s*}, 201\)", content, re.DOTALL)
if old_orders_post:
    new_orders_post = '''if path == '/api/orders':
            conn = get_db()
            cur = conn.cursor()
            buyer_uid = get_user_from_auth(self) or 1
            listing_id = int(payload.get('listingId', 9))
            
            cur.execute("SELECT * FROM Listings WHERE Id = ?", (listing_id,))
            listing = cur.fetchone()
            if not listing:
                conn.close()
                return self.send_json({"error": "Оголошення не знайдено"}, 404)
            
            seller_id = int(payload.get('sellerId') or listing['SellerId'])
            total_price = float(payload.get('customTotalPrice') or payload.get('totalPrice') or listing['Price'])
            buyer_name = payload.get('buyerName', 'Замовник').strip()
            buyer_phone = payload.get('buyerPhone', '+380991234567').strip()
            delivery_city = payload.get('deliveryCity', 'Локація').strip()
            delivery_branch = payload.get('deliveryBranch', 'Деталі робіт').strip()
            payment_method = payload.get('paymentMethod', 'Після виконання робіт').strip()
            comment = payload.get('comment', '').strip()
            
            status = 'PendingConfirmation'
            
            cur.execute("""
                INSERT INTO Orders (ListingId, BuyerId, SellerId, TotalPrice, Status, BuyerName, BuyerPhone, DeliveryCity, DeliveryBranch, PaymentMethod, Comment, CreatedAt)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, datetime('now'))
            """, (
                listing_id,
                buyer_uid,
                seller_id,
                str(total_price),
                status,
                buyer_name,
                buyer_phone,
                delivery_city,
                delivery_branch,
                payment_method,
                comment
            ))
            order_id = cur.lastrowid
            
            # Внутрішнє сповіщення для виконавця/продавця
            cur.execute("""
                INSERT INTO Notifications (UserId, Title, Message, Link, IsRead, CreatedAt)
                VALUES (?, ?, ?, ?, 0, datetime('now'))
            """, (
                seller_id,
                "Нове замовлення послуги!",
                f"Клієнт {buyer_name} замовив «{listing['Title']}» ({total_price} ₴). Адреса: {delivery_city}. Підтвердіть у профілі!",
                "/profile.html#orders"
            ))
            
            # Отримуємо дані виконавця для надсилання Telegram
            cur.execute("SELECT * FROM Users WHERE Id = ?", (seller_id,))
            seller_user = cur.fetchone()
            conn.commit()
            conn.close()
            
            if seller_user and seller_user['TelegramChatId']:
                tg_text = (
                    f"🔔 <b>Нове замовлення послуги в Loop Market!</b>\\n\\n"
                    f"📌 <b>Послуга:</b> {listing['Title']}\\n"
                    f"👤 <b>Замовник:</b> {buyer_name} (📞 {buyer_phone})\\n"
                    f"📍 <b>Адреса виконання:</b> {delivery_city}\\n"
                    f"🕒 <b>Обсяг / Час:</b> {delivery_branch}\\n"
                    f"💰 <b>Сума до виплати:</b> {total_price} ₴\\n"
                    f"💳 <b>Оплата:</b> {payment_method}\\n"
                )
                if comment:
                    tg_text += f"💬 <b>Коментар:</b> <i>{comment}</i>\\n"
                tg_text += f"\\n👉 Перейдіть на сайт у розділ «Мої продажі» та <b>підтвердіть</b> замовлення!"
                send_telegram_notification(seller_user['TelegramChatId'], tg_text)
            
            return self.send_json({
                "id": order_id,
                "totalPrice": total_price,
                "status": status,
                "message": "Замовлення успішно створено та очікує підтвердження виконавцем!"
            }, 201)'''
    content = content[:old_orders_post.start()] + new_orders_post + content[old_orders_post.end():]
    print("Replaced POST /api/orders")

# 3. Replace PATCH /api/orders/
old_orders_patch = re.search(r"if '/api/orders/' in path:.*?return self\.send_json\({'error': str\(e\)}, 500\)", content, re.DOTALL)
if old_orders_patch:
    new_orders_patch = '''if '/api/orders/' in path:
            try:
                parts = path.strip('/').split('/')
                # parts: ['api', 'orders', '3', 'status'] or ['api', 'orders', '3', 'price']
                order_id = int(parts[2])
                conn = get_db()
                cur = conn.cursor()
                cur.execute('SELECT * FROM Orders WHERE Id = ?', (order_id,))
                order = cur.fetchone()
                if not order:
                    conn.close()
                    return self.send_json({'error': 'Замовлення не знайдено'}, 404)
                
                cur.execute('SELECT * FROM Listings WHERE Id = ?', (order['ListingId'],))
                listing = cur.fetchone()
                listing_title = listing['Title'] if listing else 'Послуга'
                
                cur.execute('SELECT * FROM Users WHERE Id = ?', (order['BuyerId'],))
                buyer = cur.fetchone()
                
                cur.execute('SELECT * FROM Users WHERE Id = ?', (order['SellerId'],))
                seller = cur.fetchone()
                
                fields = []
                vals = []
                
                new_status = payload.get('status')
                if new_status:
                    fields.append('Status = ?')
                    vals.append(str(new_status))
                else:
                    new_status = order['Status']
                
                if 'totalPrice' in payload:
                    fields.append('TotalPrice = ?')
                    vals.append(str(float(payload['totalPrice'])))
                
                if 'trackingNumber' in payload and payload['trackingNumber']:
                    fields.append('TrackingNumber = ?')
                    vals.append(str(payload['trackingNumber']).strip())
                
                if fields:
                    vals.append(order_id)
                    clause = ', '.join(fields)
                    cur.execute(f"UPDATE Orders SET {clause} WHERE Id = ?", vals)
                    
                    if new_status == 'Confirmed':
                        # Сповіщення в додатку покупцю
                        cur.execute("""
                            INSERT INTO Notifications (UserId, Title, Message, Link, IsRead, CreatedAt)
                            VALUES (?, ?, ?, ?, 0, datetime('now'))
                        """, (
                            order['BuyerId'],
                            "Замовлення послуги підтверджено!",
                            f"Виконавець {seller['FirstName'] if seller else 'Майстер'} підтвердив ваше замовлення «{listing_title}»! Адреса: {order['DeliveryCity']}.",
                            "/profile.html#orders"
                        ))
                        # Надсилання Telegram покупцю
                        if buyer and buyer['TelegramChatId']:
                            seller_name = f"{seller['FirstName']} {seller['LastName'] or ''}".strip() if seller else 'Виконавець'
                            seller_phone = seller['PhoneNumber'] if seller else ''
                            tg_text = (
                                f"✅ <b>Ваше замовлення послуги підтверджено!</b>\\n\\n"
                                f"📌 <b>Послуга:</b> {listing_title}\\n"
                                f"👨‍🔧 <b>Виконавець:</b> {seller_name} (📞 {seller_phone})\\n"
                                f"📍 <b>Адреса виконання:</b> {order['DeliveryCity']}\\n"
                                f"🕒 <b>Обсяг / Час:</b> {order['DeliveryBranch']}\\n"
                                f"💰 <b>Сума до сплати:</b> {order['TotalPrice']} ₴\\n\\n"
                                f"Виконавець взяв замовлення в роботу та зв'яжеться з вами найближчим часом!"
                            )
                            send_telegram_notification(buyer['TelegramChatId'], tg_text)
                    
                    elif new_status == 'Completed':
                        if listing and listing['CategoryId'] != 8:
                            cur.execute("UPDATE Listings SET Status = 'Sold' WHERE Id = ?", (order['ListingId'],))
                        
                        cur.execute("""
                            INSERT INTO Notifications (UserId, Title, Message, Link, IsRead, CreatedAt)
                            VALUES (?, ?, ?, ?, 0, datetime('now'))
                        """, (
                            order['BuyerId'],
                            "Послугу успішно виконано!",
                            f"Замовлення «{listing_title}» позначено як виконане. Дякуємо за співпрацю!",
                            "/profile.html#orders"
                        ))
                        if buyer and buyer['TelegramChatId']:
                            tg_text = (
                                f"🎉 <b>Замовлення послуги успішно виконано!</b>\\n\\n"
                                f"📌 <b>Послуга:</b> {listing_title}\\n"
                                f"💰 <b>Сума:</b> {order['TotalPrice']} ₴\\n\\n"
                                f"Дякуємо, що скористалися послугами студентів на Loop Market!"
                            )
                            send_telegram_notification(buyer['TelegramChatId'], tg_text)
                    
                    elif new_status == 'Cancelled':
                        cur.execute("""
                            INSERT INTO Notifications (UserId, Title, Message, Link, IsRead, CreatedAt)
                            VALUES (?, ?, ?, ?, 0, datetime('now'))
                        """, (
                            order['BuyerId'],
                            "Замовлення скасовано",
                            f"Замовлення «{listing_title}» було скасовано.",
                            "/profile.html#orders"
                        ))
                    
                    conn.commit()
                
                cur.execute('SELECT * FROM Orders WHERE Id = ?', (order_id,))
                updated = dict(cur.fetchone())
                conn.close()
                return self.send_json({
                    'id': updated['Id'],
                    'status': updated['Status'],
                    'totalPrice': float(updated['TotalPrice']),
                    'trackingNumber': updated['TrackingNumber'],
                    'message': 'Статус та заробіток успішно оновлено!'
                })
            except Exception as e:
                return self.send_json({'error': str(e)}, 500)'''
    content = content[:old_orders_patch.start()] + new_orders_patch + content[old_orders_patch.end():]
    print("Replaced PATCH /api/orders/")

with open(target_file, "w", encoding="utf-8") as f:
    f.write(content)
print("Updated preview_server.py successfully")
