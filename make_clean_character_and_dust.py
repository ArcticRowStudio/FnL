from PIL import Image
import numpy as np

im = Image.open('scratch/clean_bracing.png')
arr = np.array(im, copy=True)
h, w, _ = arr.shape

r = arr[:, :, 0].astype(int)
g = arr[:, :, 1].astype(int)
b = arr[:, :, 2].astype(int)
a = arr[:, :, 3].astype(int)

# In slice y >= 520 (feet region):
# Character parts:
# - Blue pants/cuffs: b - r > 15
# - Silver boot armor: (b >= r) & (r > 150)
# - Boot dark lines: (r < 50) & (b >= r)
# - Greatsword blade: x >= 470, abs(r-b) <= 8
# - Boot white highlights: r > 200, g > 200, b > 210

# Dust:
# Must be y >= 525 (below knee gold trim completely!)
is_below_knee = np.arange(h)[:, None] >= 525

# Dust is tan/brown:
# r >= g and r > b + 8
is_dust = is_below_knee & (a > 10) & (
    # Warm brown/tan body of dust
    ((r - b >= 8) & (r >= g - 2) & (r >= 55) & (b < 200)) |
    # Outlying dust specks (x < 100 or x in [240..370] with y > 530)
    (np.arange(w)[None, :] < 100) |
    ((np.arange(w)[None, :] >= 235) & (np.arange(w)[None, :] <= 370) & (np.arange(h)[:, None] >= 530))
)

# Protect sword blade:
is_sword = (np.arange(w)[None, :] >= 475) & (np.abs(r - b) <= 10) & (r > 60)

# Protect boots:
is_boot = (
    # Blue parts
    (b - r >= 5) |
    # Silver armor
    ((b >= r) & (r >= 140) & (b >= 150)) |
    # White highlight on boot tip
    ((r >= 210) & (g >= 220) & (b >= 230)) |
    # Dark outline of boots (navy)
    ((r <= 45) & (g <= 45) & (b <= 65) & (b >= r))
)

is_dust_final = is_dust & (~is_sword) & (~is_boot)

# Clean character
arr_char = arr.copy()
arr_char[is_dust_final, 3] = 0

# Remove any isolated tiny alpha specks in bottom left (x < 110, y > 580)
specks_left = (np.arange(w)[None, :] < 105) & (np.arange(h)[:, None] >= 560)
arr_char[specks_left, 3] = 0

# Remove any stray speck between feet (x in [200..360], y > 570)
specks_mid = (np.arange(w)[None, :] >= 190) & (np.arange(w)[None, :] <= 360) & (np.arange(h)[:, None] >= 550)
# But don't remove front boot
front_boot_x = (np.arange(w)[None, :] >= 370)
arr_char[specks_mid & (~is_boot), 3] = 0

# Clean dust only
arr_dust = np.zeros_like(arr)
# Dust is only y >= 525
arr_dust[is_dust_final & is_below_knee] = arr[is_dust_final & is_below_knee]
# Remove any accidental boot pixels in dust
arr_dust[is_boot, 3] = 0
arr_dust[is_sword, 3] = 0

# Save full-res versions
Image.fromarray(arr_char, 'RGBA').save('scratch/full_char_clean.png')
Image.fromarray(arr_dust, 'RGBA').save('scratch/full_dust_clean.png')

print("Created full_char_clean.png and full_dust_clean.png")
