using Microsoft.Data.Sqlite;
using Rimdex.Models;

namespace Rimdex.Data;

internal readonly record struct DbStats(long Mods, long Embeddings);

internal sealed record PendingEmbeddingRow(int ModId, string SearchText, string SearchTextHash);

internal sealed record ModEmbedding(int ModId, string SearchTextHash, byte[] Embedding, int Dimension);

internal sealed record SearchResultRow(
    string PublishedFileId,
    string Title,
    string Description,
    string PreviewUrl,
    long Subscriptions,
    long Views);

internal sealed class ModRepository(string dbPath) {
    private static bool _schemaInitialized;

    public void Import(IReadOnlyList<ModDetail> mods) {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
                              insert into mods (
                                publishedfileid, title, description, preview_url,
                                subscriptions, views, time_updated, is_translation,
                                search_text, search_text_hash
                              ) values (
                                $publishedfileid, $title, $description, $preview_url,
                                $subscriptions, $views, $time_updated, $is_translation,
                                $search_text, $search_text_hash
                              )
                              on conflict(publishedfileid) do update set
                                title = excluded.title,
                                description = excluded.description,
                                preview_url = excluded.preview_url,
                                subscriptions = excluded.subscriptions,
                                views = excluded.views,
                                time_updated = excluded.time_updated,
                                is_translation = excluded.is_translation,
                                search_text = excluded.search_text,
                                search_text_hash = excluded.search_text_hash
                              """;

        var pId = command.Parameters.Add("$publishedfileid", SqliteType.Text);
        var pTitle = command.Parameters.Add("$title", SqliteType.Text);
        var pDesc = command.Parameters.Add("$description", SqliteType.Text);
        var pPrev = command.Parameters.Add("$preview_url", SqliteType.Text);
        var pSubs = command.Parameters.Add("$subscriptions", SqliteType.Integer);
        var pViews = command.Parameters.Add("$views", SqliteType.Integer);
        var pUpdated = command.Parameters.Add("$time_updated", SqliteType.Integer);
        var pTrans = command.Parameters.Add("$is_translation", SqliteType.Integer);
        var pSearch = command.Parameters.Add("$search_text", SqliteType.Text);
        var pHash = command.Parameters.Add("$search_text_hash", SqliteType.Text);

        foreach (var mod in mods) {
            pId.Value = mod.PublishedFileId;
            pTitle.Value = mod.Title;
            pDesc.Value = mod.Description;
            pPrev.Value = mod.PreviewUrl;
            pSubs.Value = mod.Subscriptions;
            pViews.Value = mod.Views;
            pUpdated.Value = mod.TimeUpdated;
            pTrans.Value = mod.IsTranslation ? 1 : 0;
            pSearch.Value = mod.SearchText;
            pHash.Value = mod.SearchTextHash;
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public DbStats ReadStats() {
        using var connection = OpenConnection();
        return new DbStats(Count(connection, "mods"), Count(connection, "mod_embeddings"));
    }

    public long ReadMaxUpdatedTime() {
        using var connection = OpenConnection();
        var value = ExecuteScalar(connection, "select max(time_updated) from mods");
        return value is null or DBNull ? 0 : (long)value;
    }

    public IReadOnlyList<PendingEmbeddingRow> ReadPendingEmbeddingRows(int limit, string model) {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              select
                                mods.id,
                                mods.search_text,
                                mods.search_text_hash
                              from mods
                              left join mod_embeddings on mod_embeddings.mod_id = mods.id
                              where mod_embeddings.mod_id is null
                                 or mod_embeddings.model != $model
                                 or mod_embeddings.search_text_hash != mods.search_text_hash
                              order by mods.id
                              limit $limit
                              """;
        command.Parameters.AddWithValue("$model", model);
        command.Parameters.AddWithValue("$limit", limit);

        using var reader = command.ExecuteReader();
        var rows = new List<PendingEmbeddingRow>();
        while (reader.Read()) {
            rows.Add(new PendingEmbeddingRow(reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
        }

        return rows;
    }

    public void UpsertEmbeddings(string model, IReadOnlyList<ModEmbedding> embeddings) {
        if (embeddings.Count == 0) {
            return;
        }

        using var connection = OpenConnection();
        ValidateEmbeddingDimension(connection, model, embeddings[0].Dimension);
        CreateVectorSchema(connection, model, embeddings[0].Dimension);

        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
                              insert into mod_embeddings (
                                mod_id,
                                model,
                                dimension,
                                search_text_hash,
                                embedded_at
                              ) values (
                                $mod_id,
                                $model,
                                $dimension,
                                $search_text_hash,
                                $embedded_at
                              )
                              on conflict(mod_id) do update set
                                model = excluded.model,
                                dimension = excluded.dimension,
                                search_text_hash = excluded.search_text_hash,
                                embedded_at = excluded.embedded_at
                              """;
        var pModId = command.Parameters.Add("$mod_id", SqliteType.Integer);
        var pModel = command.Parameters.Add("$model", SqliteType.Text);
        var pDim = command.Parameters.Add("$dimension", SqliteType.Integer);
        var pHash = command.Parameters.Add("$search_text_hash", SqliteType.Text);
        var pAt = command.Parameters.Add("$embedded_at", SqliteType.Text);

        using var vectorDeleteCommand = connection.CreateCommand();
        vectorDeleteCommand.Transaction = transaction;
        vectorDeleteCommand.CommandText = "delete from mod_embedding_vectors where rowid = $mod_id";
        var pDelId = vectorDeleteCommand.Parameters.Add("$mod_id", SqliteType.Integer);

        using var vectorInsertCommand = connection.CreateCommand();
        vectorInsertCommand.Transaction = transaction;
        vectorInsertCommand.CommandText = """
                                          insert into mod_embedding_vectors(rowid, embedding)
                                          values ($mod_id, $embedding)
                                          """;
        var pInsId = vectorInsertCommand.Parameters.Add("$mod_id", SqliteType.Integer);
        var pInsEmb = vectorInsertCommand.Parameters.Add("$embedding", SqliteType.Blob);

        var embeddedAt = DateTimeOffset.UtcNow.ToString("O");
        foreach (var embedding in embeddings) {
            if (embedding.Dimension != embeddings[0].Dimension) {
                throw new InvalidDataException("Embedding API returned inconsistent vector dimensions");
            }

            pModId.Value = embedding.ModId;
            pModel.Value = model;
            pDim.Value = embedding.Dimension;
            pHash.Value = embedding.SearchTextHash;
            pAt.Value = embeddedAt;
            command.ExecuteNonQuery();

            pDelId.Value = embedding.ModId;
            vectorDeleteCommand.ExecuteNonQuery();

            pInsId.Value = embedding.ModId;
            pInsEmb.Value = embedding.Embedding;
            vectorInsertCommand.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void EnsureSearchIndex(string model) {
        using var connection = OpenConnection();
        if (!TableExists(connection, "mod_embedding_vectors")) {
            throw new InvalidOperationException("Missing vector index. Run rimdex embed.");
        }
    }

    public IReadOnlyList<SearchResultRow> SearchSemantic(string model, byte[] queryEmbedding, int limit) {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              with matches as (
                                select rowid, distance
                                from mod_embedding_vectors
                                where embedding match $query
                                  and k = $limit
                                  and rowid in (select id from mods where is_translation = 0)
                              )
                              select
                                mods.publishedfileid,
                                mods.title,
                                mods.description,
                                mods.preview_url,
                                mods.subscriptions,
                                mods.views
                              from matches
                              join mods on mods.id = matches.rowid
                              order by matches.distance
                              """;
        command.Parameters.AddWithValue("$query", queryEmbedding);
        command.Parameters.AddWithValue("$limit", limit);

        using var reader = command.ExecuteReader();
        return ReadSearchResults(reader);
    }

    public IReadOnlyList<SearchResultRow> SearchKeywords(string query, int limit) {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              select
                                mods.publishedfileid,
                                mods.title,
                                mods.description,
                                mods.preview_url,
                                mods.subscriptions,
                                mods.views
                              from mod_search
                              join mods on mods.id = mod_search.rowid
                              where mod_search match $query
                                and mods.is_translation = 0
                              order by bm25(mod_search, 10.0, 1.0), mods.id
                              limit $limit
                              """;
        command.Parameters.AddWithValue("$query", BuildKeywordQuery(query));
        command.Parameters.AddWithValue("$limit", limit);

        using var reader = command.ExecuteReader();
        return ReadSearchResults(reader);
    }

    private static IReadOnlyList<SearchResultRow> ReadSearchResults(SqliteDataReader reader) {
        var rows = new List<SearchResultRow>();
        while (reader.Read()) {
            rows.Add(new SearchResultRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt64(4),
                reader.GetInt64(5)));
        }

        return rows;
    }

    private static string BuildKeywordQuery(string query) =>
        string.Join(" AND ", query
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(term => $"\"{term.Replace("\"", "\"\"")}\""));

    private SqliteConnection OpenConnection() {
        EnsureDbDirectory();
        var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        Execute(connection, "pragma journal_mode = wal; pragma foreign_keys = on;");
        if (!_schemaInitialized) {
            CreateSchema(connection);
            MigrateSchema(connection);
            _schemaInitialized = true;
        }

        return connection;
    }

    private static void MigrateSchema(SqliteConnection connection) {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var command = connection.CreateCommand();
        command.CommandText = "pragma table_info(mods)";
        using (var reader = command.ExecuteReader()) {
            while (reader.Read()) {
                columns.Add(reader.GetString(1));
            }
        }

        if (columns.Count > 0 && !columns.Contains("is_translation")) {
            Execute(connection, "alter table mods add column is_translation integer not null default 0");
            if (columns.Contains("tags_json")) {
                Execute(connection, """
                                    update mods set is_translation = 1 where exists (
                                        select 1 from json_each(mods.tags_json) where json_each.value = 'Translation'
                                    )
                                    """);
            }
        }

        if (columns.Count > 0 && !columns.Contains("search_text_hash")) {
            Execute(connection, "alter table mods add column search_text_hash text");
            Execute(connection, """
                                update mods set search_text_hash = (
                                    select mod_embeddings.search_text_hash
                                    from mod_embeddings
                                    where mod_embeddings.mod_id = mods.id
                                )
                                """);
        }
    }

    private static void CreateSchema(SqliteConnection connection) {
        Execute(connection, """
                            create table if not exists mods (
                              id integer primary key,
                              publishedfileid text not null unique,
                              title text not null,
                              description text not null,
                              preview_url text not null,
                              subscriptions integer not null,
                              views integer not null,
                              time_updated integer not null,
                              is_translation integer not null,
                              search_text text not null,
                              search_text_hash text not null
                            );
                            create index if not exists mods_time_updated_idx on mods(time_updated);
                            create table if not exists mod_embeddings (
                              mod_id integer primary key references mods(id) on delete cascade,
                              model text not null,
                              dimension integer not null,
                              search_text_hash text not null,
                              embedded_at text not null
                            );
                            create table if not exists mod_embedding_vector_metadata (
                              key text primary key,
                              value text not null
                            );
                            create virtual table if not exists mod_search using fts5(
                              title,
                              description,
                              content = 'mods',
                              content_rowid = 'id'
                            );
                            create trigger if not exists mods_search_after_insert after insert on mods begin
                              insert into mod_search(rowid, title, description)
                              values (new.id, new.title, new.description);
                            end;
                            create trigger if not exists mods_search_after_update after update of title, description on mods begin
                              insert into mod_search(mod_search, rowid, title, description)
                              values ('delete', old.id, old.title, old.description);
                              insert into mod_search(rowid, title, description)
                              values (new.id, new.title, new.description);
                            end;
                            create trigger if not exists mods_search_after_delete after delete on mods begin
                              insert into mod_search(mod_search, rowid, title, description)
                              values ('delete', old.id, old.title, old.description);
                            end;
                            """);
    }

    private static void CreateVectorSchema(SqliteConnection connection, string model, int dimension) {
        if (TableExists(connection, "mod_embedding_vectors")) {
            ValidateVectorMetadata(connection, model, dimension);
            return;
        }

        Execute(connection, $"""
                             create virtual table mod_embedding_vectors using vec0(
                               embedding float[{dimension}] distance_metric=cosine
                             )
                             """);

        InsertVectorMetadata(connection, "model", model);
        InsertVectorMetadata(connection, "dimension", dimension.ToString());
    }

    private static void ValidateVectorMetadata(SqliteConnection connection, string model, int dimension) {
        var storedModel = ReadVectorMetadata(connection, "model");
        var storedDimension = ReadVectorMetadata(connection, "dimension");

        if (storedModel != model) {
            throw new InvalidDataException($"Vector index model {storedModel} does not match configured model {model}");
        }

        if (storedDimension != dimension.ToString()) {
            throw new InvalidDataException(
                $"Vector index dimension {storedDimension} does not match stored embedding dimension {dimension}");
        }
    }

    private static int? ReadStoredEmbeddingDimension(SqliteConnection connection, string model) {
        using var command = connection.CreateCommand();
        command.CommandText = "select dimension from mod_embeddings where model = $model limit 1";
        command.Parameters.AddWithValue("$model", model);
        var result = command.ExecuteScalar();
        return result is null ? null : (int)(long)result;
    }

    private static string ReadVectorMetadata(SqliteConnection connection, string key) {
        using var command = connection.CreateCommand();
        command.CommandText = "select value from mod_embedding_vector_metadata where key = $key";
        command.Parameters.AddWithValue("$key", key);
        return (string?)command.ExecuteScalar()
               ?? throw new InvalidDataException($"Missing vector index metadata: {key}");
    }

    private static void InsertVectorMetadata(SqliteConnection connection, string key, string value) {
        using var command = connection.CreateCommand();
        command.CommandText = """
                              insert or replace into mod_embedding_vector_metadata(key, value)
                              values ($key, $value)
                              """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    private static void ValidateEmbeddingDimension(SqliteConnection connection, string model, int dimension) {
        var existing = ReadStoredEmbeddingDimension(connection, model);
        if (existing is not null && existing.Value != dimension) {
            throw new InvalidDataException(
                $"Embedding dimension {dimension} does not match stored dimension {existing}");
        }
    }

    private static long Count(SqliteConnection connection, string table, string? column = null, string? value = null) {
        using var command = connection.CreateCommand();
        command.CommandText = column is null
            ? $"select count(*) from {table}"
            : $"select count(*) from {table} where {column} = $value";
        if (column is not null) {
            command.Parameters.AddWithValue("$value", value);
        }

        return (long)command.ExecuteScalar()!;
    }

    private static bool TableExists(SqliteConnection connection, string table) {
        using var command = connection.CreateCommand();
        command.CommandText = """
                              select count(*)
                              from sqlite_master
                              where type in ('table', 'virtual table') and name = $name
                              """;
        command.Parameters.AddWithValue("$name", table);
        return (long)command.ExecuteScalar()! > 0;
    }

    private static void Execute(SqliteConnection connection, string sql, SqliteTransaction? transaction = null) {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static object? ExecuteScalar(SqliteConnection connection, string sql) {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private void EnsureDbDirectory() {
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory)) {
            Directory.CreateDirectory(directory);
        }
    }
}