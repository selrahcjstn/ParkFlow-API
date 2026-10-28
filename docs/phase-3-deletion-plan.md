# Historical financial cleanup — deferred

The previous deletion proposal was unpublished and unapplied in the confirmed shared target. It has been replaced in the EF release chain by `20261027202102_RetireFinancialContractsPreserveHistory`, which retains historical tables, invoice columns, and notifications.

See [deployment-migration-safety.md](deployment-migration-safety.md) for the current operator/release process. The original C# proposal is archived as `deferred-migrations/20261027202102_RemovePaymentAndViolationHistory.cs.txt`; it is not compiled or part of EF migration discovery.

No deletion is approved by this release. A future cleanup requires a new retention decision, exact target/candidate review, verified backup and restore rehearsal, and separate explicit approval. Never add cleanup to API startup or enable its approval setting on ordinary runtime connections. If the archived migration ID is discovered as already applied in another target, stop and resolve that history/recovery conflict before any migration changes.
