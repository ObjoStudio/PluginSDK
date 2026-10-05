#ifndef ACME_NATIVE_MATHS_H
#define ACME_NATIVE_MATHS_H

#include <stdint.h>

#if defined(_WIN32)
#define ACME_NATIVE_MATHS_API __declspec(dllexport)
#else
#define ACME_NATIVE_MATHS_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

/* Returns the sum of two 64-bit integers. */
ACME_NATIVE_MATHS_API int64_t acme_nativemaths_add(int64_t a, int64_t b);

#ifdef __cplusplus
}
#endif

#endif /* ACME_NATIVE_MATHS_H */
