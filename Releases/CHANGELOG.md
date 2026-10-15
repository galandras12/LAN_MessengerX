# Changelog

All notable changes to the binaries in this folder (`LANMessengerX-*.apk`, `LanmessengerX-Windows.exe`).
Newest first. The Windows/Linux client (C#) and the Android client (Kotlin) share the protocol and the version number.
Compatibility baseline: the original LAN Messenger **1.3.x**, plus **2.x**. See [`../Protocol/PROTOCOL.md`](../Protocol/PROTOCOL.md).

Hashes of the current binaries are in [`README.md`](README.md).

## 3.1.0 (current)

The Windows exe was rebuilt several times under this version number while fixing discovery problems found on real
Windows 11 machines. The list below is the state of the current exe; the APK contains the Android items.

### Fixed
- **Windows: contacts never showed up next to real original (1.3.x) clients, or only after 10-15 minutes.**
  Every connection used up thread-pool threads for its whole lifetime (blocking reads, async write loop). The original
  client reconnects on every announce and leaves the old connections open, so the pool ran dry and new key exchanges
  waited. All blocking socket work now runs on dedicated threads, the pool minimum is raised, and the subnet scan opens
  at most 32 connections at a time on its own threads. *Not yet confirmed on a real Windows 11 + 1.3.x setup.*
- Discovery no longer repeats the announce after 2 s / 6 s. Announces go out on start and every refresh interval
  (default 60 s), like the original.
- The subnet scan is only a fallback: first run after 20 s, and only if no client has connected to us or no contact
  is known yet. Connections are never closed by us because of a timeout (the original client drops the user when a
  stream closes).
- Interop with the current original **2.x**: the announce carries `<tcpport>`, and file accepts honour the peer's
  `<tcpport>`; 2.x clients now see us in their contact list. If the configured port is taken, a free port is used and
  advertised.
- Empty files are skipped in folder transfers to peers older than 3.0.0 (the original receiver aborts on them).
- Android: the subnet scan no longer blocks the engine thread.

### Added
- **Windows: "Copy anonymous log"** (Settings -> Network). Copies the diagnostics log with usernames, display names,
  user ids, host names, MAC addresses, profile paths and IP addresses replaced by numbered placeholders
  (`user#1`, `ip#2`, ...), preceded by a one-line `SUMMARY:` with counters. Safe to share under strict privacy rules.
- Diagnostics in the log: every protocol step of a connection (`received PUBKEY/HNDSHK ...` with the engine queue
  length), `engine is stuck ... in <job>` when the engine thread is blocked, decrypt/handshake failures.
- Windows: Settings window like the original 1.x (General, Account, Messages, History, Alerts, Network, Advanced pages).
- Languages: all languages of the original 1.3 plus **Hungarian**; hu, de, fr, es, it, pt_BR, ru and pl are complete,
  the other languages fall back to English for strings the original did not translate. The same `lang/*.json` files
  are used by both clients.
- Profile picture gallery with stock pictures and a random default on first start, plus "Browse...".
- Status message: click your own picture on the main screen (Windows and Android).
- Contact groups, public chat, folder transfer (both directions), transfers window, sounds, emoticons, autostart.
- Android: stock pictures, profile bar, quick status dialog, public chat, folder send/receive, language selection.
- The Windows firewall rule is offered once (first start), not on every start.

### Known issues
- The exe has never run on Windows in the author's lab (Linux only), and the APK has not been run on a device.
- Large-file stress test Kotlin -> original client is occasionally flaky in the Linux lab.

## 3.0.0

- First release of the Qt-free rewrite: Kotlin Android client (Material 3, no AndroidX) and C# / Avalonia client for
  Windows and Linux, sharing the documented wire protocol (UDP announce/depart, TCP handshake with RSA + AES-256-CBC,
  XML messages, file transfer, group chat).
- Interop test lab against the original client (network namespaces, headless Qt harness).
- The original Qt sources moved to `legacy-qt/`.
- Windows: diagnostics log, adapter picker, firewall fix button.
