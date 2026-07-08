# Migrations

This project uses **EF Core Migrations**, not a hand-rolled SQL runner — the mechanics differ
from a Node.js-style `migrations/*.sql` + glob-and-execute script in ways that change which
precautions actually matter here.

- **`dotnet ef database update` is not blind re-execution.** EF Core tracks every applied
  migration in a `__EFMigrationsHistory` table per database (`UserManagementDb`,
  `ProductCatalogDb`). `dotnet ef database update` only applies migrations *not yet* recorded
  there — it does not re-run `Initial` or any other already-applied migration on every call.
  The classic idempotency guards from raw-SQL migration runners (`IF NOT EXISTS` on
  `CREATE TABLE`/`CREATE INDEX`, `ON CONFLICT DO NOTHING` on seed `INSERT`s) are generally
  unnecessary for standard `migrationBuilder.CreateTable(...)`, `AddColumn(...)`,
  `CreateIndex(...)` calls, because the history table already gates re-execution.
- **The one place idempotency still matters: raw SQL inside a migration.** If a migration ever
  needs `migrationBuilder.Sql("...")` for something the builder can't express (backfilling a
  column, seeding reference data), write that SQL defensively anyway
  (`WHERE NOT EXISTS (...)`, `IF NOT EXISTS`) — the history table protects against *EF Core*
  re-running the migration, but not against someone applying it against a database that already
  has the same data from a manual script, a restored backup, or a manually-edited
  `__EFMigrationsHistory` row.
- **Never edit a migration file under `Migrations/` once it may have been applied anywhere**
  (dev box, CI, teammate's machine) — this is already called out as `IMPORTANT` in
  `CLAUDE.md`. Editing an applied migration's `Up()`/`Down()` does nothing on databases that
  already ran the old version (the history table just sees the same migration ID as "done"),
  so the file silently drifts from what actually executed. Add a new migration
  (`dotnet ef migrations add <Name>`) that makes the additional change instead of touching the
  old file. The only safe exception is a migration that has **only ever existed locally and was
  never applied to a shared/committed database** — use `dotnet ef migrations remove` to delete
  it and regenerate, rather than hand-editing it.
- **Down() exists, but treat it like the Node.js "no rollback" rule anyway for anything already
  applied beyond a throwaway local database.** EF Core generates a `Down()` method, and
  `dotnet ef database update <PreviousMigrationName>` will run it — but `Down()` for a dropped
  column or dropped table cannot restore the data that was in it. Prefer "add a new forward
  migration that reverses the change" over relying on `Down()` once a migration has shipped
  anywhere real data could have been added.
- **Destructive operations (`DropColumn`, `DropTable`, `migrationBuilder.Sql("TRUNCATE ...")`)
  need the same "what if this runs against a partially-migrated database" thinking** the
  Node.js guidance calls for, even though EF Core wraps each migration in a transaction on SQL
  Server (so a mid-migration failure rolls back that migration, not just aborts uncommitted).
  Think through: does this migration assume a prior migration's column/table already exists on
  every environment this will run against? If a column being dropped might not exist on some
  environment's history (e.g. a migration was skipped or applied out of band), guard the raw
  SQL path with `IF EXISTS`.
- **The two services have independent, unrelated migration histories.** UserManagement's
  `Migrations/` and ProductCatalog's `Migrations/` are separate projects with separate
  `__EFMigrationsHistory` tables in separate databases — never assume applying one implies
  anything about the other. Always pass the correct `--project`/`--startup-project` pair (see
  `CLAUDE.md` Commands section) and check `dotnet ef migrations list --project ... --startup-project ...`
  for the specific service before assuming what's already applied.
