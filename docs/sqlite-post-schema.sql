PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS profiles (
  id TEXT NOT NULL PRIMARY KEY,
  user_name TEXT NULL,
  name TEXT NULL,
  banner_url TEXT NULL,
  image_url TEXT NULL,
  following INTEGER NULL,
  count_media INTEGER NULL
);

CREATE TABLE IF NOT EXISTS posts (
  id TEXT NOT NULL PRIMARY KEY,
  profile_id TEXT NOT NULL,
  description TEXT NOT NULL,
  retweeted INTEGER NOT NULL,
  favorited INTEGER NOT NULL,
  bookmarked INTEGER NOT NULL,
  created_at TEXT NOT NULL,
  FOREIGN KEY (profile_id) REFERENCES profiles(id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS post_hashtags (
  id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
  post_id TEXT NOT NULL,
  value TEXT NOT NULL,
  ordinal INTEGER NOT NULL,
  FOREIGN KEY (post_id) REFERENCES posts(id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS post_medias (
  id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
  post_id TEXT NOT NULL,
  media_id TEXT NOT NULL,
  url TEXT NOT NULL,
  type TEXT NOT NULL,
  video_duration_milis INTEGER NULL,
  ordinal INTEGER NOT NULL,
  FOREIGN KEY (post_id) REFERENCES posts(id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS post_media_variants (
  id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
  media_ref_id INTEGER NOT NULL,
  content_type TEXT NOT NULL,
  bitrate INTEGER NULL,
  url TEXT NOT NULL,
  ordinal INTEGER NOT NULL,
  FOREIGN KEY (media_ref_id) REFERENCES post_medias(id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS post_index_entries (
  id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
  post_id TEXT NOT NULL,
  user_id TEXT NOT NULL,
  origin TEXT NOT NULL,
  previous TEXT NULL,
  next TEXT NULL,
  FOREIGN KEY (post_id) REFERENCES posts(id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS post_changes (
  id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
  post_id TEXT NOT NULL,
  user_id TEXT NOT NULL,
  date TEXT NOT NULL,
  change_type TEXT NOT NULL,
  FOREIGN KEY (post_id) REFERENCES posts(id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS post_change_fields (
  id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
  change_id INTEGER NOT NULL,
  field TEXT NOT NULL,
  old_value_json TEXT NULL,
  new_value_json TEXT NULL,
  FOREIGN KEY (change_id) REFERENCES post_changes(id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS post_meta (
  id TEXT NOT NULL PRIMARY KEY,
  hash TEXT NOT NULL,
  deleted INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS ix_post_hashtags_post_id_ordinal
  ON post_hashtags(post_id, ordinal);

CREATE INDEX IF NOT EXISTS ix_post_medias_post_id_ordinal
  ON post_medias(post_id, ordinal);

CREATE INDEX IF NOT EXISTS ix_post_media_variants_media_ref_id_ordinal
  ON post_media_variants(media_ref_id, ordinal);

CREATE UNIQUE INDEX IF NOT EXISTS ix_post_index_entries_post_id_user_id_origin
  ON post_index_entries(post_id, user_id, origin);

CREATE INDEX IF NOT EXISTS ix_post_index_entries_user_id_origin_post_id
  ON post_index_entries(user_id, origin, post_id);

CREATE INDEX IF NOT EXISTS ix_post_changes_post_id_date_id
  ON post_changes(post_id, date, id);

CREATE INDEX IF NOT EXISTS ix_post_change_fields_change_id_field
  ON post_change_fields(change_id, field);
