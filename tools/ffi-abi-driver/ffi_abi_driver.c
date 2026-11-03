/*
 * Native C-ABI driver for pdfoxide_bindings.
 *
 * Loads the cdylib with dlopen/LoadLibrary and resolves every symbol through the
 * platform loader, independent of .NET marshalling. This is the A1/D8 harness.
 *
 * Usage:
 *   ffi_abi_driver <library-path> symbols
 *   ffi_abi_driver <library-path> smoke
 *   ffi_abi_driver <library-path> null-path
 */

#include <stddef.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#ifdef _WIN32
#include <windows.h>
typedef HMODULE library_handle;

static library_handle load_library(const char* path)
{
    return LoadLibraryA(path);
}

static void* load_symbol(library_handle library, const char* name)
{
    return (void*)GetProcAddress(library, name);
}

static void unload_library(library_handle library)
{
    FreeLibrary(library);
}

static void make_temp_path(char* buffer, size_t size)
{
    char directory[MAX_PATH];
    DWORD length = GetTempPathA(MAX_PATH, directory);
    if (length == 0) {
        snprintf(buffer, size, "ffi_driver_sample.pdf");
        return;
    }
    snprintf(buffer, size, "%sffi_driver_%lu.pdf", directory,
             (unsigned long)GetCurrentProcessId());
}
#else
#include <dlfcn.h>
#include <pthread.h>
#include <unistd.h>
typedef void* library_handle;

static library_handle load_library(const char* path)
{
    return dlopen(path, RTLD_NOW);
}

static void* load_symbol(library_handle library, const char* name)
{
    return dlsym(library, name);
}

static void unload_library(library_handle library)
{
    dlclose(library);
}

static void make_temp_path(char* buffer, size_t size)
{
    const char* directory = getenv("TMPDIR");
    if (directory == NULL) {
        directory = "/tmp";
    }
    snprintf(buffer, size, "%s/ffi_driver_%lu.pdf", directory,
             (unsigned long)getpid());
}
#endif

typedef char* (*extract_text_fn)(const char* path);
typedef int (*get_page_count_fn)(const char* path);
typedef int (*from_text_fn)(const char* text, const char* output_path);
typedef char* (*last_error_fn)(void);
typedef void (*string_free_fn)(char* pointer);
typedef char* (*markdown_all_fn)(const char* path);

typedef struct {
    from_text_fn from_text;
    extract_text_fn extract_text;
    get_page_count_fn get_page_count;
    markdown_all_fn markdown_all;
    last_error_fn last_error;
    string_free_fn string_free;
} api;

typedef struct {
    const api* ffi;
    const char* path;
    long failures;
} worker_context;

static void worker_body(worker_context* context)
{
    for (int index = 0; index < 20; index++) {
        if (context->ffi->get_page_count(context->path) < 1) {
#if defined(_WIN32)
            InterlockedIncrement((volatile LONG*)&context->failures);
#else
            __atomic_fetch_add(&context->failures, 1, __ATOMIC_SEQ_CST);
#endif
            return;
        }

        char* text = context->ffi->extract_text(context->path);
        if (text == NULL) {
#if defined(_WIN32)
            InterlockedIncrement((volatile LONG*)&context->failures);
#else
            __atomic_fetch_add(&context->failures, 1, __ATOMIC_SEQ_CST);
#endif
            return;
        }
        context->ffi->string_free(text);
    }
}

#if defined(_WIN32)
static DWORD WINAPI worker_entry(LPVOID parameter)
{
    worker_body((worker_context*)parameter);
    return 0;
}
#else
static void* worker_entry(void* parameter)
{
    worker_body((worker_context*)parameter);
    return NULL;
}
#endif

static const char* REQUIRED_SYMBOLS[] = {
    "pdf_extract_text",
    "pdf_to_markdown_all",
    "pdf_last_error_message",
    "pdf_string_free",
    "pdf_get_string_len",
    "pdfoxide_csharp_pdf_get_page_count",
    "pdfoxide_csharp_pdf_from_markdown",
    "pdfoxide_csharp_pdf_from_text",
    "pdf_builder_create",
    "pdf_builder_free",
    "pdf_builder_streaming_table_begin",
    "pdf_builder_streaming_table_add_row",
    "pdf_builder_streaming_table_end",
    "pdf_builder_add_page_like",
    "pdfoxide_editor_open_with_password",
    "pdfoxide_editor_open_from_bytes_with_password",
    "document_editor_free",
    "document_editor_save_encrypted",
    "pdf_certificate_load_from_bytes",
    "pdf_certificate_free",
    "pdf_signature_verify_detached",
    "free_bytes",
    "free_string",
    NULL,
};

static void report_error(api* ffi)
{
    char* message = ffi->last_error();
    if (message != NULL) {
        fprintf(stderr, "error: %s\n", message);
        ffi->string_free(message);
    } else {
        fprintf(stderr, "error: <no error message>\n");
    }
}

static int resolve_api(library_handle library, api* ffi)
{
    ffi->from_text = (from_text_fn)load_symbol(library, "pdfoxide_csharp_pdf_from_text");
    ffi->extract_text = (extract_text_fn)load_symbol(library, "pdf_extract_text");
    ffi->get_page_count = (get_page_count_fn)load_symbol(
        library, "pdfoxide_csharp_pdf_get_page_count");
    ffi->markdown_all = (markdown_all_fn)load_symbol(library, "pdf_to_markdown_all");
    ffi->last_error = (last_error_fn)load_symbol(library, "pdf_last_error_message");
    ffi->string_free = (string_free_fn)load_symbol(library, "pdf_string_free");

    if (ffi->from_text == NULL || ffi->extract_text == NULL ||
        ffi->get_page_count == NULL || ffi->markdown_all == NULL ||
        ffi->last_error == NULL || ffi->string_free == NULL) {
        fprintf(stderr, "failed to resolve the document API symbols\n");
        return 0;
    }
    return 1;
}

static int scenario_symbols(library_handle library)
{
    size_t total = (sizeof(REQUIRED_SYMBOLS) / sizeof(REQUIRED_SYMBOLS[0])) - 1;
    size_t missing = 0;

    for (const char** symbol = REQUIRED_SYMBOLS; *symbol != NULL; ++symbol) {
        if (load_symbol(library, *symbol) == NULL) {
            fprintf(stderr, "missing symbol: %s\n", *symbol);
            missing++;
        }
    }

    if (missing != 0) {
        fprintf(stderr, "%zu of %zu required symbols are missing\n", missing, total);
        return 1;
    }

    printf("all %zu required symbols are exported\n", total);
    return 0;
}

static int scenario_smoke(library_handle library)
{
    api ffi;
    char path[1024];

    if (!resolve_api(library, &ffi)) {
        return 2;
    }

    make_temp_path(path, sizeof(path));
    printf("temp pdf: %s\n", path);

    if (ffi.from_text("hello from the FFI ABI driver", path) != 0) {
        report_error(&ffi);
        remove(path);
        return 1;
    }

    int count = ffi.get_page_count(path);
    if (count < 1) {
        fprintf(stderr, "unexpected page count: %d\n", count);
        report_error(&ffi);
        remove(path);
        return 1;
    }

    char* text = ffi.extract_text(path);
    if (text == NULL) {
        report_error(&ffi);
        remove(path);
        return 1;
    }
    printf("extracted %zu text bytes\n", strlen(text));
    ffi.string_free(text);

    char* markdown = ffi.markdown_all(path);
    if (markdown == NULL) {
        report_error(&ffi);
        remove(path);
        return 1;
    }
    printf("converted to %zu markdown bytes\n", strlen(markdown));
    ffi.string_free(markdown);

    remove(path);
    return 0;
}

static int scenario_threads(library_handle library)
{
    enum { THREAD_COUNT = 4 };
    api ffi;
    char path[1024];
    worker_context context;

    if (!resolve_api(library, &ffi)) {
        return 2;
    }

    make_temp_path(path, sizeof(path));
    if (ffi.from_text("threads scenario", path) != 0) {
        report_error(&ffi);
        remove(path);
        return 1;
    }

    context.ffi = &ffi;
    context.path = path;
    context.failures = 0;

#if defined(_WIN32)
    HANDLE threads[THREAD_COUNT] = {0};
    for (int index = 0; index < THREAD_COUNT; index++) {
        threads[index] = CreateThread(NULL, 0, worker_entry, &context, 0, NULL);
        if (threads[index] == NULL) {
            context.failures++;
        }
    }
    for (int index = 0; index < THREAD_COUNT; index++) {
        if (threads[index] != NULL) {
            WaitForSingleObject(threads[index], 30000);
            CloseHandle(threads[index]);
        }
    }
#else
    pthread_t threads[THREAD_COUNT];
    int created[THREAD_COUNT] = {0};
    for (int index = 0; index < THREAD_COUNT; index++) {
        if (pthread_create(&threads[index], NULL, worker_entry, &context) == 0) {
            created[index] = 1;
        } else {
            __atomic_fetch_add(&context.failures, 1, __ATOMIC_SEQ_CST);
        }
    }
    for (int index = 0; index < THREAD_COUNT; index++) {
        if (created[index]) {
            pthread_join(threads[index], NULL);
        }
    }
#endif

    remove(path);
    if (context.failures != 0) {
        fprintf(stderr, "thread failures: %ld\n", context.failures);
        return 1;
    }

    printf("%d threads completed\n", THREAD_COUNT);
    return 0;
}

static int scenario_null_path(library_handle library)
{
    api ffi;

    if (!resolve_api(library, &ffi)) {
        return 2;
    }

    char* result = ffi.extract_text(NULL);
    if (result != NULL) {
        fprintf(stderr, "null path unexpectedly returned a string\n");
        ffi.string_free(result);
        return 1;
    }

    char* message = ffi.last_error();
    if (message == NULL) {
        fprintf(stderr, "null path produced no error message\n");
        return 1;
    }
    printf("null path error: %s\n", message);
    ffi.string_free(message);
    ffi.string_free(NULL);
    return 0;
}

int main(int argc, char** argv)
{
    const char* library_path;
    const char* scenario;
    library_handle library;
    int result;

    if (argc < 3) {
        fprintf(stderr, "usage: %s <library-path> <symbols|smoke|null-path|threads>\n", argv[0]);
        return 2;
    }

    library_path = argv[1];
    scenario = argv[2];
    library = load_library(library_path);
    if (library == NULL) {
        fprintf(stderr, "failed to load native library: %s\n", library_path);
        return 2;
    }

    if (strcmp(scenario, "symbols") == 0) {
        result = scenario_symbols(library);
    } else if (strcmp(scenario, "smoke") == 0) {
        result = scenario_smoke(library);
    } else if (strcmp(scenario, "null-path") == 0) {
        result = scenario_null_path(library);
    } else if (strcmp(scenario, "threads") == 0) {
        result = scenario_threads(library);
    } else {
        fprintf(stderr, "unknown scenario: %s\n", scenario);
        result = 2;
    }

    unload_library(library);
    return result;
}
