import sqlite3
conn = sqlite3.connect('TradeStorm.Api/loopmarket.db')
conn.row_factory = sqlite3.Row
c = conn.cursor()
c.execute('SELECT Id, Title, Price, CategoryId, SellerId, Status, Condition, Location FROM Listings ORDER BY Id DESC LIMIT 15')
for r in c.fetchall():
    d = dict(r)
    # clean non-ascii for safe printing
    safe_title = d["Title"].encode('ascii', 'replace').decode()
    safe_loc = (d["Location"] or "").encode('ascii', 'replace').decode()
    print(f"ID={d['Id']} Cat={d['CategoryId']} Seller={d['SellerId']} Status={d['Status']} Price={d['Price']} Title={safe_title} Loc={safe_loc}")
