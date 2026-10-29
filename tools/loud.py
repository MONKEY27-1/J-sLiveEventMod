"""blender -b -P loud.py -- <in.mp3> [<out.mp3> <target_rms_dbfs>]
Prints peak / rms of an audio file; with an out file and a target it raises the volume to that RMS (soft-limited above -3 dBFS so it never clips) and writes an mp3."""
import sys, os, numpy as np, aud
a = sys.argv[sys.argv.index('--') + 1:]; snd = aud.Sound(a[0]); rate = snd.specs[0]; d = snd.data().astype(np.float32)
pk = float(np.abs(d).max()); rms = float(np.sqrt((d ** 2).mean()))
print('LEVEL', os.path.basename(a[0])[:50], 'peak %.1f dBFS' % (20 * np.log10(pk + 1e-9)), 'rms %.1f dBFS' % (20 * np.log10(rms + 1e-9)), 'dur %.1f' % (len(d) / rate))
if len(a) >= 3:
    tgt = float(a[2]); g = 10 ** (tgt / 20) / max(rms, 1e-6); y = d * g
    y = np.where(np.abs(y) > 0.7, np.sign(y) * (0.7 + 0.3 * np.tanh((np.abs(y) - 0.7) / 0.3)), y)
    print('gain %.1f dB' % (20 * np.log10(g)), 'new peak %.1f dBFS rms %.1f dBFS' % (20 * np.log10(np.abs(y).max()), 20 * np.log10(np.sqrt((y ** 2).mean()))))
    s2 = aud.Sound.buffer(y.astype(np.float32), rate); s2.write(a[1], int(rate), int(snd.specs[1]), aud.FORMAT_FLOAT32, aud.CONTAINER_MP3, aud.CODEC_MP3, 192)
    print('wrote', a[1])
