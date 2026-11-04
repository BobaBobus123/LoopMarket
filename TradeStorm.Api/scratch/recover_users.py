import sqlite3

# 1. Read users from healthy D:\LoopMarket\loopmarket.db
conn_healthy = sqlite3.connect(r"D:\LoopMarket\loopmarket.db")
healthy_users = conn_healthy.execute("SELECT * FROM Users;").fetchall()
user_cols = [c[1] for c in conn_healthy.execute("PRAGMA table_info(Users);").fetchall()]
conn_healthy.close()

print(f"Healthy DB has {len(healthy_users)} users:")
for u in healthy_users:
    print(" ", u[0], u[1], u[2], u[3], u[5]) # id, firstName, lastName, email, role

# 2. Try reading from corrupted backup row by row
conn_bak = sqlite3.connect(r"D:\LoopMarket\TradeStorm.Api\loopmarket.db.bak")
recovered_users = []
for uid in range(1, 100):
    try:
        row = conn_bak.execute("SELECT * FROM Users WHERE Id = ?;", (uid,)).fetchone()
        if row:
            recovered_users.append(row)
            print(f"  Recovered User #{uid}: {row[1]} {row[2]} ({row[3]})")
    except Exception as e:
        print(f"  Error reading User #{uid}: {e}")
conn_bak.close()

# 3. Insert into D:\LoopMarket\TradeStorm.Api\loopmarket.db
conn_dst = sqlite3.connect(r"D:\LoopMarket\TradeStorm.Api\loopmarket.db")
cols_str = ', '.join([f'"{c}"' for c in user_cols])
placeholders = ', '.join(['?'] * len(user_cols))

# Use recovered users if more, else healthy users
final_users = recovered_users if len(recovered_users) >= len(healthy_users) else healthy_users
for u in final_users:
    try:
        conn_dst.execute(f"INSERT OR REPLACE INTO Users ({cols_str}) VALUES ({placeholders})", u)
    except Exception as e:
        print(f"Error inserting user {u[0]}: {e}")

conn_dst.commit()
count = conn_dst.execute("SELECT COUNT(*) FROM Users;").fetchone()[0]
print(f"Total Users now in clean loopmarket.db: {count}")
conn_dst.close()
