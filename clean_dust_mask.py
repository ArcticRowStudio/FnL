from PIL import Image
import numpy as np

im = Image.open('scratch/clean_bracing.png')
arr = np.array(im, copy=True)
h, w, _ = arr.shape

r = arr[:, :, 0].astype(int)
g = arr[:, :, 1].astype(int)
b = arr[:, :, 2].astype(int)
a = arr[:, :, 3].astype(int)

# In slice (y >= 480):
# Character colors:
# - Blue: b - r > 15
# - Silver / White: r > 170, g > 185, b > 200 (b >= r)
# - Boot dark outline: r < 60, g < 60, b < 90, b >= r (cool/navy black)
# - Sword: grey, abs(r-b) <= 8 and r in [80..220]

# Dust colors:
# Tan/brown: r > b + 10, with warm hue (r >= g >= b)
# Light dust: r in [170..230], g in [155..215], b in [140..200], r - b in [15..45]
# Mid dust: r in [120..175], g in [105..160], b in [90..140], r - b in [15..40]
# Dark dust outline: r in [70..120], g in [60..105], b in [50..90], r - b in [12..35]
# Specks: r > b + 8 in dust regions

is_y_dust = np.arange(h)[:, None] >= 490

is_dust = is_y_dust & (a > 10) & (
    # Warm brown / tan
    ((r - b >= 10) & (r >= g) & (g >= b - 5) & (r > 65)) |
    # Very light dust fringe
    ((r - b >= 6) & (r > 160) & (g > 150) & (b < 195) & (r >= g)) |
    # Floating dust pebbles to the left (x < 110)
    (np.arange(w)[None, :] < 95) |
    # Dust puffs between feet (x in [240..360] and y > 530)
    ((np.arange(w)[None, :] >= 240) & (np.arange(w)[None, :] <= 360) & (np.arange(h)[:, None] >= 530))
)

# Protect sword blade: x > 470, y in [480..620], silver/grey
is_sword = (np.arange(w)[None, :] >= 470) & (np.abs(r - b) <= 10) & (r > 60)
# Protect boots: cool colors
is_boot = (b - r >= 5) | ((b >= r) & (r > 160) & (b > 170)) | ((r < 50) & (b >= r))

is_dust_clean = is_dust & (~is_sword) & (~is_boot)

# Character without dust:
arr_clean = arr.copy()
arr_clean[is_dust_clean, 3] = 0

# Also clean up any isolated tiny transparent alpha remnants
# Save clean bracing
Image.fromarray(arr_clean, 'RGBA').save('scratch/bracing_isolated.png')

# Dust only:
arr_dust = np.zeros_like(arr)
arr_dust[is_dust_clean] = arr[is_dust_clean]
Image.fromarray(arr_dust, 'RGBA').save('scratch/dust_isolated.png')

print("Created bracing_isolated.png and dust_isolated.png")
