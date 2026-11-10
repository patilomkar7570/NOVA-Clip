# Security Policy for NOVA Clip

The NOVA Clip project places user security and local data confidentiality as its highest priority.

---

## Security Model

### 1. Zero Cloud Exposure
- NOVA Clip performs **no network calls**.
- It does not contain any analytics, telemetry, remote reporting, or external dependencies.
- It is designed to work in strictly air-gapped environments.

### 2. Encryption at Rest (DPAPI)
- Stored clipboard history, snippets, and queue payloads are encrypted with **Windows Data Protection API (DPAPI)** using `DataProtectionScope.CurrentUser`.
- The encryption keys are managed directly by Windows and tied cryptographically to the current Windows user profile.
- Other Windows user accounts or foreign systems accessing the physical hard drive cannot decrypt the stored payloads without the user's Windows credentials.

### 3. In-Memory Search Strategy
- Sensitive clipboard text is never stored in unencrypted persistent search indexes.
- Search indexes are evaluated in memory on decrypted payloads on demand.
- Database records only contain encrypted binary blobs or references to encrypted asset files.

### 4. Heuristic Sensitive Content Protection
- When enabled in Settings, the `PrivacyFilter` automatically detects and skips capturing:
  - Payment card numbers (verified with ISO/IEC 7812 Luhn checksum validation)
  - RSA/OpenSSH/PGP private keys
  - API tokens and access keys (e.g. GitHub, Slack, AWS)
  - JWT tokens
  - Password and credential patterns
  - One-time passcodes (OTP) and two-factor verification codes
- *Note:* Heuristic detection is designed as an additional layer of defensive privacy, not an absolute guarantee. Users can pause monitoring or configure application exclusions at any time.

### 5. Application Exclusions
- The application includes built-in exclusions for popular password managers:
  - 1Password (`1Password.exe`)
  - KeePass (`KeePass.exe`)
  - KeePassXC (`KeePassXC.exe`)
  - Bitwarden (`Bitwarden.exe`)
  - LastPass (`LastPass.exe`)
  - Dashlane (`Dashlane.exe`)
  - Enpass (`Enpass.exe`)
- Users can add any custom executable to the exclusion list in Settings.

### 6. Logging Hygiene
- Application loggers strictly forbid outputting clipboard content, passwords, URLs, file paths, or image data.
- Only non-sensitive diagnostic metadata (timestamps, item types, process identifiers) is logged.

---

## Reporting Vulnerabilities

If you identify a security or privacy vulnerability in NOVA Clip, please report it privately to the maintainers rather than opening a public issue.
