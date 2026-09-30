#ifndef ACME_EFFECTS_H
#define ACME_EFFECTS_H

#include <stdint.h>

#if defined(_WIN32)
#  if defined(ACME_EFFECTS_BUILD)
#    define ACME_EFFECTS_API __declspec(dllexport)
#  else
#    define ACME_EFFECTS_API __declspec(dllimport)
#  endif
#else
#  define ACME_EFFECTS_API
#endif

#ifdef __cplusplus
extern "C" {
#endif

/*
 * Deterministic mix of two 64-bit values, implemented on top of the native
 * core library so the two staged dylibs exercise a real transitive native
 * dependency edge.
 */
ACME_EFFECTS_API int64_t acme_effects_mix(int64_t a, int64_t b);

/* Return nonzero to keep processing. Called once per row and at completion. */
typedef int32_t (*acme_effects_progress)(int32_t percent, void *context);

/*
 * Raw image operations. Components are interleaved 8-bit or little-endian
 * 16-bit; channels are 1, 3 or 4. Row order is represented by the caller's
 * byte layout and is preserved. The optional mask is one 8-bit value per
 * pixel, with its own stride and row order. Return 0 on success, 1 when
 * cancelled by progress, or -1 for invalid arguments.
 */
ACME_EFFECTS_API int32_t acme_effects_box_blur(const uint8_t *source, uint8_t *target,
    int32_t width, int32_t height, int32_t stride, int32_t channels,
    int32_t bits, int32_t bottom_up, int32_t alpha_mode, const uint8_t *mask,
    int32_t mask_stride, int32_t mask_bottom_up, int32_t radius,
    acme_effects_progress progress, void *context);

ACME_EFFECTS_API int32_t acme_effects_gain(const uint8_t *source, uint8_t *target,
    int32_t width, int32_t height, int32_t stride, int32_t channels,
    int32_t bits, int32_t bottom_up, int32_t alpha_mode, const uint8_t *mask,
    int32_t mask_stride, int32_t mask_bottom_up, int32_t gain_percent,
    acme_effects_progress progress, void *context);

#ifdef __cplusplus
}
#endif

#endif /* ACME_EFFECTS_H */
