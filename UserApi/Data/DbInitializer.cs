using Npgsql;

namespace UserApi.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(NpgsqlDataSource dataSource)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS classes (
                id          TEXT PRIMARY KEY,
                name        TEXT NOT NULL,
                total_seats INTEGER NOT NULL CHECK (total_seats >= 0),
                created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS holds (
                id         TEXT PRIMARY KEY,
                class_id   TEXT NOT NULL REFERENCES classes(id) ON DELETE CASCADE,
                student_id TEXT NOT NULL,
                status     TEXT NOT NULL,
                expires_at TIMESTAMPTZ NOT NULL,
                created_at TIMESTAMPTZ NOT NULL DEFAULT now()
            );

            CREATE INDEX IF NOT EXISTS idx_holds_class   ON holds (class_id);
            CREATE INDEX IF NOT EXISTS idx_holds_student ON holds (student_id);
            CREATE INDEX IF NOT EXISTS idx_holds_status  ON holds (status);
            """;

        await using var cmd = dataSource.CreateCommand(sql);
        await cmd.ExecuteNonQueryAsync();
    }
}
