import os
import shutil

preview_path = r"C:\Users\nikit\.gemini\antigravity\brain\8485f418-e191-4d0f-84bd-3c398ead35b3\scratch\preview_server.py"
backup_path = preview_path + ".bak"
shutil.copyfile(preview_path, backup_path)
print("Backup created at", backup_path)
