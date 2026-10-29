import sys; sys.path.insert(0,'.')
import real_monster as M
ms,cw,cx,cz,ks,seg_u=M.build_path(M.OFFSET,M.OFFSET+M.DUR)
print(seg_u)
for m in [282,283,284,285,286,287,287.5,288,288.2,288.5,289,289.5,290,290.5,291,291.6,292,293]:
    i=int(round((m-M.OFFSET)*M.FPS)); print(m,'clip',[round(x,2) for x in cw[i]],'cmp',round(cx[i],2),round(cz[i],2),'k',round(ks[i],2))
