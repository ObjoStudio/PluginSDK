#include "acme/core.h"

int64_t acme_core_rotate_left(int64_t value, int64_t bits)
{
    int shift = (int)(bits & 63);
    uint64_t u = (uint64_t)value;
    return (int64_t)((u << shift) | (u >> ((64 - shift) & 63)));
}
