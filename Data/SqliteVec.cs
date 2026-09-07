using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;
using SQLitePCL;

namespace Rimdex.Data;

internal static partial class SqliteVec {
    public static void Register(SqliteConnection connection) {
        var handle = connection.Handle
                     ?? throw new InvalidOperationException("SQLite connection is not open");
        var added = false;

        try {
            handle.DangerousAddRef(ref added);
            var rc = Init(handle.DangerousGetHandle(), out var error, IntPtr.Zero);
            if (rc == 0) return;

            var message = error == IntPtr.Zero
                ? $"sqlite-vec initialization failed with code {rc}"
                : Marshal.PtrToStringUTF8(error) ?? $"sqlite-vec initialization failed with code {rc}";
            if (error != IntPtr.Zero) {
                raw.sqlite3_free(error);
            }

            throw new InvalidOperationException(message);
        } finally {
            if (added) {
                handle.DangerousRelease();
            }
        }
    }

    [LibraryImport("e_sqlite3", EntryPoint = "sqlite3_vec_init")]
    private static partial int Init(IntPtr db, out IntPtr error, IntPtr api);
}