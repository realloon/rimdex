using SQLitePCL;
using Rimdex.Cli;

raw.SetProvider(new SQLite3Provider_e_sqlite3());
return await RimdexCommand.RunAsync(args);