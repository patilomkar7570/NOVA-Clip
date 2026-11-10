# Troubleshooting Guide — NOVA Clip 1.0

This guide addresses common issues, Windows permission quirks, and resolution steps for NOVA Clip.

---

## 1. Global Shortcut Conflict
### Symptom
Pressing `Ctrl + Shift + Space` does not open the Floating Quick Panel, or a warning appears in the logs.
### Cause
Another application (such as an IDE, screen recording tool, or text utility) has already registered the same hotkey combination.
### Solution
1. Open NOVA Clip's Main Window from the System Tray.
2. Go to **Settings** > **Activation**.
3. Choose an alternative modifier (e.g. `Ctrl + Alt` or `Win + Shift`) and key (e.g. `V` or `Insert`).
4. Click **Save Settings**. The hotkey is immediately re-registered.

---

## 2. Clipboard Is Not Capturing Items
### Symptom
Copied text or images do not appear in the clipboard history.
### Cause & Solutions
1. **Monitoring is paused**:
   - Check the System Tray icon or the status indicator in the top-left of the Main Window.
   - Click **Resume Monitoring** or toggle it in Settings.
2. **Excluded Application**:
   - Check if you are copying from an application on the excluded list (e.g., a password manager like 1Password or Bitwarden).
   - In **Settings** > **Privacy**, inspect the list of excluded executable names.
3. **Sensitive Content Protection**:
   - NOVA Clip's privacy filter automatically skips credit card numbers, OTP codes, private keys, and passwords.
   - If this is intentional, you can disable sensitive content protection in **Settings** > **Privacy**.
4. **Temporary Clipboard Lock**:
   - Some heavy applications lock the Windows clipboard for extended periods. NOVA Clip uses an automated 5-attempt retry mechanism with exponential backoff, but if an external app holds the lock indefinitely, Windows denies access. Restarting the offending application releases the lock.

---

## 3. Paste Next Injection into Elevated Windows
### Symptom
`Paste Next` automatically copies the item, but does not paste into administrative tools (e.g., elevated Command Prompt, Task Manager, or Registry Editor).
### Cause
Windows User Interface Privilege Isolation (UIPI) prohibits non-elevated applications from injecting keyboard inputs into elevated (admin) windows.
### Solution
- When pasting into elevated administrator windows, NOVA Clip safely keeps the item on your clipboard and preserves queue position. Simply press `Ctrl + V` manually to paste.

---

## 4. Resetting Stored Application State
### Symptom
You wish to reset all settings, queues, and history to factory defaults.
### Solution
1. Right-click the NOVA Clip tray icon and click **Exit**.
2. Press `Win + R`, type `%LOCALAPPDATA%`, and press Enter.
3. Rename or delete the folder named `NOVA.Clip`.
4. Relaunch NOVA Clip. A fresh SQLite database and default settings will be created automatically.
