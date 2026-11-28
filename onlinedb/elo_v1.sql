USE onlinechessdb;
-- Run with Gateway/Auth stopped, after game_auth.sql and online_matches.sql.
-- DBA MUST also upgrade runtime grants separately: SELECT,INSERT,UPDATE on UserGameStats.
-- See docs/ELO_POLICY_AND_VERIFICATION.md; this migration intentionally grants no account privileges.
-- Existing scores/counters and historical terminal matches are never replayed/reset.
-- Engine conversion may rebuild/lock tables; back up and schedule maintenance first.
ALTER TABLE Users ENGINE=InnoDB;
ALTER TABLE UserGameStats ENGINE=InnoDB;
ALTER TABLE OnlineMatches ENGINE=InnoDB;
-- Unique (user_id,game_type) already exists in game_auth.sql.
INSERT INTO UserGameStats(user_id,game_type,elo,wins,losses,draws)
SELECT u.user_id, p.game_type,1200,0,0,0 FROM Users u
CROSS JOIN (SELECT 'CHESS' AS game_type UNION ALL SELECT 'CARO') p
LEFT JOIN UserGameStats s ON s.user_id=u.user_id AND s.game_type=p.game_type
WHERE s.user_id IS NULL;
-- Settlement audit lives in OnlineMatches.snapshot.Rating in the same transaction.
-- No DDL/ledger is needed for that optional JSON field; old snapshots remain readable.
