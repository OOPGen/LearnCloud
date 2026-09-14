# Archive

Nothing in this folder is authoritative. It is kept for history only.

## 2026-08-fix-reports/

45 status reports and audits written before September 2026. **They describe the
system as complete, security-hardened and production-ready. It was not, and is
not.** They were authored in an environment with no .NET SDK, so no claim in any
of them was ever verified against a compiler. `Fix_C1_Buildable_Solution_Report.md`
says so in its own status line.

Treat every checkmark in these files as an intention, not a result.

## unwired-source/

`Permissions.cs`, `SeedDefaultRoles.cs` and `seed_roles.sql` sat in the repository
root. They belong to no project and are compiled by nothing. The permission
catalogue and role seed data in them may be worth reusing when role seeding is
built for real; nothing references them today.

## ui-previews/

Two standalone login page mockups. Superseded by `src/LearnCloud.Web`.
