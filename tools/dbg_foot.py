import sys, json; sys.path.insert(0,'.')
tr=json.load(open('cache/showdown/monster_trace.json'))['b']
for sd in 'lr':
    f=tr['foot_'+sd]; b=tr['ball_'+sd]
    pl=sorted(f[i][1] for i in range(len(f)) if b[i][1]<3 and f[i][1]>-3)
    allf=sorted(p[1] for p in f if p[1]>-3)
    n=len(pl); m=len(allf)
    print(sd,'planted foot y: p10 %.1f p50 %.1f p90 %.1f | all(foot above ground): p50 %.1f p90 %.1f p99 %.1f max %.1f'%(pl[n//10],pl[n//2],pl[9*n//10],allf[m//2],allf[9*m//10],allf[99*m//100],allf[-1]))
