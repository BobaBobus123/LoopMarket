import sqlite3

conn = sqlite3.connect(r"D:\LoopMarket\TradeStorm.Api\loopmarket.db")
cur = conn.cursor()
tables = [r[0] for r in cur.execute("SELECT name FROM sqlite_master WHERE type='table';").fetchall()]
print("Tables in D:\\LoopMarket\\TradeStorm.Api\\loopmarket.db:")
for t in tables:
    if not t.startswith('sqlite_'):
        try:
            count = cur.execute(f'SELECT COUNT(*) FROM "{t}";').fetchone()[0]
            print(f"  {t}: {count} rows")
        except Exception as e:
            print(f"  {t}: ERROR {e}")
conn.close()
