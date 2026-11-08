from PIL import Image
import numpy as np

im = Image.open('scratch/clean_bracing.png')
arr = np.array(im)

# Let's inspect the columns at y > 530:
# Where is the back foot?
# Where is the front foot?
# Where is the greatsword tip?
# Let's save an annotated slice of y=480..650
slice_img = im.crop((0, 480, im.width, 650))
slice_img.save('scratch/slice_feet_raw.png')
print("Saved slice_feet_raw.png")
