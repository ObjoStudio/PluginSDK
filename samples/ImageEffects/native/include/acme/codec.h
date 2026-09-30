#ifndef ACME_CODEC_H
#define ACME_CODEC_H

#include <stdint.h>
#include "effects.h"

#ifdef __cplusplus
extern "C" {
#endif

/*
 * PNG/JPEG interoperability for the image fixture. Returned allocations
 * belong to acme_codec_free. Status: 0 success, -1 malformed/unsupported,
 * -2 embedded ICC profile (the codec does not preserve it), -3 size limit.
 * Raw 16-bit components are little-endian on the supported RIDs.
 */
ACME_EFFECTS_API int32_t acme_codec_decode(const uint8_t *encoded, int32_t length,
    uint8_t **pixels, int32_t *width, int32_t *height,
    int32_t *channels, int32_t *bits);

ACME_EFFECTS_API int32_t acme_codec_encode_png(const uint8_t *pixels, int32_t width,
    int32_t height, int32_t channels, int32_t stride, int32_t bottom_up,
    uint8_t **encoded, int32_t *length);

ACME_EFFECTS_API int32_t acme_codec_encode_jpeg(const uint8_t *pixels, int32_t width,
    int32_t height, int32_t channels, int32_t stride, int32_t bottom_up,
    int32_t quality, uint8_t **encoded, int32_t *length);

ACME_EFFECTS_API void acme_codec_free(void *buffer);

#ifdef __cplusplus
}
#endif

#endif
