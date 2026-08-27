# Why
The advertised backup endpoint stores only metadata and restore only records a payload. Operators cannot recover a deployment from these artifacts.

# What Changes
Add encrypted, integrity-checked full backup artifacts, restore validation, an offline restore workflow, retention controls, and recovery drills.

# Capabilities
## New Capabilities
- `restorable-self-hosted-backups`: backups that can recreate supported database and object-storage state.

# Dependencies and Non-goals
- Dependencies: `platform-foundation` and self-hosted distribution.
- Non-goals: cross-major-version migration, managed-cloud disaster recovery, and continuous replication.

# Impact
Changes admin backup contracts, storage adapters, operator tooling, audit records, Flutter admin status, and deployment documentation.
