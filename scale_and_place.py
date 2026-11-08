from PIL import Image

scale = 0.205
paste_y = 39
paste_x = 70

char_raw = Image.open('scratch/full_char_clean.png')
dust_raw = Image.open('scratch/full_dust_clean.png')

new_w = int(round(char_raw.width * scale))
new_h = int(round(char_raw.height * scale))

scaled_char = char_raw.resize((new_w, new_h), Image.Resampling.LANCZOS)
scaled_dust = dust_raw.resize((new_w, new_h), Image.Resampling.LANCZOS)

canvas_char = Image.new('RGBA', (256, 192), (0, 0, 0, 0))
canvas_char.paste(scaled_char, (paste_x, paste_y), scaled_char)
canvas_char.save('scratch/canvas_bracing_clean.png')

canvas_dust = Image.new('RGBA', (256, 192), (0, 0, 0, 0))
canvas_dust.paste(scaled_dust, (paste_x, paste_y), scaled_dust)
canvas_dust.save('scratch/canvas_dust.png')

# Combined test
comb = Image.alpha_composite(canvas_char, canvas_dust)
comb.save('scratch/canvas_combined.png')

# Side by side preview
strip = Image.new('RGBA', (256 * 3, 192), (0, 0, 0, 0))
strip.paste(canvas_char, (0, 0))
strip.paste(canvas_dust, (256, 0))
strip.paste(comb, (512, 0))
strip.save('scratch/dust_comparison.png')

print("Created canvas_bracing_clean.png, canvas_dust.png, and dust_comparison.png")
