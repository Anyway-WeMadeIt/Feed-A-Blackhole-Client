import json, math, uuid, copy, shutil
from pathlib import Path
import numpy as np
from PIL import Image
# This script lives in Tools/BattleBackground, so the repository root is two folders up.
ROOT=Path(__file__).resolve().parents[2]
T=ROOT/'Assets'/'VFX'/'Background'
N=1024
lines=np.zeros((N,N),np.float32); stars=lines.copy(); halos=lines.copy()
flow=np.zeros((N,N,4),np.float32);flow[:,:,:2]=.5
paths=[]; points=[]
for r,begin,end in [(.442,15,135),(.442,195,315)]:
 ang=np.linspace(math.radians(begin),math.radians(end),650)
 paths.append(np.stack([.5+r*np.cos(ang),.5+r*np.sin(ang)],axis=1))
chains=[]
for c in chains: paths.append(np.array(c));points.extend(c)
# All coordinates are texture-space, y upwards. The same paths produce visuals and flow data.
for pi,path in enumerate(paths):
 lens=np.linalg.norm(np.diff(path,axis=0),axis=1);total=lens.sum();cum=0
 for j,(p,q) in enumerate(zip(path[:-1],path[1:])):
  x0=max(0,int((min(p[0],q[0])-.006)*N));x1=min(N,int((max(p[0],q[0])+.006)*N)+1)
  y0=max(0,int((min(p[1],q[1])-.006)*N));y1=min(N,int((max(p[1],q[1])+.006)*N)+1)
  yy,xx=np.mgrid[y0:y1,x0:x1]; v=q-p; u=np.clip(((xx/N-p[0])*v[0]+(yy/N-p[1])*v[1])/np.dot(v,v),0,1)
  dist=np.sqrt((xx/N-p[0]-u*v[0])**2+(yy/N-p[1]-u*v[1])**2)
  stroke=np.clip((.00066-dist)*N+.5,0,1)
  lines[y0:y1,x0:x1]=np.maximum(lines[y0:y1,x0:x1],stroke)
  coverage=np.exp(-(dist/.0018)**2)
  phase=(cum+u*lens[j])/total
  coverage *= np.clip(phase/0.035,0,1)*np.clip((1-phase)/0.035,0,1)
  f=flow[y0:y1,x0:x1];hit=coverage>f[:,:,2]
  f[:,:,0][hit]=(.5+.49*np.cos(phase*math.tau))[hit];f[:,:,1][hit]=(.5+.49*np.sin(phase*math.tau))[hit]
  f[:,:,2][hit]=coverage[hit];f[:,:,3][hit]=(pi*.173)%1
  cum+=lens[j]
rng=np.random.default_rng(48)
points += [(0.5+0.43*math.cos(v),0.5+0.43*math.sin(v)) for v in rng.uniform(0,math.tau,18)]
for idx,(x,y) in enumerate(points):
 size=.006 if idx<len(sum(chains,[])) else rng.uniform(.0012,.0026)
 pad=int(max(size*3,.01)*N);cx=int(x*N);cy=int(y*N)
 x0=max(0,cx-pad);x1=min(N,cx+pad+1);y0=max(0,cy-pad);y1=min(N,cy+pad+1)
 yy,xx=np.mgrid[y0:y1,x0:x1];dx=(xx/N-x);dy=(yy/N-y)
 d=np.sqrt(dx*dx+dy*dy)
 core=np.exp(-(d/(size*.22))**2)
 if idx<len(sum(chains,[])):
  core=np.maximum(core,np.clip(1-(np.sqrt(abs(dx)/size)+np.sqrt(abs(dy)/size)),0,1))
 stars[y0:y1,x0:x1]=np.maximum(stars[y0:y1,x0:x1],core)
 halos[y0:y1,x0:x1]=np.maximum(halos[y0:y1,x0:x1],np.exp(-(d/(size*1.3))**2)*.35)
def save(arr,name): Image.fromarray((np.clip(arr,0,1)*255+.5).astype('uint8')[::-1]).save(T/name)
save(np.stack([lines,stars,halos,np.ones_like(lines)],2),'Battle_Masks.png')
save(flow,'Battle_Flow.png')
# Editable vector layout source.
svg=['<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1024 1024"><g fill="none" stroke="white" stroke-width="2">']
for path in paths: svg.append('<polyline points="'+' '.join(f'{x*N:.2f},{(1-y)*N:.2f}' for x,y in path)+'"/>')
svg.append('</g><g fill="white">')
for x,y in points:svg.append(f'<circle cx="{x*N:.2f}" cy="{(1-y)*N:.2f}" r="2.5"/>')
svg.append('</g></svg>');(Path(__file__).resolve().parent/'Battle_Layout.svg').write_text('\n'.join(svg))
