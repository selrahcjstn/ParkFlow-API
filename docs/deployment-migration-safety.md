# Database and release safety

## Startup

Normal startup performs a read-only migration-history/model check, then runs the existing administrator seeder. It never creates a database or runs EF migrations. Missing, unknown or conflicting migration IDs stop startup with an actionable error. Connection failures also stop startup before seeding.

`ParkFlow.API --check-database` performs the same check and exits without seeding, starting background workers, or serving requests. Supply the same configuration/environment as the deployed service. This is the staged release preflight; its database identity only needs metadata read permissions.

## History-preserving compatibility migration

`20261027202102_RetireFinancialContractsPreserveHistory` replaces the unpublished, unapplied deletion proposal. Production was confirmed to use the one configured RDS database; read-only inspection on 2026-10-28 found the last applied migration was `20261021104419_AddDriverDocumentsAndDurableSettings`. No previously applied migration was changed.

The current EF model/snapshot retires financial CLR mappings. The replacement migration verifies the expected historical table/invoice columns and records the model transition without dropping, deleting or updating historical storage. `Violations`, `Feedbacks.Invoice*` and `UserNotifications` remain intact. Its `Down` needs no physical schema change. Applying it still writes EF migration metadata and therefore requires a separate operator step; it has not been applied during implementation.

If `20261027202102_RemovePaymentAndViolationHistory` is found in any target history, STOP. Startup and the compatibility migration reject that conflict. Do not rename history rows or reinterpret a destructive migration as the compatibility migration. Review the actual target and recovery policy before proceeding.

The original deletion proposal is archived as `docs/deferred-migrations/20261027202102_RemovePaymentAndViolationHistory.cs.txt`. It is not compiled, discovered by EF, or executable through the release workflow. Historical cleanup needs a new, separately approved retention/backup/recovery review; this release does not authorize deletion. The normal-release migration safety test rejects destructive operations/raw SQL cleanup.

## Separate operator migration step

1. Confirm the target, applied IDs, verified backup and restore rehearsal. Never expose credentials in generated artifacts or logs.
2. Build the exact reviewed commit and inspect model consistency using `dotnet ef migrations has-pending-model-changes --project ParkFlow.Persistence --startup-project ParkFlow.API`.
3. Generate and review the transition from the verified target's last applied ID (expected `20261021104419_AddDriverDocumentsAndDurableSettings`, subject to target inspection) through `20261028140506_EnsureCorDocumentColumns` using `dotnet ef migrations script <from> <to> --project ParkFlow.Persistence --startup-project ParkFlow.API --output <reviewed-file>`. The four pending migrations, in order, are `20261027202102_RetireFinancialContractsPreserveHistory`, `20261028094839_SeparateVisitorVehicles`, `20261028134540_EnforceVehiclePlateOwnership`, and `20261028140506_EnsureCorDocumentColumns`. Review visitor/plate conflicts, preserved history, required role grants and recovery before authorizing application. Synthetic rehearsal is not production-data approval.
4. An authorized operator applies the reviewed SQL separately with migration credentials. The application/deployment workflow does not apply it and no production migration workflow currently exists. Verify historical records/columns/counts, all four migration-history rows and runtime registry/function permissions afterward. The visitor/plate/COR transitions refuse automatic destructive reversal; use a reviewed recovery plan.
5. Run `--check-database` using production configuration before approving release. Never set a financial-deletion approval option on the runtime connection.

## CI/CD gates

- API production deployment is manual from `main` and requires all three release/schema/client-compatibility confirmations. It calls the existing Verification and OCR workflow for the same commit; any test/build failure blocks deployment. The `production` environment is an additional protection boundary.
- Configure required reviewers and restrict `production` to `main` in GitHub repository settings. Workflow source cannot create those server-side protections. Explicit manual confirmations remain mandatory even if environment reviewers are not yet configured.
- New binaries are staged under `/home/ubuntu/app/releases/<sha>`. The existing `appsettings.Production.json` is copied on the server for the read-only preflight before stopping the live service. If that file is absent or differs from service runtime configuration, stop and align the preflight configuration; do not bypass it. API paths, SSH secret names and the systemd service remain unchanged. No migration command runs over SSH. A post-start HTTP smoke check uses the existing port 5000 Swagger endpoint and verifies the service remains active.
- Mobile type and contract/interaction checks run on `main` and `release/phase-4-5-*` pushes and PRs targeting `main`. OTA/native release is manual from `main`, depends on those checks and explicit backend-compatibility/release confirmations, and uses the `mobile-preview` environment. Configure its required reviewers and the HTTPS `EXPO_PUBLIC_API_URL` variable in the mobile repository. A secret with that name does not satisfy the workflow's variable reference. Publication remains on the existing preview channel.
- Admin release branches run validation only; `vercel.json` disables Vercel Git deployment for `release/phase-4-5-*`. Admin production publishing is not supplied by a GitHub Actions workflow and must remain blocked until its approved deployment path and required checks are configured. Do not treat an automatic Vercel integration as a protected Actions release.
- Full backend/admin/mobile suites passed locally during Phase 4.5; hosted verification must still execute on the exact release commits. No checks are disabled or allowed to fail. Removed API routes/contracts require a compatible client rollout; keeping storage does not restore those endpoints. Confirm real TLS, bootstrap security, backups, database grants, release environment reviewers and patched published runtime before any production operation.

## Limits

Startup checks verify migration history/model and mapped column presence, but do not detect all manual schema drift or verify production grants. A staged schema check is not an authenticated end-to-end health check. This workflow does not provide automatic application/data rollback; keep a reviewed recovery/backup plan. Only run the release transition after confirming no target has applied the archived proposal. Do not claim that a `production` environment reference creates required reviewers: operators must configure and verify those server-side protections before release.

References: [EF migration deployment guidance](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying), [GitHub reusable verification workflows](https://docs.github.com/en/actions/how-tos/reuse-automations/reuse-workflows).
