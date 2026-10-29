import sys, json, math; sys.path.insert(0,'.')
import monster_events as ME
rt=json.load(open('cache/showdown/robot_trace.json'))['b']
bad=[]
for t in range(84,296,2):
    m=ME.at('pelvis',t); r=rt['pelvis'][int(t/0.1)]; d=math.hypot(m[0]-r[0],m[2]-r[2])
    hidden = 128.9<t<156.3
    if d<55 and not hidden: bad.append((t,round(d)))
print(bad)
