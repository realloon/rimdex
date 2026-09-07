#define SQLITE_ENABLE_FTS5 1
#define SQLITE_CORE 1
#define SQLITE_VEC_STATIC 1
#define SQLITE_VEC_OMIT_FS 1
#define SQLITE_EXTRA_INIT rimdex_sqlite_init

#include "sqlite3.c"
#include "sqlite-vec.c"

int rimdex_sqlite_init(const char *dummy) {
    return sqlite3_auto_extension((void(*)(void))sqlite3_vec_init);
}