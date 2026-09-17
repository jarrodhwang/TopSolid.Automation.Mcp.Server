"""Validate all local pages, symbol mappings and publisher xref freshness."""
import hashlib, json
from collections import Counter
from urllib.request import urlopen
from export_local_reference import DEST, ROOT, CACHE, BASE

index = json.loads((DEST/'reference-index.json').read_text(encoding='utf-8'))
manifest = json.loads((DEST/'manifest.json').read_text(encoding='utf-8'))
pages = {p['sourcePath']: p for p in manifest['pagesManifest']}
assert len(pages) == manifest['pages']
assert len(index['entries']) == manifest['symbols']
assert {e['href'] for e in index['entries']} == set(pages)
kinds = Counter()
for p in pages.values():
    raw = (DEST/p['originalHtml']).read_bytes()
    assert hashlib.sha256(raw).hexdigest() == p['sourceSha256']
    article = json.loads((DEST/p['article']).read_text(encoding='utf-8'))
    markdown = (DEST/p['markdown']).read_text(encoding='utf-8')
    assert article['path'] == p['sourcePath'] and article['sha256'] == p['sourceSha256']
    assert article['text'] in markdown and p['sourceUrl'] in markdown
    title = next(line for line in article['text'].splitlines() if line.startswith('# '))
    kinds[title.split()[1]] += 1
for e in index['entries']:
    assert e['article'] == pages[e['href']]['article'] and e['markdown'] == pages[e['href']]['markdown']
remote = urlopen(BASE+'xrefmap.yml', timeout=30).read()
assert hashlib.sha256(remote).digest() == hashlib.sha256((CACHE/'xrefmap.yml').read_bytes()).digest(), 'Publisher xref changed: refresh cache and export again'
report = {'symbols': len(index['entries']), 'pages': len(pages), 'allFilesAndSourceHashesVerified': True,
          'publisherXrefMatches': True, 'pageKinds': dict(kinds)}
(ROOT/'artifacts/local-reference-verification.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report))
