namespace EciSupportCentre.Data;

/// <summary>
/// Database schema, versioned with <c>PRAGMA user_version</c>. Never edit a released migration; append a new one.
/// </summary>
internal static class Schema
{
    /// <summary>Migration <c>i</c> upgrades the database from version <c>i</c> to <c>i + 1</c>.</summary>
    public static readonly string[] Migrations =
    [
        // v1
        """
        CREATE TABLE initiative (
            id                    INTEGER PRIMARY KEY,
            pub_reg_num           TEXT    NOT NULL,
            year                  TEXT    NOT NULL,
            number                TEXT    NOT NULL,
            status                TEXT    NOT NULL,
            total_supporters      INTEGER NOT NULL DEFAULT 0,
            last_call             INTEGER NOT NULL DEFAULT 0,
            latest_update         TEXT,
            logo_id               INTEGER,
            original_language     TEXT,
            registration_date     TEXT,
            start_collection_date TEXT,
            collection_end_date   TEXT,
            details_latest_update TEXT,
            details_fetched_utc   TEXT,
            first_seen_utc        TEXT    NOT NULL,
            updated_utc           TEXT    NOT NULL
        );

        CREATE TABLE initiative_text (
            initiative_id INTEGER NOT NULL REFERENCES initiative(id) ON DELETE CASCADE,
            language      TEXT    NOT NULL,
            title         TEXT    NOT NULL,
            support_link  TEXT,
            PRIMARY KEY (initiative_id, language)
        );

        CREATE TABLE initiative_details (
            initiative_id INTEGER PRIMARY KEY REFERENCES initiative(id) ON DELETE CASCADE,
            json          TEXT NOT NULL
        );

        CREATE TABLE initiative_change (
            id            INTEGER PRIMARY KEY AUTOINCREMENT,
            initiative_id INTEGER NOT NULL REFERENCES initiative(id) ON DELETE CASCADE,
            kind          TEXT    NOT NULL,
            old_value     TEXT,
            new_value     TEXT,
            detected_utc  TEXT    NOT NULL,
            notified      INTEGER NOT NULL DEFAULT 0
        );
        CREATE INDEX ix_initiative_change_pending ON initiative_change (notified, id);

        CREATE TABLE news_item (
            id               TEXT    NOT NULL,
            language         TEXT    NOT NULL,
            title            TEXT    NOT NULL,
            link             TEXT,
            published_utc    TEXT,
            description_html TEXT,
            category         TEXT,
            image_url        TEXT,
            fetched_utc      TEXT    NOT NULL,
            is_read          INTEGER NOT NULL DEFAULT 0,
            PRIMARY KEY (id, language)
        );
        CREATE INDEX ix_news_item_published ON news_item (language, published_utc DESC);

        CREATE TABLE follow (
            initiative_id INTEGER PRIMARY KEY,
            followed_utc  TEXT NOT NULL
        );

        CREATE TABLE sync_state (
            key   TEXT PRIMARY KEY,
            value TEXT
        );
        """,
    ];

    public static int CurrentVersion => Migrations.Length;
}
