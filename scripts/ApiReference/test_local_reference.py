"""Read-only protocol check of on-demand local reference access; no CAD tool calls."""
import json, sys, time
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1]/'LiveWorkflow'))
from native_workflow import Mcp, ROOT

server = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else ROOT/'TopSolid.Automation.Mcp.Server.AddIn/bin/Release/net48/TopSolid.Automation.Mcp.Server.AddIn.exe'
index = json.loads((server.parent/'TopSolid.Automation/reference-index.json').read_text(encoding='utf-8'))
mcp = Mcp(server)
results = []
try:
    for module in ('kernel','cad','drafting','cam','pdmexplorer','electrode','wire','cae'):
        entry = next(e for e in index['entries'] if e['href'].startswith('api/'+module+'/'))
        start = time.perf_counter()
        result = mcp.tool('topsolid_get_api_reference', {'symbol': entry['uid']})
        elapsed = time.perf_counter()-start
        article = json.loads((server.parent/'TopSolid.Automation'/entry['article']).read_text(encoding='utf-8'))
        assert result['text'] == article['text'][:4000] and result['text'].startswith('# '), 'Must use local Markdown article rather than embedded fallback'
        assert result['sourceSha256'] == article['sha256']
        assert result['localFile'].startswith('TopSolid.Automation/'), 'Reference result must identify a bundled local file'
        assert 'https://help.topsolid.com/' not in json.dumps(result), 'Reference result must not expose a website URL'
        results.append({'module': module, 'milliseconds': round(elapsed*1000, 2)})
    start = time.perf_counter()
    matches = mcp.tool('topsolid_search_api_reference', {'query':'IPdm GetName','module':'kernel'})
    assert matches['total'] > 0
    assert all(item['localFile'].startswith('TopSolid.Automation/') for item in matches['items'])
    assert 'https://help.topsolid.com/' not in json.dumps(matches), 'Reference search must not expose a website URL'
    results.append({'searchMilliseconds': round((time.perf_counter()-start)*1000, 2)})
    (ROOT/'artifacts/local-reference-protocol.json').write_text(json.dumps(results, indent=2), encoding='utf-8')
    print(json.dumps(results))
finally:
    mcp.close()
