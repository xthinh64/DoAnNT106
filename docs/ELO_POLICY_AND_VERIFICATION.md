# Internal CHESS / CARO Elo v1

## Policy (not official FIDE / RIF compliance)

Each `(user_id, game_type)` has an independent rating, initially **1200**. Fixed **K=32**, no rating floor and no 400-point cap:

```
expectedA = 1 / (1 + 10^((ratingB - ratingA) / 400))
scoreA = 1 (FIRST wins), 0.5 (DRAW), 0 (SECOND wins)
delta = round(32 * (scoreA - expectedA), midpoint away from zero)
newA = oldA + delta
newB = oldB - delta
```

Round **one** delta, then negate it for B. Equal-rating decisive games exchange 16; equal-rating draws exchange zero. A 1400 vs 1200 player changes +8 / -8 / -24 for win / draw / loss. Wins/losses/draws increment once in that game's pool even when delta is zero. Server-validated wins, agreed draws, resignations and clock adjudications use the same terminal persistence path. `ABORTED` (including pending leave/expiry and startup abort), local/engine games and historical terminal rows are unrated. Disconnect itself freezes for recovery rather than awarding a win.

This is an **application policy**, not a universal Caro standard. FIDE standard regulations use a probability table, history/age-dependent K and rating periods; 1200/K32/per-game logistic updates are not those regulations. RIF Renju uses WHR since 2021, and its current Gomoku rules specify WHR effective October 2026. Vietnamese Caro variants must not automatically be equated with RIF Gomoku/Swap2.

Sources read for the policy decision:
- FIDE standard rating regulations: https://handbook.fide.com/chapter/B022024 (sections 8.1–8.3; amendment effective October 2025).
- RIF qualification/rating system: https://www.renju.net/qsystem (current WHR vs historical Elo appendix).
- RIF Gomoku rules: https://www.renju.net/gomokurules (rating rules and competition scope).
- A distinct local Gomoku policy, not a universal rule: http://www.piskvorky.cz/en/ratings/rating-system .

## Persistence and deployment

1. Back up the database. Stop Gateway/Auth and all writers. Do not run against an unverified database name.
2. For fresh installations import `onlinedb/game_auth.sql`, then `onlinedb/online_matches.sql`.
3. Import `onlinedb/elo_v1.sql` into the intended `onlinechessdb` (edit its `USE` deliberately for a differently named deployment). It ensures InnoDB and fills only missing CHESS/CARO stats. Existing scores/counters are not reset. Engine conversion can rebuild/lock large tables; schedule maintenance. Re-running is logically idempotent, though conversion can still take time. The existing unique `(user_id,game_type)` key is required; inspect/repair custom legacy schemas separately rather than silently deleting duplicate data.
4. As the DBA, upgrade the existing runtime account (use the exact deployed account/host, not a new wildcard account):
   ```sql
   GRANT SELECT, INSERT ON onlinechessdb.Users TO 'chess_auth'@'127.0.0.1';
   GRANT SELECT, INSERT, UPDATE ON onlinechessdb.UserGameStats TO 'chess_auth'@'127.0.0.1';
   GRANT SELECT, INSERT, UPDATE ON onlinechessdb.OnlineMatches TO 'chess_auth'@'127.0.0.1';
   ```
   `elo_v1.sql` deliberately does not create accounts or grant privileges: schema migration alone is NOT a complete upgrade. No DDL, DELETE, GRANT OPTION or administrative privileges are needed at runtime. Only when enabling legacy password migration also grant `UPDATE(password_hash)` on `Users`; revoke it after the controlled migration window.
5. Deploy server and client together, then restart. Both console startup and ServerManager start/test-DB call shared `DatabaseReadiness.CheckAsync` before starting workers/listeners. It requires InnoDB on Users/UserGameStats/OnlineMatches, the exact two-column unique stats key and all rating counters; zero-row SELECTs and plain EXPLAIN DML validate effective runtime permissions without executing writes. Missing schema/permissions produce a controlled migration/permission error, not provider text. Repair custom schemas explicitly during maintenance; startup never migrates them. There is no backfill/replay of historical terminal matches.

`MySqlMatchStore.UpdateAsync` locks the authoritative open match row with its generation/revision fence, reads the stored participants/pool, initializes/locks both stat rows in ascending user-ID order, computes from their locked pre-result scores, updates both counters/ratings and writes `snapshot.Rating` in one transaction. Commit is the only award point. `result IS NULL` rejects duplicate/conflicting/historical terminal writes. Rollback removes all partial effects, including missing-stat inserts. Optional snapshot metadata contains policy, participant IDs, pool, pre-ratings, single delta and computed post-ratings. No separate replay job is used.

The gateway keeps `NeedsReload` set through persistence and rereads the committed state before publishing. On an ambiguous failure recovery rereads the row; it does not blindly award a second result. Authenticated game connect and every new queue request reread authoritative profiles, including tickets from an existing login session. The client applies settlement to `AuthConnection.Player` **before** result callbacks; the dashboard refreshes its separate labels when activated. The authenticated packet also carries the fresh profile for reconnect.

## Executed verification (Windows .NET / disposable Windows MySQL)

No user database, installed datadir, `.env`, saved password or credential file was used. `with-temporary-mysql.ps1` initializes a unique insecure datadir, binds loopback port 33370, readiness-checks SQL and cleans only processes containing that owned path. Logs are retained. This is single-host TLS/SQL evidence, not multi-PC LAN evidence.

Commands from repository root in Windows PowerShell:

```powershell
dotnet build OnlineChessGame.slnx --configuration Release
./scripts/phase3/with-temporary-mysql.ps1 -Elo -Output artifacts/elo-full
```

The Elo switch sets both disposable test connection variables, builds/tests projects serially (`-m:1`), rejects skips/failures, retains TRX and restores environment in `finally`. Use a fresh output directory per run; aggregation includes all TRX present there. The opt-in phase-3 capacity benchmark is deliberately excluded.

Production-blocker follow-up (2026-11-28): `artifacts/elo-blockers-red3/` records both expected readiness assertion failures (missing UPDATE and missing counter accepted by the initial no-op preflight). Production decorator tests separately failed with stale 1200 profiles for both console and GUI chains before forwarding was added. `artifacts/elo-blockers-final/` records **208 passed, zero failed/skipped** (Transport 126); subsequent full Release solution build succeeded with **0 warnings, 0 errors**. Tests use a disposable account restricted to table-level runtime grants, reject missing stats UPDATE, upgrade it and complete a terminal rating transaction, exercise both actual decorator classes with persisted profiles/same-login reconnect tickets, reject missing counters/unique key/non-InnoDB, and verify optional legacy password UPDATE separately. No installed/user database was used.

Results:
- Formula RED: missing `EloPolicy` compiler failure in `artifacts/elo-red.txt`; six formula examples then passed.
- Real MySQL RED: no stats row after terminal commit, `artifacts/elo-red-mysql.txt` plus TRX. That run also had one unrelated recovery cancellation/timing failure; later complete runs passed.
- Profile/packet RED: missing reader interface, settlement Apply and packet Player compile failures observed before their implementations.
- `artifacts/elo-pass-mysql.txt`: **122 transport tests passed, zero skipped/failed**, real disposable SQL.
- `artifacts/elo-full.txt` and five TRX under `artifacts/elo-full/`: **204 passed**, zero skipped/failed (Auth 29, Caro 39, Chess 10, ServerManager 4, Transport 122).
- `artifacts/elo-build.txt`: full Release solution build succeeded, 0 errors, 2 pre-existing unused-event warnings in LoginForm/RegisterForm.

SQL tests cover conflicting/duplicate terminal CAS, missing-stat initialization, independent pools, draws/counters, concurrent shared-player settlements, rollback via an injected SQL trigger, immutable metadata reload, abort/legacy non-awards and repeated migration preserving an existing rating. A real TLS gateway + worker + MySQL regression verifies resignation publishes committed settlement and a second match on the **same sockets** advertises persisted 1216/1184 opponent ratings.

Limitations: no live WinForms visual/manual two-account session was performed for the new labels; UI changes were compiled and the underlying profile-application behavior tested. Not every later integration assertion was introduced in a separate RED cycle; the initial database, formula and profile/protocol tracer bullets were RED→GREEN. No claim of exhaustive strict TDD for all added assertions. No commit was made; existing dirty work was preserved.
