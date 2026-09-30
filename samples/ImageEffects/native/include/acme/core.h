#ifndef ACME_CORE_H
#define ACME_CORE_H

#include <stdint.h>

#if defined(_WIN32)
#  if defined(ACME_CORE_BUILD)
#    define ACME_CORE_API __declspec(dllexport)
#  else
#    define ACME_CORE_API __declspec(dllimport)
#  endif
#else
#  define ACME_CORE_API
#endif

#ifdef __cplusplus
extern "C" {
#endif

/*
 * Rotates a 64-bit value left by the low six bits of `bits`.
 * The deterministic primitive of the transitive native core library.
 */
ACME_CORE_API int64_t acme_core_rotate_left(int64_t value, int64_t bits);

#ifdef __cplusplus
}
#endif

#endif /* ACME_CORE_H */
