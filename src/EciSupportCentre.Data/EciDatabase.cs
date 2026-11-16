using System.Globalization;
using System.Text.Json;
using EciSupportCentre.Core;
using EciSupportCentre.Core.Models;
using EciSupportCentre.Core.Serialization;
using EciSupportCentre.Core.Storage;
using Microsoft.Data.Sqlite;

namespace EciSupportCentre.Data;

/// <summary>
/// SQLite database on the device. Everything the app shows is read from here, so the app works offline.
/// Call <see cref="InitializeAsync"/> once before use.
/// </summary>
public sealed class EciDatabase : IEciStore
{
    private readonly string _connectionString;

    public EciDatabase(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = path;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
        }.ToString();
    }

    public string Path { get; }

    /// <summary>Creates the database or upgrades it to <see cref="Schema.CurrentVersion"/>.</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, null, "PRAGMA journal_mode = WAL;", cancellationToken).ConfigureAwait(false);

        var version = Convert.ToInt32(
            await ScalarAsync(connection, null, "PRAGMA user_version;", cancellationToken).ConfigureAwait(false),
            CultureInfo.InvariantCulture);

        if (version > Schema.CurrentVersion)
        {
            throw new InvalidOperationException(
                $"Database version {version} is newer than this app supports ({Schema.CurrentVersion}).");
        }

        for (var v = version; v < Schema.CurrentVersion; v++)
        {
            await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await ExecuteAsync(connection, tx, Schema.Migrations[v], cancellationToken).ConfigureAwait(false);
            await ExecuteAsync(connection, tx, $"PRAGMA user_version = {v + 1};", cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<int> GetSchemaVersionAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(
            await ScalarAsync(connection, null, "PRAGMA user_version;", cancellationToken).ConfigureAwait(false),
            CultureInfo.InvariantCulture);
    }

    // ---------------------------------------------------------------- IEciStore (writes used by sync)

    public async Task<IReadOnlyDictionary<long, InitiativeSnapshot>> GetSnapshotsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT i.id, i.year, i.number, i.status, i.total_supporters, i.last_call, i.latest_update,
                   i.details_latest_update, EXISTS (SELECT 1 FROM initiative_details d WHERE d.initiative_id = i.id)
            FROM initiative i;
            """;

        var result = new Dictionary<long, InitiativeSnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var snapshot = new InitiativeSnapshot
            {
                Id = reader.GetInt64(0),
                Year = reader.GetString(1),
                Number = reader.GetString(2),
                Status = EciStatusExtensions.ParseStatus(reader.GetString(3)),
                TotalSupporters = reader.GetInt64(4),
                LastCall = reader.GetBoolean(5),
                LatestUpdateDate = Db.ToDateTime(reader, 6),
                DetailsLatestUpdate = Db.ToDateTime(reader, 7),
                HasDetails = reader.GetBoolean(8),
            };
            result[snapshot.Id] = snapshot;
        }

        return result;
    }

    public async Task SaveSummariesAsync(
        string language,
        IReadOnlyList<InitiativeSummary> summaries,
        IReadOnlyList<InitiativeChange> changes,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(summaries);
        ArgumentNullException.ThrowIfNull(changes);
        var lang = EciSources.Normalize(language);

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using var upsert = connection.CreateCommand();
        upsert.Transaction = tx;
        upsert.CommandText = """
            INSERT INTO initiative (id, pub_reg_num, year, number, status, total_supporters, last_call, latest_update, logo_id, first_seen_utc, updated_utc)
            VALUES ($id, $pub, $year, $number, $status, $supporters, $lastCall, $latest, $logo, $now, $now)
            ON CONFLICT (id) DO UPDATE SET
                pub_reg_num = excluded.pub_reg_num, year = excluded.year, number = excluded.number,
                status = excluded.status, total_supporters = excluded.total_supporters, last_call = excluded.last_call,
                latest_update = excluded.latest_update, logo_id = COALESCE(excluded.logo_id, initiative.logo_id),
                updated_utc = excluded.updated_utc;
            """;
        var p = Db.Parameters(upsert, "$id", "$pub", "$year", "$number", "$status", "$supporters", "$lastCall", "$latest", "$logo", "$now");

        await using var text = TextUpsert(connection, tx);
        var t = Db.Parameters(text, "$id", "$lang", "$title", "$link");

        foreach (var s in summaries)
        {
            p["$id"].Value = s.Id;
            p["$pub"].Value = s.PubRegNum;
            p["$year"].Value = s.Year;
            p["$number"].Value = s.Number;
            p["$status"].Value = s.StatusCode;
            p["$supporters"].Value = s.TotalSupporters;
            p["$lastCall"].Value = s.LastCall;
            p["$latest"].Value = Db.From(s.LatestUpdateDate);
            p["$logo"].Value = (object?)s.Logo?.Id ?? DBNull.Value;
            p["$now"].Value = Db.From(now);
            await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(s.Title))
            {
                t["$id"].Value = s.Id;
                t["$lang"].Value = string.IsNullOrEmpty(s.LanguageCode) ? lang : s.LanguageCode.ToLowerInvariant();
                t["$title"].Value = s.Title.Trim();
                t["$link"].Value = Db.From(s.SupportLink);
                await text.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        await InsertChangesAsync(connection, tx, changes, cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveDetailsAsync(InitiativeDetails details, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(details);

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Normally the list entry exists already; create a minimal row if details arrive first.
        var (year, number) = ParseRegNum(details.ComRegNum);
        await using (var ensure = connection.CreateCommand())
        {
            ensure.Transaction = tx;
            ensure.CommandText = """
                INSERT OR IGNORE INTO initiative (id, pub_reg_num, year, number, status, last_call, latest_update, first_seen_utc, updated_utc)
                VALUES ($id, $pub, $year, $number, $status, $lastCall, $latest, $now, $now);
                """;
            ensure.Parameters.AddWithValue("$id", details.Id);
            ensure.Parameters.AddWithValue("$pub", details.ComRegNum);
            ensure.Parameters.AddWithValue("$year", year);
            ensure.Parameters.AddWithValue("$number", number);
            ensure.Parameters.AddWithValue("$status", details.StatusCode);
            ensure.Parameters.AddWithValue("$lastCall", details.LastCall);
            ensure.Parameters.AddWithValue("$latest", Db.From(details.LatestUpdateDate));
            ensure.Parameters.AddWithValue("$now", Db.From(now));
            await ensure.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = tx;
            update.CommandText = """
                UPDATE initiative SET
                    original_language = $original, registration_date = $registered, start_collection_date = $start,
                    collection_end_date = $end, logo_id = COALESCE($logo, logo_id),
                    details_latest_update = $latest, details_fetched_utc = $now
                WHERE id = $id;
                """;
            update.Parameters.AddWithValue("$id", details.Id);
            update.Parameters.AddWithValue("$original", (object?)details.OriginalVersion?.LanguageCode.ToLowerInvariant() ?? DBNull.Value);
            update.Parameters.AddWithValue("$registered", Db.From(details.RegistrationDate));
            update.Parameters.AddWithValue("$start", Db.From(details.StartCollectionDate));
            update.Parameters.AddWithValue("$end", Db.From(details.CollectionEndDate));
            update.Parameters.AddWithValue("$logo", (object?)details.Logo?.Id ?? DBNull.Value);
            update.Parameters.AddWithValue("$latest", Db.From(details.LatestUpdateDate));
            update.Parameters.AddWithValue("$now", Db.From(now));
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var json = connection.CreateCommand())
        {
            json.Transaction = tx;
            json.CommandText = """
                INSERT INTO initiative_details (initiative_id, json) VALUES ($id, $json)
                ON CONFLICT (initiative_id) DO UPDATE SET json = excluded.json;
                """;
            json.Parameters.AddWithValue("$id", details.Id);
            json.Parameters.AddWithValue("$json", JsonSerializer.Serialize(details, EciJsonContext.Default.InitiativeDetails));
            await json.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var text = TextUpsert(connection, tx);
        var t = Db.Parameters(text, "$id", "$lang", "$title", "$link");
        foreach (var version in details.LinguisticVersions.Where(v => !string.IsNullOrWhiteSpace(v.Title) && !string.IsNullOrEmpty(v.LanguageCode)))
        {
            t["$id"].Value = details.Id;
            t["$lang"].Value = version.LanguageCode.ToLowerInvariant();
            t["$title"].Value = version.Title.Trim();
            t["$link"].Value = Db.From(version.SupportLink);
            await text.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> SaveNewsAsync(IReadOnlyList<NewsItem> items, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using var exists = connection.CreateCommand();
        exists.Transaction = tx;
        exists.CommandText = "SELECT 1 FROM news_item WHERE id = $id AND language = $lang;";
        var e = Db.Parameters(exists, "$id", "$lang");

        await using var upsert = connection.CreateCommand();
        upsert.Transaction = tx;
        upsert.CommandText = """
            INSERT INTO news_item (id, language, title, link, published_utc, description_html, category, image_url, fetched_utc)
            VALUES ($id, $lang, $title, $link, $published, $description, $category, $image, $now)
            ON CONFLICT (id, language) DO UPDATE SET
                title = excluded.title, link = excluded.link, published_utc = excluded.published_utc,
                description_html = excluded.description_html, category = excluded.category, image_url = excluded.image_url;
            """;
        var p = Db.Parameters(upsert, "$id", "$lang", "$title", "$link", "$published", "$description", "$category", "$image", "$now");

        var added = 0;
        foreach (var item in items)
        {
            var lang = EciSources.Normalize(item.Language);
            e["$id"].Value = item.Id;
            e["$lang"].Value = lang;
            if (await exists.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null)
            {
                added++;
            }

            p["$id"].Value = item.Id;
            p["$lang"].Value = lang;
            p["$title"].Value = item.Title;
            p["$link"].Value = Db.From(item.Link);
            p["$published"].Value = Db.From(item.Published);
            p["$description"].Value = (object?)item.DescriptionHtml ?? DBNull.Value;
            p["$category"].Value = (object?)item.Category ?? DBNull.Value;
            p["$image"].Value = Db.From(item.ImageUrl);
            p["$now"].Value = Db.From(now);
            await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return added;
    }

    public async Task<string?> GetStateAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM sync_state WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
    }

    public async Task SetStateAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO sync_state (key, value) VALUES ($key, $value)
            ON CONFLICT (key) DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", (object?)value ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    // ---------------------------------------------------------------- Reads used by the UI

    /// <summary>Every initiative, titled in <paramref name="language"/> (else English, else the original language).</summary>
    public Task<IReadOnlyList<InitiativeListItem>> GetInitiativesAsync(string language, CancellationToken cancellationToken = default) =>
        LoadItemsAsync(language, null, cancellationToken);

    public async Task<IReadOnlyList<InitiativeListItem>> QueryInitiativesAsync(InitiativeQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return query.Apply(await LoadItemsAsync(query.Language, null, cancellationToken).ConfigureAwait(false));
    }

    public async Task<InitiativeListItem?> GetInitiativeAsync(long id, string language, CancellationToken cancellationToken = default)
    {
        var items = await LoadItemsAsync(language, id, cancellationToken).ConfigureAwait(false);
        return items.Count > 0 ? items[0] : null;
    }

    public async Task<InitiativeDetails?> GetDetailsAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT json FROM initiative_details WHERE initiative_id = $id;";
        command.Parameters.AddWithValue("$id", id);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is string json
            ? JsonSerializer.Deserialize(json, EciJsonContext.Default.InitiativeDetails)
            : null;
    }

    /// <summary>Changes the user has not been notified about, oldest first.</summary>
    public Task<IReadOnlyList<InitiativeChange>> GetPendingChangesAsync(CancellationToken cancellationToken = default) =>
        LoadChangesAsync("WHERE notified = 0", null, cancellationToken);

    /// <summary>History of one initiative, oldest first.</summary>
    public Task<IReadOnlyList<InitiativeChange>> GetChangesAsync(long initiativeId, CancellationToken cancellationToken = default) =>
        LoadChangesAsync("WHERE initiative_id = $id", initiativeId, cancellationToken);

    public async Task MarkChangesNotifiedAsync(IEnumerable<long> changeIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(changeIds);

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "UPDATE initiative_change SET notified = 1 WHERE id = $id;";
        var id = command.Parameters.Add("$id", SqliteType.Integer);
        foreach (var changeId in changeIds)
        {
            id.Value = changeId;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetFollowedAsync(long initiativeId, bool followed, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = followed
            ? "INSERT OR IGNORE INTO follow (initiative_id, followed_utc) VALUES ($id, $now);"
            : "DELETE FROM follow WHERE initiative_id = $id;";
        command.Parameters.AddWithValue("$id", initiativeId);
        command.Parameters.AddWithValue("$now", Db.From(now));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Latest news in <paramref name="language"/>, newest first.</summary>
    public async Task<IReadOnlyList<NewsItem>> GetNewsAsync(string language, int limit = 50, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, language, title, link, published_utc, description_html, category, image_url
            FROM news_item WHERE language = $lang
            ORDER BY published_utc DESC, id
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$lang", EciSources.Normalize(language));
        command.Parameters.AddWithValue("$limit", limit);

        var items = new List<NewsItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            items.Add(new NewsItem
            {
                Id = reader.GetString(0),
                Language = reader.GetString(1),
                Title = reader.GetString(2),
                Link = Db.ToUri(reader, 3),
                Published = Db.ToDateTimeOffset(reader, 4),
                DescriptionHtml = reader.IsDBNull(5) ? null : reader.GetString(5),
                Category = reader.IsDBNull(6) ? null : reader.GetString(6),
                ImageUrl = Db.ToUri(reader, 7),
            });
        }

        return items;
    }

    public async Task<RegisterStatistics?> GetStatisticsAsync(CancellationToken cancellationToken = default) =>
        await GetStateAsync(StateKeys.Statistics, cancellationToken).ConfigureAwait(false) is { } json
            ? JsonSerializer.Deserialize(json, EciJsonContext.Default.RegisterStatistics)
            : null;

    public async Task<DateTimeOffset?> GetLastSyncAsync(CancellationToken cancellationToken = default) =>
        DateTimeOffset.TryParse(
            await GetStateAsync(StateKeys.LastSync, cancellationToken).ConfigureAwait(false),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var value)
            ? value
            : null;

    // ---------------------------------------------------------------- Helpers

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async Task<IReadOnlyList<InitiativeListItem>> LoadItemsAsync(string language, long? id, CancellationToken cancellationToken)
    {
        var lang = EciSources.Normalize(language);
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        // Titles in every stored language, to pick: requested > English > original > any.
        var texts = new Dictionary<long, Dictionary<string, (string Title, Uri? Link)>>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT initiative_id, language, title, support_link FROM initiative_text" + (id is null ? ";" : " WHERE initiative_id = $id;");
            command.Parameters.AddWithValue("$id", (object?)id ?? DBNull.Value);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var key = reader.GetInt64(0);
                if (!texts.TryGetValue(key, out var byLanguage))
                {
                    texts[key] = byLanguage = new Dictionary<string, (string, Uri?)>(StringComparer.Ordinal);
                }

                byLanguage[reader.GetString(1)] = (reader.GetString(2), Db.ToUri(reader, 3));
            }
        }

        var items = new List<InitiativeListItem>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT i.id, i.pub_reg_num, i.year, i.number, i.status, i.total_supporters, i.last_call, i.latest_update,
                       i.logo_id, i.original_language, i.registration_date, i.start_collection_date, i.collection_end_date,
                       i.details_fetched_utc IS NOT NULL, f.initiative_id IS NOT NULL
                FROM initiative i
                LEFT JOIN follow f ON f.initiative_id = i.id
                """ + (id is null ? ";" : " WHERE i.id = $id;");
            command.Parameters.AddWithValue("$id", (object?)id ?? DBNull.Value);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var itemId = reader.GetInt64(0);
                var pubRegNum = reader.GetString(1);
                var original = reader.IsDBNull(9) ? null : reader.GetString(9);
                var (title, titleLanguage, link) = PickText(texts.GetValueOrDefault(itemId), lang, original, pubRegNum);

                items.Add(new InitiativeListItem
                {
                    Id = itemId,
                    PubRegNum = pubRegNum,
                    Year = reader.GetString(2),
                    Number = reader.GetString(3),
                    Status = EciStatusExtensions.ParseStatus(reader.GetString(4)),
                    TotalSupporters = reader.GetInt64(5),
                    LastCall = reader.GetBoolean(6),
                    LatestUpdateDate = Db.ToDateTime(reader, 7),
                    LogoId = reader.IsDBNull(8) ? null : reader.GetInt64(8),
                    RegistrationDate = Db.ToDate(reader, 10),
                    StartCollectionDate = Db.ToDate(reader, 11),
                    CollectionEndDate = Db.ToDate(reader, 12),
                    HasDetails = reader.GetBoolean(13),
                    IsFollowed = reader.GetBoolean(14),
                    Title = title,
                    TitleLanguage = titleLanguage,
                    SupportLink = link,
                });
            }
        }

        return items;
    }

    private static (string Title, string Language, Uri? Link) PickText(
        Dictionary<string, (string Title, Uri? Link)>? texts, string language, string? original, string fallbackTitle)
    {
        if (texts is null || texts.Count == 0)
        {
            return (fallbackTitle, language, null);
        }

        foreach (var candidate in new[] { language, EciSources.DefaultLanguage, original })
        {
            if (candidate is not null && texts.TryGetValue(candidate, out var text))
            {
                // Prefer the signing page in the requested language even when the title falls back.
                var link = texts.TryGetValue(language, out var own) && own.Link is not null ? own.Link : text.Link;
                return (text.Title, candidate, link);
            }
        }

        var any = texts.OrderBy(kv => kv.Key, StringComparer.Ordinal).First();
        return (any.Value.Title, any.Key, any.Value.Link);
    }

    private async Task<IReadOnlyList<InitiativeChange>> LoadChangesAsync(string where, long? initiativeId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT id, initiative_id, kind, old_value, new_value, detected_utc, notified FROM initiative_change {where} ORDER BY id;";
        command.Parameters.AddWithValue("$id", (object?)initiativeId ?? DBNull.Value);

        var changes = new List<InitiativeChange>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!Enum.TryParse<ChangeKind>(reader.GetString(2), out var kind))
            {
                continue; // Written by a newer app version.
            }

            changes.Add(new InitiativeChange
            {
                Id = reader.GetInt64(0),
                InitiativeId = reader.GetInt64(1),
                Kind = kind,
                OldValue = reader.IsDBNull(3) ? null : reader.GetString(3),
                NewValue = reader.IsDBNull(4) ? null : reader.GetString(4),
                DetectedAt = Db.ToDateTimeOffset(reader, 5) ?? DateTimeOffset.MinValue,
                Notified = reader.GetBoolean(6),
            });
        }

        return changes;
    }

    private static async Task InsertChangesAsync(SqliteConnection connection, SqliteTransaction tx, IReadOnlyList<InitiativeChange> changes, CancellationToken cancellationToken)
    {
        if (changes.Count == 0)
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = """
            INSERT INTO initiative_change (initiative_id, kind, old_value, new_value, detected_utc, notified)
            VALUES ($id, $kind, $old, $new, $detected, $notified);
            """;
        var p = Db.Parameters(command, "$id", "$kind", "$old", "$new", "$detected", "$notified");
        foreach (var change in changes)
        {
            p["$id"].Value = change.InitiativeId;
            p["$kind"].Value = change.Kind.ToString();
            p["$old"].Value = (object?)change.OldValue ?? DBNull.Value;
            p["$new"].Value = (object?)change.NewValue ?? DBNull.Value;
            p["$detected"].Value = Db.From(change.DetectedAt);
            p["$notified"].Value = change.Notified;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static SqliteCommand TextUpsert(SqliteConnection connection, SqliteTransaction tx)
    {
        var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = """
            INSERT INTO initiative_text (initiative_id, language, title, support_link) VALUES ($id, $lang, $title, $link)
            ON CONFLICT (initiative_id, language) DO UPDATE SET
                title = excluded.title, support_link = COALESCE(excluded.support_link, initiative_text.support_link);
            """;
        return command;
    }

    private static async Task ExecuteAsync(SqliteConnection connection, SqliteTransaction? tx, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<object?> ScalarAsync(SqliteConnection connection, SqliteTransaction? tx, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Splits <c>ECI(2026)000009</c> into year and number.</summary>
    internal static (string Year, string Number) ParseRegNum(string regNum)
    {
        var open = regNum.IndexOf('(', StringComparison.Ordinal);
        var close = regNum.IndexOf(')', StringComparison.Ordinal);
        return open >= 0 && close > open
            ? (regNum[(open + 1)..close], regNum[(close + 1)..])
            : ("", regNum);
    }
}
