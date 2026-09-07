#define SQLITE_ENABLE_FTS5 1
#define SQLITE_CORE 1
#define SQLITE_VEC_STATIC 1
#define SQLITE_VEC_OMIT_FS 1
#define SQLITE_EXTRA_INIT rimdex_sqlite_init

#include "sqlite3.c"

#define u64 sqlite_vec_u64
#include "sqlite-vec.c"
#undef u64

int rimdex_sqlite_init(const char *dummy) {
    return sqlite3_auto_extension((void(*)(void))sqlite3_vec_init);
}