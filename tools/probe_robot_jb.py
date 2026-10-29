import os, sys
os.environ['ACTOR']='robot'; sys.path.insert(0,'.')
from gcore import *
xs=[];ys=[];zs=[]
for j in ssk['joints']:
    G=s_G0[j]; xs.append(-G[0][3]); ys.append(G[1][3]); zs.append(G[2][3])
print('robot joints: x [%.1f,%.1f] y [%.1f,%.1f] z [%.1f,%.1f]'%(min(xs),max(xs),min(ys),max(ys),min(zs),max(zs)))
H=max(ys)-min(ys); print('jb height',H,'KM',KM,'HEIGHT_M',HEIGHT_M,'BODY_H',BODY_H,'loader scale (m per unit) =',HEIGHT_M/H,'vs KM',KM)
print('center x %.2f z %.2f (units) -> metres %.1f %.1f ; min y %.2f -> m %.1f'%((max(xs)+min(xs))/2,(max(zs)+min(zs))/2,(max(xs)+min(xs))/2*HEIGHT_M/H,(max(zs)+min(zs))/2*HEIGHT_M/H,min(ys),min(ys)*HEIGHT_M/H))
