import sys; sys.path.insert(0,'.')
import real_clip as rc
from rig import *
tot=0
for i,im in enumerate(sg.get('images',[])):
    bv=sg['bufferViews'][im['bufferView']]; print(i,im.get('mimeType'),bv['byteLength']//1024,'KB',im.get('name'))
    tot+=bv['byteLength']
print('images total',tot//1024//1024,'MB; buffer total',len(sb)//1024//1024,'MB; meshes',len(sg['meshes']),'materials',len(sg['materials']))
acc=0
for m in sg['meshes']:
    for p in m['primitives']:
        for k,a in p['attributes'].items(): acc+=sg['accessors'][a]['count']
        print('prim verts',sg['accessors'][p['attributes']['POSITION']]['count'], 'idx', sg['accessors'][p['indices']]['count'] if 'indices' in p else 0)
