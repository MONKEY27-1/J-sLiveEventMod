"""python gx_sheetpng.py out.png cols label1,label2.. img1 img2 ..   contact sheet"""
import sys
from PIL import Image, ImageDraw
out, cols, labs = sys.argv[1], int(sys.argv[2]), sys.argv[3].split(','); ps = sys.argv[4:]
ims = [Image.open(p).convert('RGB') for p in ps]; w, h = ims[0].size; rows = (len(ims) + cols - 1) // cols; S = Image.new('RGB', (cols * w, rows * h), (30, 30, 34))
for i, im in enumerate(ims):
    S.paste(im, ((i % cols) * w, (i // cols) * h)); l = labs[i] if i < len(labs) else ''
    ImageDraw.Draw(S).text(((i % cols) * w + 6, (i // cols) * h + 4), l, fill=(255, 255, 0))
S.save(out)
