# Archived hand-written SQL migrations

These 47 scripts were the original schema, written by hand in MySQL and later
converted to PostgreSQL. **Do not run them.** They are kept for reference only.

They were retired in Phase 2 (September 2026) because:

- They were never executed by anything: there was no migration runner.
- They disagreed with each other and with the code. `grades` was created by two
  different scripts; version `V2` was missing and `V15` was used twice; MySQL and
  PostgreSQL copies were maintained side by side and drifted.
- They did not match the entity model the services actually query.

The schema is now generated from the EF Core model and lives in
`src/LearnCloud.Api/Data/Migrations`. Apply it with:

```
dotnet run --project src/LearnCloud.Api -- --migrate
```

Files are grouped by the module they came from (`Auth/`, `Fees/`, ...), with the
`deployment/` copies kept separately.

Worth carrying forward from these files when the relevant work comes up:

- `Auth/V19_Postgres_Migration.sql` contains PostgreSQL
  row-level security policies. The API does not rely on them, but they are a
  candidate defence-in-depth layer for tenant isolation.
- `V17`/`V18` optimisation scripts list indexes chosen for expected query patterns;
  compare against the generated indexes before production load testing.

If a Supabase or other database was ever created from these scripts, recreate it from
the EF Core migrations rather than trying to reconcile the two.
