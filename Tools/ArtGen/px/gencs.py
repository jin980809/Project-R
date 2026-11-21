import json
from npcs import NPCS
items=json.load(open('out/scene.json'))
def f(v): return f"{v:.3f}f".replace('.000f','f') if isinstance(v,float) or isinstance(v,int) else v
def F(v): return f"{float(v):.3f}f"
L=[]
L.append("// 자동 생성: Tools/ArtGen/px/gencs.py (scene.py, npcs.py). 직접 고치지 말고 생성기를 고친다.")
L.append("// 로비 마을(픽셀아트) 배치. 좌표는 월드 유닛(발밑 기준), 겹침 애니메이션 위치는 그림의 왼쪽 위 기준 픽셀.")
L.append("public static class VillageLayout\n{")
L.append("    public struct Loop { public string prefix; public int frames; public float px, py, fps, offset; public Loop(string p, int n, float x, float y, float f, float o) { prefix = p; frames = n; px = x; py = y; fps = f; offset = o; } }")
L.append("    public struct Item\n    {\n        public string kind, name, file; public float x, y; public bool hasCollider; public float cw, ch; public bool hasFade; public float fx0, fy0, fx1, fy1; public float width; public Loop[] loops; public string light; public float lx, ly;\n    }")
L.append("    public struct Npc\n    {\n        public string name, costume; public float x, y; public bool faceLeft; public string itemType, itemSprite; public int hair, eye, beard, hairColor, skinColor; public float scale, speed; public bool work; public float[] waypoints; public string[] lines;\n    }")
L.append("    public static readonly Item[] Items =\n    {")
for it in items:
    c=it['coll']; fd=it['fade']; loops=it['loops']; lt=it.get('light')
    ls="null" if not loops else "new[] { "+", ".join(f'new Loop("{p}", {n}, {F(x)}, {F(y)}, {F(fp)}, {F(o)})' for (p,n,(x,y),fp,o) in loops)+" }"
    L.append("        new Item { kind = \"%s\", name = \"%s\", file = \"%s\", x = %s, y = %s, hasCollider = %s, cw = %s, ch = %s, hasFade = %s, fx0 = %s, fy0 = %s, fx1 = %s, fy1 = %s, width = %s, loops = %s, light = %s, lx = %s, ly = %s },"%(
        it['kind'],it['name'],it['file'],F(it['x']),F(it['y']),'true' if c else 'false',F(c[0] if c else 0),F(c[1] if c else 0),
        'true' if fd else 'false',*(F(v) for v in (fd or (0,0,0,0))),F(it.get('width',0)),ls,
        ('"%s"'%lt[0]) if lt else 'null',F(lt[1] if lt else 0),F(lt[2] if lt else 0)))
L.append("    };")
L.append("    public static readonly Npc[] Npcs =\n    {")
for n in NPCS:
    it=n.get('item'); wp=n.get('wp')
    wps="null" if not wp else "new[] { "+", ".join(F(v) for p in wp for v in p)+" }"
    lines="new[] { "+", ".join('"%s"'%s for s in n['lines'])+" }"
    h,e,b,hc,sc=n['look']
    L.append("        new Npc { name = \"%s\", costume = \"%s\", x = %s, y = %s, faceLeft = %s, itemType = %s, itemSprite = %s, hair = %d, eye = %d, beard = %d, hairColor = %d, skinColor = %d, scale = %s, speed = %s, work = %s, waypoints = %s, lines = %s },"%(
        n['name'],n['c'],F(n['pos'][0]),F(n['pos'][1]),'true' if n['left'] else 'false',('"%s"'%it[0]) if it else 'null',('"%s"'%it[1]) if it else 'null',h,e,b,hc,sc,F(n.get('scale',1)),F(n.get('speed',0)),'true' if n.get('work') else 'false',wps,lines))
L.append("    };\n}")
open('VillageLayout.cs','w',encoding='utf-8-sig').write("\n".join(L)+"\n")
print(len(L))
