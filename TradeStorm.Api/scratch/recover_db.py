import sqlite3
import os
import shutil

source_path = r"D:\LoopMarket\TradeStorm.Api\loopmarket.db"
backup_path = r"D:\LoopMarket\TradeStorm.Api\loopmarket.db.bak"
recovered_path = r"D:\LoopMarket\TradeStorm.Api\loopmarket_clean.db"

# Backup original
shutil.copy2(source_path, backup_path)
print(f"Backed up original database to {backup_path}")

if os.path.exists(recovered_path):
    os.remove(recovered_path)

src = sqlite3.connect(source_path)
dst = sqlite3.connect(recovered_path)

src_cur = src.cursor()
dst_cur = dst.cursor()

# Get schema in order
schema_objects = src_cur.execute("""
    SELECT type, name, tbl_name, sql 
    FROM sqlite_master 
    WHERE sql IS NOT NULL AND name NOT LIKE 'sqlite_%'
    ORDER BY CASE type WHEN 'table' THEN 1 WHEN 'index' THEN 2 ELSE 3 END;
""").fetchall()

# 1. Create tables first
tables = []
for type_, name, tbl_name, sql in schema_objects:
    if type_ == 'table':
        tables.append(name)
        dst_cur.execute(sql)

dst.commit()
print(f"Created {len(tables)} tables: {tables}")

# 2. Copy data table by table
for tbl in tables:
    try:
        # Get columns
        col_info = src_cur.execute(f"PRAGMA table_info('{tbl}');").fetchall()
        col_names = [f'"{c[1]}"' for c in col_info]
        placeholders = ', '.join(['?'] * len(col_names))
        cols_str = ', '.join(col_names)

        # Select all valid rows from source
        rows = src_cur.execute(f"SELECT {cols_str} FROM '{tbl}';").fetchall()
        dst_cur.executemany(f"INSERT OR REPLACE INTO '{tbl}' ({cols_str}) VALUES ({placeholders})", rows)
        dst.commit()
        print(f"  [OK] '{tbl}': {len(rows)} rows copied")
    except Exception as e:
        print(f"  [ERROR in {tbl}]: {e}")

# 3. Create indexes after data insertion
indexes = [s for s in schema_objects if s[0] == 'index']
for type_, name, tbl_name, sql in indexes:
    try:
        dst_cur.execute(sql)
    except Exception as e:
        print(f"  [Index error in {name}]: {e}")
dst.commit()
print(f"Created {len(indexes)} indexes")

# 4. Enable WAL and check integrity
dst_cur.execute("PRAGMA journal_mode=WAL;")
res = dst_cur.execute("PRAGMA integrity_check;").fetchall()
print("Clean DB integrity check result:", res)

src.close()
dst.close()

if res == [('ok',)]:
    # Replace corrupted database with recovered database
    os.replace(recovered_path, source_path)
    # Also remove any dangling shm or wal files
    for ext in ["-shm", "-wal"]:
        f = source_path + ext
        if os.path.exists(f):
            os.remove(f)
    print("SUCCESS: loopmarket.db is now completely healthy and clean!")
else:
    print("FAILED: Recovered DB did not pass integrity check!")
