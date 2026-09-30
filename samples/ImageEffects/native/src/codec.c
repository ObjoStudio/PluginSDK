#include "acme/codec.h"
#include <limits.h>
#include <stddef.h>
#include <stdlib.h>
#include <string.h>

#define STBI_ONLY_PNG
#define STBI_ONLY_JPEG
#define STBI_NO_STDIO
#define STB_IMAGE_IMPLEMENTATION
#include "stb_image.h"

#define STBI_WRITE_NO_STDIO
#define STB_IMAGE_WRITE_IMPLEMENTATION
#include "stb_image_write.h"

#define ACME_MAX_PIXELS (256 * 1024 * 1024)
#define ACME_MAX_ENCODED (256 * 1024 * 1024)

static uint32_t big_endian_32(const uint8_t *p)
{
    return ((uint32_t)p[0] << 24) | ((uint32_t)p[1] << 16) |
        ((uint32_t)p[2] << 8) | (uint32_t)p[3];
}

/* Reject ICC data rather than silently dropping the image's profile. */
static int32_t has_profile(const uint8_t *data, int32_t length, int32_t png)
{
    if (png) {
        size_t offset = 8;
        while (offset + 12 <= (size_t)length) {
            uint32_t chunk_length = big_endian_32(data + offset);
            if ((size_t)chunk_length > (size_t)length - offset - 12) return -1;
            if (memcmp(data + offset + 4, "iCCP", 4) == 0) return 1;
            offset += 12 + (size_t)chunk_length;
        }
        return 0;
    }
    static const uint8_t icc_marker[] = "ICC_PROFILE\0";
    for (size_t i = 0; i + sizeof(icc_marker) - 1 <= (size_t)length; ++i)
        if (memcmp(data + i, icc_marker, sizeof(icc_marker) - 1) == 0) return 1;
    return 0;
}

int32_t acme_codec_decode(const uint8_t *encoded, int32_t length,
    uint8_t **pixels, int32_t *width, int32_t *height,
    int32_t *channels, int32_t *bits)
{
    if (!encoded || !pixels || !width || !height || !channels || !bits ||
        length <= 0 || length > ACME_MAX_ENCODED) return -1;
    *pixels = NULL;
    int32_t png = length >= 8 && memcmp(encoded, "\x89PNG\r\n\x1a\n", 8) == 0;
    int32_t jpeg = length >= 2 && encoded[0] == 0xff && encoded[1] == 0xd8;
    if (!png && !jpeg) return -1;
    int32_t profile = has_profile(encoded, length, png);
    if (profile != 0) return profile > 0 ? -2 : -1;

    int w = 0, h = 0, c = 0;
    if (!stbi_info_from_memory(encoded, length, &w, &h, &c)) return -1;
    if (w <= 0 || h <= 0 || (c != 1 && c != 3 && c != 4)) return -1;
    int depth = stbi_is_16_bit_from_memory(encoded, length) ? 16 : 8;
    /* A decoder may expand palette or grey input to four output channels.
       Bound its allocation before invoking the decoder, then verify the
       actual output layout as well. */
    if ((uint64_t)w * (uint64_t)h * 4u * (uint64_t)(depth / 8) > ACME_MAX_PIXELS)
        return -3;
    if (depth == 16) {
        if (!png) return -1;
        *pixels = (uint8_t *)stbi_load_16_from_memory(encoded, length, &w, &h, &c, 0);
    } else {
        *pixels = stbi_load_from_memory(encoded, length, &w, &h, &c, 0);
    }
    if (!*pixels) return -1;
    if (w <= 0 || h <= 0 || (c != 1 && c != 3 && c != 4) ||
        (uint64_t)w * (uint64_t)h * (uint64_t)c * (uint64_t)(depth / 8) > ACME_MAX_PIXELS) {
        stbi_image_free(*pixels);
        *pixels = NULL;
        return -3;
    }
    *width = w;
    *height = h;
    *channels = c;
    *bits = depth;
    return 0;
}

static int32_t prepare_rows(const uint8_t *pixels, int32_t width,
    int32_t height, int32_t channels, int32_t stride, int32_t bottom_up,
    uint8_t **prepared, int32_t *prepared_stride)
{
    if (!pixels || width <= 0 || height <= 0 ||
        (channels != 1 && channels != 3 && channels != 4)) return -1;
    int64_t row = (int64_t)width * channels;
    if (stride < row || (int64_t)stride * height > ACME_MAX_PIXELS) return -3;
    if (!bottom_up) {
        *prepared = (uint8_t *)pixels;
        *prepared_stride = stride;
        return 0;
    }
    int64_t total = row * height;
    if (total > ACME_MAX_PIXELS) return -3;
    *prepared = (uint8_t *)malloc((size_t)total);
    if (!*prepared) return -3;
    *prepared_stride = (int32_t)row;
    for (int32_t y = 0; y < height; ++y)
        memcpy(*prepared + (size_t)y * row,
            pixels + (size_t)(height - 1 - y) * stride, (size_t)row);
    return 0;
}

int32_t acme_codec_encode_png(const uint8_t *pixels, int32_t width,
    int32_t height, int32_t channels, int32_t stride, int32_t bottom_up,
    uint8_t **encoded, int32_t *length)
{
    if (!encoded || !length) return -1;
    *encoded = NULL;
    *length = 0;
    uint8_t *prepared = NULL;
    int32_t prepared_stride = 0;
    int32_t status = prepare_rows(pixels, width, height, channels,
        stride, bottom_up, &prepared, &prepared_stride);
    if (status != 0) return status;
    int result_length = 0;
    *encoded = stbi_write_png_to_mem(prepared, prepared_stride,
        width, height, channels, &result_length);
    if (prepared != pixels) free(prepared);
    if (!*encoded) return -1;
    if (result_length <= 0 || result_length > ACME_MAX_ENCODED) {
        free(*encoded);
        *encoded = NULL;
        return -3;
    }
    *length = result_length;
    return 0;
}

typedef struct {
    uint8_t *bytes;
    int32_t length;
    int32_t capacity;
    int32_t failed;
} acme_output;

static void append_jpeg(void *context, void *data, int size)
{
    acme_output *output = (acme_output *)context;
    if (output->failed || size < 0 || size > ACME_MAX_ENCODED - output->length) {
        output->failed = 1;
        return;
    }
    int32_t needed = output->length + size;
    if (needed > output->capacity) {
        int32_t capacity = output->capacity == 0 ? 4096 : output->capacity;
        while (capacity < needed) {
            if (capacity > ACME_MAX_ENCODED / 2) { capacity = ACME_MAX_ENCODED; break; }
            capacity *= 2;
        }
        uint8_t *grown = (uint8_t *)realloc(output->bytes, (size_t)capacity);
        if (!grown) { output->failed = 1; return; }
        output->bytes = grown;
        output->capacity = capacity;
    }
    memcpy(output->bytes + output->length, data, (size_t)size);
    output->length = needed;
}

int32_t acme_codec_encode_jpeg(const uint8_t *pixels, int32_t width,
    int32_t height, int32_t channels, int32_t stride, int32_t bottom_up,
    int32_t quality, uint8_t **encoded, int32_t *length)
{
    if (!encoded || !length || (channels != 1 && channels != 3) ||
        quality < 1 || quality > 100) return -1;
    *encoded = NULL;
    *length = 0;
    uint8_t *prepared = NULL;
    int32_t prepared_stride = 0;
    int32_t status = prepare_rows(pixels, width, height, channels,
        stride, bottom_up, &prepared, &prepared_stride);
    if (status != 0) return status;
    uint8_t *packed = prepared;
    if (prepared_stride != width * channels) {
        packed = (uint8_t *)malloc((size_t)width * channels * height);
        if (!packed) { if (prepared != pixels) free(prepared); return -3; }
        for (int32_t y = 0; y < height; ++y)
            memcpy(packed + (size_t)y * width * channels,
                prepared + (size_t)y * prepared_stride, (size_t)width * channels);
    }
    acme_output output = {0};
    int success = stbi_write_jpg_to_func(append_jpeg, &output,
        width, height, channels, packed, quality);
    if (packed != prepared) free(packed);
    if (prepared != pixels) free(prepared);
    if (!success || output.failed || output.length == 0) {
        free(output.bytes);
        return -1;
    }
    *encoded = output.bytes;
    *length = output.length;
    return 0;
}

void acme_codec_free(void *buffer)
{
    free(buffer);
}
