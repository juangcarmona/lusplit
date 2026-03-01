import { DatabaseSync } from 'node:sqlite';

const MIGRATION_V1_SQL = [
  // Child tables use (group_id, id) as the relational key for group-scoped foreign keys,
  // and UNIQUE(id) to keep ids globally unique so application getById(id) remains unambiguous.
  `CREATE TABLE IF NOT EXISTS groups (
    id TEXT PRIMARY KEY,
    currency TEXT NOT NULL,
    closed INTEGER NOT NULL CHECK (closed IN (0, 1))
  )`,
  `CREATE TABLE IF NOT EXISTS economic_units (
    group_id TEXT NOT NULL,
    id TEXT NOT NULL,
    owner_participant_id TEXT NOT NULL,
    name TEXT,
    PRIMARY KEY (group_id, id),
    UNIQUE (id),
    FOREIGN KEY (group_id) REFERENCES groups(id) ON DELETE NO ACTION
  )`,
  `CREATE TABLE IF NOT EXISTS participants (
    group_id TEXT NOT NULL,
    id TEXT NOT NULL,
    economic_unit_id TEXT NOT NULL,
    name TEXT NOT NULL,
    consumption_category TEXT NOT NULL CHECK (consumption_category IN ('FULL', 'HALF', 'CUSTOM')),
    custom_consumption_weight TEXT,
    PRIMARY KEY (group_id, id),
    UNIQUE (id),
    FOREIGN KEY (group_id) REFERENCES groups(id) ON DELETE NO ACTION,
    FOREIGN KEY (group_id, economic_unit_id) REFERENCES economic_units(group_id, id) ON DELETE NO ACTION
  )`,
  `CREATE TABLE IF NOT EXISTS expenses (
    group_id TEXT NOT NULL,
    id TEXT NOT NULL,
    title TEXT NOT NULL,
    paid_by_participant_id TEXT NOT NULL,
    amount_minor INTEGER NOT NULL,
    date TEXT NOT NULL,
    split_definition_json TEXT NOT NULL,
    notes TEXT,
    PRIMARY KEY (group_id, id),
    UNIQUE (id),
    FOREIGN KEY (group_id) REFERENCES groups(id) ON DELETE NO ACTION,
    FOREIGN KEY (group_id, paid_by_participant_id) REFERENCES participants(group_id, id) ON DELETE NO ACTION
  )`,
  `CREATE TABLE IF NOT EXISTS transfers (
    group_id TEXT NOT NULL,
    id TEXT NOT NULL,
    from_participant_id TEXT NOT NULL,
    to_participant_id TEXT NOT NULL,
    amount_minor INTEGER NOT NULL,
    date TEXT NOT NULL,
    type TEXT NOT NULL CHECK (type IN ('GENERATED', 'MANUAL')),
    note TEXT,
    PRIMARY KEY (group_id, id),
    UNIQUE (id),
    FOREIGN KEY (group_id) REFERENCES groups(id) ON DELETE NO ACTION,
    FOREIGN KEY (group_id, from_participant_id) REFERENCES participants(group_id, id) ON DELETE NO ACTION,
    FOREIGN KEY (group_id, to_participant_id) REFERENCES participants(group_id, id) ON DELETE NO ACTION
  )`,
  `CREATE TABLE IF NOT EXISTS projection_snapshots (
    id TEXT PRIMARY KEY,
    group_id TEXT NOT NULL,
    projection_type TEXT NOT NULL,
    payload_json TEXT NOT NULL,
    created_at TEXT NOT NULL,
    FOREIGN KEY (group_id) REFERENCES groups(id) ON DELETE NO ACTION
  )`,
  'CREATE INDEX IF NOT EXISTS idx_participants_group_id ON participants(group_id, id)',
  'CREATE INDEX IF NOT EXISTS idx_economic_units_group_id ON economic_units(group_id, id)',
  'CREATE INDEX IF NOT EXISTS idx_expenses_group_id ON expenses(group_id, id)',
  'CREATE INDEX IF NOT EXISTS idx_transfers_group_id ON transfers(group_id, id)'
];

export const applyMigrations = async (db: DatabaseSync): Promise<void> => {
  db.exec('PRAGMA foreign_keys = ON');

  db.exec(`CREATE TABLE IF NOT EXISTS schema_version (
    version INTEGER PRIMARY KEY,
    applied_at TEXT NOT NULL
  )`);

  const applied = db.prepare('SELECT version FROM schema_version WHERE version = 1').get() as { version: number } | undefined;
  if (applied?.version === 1) {
    return;
  }

  db.exec('BEGIN');

  try {
    for (const statement of MIGRATION_V1_SQL) {
      db.exec(statement);
    }

    db.prepare('INSERT INTO schema_version (version, applied_at) VALUES (?, ?)').run(
      1,
      new Date().toISOString()
    );

    db.exec('COMMIT');
  } catch (error) {
    db.exec('ROLLBACK');
    throw error;
  }
};
