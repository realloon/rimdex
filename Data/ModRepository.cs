using Microsoft.Data.Sqlite;
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

    private SqliteConnection OpenConnection() {
        var builder = new SqliteConnectionStringBuilder {
            DataSource = dbPath
        };
        var connection = new SqliteConnection(builder.ToString());

        connection.Open();
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