# Privacy Policy — NOVA Clip

**Product:** NOVA Clip  
**Version:** 1.0.0  
**Effective Date:** 2026

NOVA Clip is built on a simple promise: **Your clipboard is personal, confidential, and belongs strictly to you.**

---

## 1. Local-Only Storage
NOVA Clip operates **100% locally on your computer**.
- We do not run cloud servers.
- We do not store your clipboard entries online.
- We do not require an account, email address, or registration.
- No third party ever has access to your copied content.

---

## 2. No Telemetry or Tracking
NOVA Clip contains:
- **No telemetry**
- **No analytics tracking**
- **No crash report uploading**
- **No background network connections**

You can inspect the application code or monitor its network traffic using Windows Resource Monitor or Wireshark; NOVA Clip never initiates outbound network traffic.

---

## 3. Cryptographic Protection on Disk
When NOVA Clip saves clipboard history, reusable snippets, or Paste Queue items to your local storage, all payloads are encrypted using **Windows DPAPI (Data Protection API)** with `CurrentUser` scope.

- Only your active Windows account session can decrypt the saved content.
- If another user logs onto the same machine, they cannot decrypt or view your saved clipboard items.
- If your hard drive is inspected offline, the payloads remain encrypted ciphertext.

---

## 4. Heuristic Sensitive Content Filtering
NOVA Clip includes an automated privacy engine that actively inspects clipboard changes:
- It detects and skips saving payment cards, OTP / 2FA codes, private cryptographic keys, API tokens, and passwords.
- It detects and excludes copying operations initiated from password managers (such as 1Password, Bitwarden, KeePass).
- You can pause clipboard monitoring at any time from the system tray icon or Settings.

---

## 5. Retention & User Control
You have total control over your stored history:
- Configure automatic expiration (7 days, 30 days, 90 days, or Never).
- Set maximum storage capacity (MB) and item limits.
- Clear history, clear all saved images, or delete individual entries at any time with one click.
- Deleting an entry permanently deletes the encrypted database record and any associated storage asset files from your hard drive.

---

## 6. Questions
For any questions regarding privacy in NOVA Clip, refer to the local documentation in `docs/` or inspect the open architecture files.
