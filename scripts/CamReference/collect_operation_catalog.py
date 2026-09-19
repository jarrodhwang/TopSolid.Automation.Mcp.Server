"""Collect official CAM navigation and ADS operation metadata; never execute downloaded JS.

Cache lives under artifacts. The reviewed mapping is kept separately from discovery.
"""
import argparse
import concurrent.futures
import hashlib
import html
import json
import re
from datetime import datetime, timezone
from pathlib import Path
from urllib.parse import urljoin, urlparse
from urllib.request import Request, urlopen

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'artifacts/cam-operation-research'
BASE = "https://help.topsolid.com/7.20/en/TopSolid%27Cam/"


def fetch(url):
    if urlparse(url).hostname not in {'help.topsolid.com', 'ads.topsolid.com'}:
        raise ValueError('Only official documentation hosts are permitted')
    OUT.mkdir(parents=True, exist_ok=True)
    cache = OUT / (hashlib.sha256(url.encode()).hexdigest() + '.html')
    if cache.exists():
        return cache.read_text(encoding='utf-8')
    with urlopen(Request(url, headers={'User-Agent': 'TopSolid-operation-reference-audit/1.0'}), timeout=25) as response:
        if urlparse(response.url).hostname != urlparse(url).hostname:
            raise ValueError('Unexpected documentation redirect')
        data = response.read(4_000_001)
        if len(data) > 4_000_000:
            raise ValueError('Documentation page exceeded limit')
        # RoboHelp mixes UTF-8 pages and legacy Windows-1252 topics.
        declared = re.search(rb'charset\s*=\s*["\']?([\w-]+)', data[:8000], re.I)
        encoding = declared[1].decode('ascii') if declared else response.headers.get_content_charset() or 'utf-8-sig'
        try:
            result = data.decode(encoding)
        except UnicodeDecodeError:
            if urlparse(url).hostname != 'help.topsolid.com':
                raise
            result = data.decode('windows-1252')
        cache.write_text(result, encoding='utf-8')
        return result


def clean(text):
    return re.sub(r'\s+', ' ', html.unescape(re.sub('<[^>]+>', ' ', text))).strip()


def toc():
    pending = {'toc': []}
    visited = set()
    topics = []
    while pending:
        batch = list(pending.items())
        pending = {}
        with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
            pages = list(pool.map(lambda item: fetch(BASE + 'whxdata/' + item[0] + '.new.js'), batch))
        for (key, parents), page in zip(batch, pages):
            visited.add(key)
            match = re.search(r'var toc\s*=\s*(\[.*?\]);', page, re.S)
            if not match:
                raise ValueError('Unexpected navigation format: ' + key)
            for row in json.loads(match[1]):
                if 'url' in row:
                    topics.append({'title': row['name'], 'url': urljoin(BASE, row['url']), 'path': row['url'], 'section': parents})
                if 'key' in row and row['key'] not in visited:
                    pending[row['key']] = parents + [row['name']]
        if len(visited) > 150:
            raise ValueError('Navigation exceeded collection bound')
    return topics


def ads(corpus):
    result = []
    for line in (corpus / 'INDEX.md').open(encoding='utf-8-sig'):
        if not line.startswith('| type |'):
            continue
        match = re.search(r'\[([^\]]+Operation Class)\]\((topics/[^)]+)\).*?`T:(TopSolid\.Cam\.NC\.[^`]+Operation)`', line)
        if not match or '.DB' not in match[3]:
            continue
        text = (corpus / match[2]).read_text(encoding='utf-8-sig')
        header = text.split('---', 2)[1]
        fields = {m[1]: json.loads(m[2]) for m in re.finditer(r'^(\w+): (".*")$', header, re.M)}
        result.append({'nativeType': match[3], 'apiTitle': fields['title'], 'apiDescription': fields.get('description', ''),
                       'apiUrl': fields['source_url'], 'apiAssembly': fields.get('assembly', ''),
                       'apiSnapshotRetrievedUtc': fields.get('retrieved_utc', ''),
                       'abstract': bool(re.search(r'public\s+abstract\s+class', text)),
                       'localTopic': str(corpus / match[2])})
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--ads-corpus', type=Path, required=True)
    parser.add_argument('--verify-pages', action='store_true')
    args = parser.parse_args()
    topics = toc()
    types = ads(args.ads_corpus)
    if args.verify_pages:
        def verify(row):
            result = dict(row)
            url = row.get('url') or row['apiUrl']
            try:
                page = fetch(url)
                title = re.search(r'<title[^>]*>(.*?)</title>', page, re.S | re.I)
                result['pageTitle'] = clean(title[1]) if title else ''
                result['htmlTextSha256'] = hashlib.sha256(page.encode()).hexdigest()
                expected = row.get('nativeType')
                result['pageVerified'] = bool(result['pageTitle']) and (expected is None or expected in page)
                if expected and result['pageTitle'] != row['apiTitle']:
                    result['pageVerified'] = False
                result['links'] = [] if expected else sorted(set(urljoin(url, html.unescape(m[1])) for m in
                    re.finditer(r'href=["\']([^"\']+)["\']', page, re.I)
                    if 'operationcommand.htm' in m[1].lower()))
            except Exception as error:
                result['pageVerified'] = False
                result['error'] = str(error)
            return result
        with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
            topics = list(pool.map(verify, topics))
            types = list(pool.map(verify, types))
    icons = json.loads((ROOT / 'TopSolid.Automation.AI.Studio/Assets/TopSolid/provenance.json').read_text(encoding='utf-8-sig'))
    icon_types = {r['NativeType']: r['Key'] for r in icons if 'NativeType' in r}
    for row in types:
        row['iconKey'] = icon_types.get(row['nativeType'])
        short = row['nativeType'].rsplit('.', 1)[1].removesuffix('Operation').lower()
        # Discovery only. These candidates are not runtime mappings or equivalence claims.
        row['helpCandidates'] = [t for t in topics if short in t['path'].lower().rsplit('/', 1)[-1] and short]
    data = {'collectedUtc': datetime.now(timezone.utc).isoformat(), 'helpRoot': BASE,
            'helpTopics': topics, 'operationTypes': types,
            'iconTypesMissingFromAds': sorted(set(icon_types) - {t['nativeType'] for t in types})}
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / 'discovery.json').write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'helpTopics': len(topics), 'adsOperationTypes': len(types), 'iconTypes': len(icon_types),
                      'helpPagesVerified': sum(t.get('pageVerified', False) for t in topics),
                      'apiPagesVerified': sum(t.get('pageVerified', False) for t in types),
                      'missingFromAds': data['iconTypesMissingFromAds']}, ensure_ascii=False))


if __name__ == '__main__':
    main()
