"""Export the publisher's complete cached xref corpus as offline JSON, Markdown and original HTML.
No CAD connection. Source content is documentation, never executable instructions.
"""
from html.parser import HTMLParser
from pathlib import Path
import hashlib, json, re
from cache_reference import ROOT, CACHE, BASE, fetch

DEST = ROOT / 'TopSolid.Automation.Mcp.Server.AddIn/TopSolid.Automation'

class MarkdownArticle(HTMLParser):
    def __init__(self):
        super().__init__(); self.active = False; self.parts = []; self.pre = False; self.cell = False; self.headers = 0
    def handle_starttag(self, tag, attrs):
        if tag == 'article': self.active = True
        if not self.active: return
        if tag in ('td', 'th'):
            self.cell = True
            if tag == 'th': self.headers += 1
            return
        if self.cell: return
        if tag in ('h1','h2','h3','h4','h5','h6'): self.parts.append('\n\n' + '#' * int(tag[1]) + ' ')
        elif tag in ('p','div','table'): self.parts.append('\n')
        elif tag == 'pre': self.parts.append('\n```\n'); self.pre = True
        elif tag == 'li': self.parts.append('\n- ')
        elif tag == 'tr': self.parts.append('\n| '); self.headers = 0
        elif tag == 'br': self.parts.append('\n')
    def handle_endtag(self, tag):
        if tag == 'article': self.active = False
        if not self.active: return
        if tag in ('td','th'): self.parts.append(' | '); self.cell = False
        elif self.cell: return
        elif tag == 'tr' and self.headers: self.parts.append('\n| ' + '--- | ' * self.headers + '\n')
        elif tag == 'pre': self.parts.append('\n```\n'); self.pre = False
        elif tag in ('p','div','tr','table','h1','h2','h3','h4','h5','h6'): self.parts.append('\n')
    def handle_data(self, value):
        if self.active: self.parts.append(value if self.pre else re.sub(r'\s+', ' ', value))
    def result(self):
        return re.sub(r'\n[ \t]*\n(?:[ \t]*\n)+', '\n\n', ''.join(self.parts)).strip() + '\n'

def main():
    index = json.loads((ROOT/'docs/api/reference-index.json').read_text(encoding='utf-8'))
    pages = sorted({entry['href'] for entry in index['entries']})
    manifest = []
    for path in pages:
        cached = fetch(path)
        raw = (CACHE/'html'/path).read_bytes()
        parser = MarkdownArticle(); parser.feed(raw.decode('utf-8-sig'))
        content = parser.result()
        if len(content.strip()) < 10: raise ValueError('Missing article: ' + path)
        key = hashlib.sha256(path.encode()).hexdigest()[:24]
        module = path.split('/')[1]
        json_path = f'articles/{module}/{key}.json'
        md_path = f'markdown/{module}/{key}.md'
        html_path = f'html/{module}/{key}.html'
        article = {**cached, 'text': content, 'markdown': md_path, 'originalHtml': html_path}
        for rel, data in ((json_path, json.dumps(article, ensure_ascii=False, indent=2).encode()),
                          (md_path, (f'Source: {BASE + path}\n\nSource SHA-256: `{cached["sha256"]}`\n\n' + content).encode()),
                          (html_path, raw)):
            output = DEST / rel; output.parent.mkdir(parents=True, exist_ok=True)
            if not output.exists() or output.read_bytes() != data: output.write_bytes(data)
        manifest.append({'sourcePath': path, 'sourceUrl': cached['url'], 'sourceSha256': cached['sha256'],
                         'article': json_path, 'markdown': md_path, 'originalHtml': html_path})
    lookup = {p['sourcePath']: p for p in manifest}
    for entry in index['entries']:
        entry['article'] = lookup[entry['href']]['article']
        entry['markdown'] = lookup[entry['href']]['markdown']
    DEST.mkdir(parents=True, exist_ok=True)
    (DEST/'reference-index.json').write_text(json.dumps(index, ensure_ascii=False, indent=2), encoding='utf-8')
    report = {'source': BASE, 'symbols': len(index['entries']), 'pages': len(pages), 'failures': [], 'pagesManifest': manifest}
    (DEST/'manifest.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    links = '\n'.join(f'- [{e["uid"]}]({e["markdown"]})' for e in index['entries'])
    (DEST/'INDEX.md').write_text('# TopSolid 7.20 Automation reference\n\n' + links + '\n', encoding='utf-8')
    (DEST/'README.md').write_text(f'''# Local TopSolid.Automation reference

{len(index['entries'])} documented symbols across {len(pages)} unique official API pages.
Includes the publisher's namespaces, classes, interfaces, structures, enumerations, fields, properties, methods and overloads. Descriptions, syntax, parameters, return values, remarks and examples are retained where the publisher supplies them. Undocumented information is not invented.

- [Symbol index](INDEX.md): every xref symbol links to its article. Overloads and enum fields can share an article.
- `reference-index.json`: machine-readable symbol-to-file mapping.
- `articles/`: individual JSON articles, loaded on demand by MCP.
- `markdown/`: human-readable articles.
- `html/`: exact cached original pages for full markup fidelity; SHA-256 recorded in `manifest.json`.
- `manifest.json`: provenance and completeness against the official xref map, not a claim of undocumented internal API coverage.

Source: {BASE}
At runtime, MCP tool metadata and API-reference results use bundle-relative `TopSolid.Automation/...` paths. The official URL above is provenance only; the server reads the bundled articles locally and does not open or download the website during chat. Regenerate with `scripts/ApiReference/cache_reference.py`, then `export_local_reference.py`. Build/publish copies this folder beside the server. The embedded snapshot remains a fallback when files are absent. Documentation does not authorize or implement new executable tools.

The pre-existing DLLs in this source folder are legacy files; the build continues to reference the matched installed 7.20 SDK, not these DLLs.
''', encoding='utf-8')
    print(json.dumps({'symbols': report['symbols'], 'pages': report['pages'], 'failures': [], 'folder': str(DEST)}))

if __name__ == '__main__': main()
