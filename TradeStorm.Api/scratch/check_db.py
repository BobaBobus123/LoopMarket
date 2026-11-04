import sqlite3

conn = sqlite3.connect(r"D:\LoopMarket\loopmarket.db")
cur = conn.cursor()
tables = [r[0] for r in cur.execute("SELECT name FROM sqlite_master WHERE type='table';").fetchall()]
print("Tables in D:\\LoopMarket\\loopmarket.db:")
for t in tables:
    if not t.startswith('sqlite_'):
        count = cur.execute(f'SELECT COUNT(*) FROM "{t}";').fetchone()[0]
        print(f"  {t}: {count} rows")
conn.close()
