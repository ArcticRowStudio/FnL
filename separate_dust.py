from PIL import Image
import numpy as np

im = Image.open('scratch/clean_bracing.png')
arr = np.array(im, copy=True)
h, w, _ = arr.shape

# Dust is located only near the ground (y > 520 in raw 660h image)
# Dust colors: warm brown/tan
# Let's inspect pixels in y > 520
y_coords, x_coords = np.mgrid[0:h, 0:w]

# Condition for dust:
# Must be in lower region y >= 520
# Color check:
# R > B + 20, R > 110, G > 90, B < 170
# Also dark brown outlines: R > B + 15, R in [70..130], G in [60..115], B in [40..90]
is_near_ground = y_coords >= 510

r, g, b, a = arr[:, :, 0], arr[:, :, 1], arr[:, :, 2], arr[:, :, 3]

is_tan_dust = (
    is_near_ground & (a > 10) &
    (
        ((r.astype(int) - b.astype(int) > 20) & (r > 120) & (g > 95) & (b < 165)) |
        ((r.astype(int) - b.astype(int) > 15) & (r >= 70) & (r <= 135) & (g >= 55) & (g <= 120) & (b >= 35) & (b <= 95))
    )
)

# Greatsword blade is silver/grey (r-b is small). Boot sole is grey (r-b is small).
# Let's ensure greatsword blade and boot soles are NOT marked as dust
is_grey_or_blue = (np.abs(r.astype(int) - b.astype(int)) <= 12) | (b.astype(int) - r.astype(int) > 10)
is_dust_mask = is_tan_dust & (~is_grey_or_blue)

# Expand slightly to catch anti-aliased dust edges if surrounded by dust
# 1. No dust image: remove dust
arr_no_dust = arr.copy()
arr_no_dust[is_dust_mask, 3] = 0

# 2. Dust only image: keep only dust
arr_dust_only = np.zeros_like(arr)
arr_dust_only[is_dust_mask] = arr[is_dust_mask]

Image.fromarray(arr_no_dust, 'RGBA').save('scratch/bracing_no_dust.png')
Image.fromarray(arr_dust_only, 'RGBA').save('scratch/bracing_dust_only.png')

print("Separated no_dust and dust_only")
