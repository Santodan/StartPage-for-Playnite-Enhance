import os, re, sys
sys.stdout.reconfigure(encoding='utf-8')
# UTF-8 text that was decoded as cp1252 and saved again as UTF-8 ("â˜…" instead of "★").
pat = re.compile('[ÂÃâ][\u0080-¿ŒœŠšŸŽžƒˆ˜–—‘-„†-•…‰‹›€™]+')
for top in sys.argv[1:]:
    paths = [top] if os.path.isfile(top) else [os.path.join(r, f) for r, d, fs in os.walk(top) if not {'obj', 'bin', '.git', 'node_modules'} & set(r.split(os.sep)) for f in fs]
    for p in paths:
        if not p.endswith(('.cs', '.xaml', '.md', '.json', '.ps1', '.yaml')): continue
        try: s = open(p, encoding='utf-8-sig').read()
        except Exception: print('NOT-UTF8', p); continue
        for i, line in enumerate(s.splitlines(), 1):
            for m in pat.finditer(line):
                try: fixed = m.group().encode('cp1252').decode('utf-8')
                except Exception: continue
                print(f'{p}:{i}: {m.group()} -> {fixed}')
