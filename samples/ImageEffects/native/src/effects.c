#include "acme/effects.h"
#include "acme/core.h"
#include <stddef.h>

int64_t acme_effects_mix(int64_t a, int64_t b)
{
    int64_t rotated = acme_core_rotate_left(a ^ b, 13);
    return (int64_t)((uint64_t)rotated + (uint64_t)b);
}

static uint32_t read_component(const uint8_t *pixel, int32_t bits)
{
    return bits == 8 ? pixel[0] : (uint32_t)pixel[0] | ((uint32_t)pixel[1] << 8);
}

static void write_component(uint8_t *pixel, int32_t bits, uint32_t value)
{
    pixel[0] = (uint8_t)value;
    if (bits == 16) pixel[1] = (uint8_t)(value >> 8);
}

static int32_t storage_row(int32_t y, int32_t height, int32_t bottom_up)
{
    return bottom_up ? height - 1 - y : y;
}

static int32_t valid(const uint8_t *source, uint8_t *target,
    int32_t width, int32_t height, int32_t stride, int32_t channels,
    int32_t bits, int32_t alpha_mode, const uint8_t *mask, int32_t mask_stride)
{
    if (!source || !target || width <= 0 || height <= 0 ||
        (channels != 1 && channels != 3 && channels != 4) ||
        (bits != 8 && bits != 16) ||
        (channels == 4 ? (alpha_mode != 1 && alpha_mode != 2) : alpha_mode != 0) ||
        (int64_t)stride < (int64_t)width * channels * (bits / 8) ||
        (mask && mask_stride < width)) return 0;
    return 1;
}

static uint32_t masked(uint32_t original, uint32_t effect, uint8_t weight)
{
    return (original * (255u - weight) + effect * weight + 127u) / 255u;
}

int32_t acme_effects_box_blur(const uint8_t *source, uint8_t *target,
    int32_t width, int32_t height, int32_t stride, int32_t channels,
    int32_t bits, int32_t bottom_up, int32_t alpha_mode, const uint8_t *mask,
    int32_t mask_stride, int32_t mask_bottom_up, int32_t radius,
    acme_effects_progress progress, void *context)
{
    if (!valid(source, target, width, height, stride, channels, bits, alpha_mode, mask, mask_stride) ||
        radius < 1 || radius > 16) return -1;
    int32_t component_bytes = bits / 8;
    int32_t pixel_bytes = channels * component_bytes;
    for (int32_t y = 0; y < height; ++y) {
        if (progress && !progress((int32_t)((int64_t)y * 100 / height), context)) return 1;
        int32_t row = storage_row(y, height, bottom_up);
        for (int32_t x = 0; x < width; ++x) {
            uint8_t weight = mask ? mask[(size_t)storage_row(y, height, mask_bottom_up) * mask_stride + x] : 255;
            for (int32_t c = 0; c < channels; ++c) {
                const uint8_t *original_ptr = source + (size_t)row * stride + (size_t)x * pixel_bytes + c * component_bytes;
                uint8_t *target_ptr = target + (size_t)row * stride + (size_t)x * pixel_bytes + c * component_bytes;
                uint32_t original = read_component(original_ptr, bits);
                if (channels == 4 && c == 3) {
                    write_component(target_ptr, bits, original);
                    continue;
                }
                uint64_t sum = 0;
                uint32_t count = 0;
                for (int32_t ny = y - radius; ny <= y + radius; ++ny) {
                    if (ny < 0 || ny >= height) continue;
                    int32_t neighbour_row = storage_row(ny, height, bottom_up);
                    for (int32_t nx = x - radius; nx <= x + radius; ++nx) {
                        if (nx < 0 || nx >= width) continue;
                        const uint8_t *p = source + (size_t)neighbour_row * stride + (size_t)nx * pixel_bytes + c * component_bytes;
                        sum += read_component(p, bits);
                        ++count;
                    }
                }
                uint32_t blurred = (uint32_t)((sum + count / 2) / count);
                uint32_t result = masked(original, blurred, weight);
                if (alpha_mode == 2) {
                    uint32_t alpha = read_component(source + (size_t)row * stride +
                        (size_t)x * pixel_bytes + 3 * component_bytes, bits);
                    if (result > alpha) result = alpha;
                }
                write_component(target_ptr, bits, result);
            }
        }
    }
    if (progress && !progress(100, context)) return 1;
    return 0;
}

int32_t acme_effects_gain(const uint8_t *source, uint8_t *target,
    int32_t width, int32_t height, int32_t stride, int32_t channels,
    int32_t bits, int32_t bottom_up, int32_t alpha_mode, const uint8_t *mask,
    int32_t mask_stride, int32_t mask_bottom_up, int32_t gain_percent,
    acme_effects_progress progress, void *context)
{
    if (!valid(source, target, width, height, stride, channels, bits, alpha_mode, mask, mask_stride) ||
        gain_percent < 0 || gain_percent > 400) return -1;
    int32_t component_bytes = bits / 8;
    int32_t pixel_bytes = channels * component_bytes;
    uint32_t maximum = bits == 8 ? 255u : 65535u;
    for (int32_t y = 0; y < height; ++y) {
        if (progress && !progress((int32_t)((int64_t)y * 100 / height), context)) return 1;
        int32_t row = storage_row(y, height, bottom_up);
        for (int32_t x = 0; x < width; ++x) {
            uint8_t weight = mask ? mask[(size_t)storage_row(y, height, mask_bottom_up) * mask_stride + x] : 255;
            for (int32_t c = 0; c < channels; ++c) {
                const uint8_t *original_ptr = source + (size_t)row * stride + (size_t)x * pixel_bytes + c * component_bytes;
                uint8_t *target_ptr = target + (size_t)row * stride + (size_t)x * pixel_bytes + c * component_bytes;
                uint32_t original = read_component(original_ptr, bits);
                if (channels == 4 && c == 3) {
                    write_component(target_ptr, bits, original);
                    continue;
                }
                uint64_t scaled = ((uint64_t)original * (uint32_t)gain_percent + 50u) / 100u;
                uint32_t gained = scaled > maximum ? maximum : (uint32_t)scaled;
                uint32_t result = masked(original, gained, weight);
                if (alpha_mode == 2) {
                    uint32_t alpha = read_component(source + (size_t)row * stride +
                        (size_t)x * pixel_bytes + 3 * component_bytes, bits);
                    if (result > alpha) result = alpha;
                }
                write_component(target_ptr, bits, result);
            }
        }
    }
    if (progress && !progress(100, context)) return 1;
    return 0;
}
