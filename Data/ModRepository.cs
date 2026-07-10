using Microsoft.Data.Sqlite;
using Rimdex.Embedding;
using Rimdex.Models;

namespace Rimdex.Data;

internal sealed class ModRepository(string dbPath) {
    public void Import(IReadOnlyList<ModDetail> mods) {
        EnsureDbDirectory();

        using var connection = OpenConnection();
        CreateSchema(connection);

        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
                              insert into mods (
                                publishedfileid,
                                title,
                                description,
                                tags_json,
                                preview_url,
                                subscriptions,
                                favorited,
                                views,
                                time_created,
                                time_updated,
                                crawled_at,
                                raw_json,
                                search_text
                              ) values (
                                $publishedfileid,
                                $title,
                                $description,
                                $tags_json,
                                $preview_url,
                                $subscriptions,
                                $favorited,
                                $views,
                                $time_created,
                                $time_updated,
                                $crawled_at,
                                $raw_json,
                                $search_text
                              )
                              on conflict(publishedfileid) do update set
                                title = excluded.title,
                                description = excluded.description,
                                tags_json = excluded.tags_json,
                                preview_url = excluded.preview_url,
                                subscriptions = excluded.subscriptions,
                                favorited = excluded.favorited,
                                views = excluded.views,
                                time_created = excluded.time_created,
                                time_updated = excluded.time_updated,
                                crawled_at = excluded.crawled_at,
                                raw_json = excluded.raw_json,
                                search_text = excluded.search_text
                              """;

        AddParameters(command);

        foreach (var mod in mods) {
            command.Parameters["$publishedfileid"].Value = mod.PublishedFileId;
            command.Parameters["$title"].Value = mod.Title;
            command.Parameters["$description"].Value = mod.Description;
            command.Parameters["$tags_json"].Value = mod.TagsJson;
            command.Parameters["$preview_url"].Value = mod.PreviewUrl;
            command.Parameters["$subscriptions"].Value = mod.Subscriptions;
            command.Parameters["$favorited"].Value = mod.Favorited;
            command.Parameters["$views"].Value = mod.Views;
            command.Parameters["$time_created"].Value = mod.TimeCreated;
            command.Parameters["$time_updated"].Value = mod.TimeUpdated;
            command.Parameters["$crawled_at"].Value = mod.CrawledAt;
            command.Parameters["$raw_json"].Value = mod.RawJson;
            command.Parameters["$search_text"].Value = mod.SearchText;
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public DbStats ReadStats() {
        EnsureDbDirectory();

        using var connection = OpenConnection();
        CreateSchema(connection);

        return new DbStats(
            Count(connection, "mods"),
            TableExists(connection, "mod_embeddings") ? Count(connection, "mod_embeddings") : 0);
    }

    public IReadOnlyList<PendingEmbeddingRow> ReadPendingEmbeddingRows(int limit, string model) {
        EnsureDbDirectory();

        using var connection = OpenConnection();
        CreateSchema(connection);
        CreateEmbeddingSchema(connection);

        using var command = connection.CreateCommand();
        command.CommandText = """
                              select
                                mods.id,
                                mods.search_text,
                                mod_embeddings.model,
                                mod_embeddings.search_text_hash
                              from mods
                              left join mod_embeddings on mod_embeddings.mod_id = mods.id
                              order by mods.id
                              """;

        using var reader = command.ExecuteReader();
        var rows = new List<PendingEmbeddingRow>(limit);
        while (reader.Read() && rows.Count < limit) {
            var searchText = reader.GetString(1);
            var hash = SearchTextHash.Compute(searchText);
            var storedModel = reader.IsDBNull(2) ? null : reader.GetString(2);
            var storedHash = reader.IsDBNull(3) ? null : reader.GetString(3);

            if (storedModel == model && storedHash == hash) {
                continue;
            }

            rows.Add(new PendingEmbeddingRow(reader.GetInt32(0), searchText, hash));
        }

        return rows;
    }

    public void UpsertEmbeddings(string model, IReadOnlyList<ModEmbedding> embeddings) {
        if (embeddings.Count == 0) {
            return;
        }

        EnsureDbDirectory();

        using var connection = OpenConnection();
        CreateSchema(connection);
        CreateEmbeddingSchema(connection);
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
        command.Parameters.Add("$mod_id", SqliteType.Integer);
        command.Parameters.Add("$model", SqliteType.Text);
        command.Parameters.Add("$dimension", SqliteType.Integer);
        command.Parameters.Add("$search_text_hash", SqliteType.Text);
        command.Parameters.Add("$embedded_at", SqliteType.Text);

        using var vectorCommand = connection.CreateCommand();
        vectorCommand.Transaction = transaction;
        vectorCommand.CommandText = """
                                    insert or replace into mod_embedding_vectors(rowid, embedding)
                                    values ($mod_id, $embedding)
                                    """;
        vectorCommand.Parameters.Add("$mod_id", SqliteType.Integer);
        vectorCommand.Parameters.Add("$embedding", SqliteType.Blob);

        var embeddedAt = DateTimeOffset.UtcNow.ToString("O");
        foreach (var embedding in embeddings) {
            if (embedding.Dimension != embeddings[0].Dimension) {
                throw new InvalidDataException("Embedding API returned inconsistent vector dimensions");
            }

            command.Parameters["$mod_id"].Value = embedding.ModId;
            command.Parameters["$model"].Value = model;
            command.Parameters["$dimension"].Value = embedding.Dimension;
            command.Parameters["$search_text_hash"].Value = embedding.SearchTextHash;
            command.Parameters["$embedded_at"].Value = embeddedAt;
            command.ExecuteNonQuery();

            vectorCommand.Parameters["$mod_id"].Value = embedding.ModId;
            vectorCommand.Parameters["$embedding"].Value = embedding.Embedding;
            vectorCommand.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void EnsureSearchIndex(string model) {
        EnsureDbDirectory();

        using var connection = OpenConnection();
        CreateSchema(connection);
        CreateEmbeddingSchema(connection);
        EnsureVectorIndex(connection, model);
    }

    public IReadOnlyList<SearchCandidateRow> SearchCandidates(string model, byte[] queryEmbedding, int candidates) {
        EnsureDbDirectory();

        using var connection = OpenConnection();
        CreateSchema(connection);
        CreateEmbeddingSchema(connection);

        using var command = connection.CreateCommand();
        command.CommandText = """
                              with eligible as (
                                select mods.id
                                from mods
                                join mod_embeddings on mod_embeddings.mod_id = mods.id
                                where mod_embeddings.model = $model
                                  and not exists (
                                    select 1
                                    from json_each(mods.tags_json)
                                    where json_each.value = 'Translation'
                                  )
                              ),
                              matches as (
                                select rowid, distance
                                from mod_embedding_vectors
                                where embedding match $query
                                  and k = $candidates
                                  and rowid in (select id from eligible)
                              )
                              select
                                mods.id,
                                mods.publishedfileid,
                                mods.title,
                                mods.description,
                                mods.preview_url,
                                mods.subscriptions,
                                mods.views,
                                matches.distance
                              from matches
                              join mods on mods.id = matches.rowid
                              join eligible on eligible.id = mods.id
                              order by matches.distance
                              """;
        command.Parameters.AddWithValue("$model", model);
        command.Parameters.AddWithValue("$query", queryEmbedding);
        command.Parameters.AddWithValue("$candidates", candidates);

        using var reader = command.ExecuteReader();
        var rows = new List<SearchCandidateRow>();
        while (reader.Read()) {
            rows.Add(new SearchCandidateRow(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetInt64(5),
                reader.GetInt64(6),
                (float)reader.GetDouble(7)));
        }

        return rows;
    }

    private SqliteConnection OpenConnection() {
        var builder = new SqliteConnectionStringBuilder {
            DataSource = dbPath
        };
        var connection = new SqliteConnection(builder.ToString());

        connection.Open();
        SqliteVec.Register(connection);
        Execute(connection, "pragma journal_mode = wal");
        Execute(connection, "pragma foreign_keys = on");
        return connection;
    }

    private static void CreateSchema(SqliteConnection connection) {
        Execute(connection, """
                            create table if not exists mods (
                              id integer primary key,
                              publishedfileid text not null unique,
                              title text not null,
                              description text not null,
                              tags_json text not null,
                              preview_url text not null,
                              subscriptions integer not null,
                              favorited integer not null,
                              views integer not null,
                              time_created integer not null,
                              time_updated integer not null,
                              crawled_at text not null,
                              raw_json text not null,
                              search_text text not null
                            )
                            """);
        Execute(connection, "create index if not exists mods_time_updated_idx on mods(time_updated)");
        Execute(connection, "create index if not exists mods_subscriptions_idx on mods(subscriptions)");
    }

    private static void CreateEmbeddingSchema(SqliteConnection connection) {
        Execute(connection, """
                            create table if not exists mod_embeddings (
                              mod_id integer primary key references mods(id) on delete cascade,
                              model text not null,
                              dimension integer not null,
                              search_text_hash text not null,
                              embedded_at text not null
                            )
                            """);
        Execute(connection, "create index if not exists mod_embeddings_model_idx on mod_embeddings(model)");
        Execute(connection, """
                            create table if not exists mod_embedding_vector_metadata (
                              key text primary key,
                              value text not null
                            )
                            """);
    }

    private static void EnsureVectorIndex(SqliteConnection connection, string model) {
        var dimension = ReadStoredEmbeddingDimension(connection, model);
        if (dimension is null) {
            throw new InvalidOperationException($"No embeddings found for model: {model}");
        }

        if (!TableExists(connection, "mod_embedding_vectors")) {
            throw new InvalidOperationException("Missing vector index. Run rimdex embed.");
        }

        ValidateVectorMetadata(connection, model, dimension.Value);

        var metadataRows = Count(connection, "mod_embeddings", "model", model);
        var vectorRows = Count(connection, "mod_embedding_vectors");
        if (metadataRows != vectorRows) {
            throw new InvalidDataException(
                $"Vector index row count {vectorRows} does not match embedding metadata row count {metadataRows}");
        }
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
        command.CommandText = """
                              select dimension
                              from mod_embeddings
                              where model = $model
                              limit 1
                              """;
        command.Parameters.AddWithValue("$model", model);
        var result = command.ExecuteScalar();
        return result is null ? null : (int)(long)result;
    }

    private static string ReadVectorMetadata(SqliteConnection connection, string key) {
        using var command = connection.CreateCommand();
        command.CommandText = """
                              select value
                              from mod_embedding_vector_metadata
                              where key = $key
                              """;
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
        using var command = connection.CreateCommand();
        command.CommandText = """
                              select dimension
                              from mod_embeddings
                              where model = $model
                              limit 1
                              """;
        command.Parameters.AddWithValue("$model", model);
        var existing = command.ExecuteScalar();

        if (existing is null) {
            return;
        }

        if ((long)existing != dimension) {
            throw new InvalidDataException(
                $"Embedding dimension {dimension} does not match stored dimension {existing}");
        }
    }

    private static void AddParameters(SqliteCommand command) {
        command.Parameters.Add("$publishedfileid", SqliteType.Text);
        command.Parameters.Add("$title", SqliteType.Text);
        command.Parameters.Add("$description", SqliteType.Text);
        command.Parameters.Add("$tags_json", SqliteType.Text);
        command.Parameters.Add("$preview_url", SqliteType.Text);
        command.Parameters.Add("$subscriptions", SqliteType.Integer);
        command.Parameters.Add("$favorited", SqliteType.Integer);
        command.Parameters.Add("$views", SqliteType.Integer);
        command.Parameters.Add("$time_created", SqliteType.Integer);
        command.Parameters.Add("$time_updated", SqliteType.Integer);
        command.Parameters.Add("$crawled_at", SqliteType.Text);
        command.Parameters.Add("$raw_json", SqliteType.Text);
        command.Parameters.Add("$search_text", SqliteType.Text);
    }

    private static long Count(SqliteConnection connection, string table) {
        using var command = connection.CreateCommand();
        command.CommandText = $"select count(*) from {table}";
        return (long)command.ExecuteScalar()!;
    }

    private static long Count(SqliteConnection connection, string table, string column, string value) {
        using var command = connection.CreateCommand();
        command.CommandText = $"select count(*) from {table} where {column} = $value";
        command.Parameters.AddWithValue("$value", value);
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

    private static void Execute(SqliteConnection connection, string sql) {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private void EnsureDbDirectory() {
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory)) {
            Directory.CreateDirectory(directory);
        }
    }
}