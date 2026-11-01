# usage: blender -b -P skyfire_frames.py -- out_dir t1 t2 ...   (seconds into the mp4)
import bpy, sys, os
a = sys.argv[sys.argv.index('--')+1:]
out, times = a[0], [float(x) for x in a[1:]]
mp4 = r"C:\Users\drago\Downloads\Operation Sky Fire (Full In-Game Event Video).mp4"
sc = bpy.context.scene
sc.sequence_editor_create()
s = sc.sequence_editor.sequences.new_movie("m", mp4, 1, 1) if hasattr(sc.sequence_editor,'sequences') else sc.sequence_editor.strips.new_movie("m", mp4, 1, 1)
fps = s.fps if hasattr(s,'fps') else 30
print("fps", fps, "len", s.frame_final_duration, "size", s.elements[0].orig_width, s.elements[0].orig_height)
sc.render.fps = int(round(fps)); sc.render.resolution_x = 640; sc.render.resolution_y = 360
sc.render.image_settings.file_format = 'JPEG'; sc.render.image_settings.quality = 80
for t in times:
    sc.frame_set(int(t*fps)+1)
    sc.render.filepath = os.path.join(out, "f%07.2f.jpg" % t)
    bpy.ops.render.render(write_still=True)
