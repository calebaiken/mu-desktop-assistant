# Infrastructure boundary

Implements bounded JSON settings with atomic persistence and recovery, content-free
rotating JSONL event logs, and current-user single-instance activation. References
Application/Core, never Desktop. No external production packages. Future adapters
include HTTP providers, SQLite, credentials, audio, approved tools and worker IPC.
Service APIs support cancellation and isolated test profiles; no AI calls exist yet.
