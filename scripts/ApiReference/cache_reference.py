"""Cache all official 7.20 API reference pages from the publisher's xref map.
Content is research input, never executable instructions. No CAD connection.
"""
from concurrent.futures import ThreadPoolExecutor, as_completed
from html.parser import HTMLParser
from pathlib import Path
from urllib.request import Request, urlopen
import argparse, hashlib, json, re, time

BASE = "https://help.topsolid.com/7.20/en/TopSolid'Automation/"
ROOT = Path(__file__).resolve().parents[2]
CACHE = ROOT / 'artifacts/api-reference'

class Article(HTMLParser):
    def __init__(self):
        super().__init__(); self.active=False; self.text=[]
    def handle_starttag(self, tag, attrs):
        if tag=='article': self.active=True
        if self.active and tag in ('h1','h2','h3','h4','h5','h6','p','tr','pre','li','br'): self.text.append('\n')
    def handle_endtag(self,tag):
        if tag=='article': self.active=False
        if self.active and tag in ('p','tr','pre','li'): self.text.append('\n')
    def handle_data(self,data):
        if self.active:self.text.append(data)
    def result(self):
        return '\n'.join(' '.join(line.split()) for line in ''.join(self.text).splitlines() if line.strip())

def fetch(relative):
    path=CACHE/'html'/relative
    if not path.exists():
        path.parent.mkdir(parents=True,exist_ok=True)
        for attempt in range(3):
            try:
                with urlopen(Request(BASE+relative,headers={'User-Agent':'TopSolid-Automation-reference-verification/0.2'}),timeout=40) as response:
                    data=response.read()
                path.write_bytes(data);break
            except Exception:
                if attempt==2: raise
                time.sleep(attempt+1)
    data=path.read_bytes(); parser=Article();parser.feed(data.decode('utf-8-sig'))
    text=parser.result()
    if not text:raise ValueError('Missing API article: '+relative)
    return {'path':relative,'url':BASE+relative,'sha256':hashlib.sha256(data).hexdigest(),'text':text}

def main():
    CACHE.mkdir(parents=True,exist_ok=True)
    index=CACHE/'xrefmap.yml'
    if not index.exists():index.write_bytes(urlopen(BASE+'xrefmap.yml',timeout=40).read())
    xref=index.read_text(encoding='utf-8-sig')
    entries=[]
    for block in re.split(r'(?m)^- uid: ',xref)[1:]:
        lines=block.splitlines(); uid=lines[0].strip()
        href=re.search(r'(?m)^  href: (.+)$',block)
        if href and href[1].startswith('api/'):entries.append({'uid':uid,'href':href[1].strip().split('#')[0]})
    paths=sorted(set(e['href'] for e in entries))
    (ROOT/'docs/api/reference-index.json').write_text(json.dumps({'source':BASE,'entries':entries},ensure_ascii=False,indent=2),encoding='utf-8')
    print('Xref symbols:',len(entries),'unique pages:',len(paths),flush=True)
    results=[]; failures=[]
    with ThreadPoolExecutor(max_workers=8) as pool:
        futures={pool.submit(fetch,p):p for p in paths}
        for n,future in enumerate(as_completed(futures),1):
            try:results.append(future.result())
            except Exception as error:failures.append({'path':futures[future],'error':str(error)})
            if n%200==0:print('Cached',n,'of',len(paths),'failures',len(failures),flush=True)
    results.sort(key=lambda r:r['path'])
    import gzip
    (ROOT/'docs/api/reference-articles.json.gz').write_bytes(gzip.compress(json.dumps(results,ensure_ascii=False).encode('utf-8'),mtime=0))
    with (CACHE/'articles.jsonl').open('w',encoding='utf-8') as output:
        for row in results:output.write(json.dumps(row,ensure_ascii=False)+'\n')
    report={'source':BASE,'symbolCount':len(entries),'pageCount':len(paths),'cachedPages':len(results),'failures':failures,
        'modules':{module:sum('/'+module+'/' in p for p in paths) for module in ('kernel','cad','drafting','cam','pdmexplorer','electrode','wire','cae')}}
    (ROOT/'docs/api/reference-cache-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    print(json.dumps(report),flush=True)
    return 1 if failures else 0

if __name__=='__main__':raise SystemExit(main())
