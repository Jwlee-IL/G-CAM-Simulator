"""Hidden, offline Edge jobs and a deliberately limited reader for their PDF output.

Reader accepts conventional, unencrypted Skia PDFs with direct-length Flate streams,
Type 0/3 ToUnicode fonts, direct/indirect resources and page-content references. Other
encodings fail explicitly; this is not a general-purpose PDF parser.
"""
import base64
import html
import json
import os
from pathlib import Path
import re
import struct
import subprocess
import tempfile
import uuid
import zlib

from software_model import require, sha, VENDOR_SHA


def privacy(text, account=None):
    """Refuse machine paths and the process account; never record that account."""
    require(not re.search(r'(?:(?<![\w])[A-Za-z]:[\\/]|file:/|\\\\[^\s\\]+\\|/(?:Users|home)/|~[\\/])', text, re.I),
            'privacy: local path')
    name = account if account is not None else os.environ.get('USERNAME', os.environ.get('USER', ''))
    if name and name.lower() not in {'user', 'runner', 'root', 'admin', 'administrator'}:
        require(not re.search(r'(?<![\w])' + re.escape(name) + r'(?![\w])', text, re.I), 'privacy: account name')


def edge_path():
    # Reading executable metadata only; no registry or profile discovery.
    for root in (os.environ.get('ProgramFiles(x86)', ''), os.environ.get('ProgramFiles', '')):
        candidate = Path(root) / 'Microsoft/Edge/Application/msedge.exe'
        if root and candidate.is_file():
            return candidate
    raise ValueError('edge: executable not found in the Edge program folders')


class Edge:
    def __init__(self, scratch=None, executable=None):
        scratch = scratch or os.environ.get('GCAM_EDGE_SCRATCH')
        executable = executable or os.environ.get('GCAM_EDGE_EXECUTABLE')
        self.scratch = Path(scratch) if scratch else Path(tempfile.gettempdir()) / 'gcam-todo53' / ('browser-' + uuid.uuid4().hex)
        allowed = Path(tempfile.gettempdir()).resolve()
        require(self.scratch.resolve().is_relative_to(allowed) and
                self.scratch.resolve().relative_to(allowed).parts[0].startswith('gcam-'), 'edge: scratch must be TEMP/gcam-*')
        self.scratch.mkdir(parents=True, exist_ok=True)
        self.exe = Path(executable) if executable is not None else edge_path()
        self.profile = self.scratch / 'profile'
        self.jobs = 0

    def run(self, args, timeout=120):
        self.jobs += 1
        command = [str(self.exe), '--headless=new', '--disable-gpu', '--no-first-run', '--no-default-browser-check',
                   '--disable-background-networking', '--disable-component-update', '--disable-sync', '--disable-extensions',
                   '--disable-breakpad', '--no-pdf-header-footer', '--allow-file-access-from-files',
                   '--user-data-dir=' + str(self.profile), '--crash-dumps-dir=' + str(self.scratch / 'crashes')] + args
        # A timeout does not kill a process: never terminate a shared or owned process implicitly.
        stdout = self.scratch / f'job-{self.jobs}.stdout'
        stderr = self.scratch / f'job-{self.jobs}.stderr'
        with stdout.open('wb') as so, stderr.open('wb') as se:
            p = subprocess.Popen(command, stdout=so, stderr=se, cwd=self.scratch,
                                 creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
            try:
                result = p.wait(timeout=timeout)
            except subprocess.TimeoutExpired as error:
                raise ValueError('edge: timeout; process left running, no termination attempted') from error
        require(result == 0, 'edge: job failed; see scratch job stderr')
        return stdout.read_text(encoding='utf-8', errors='replace')

    def version(self):
        # Query the browser we actually execute, without reading Program Files metadata or the registry.
        page = self.scratch / 'version.html'
        page.write_text("""<!doctype html><meta charset="utf-8"><script>
        navigator.userAgentData.getHighEntropyValues(['fullVersionList']).then(data => {
          const edge = data.fullVersionList.find(item => item.brand === 'Microsoft Edge');
          if (edge) document.documentElement.setAttribute('data-edge-version', edge.version);
        });</script>""", encoding='utf-8')
        output = self.run(['--virtual-time-budget=1000', '--dump-dom', page.resolve().as_uri()])
        versions = re.findall(r'data-edge-version="(\d+\.\d+\.\d+\.\d+)"', output)
        require(len(versions) == 1, 'edge: absent/ambiguous runtime version')
        return versions[0]

    def print_pdf(self, page, destination):
        self.run(['--print-to-pdf=' + str(Path(destination).resolve()), Path(page).resolve().as_uri()])
        require(Path(destination).is_file(), 'edge: missing printed PDF')

    def screenshot(self, page, destination, width=1100, height=1556):
        self.run(['--hide-scrollbars', f'--window-size={width},{height}', '--force-device-scale-factor=1',
                  '--screenshot=' + str(Path(destination).resolve()), Path(page).resolve().as_uri()])
        require(Path(destination).read_bytes().startswith(b'\x89PNG\r\n\x1a\n'), 'edge: missing screenshot')

    def diagrams(self, texts, vendor):
        require(sha(Path(vendor).read_bytes()) == VENDOR_SHA, 'diagram: vendored Mermaid hash mismatch')
        # Local script only, and no Mermaid links/click actions. Mermaid's HTML labels preserve explicit
        # <br/> and leading plus signs; the text-only renderer drops the latter as a Markdown list.
        for text in texts.values():
            require(not re.search(r'(?im)^\s*(?:click\b|%%\{|https?://)', text), 'diagram: interactive/network directive')
        payload = json.dumps(texts).replace('<', '\\u003c')
        script = '''
mermaid.initialize({startOnLoad:false,securityLevel:'strict',theme:'neutral',htmlLabels:true,
  themeVariables:{fontSize:'18px'},fontFamily:'Arial',
  flowchart:{htmlLabels:true,useMaxWidth:false,nodeSpacing:20,rankSpacing:20,padding:10},
  sequence:{useMaxWidth:false,wrap:true,actorMargin:20,diagramMarginX:10,diagramMarginY:10,
    width:220,height:45,messageMargin:15,wrapPadding:5,messageFontSize:18,actorFontSize:18,noteFontSize:18,
    messageFontFamily:'Arial',actorFontFamily:'Arial',noteFontFamily:'Arial'},
  state:{useMaxWidth:false}});
(async()=>{try{
 const result={};
 for(const [key,text] of Object.entries(INPUT)){
  const {svg}=await mermaid.render(key.replaceAll('-',''),text);
  const doc=new DOMParser().parseFromString(svg,'text/html');
  const root=doc.querySelector('svg');
  if(!root||root.querySelector('script'))throw Error('unsupported SVG');
  const box=root.getAttribute('viewBox').split(/[ ,]+/).map(Number);
  root.setAttribute('width',box[2]);root.setAttribute('height',box[3]);
  const clean=new XMLSerializer().serializeToString(root);
  const visible=Array.from(root.querySelectorAll('text,foreignObject')).map(n=>n.textContent).join('').replace(/\s+/g,'');
  for(const match of text.matchAll(/\["([^"]+)"\]/g)){
    const expected=match[1].replace(/<br\s*\/?\s*>/gi,'').replace(/\s+/g,'');
    if(!visible.includes(expected))throw Error('missing diagram label: '+match[1]);
  }
  result[key]={svg:clean,width:box[2],height:box[3]};
 }
 document.getElementById('result').textContent=JSON.stringify({ok:true,result});
}catch(error){document.getElementById('result').textContent=JSON.stringify({ok:false,error:String(error)});}})();
'''.replace('INPUT', payload)
        page = self.scratch / 'diagrams.html'
        page.write_text('<!doctype html><meta charset="utf-8"><title>GCAM diagram batch</title>'
                        '<meta http-equiv="Content-Security-Policy" content="default-src \'none\'; '
                        'script-src file: \'unsafe-inline\'; style-src \'unsafe-inline\'; img-src data:; connect-src \'none\'">'
                        '<pre id="result"></pre><script src="' + html.escape(Path(vendor).resolve().as_uri(), quote=True) +
                        '"></script><script>' + script + '</script>', encoding='utf-8')
        dom = self.run(['--virtual-time-budget=15000', '--dump-dom', page.as_uri()])
        m = re.search(r'<pre id="result">(.*?)</pre>', dom, re.S)
        require(m is not None and m[1], 'diagram: no result returned')
        result = json.loads(html.unescape(m[1]))
        require(result.get('ok'), 'diagram: render failed: ' + result.get('error', 'unknown'))
        require(set(result['result']) == set(texts), 'diagram: missing result')
        for key, v in result['result'].items():
            require(not re.search(r'<script\b|(?:href|src)="(?:https?:|file:)', v['svg'], re.I), 'diagram: external content')
            scale = min(2, 1800 / v['width'], 1800 / v['height'])
            width, height = max(500, round(v['width'] * scale)), max(300, round(v['height'] * scale))
            page = self.scratch / (key + '.html'); png = self.scratch / (key + '.png')
            page.write_text('<!doctype html><meta charset="utf-8"><title>GCAM diagram</title>'
                            '<meta http-equiv="Content-Security-Policy" content="default-src \'none\'; style-src \'unsafe-inline\'; '
                            'img-src data:; connect-src \'none\'">'
                            '<style>html,body{margin:0;background:white;overflow:hidden}svg{display:block;width:' +
                            str(width) + 'px;height:' + str(height) + 'px}</style>' + v['svg'], encoding='utf-8')
            self.screenshot(page, png, width, height)
            v['png'] = png.read_bytes()
            require(v['png'].startswith(b'\x89PNG'), 'diagram: invalid PNG')
            privacy(v['svg'])
        return result['result']


def pdf_literal(value):
    """Decode a literal-string payload (without its surrounding parentheses)."""
    def escape(match):
        s = match[1]
        if s[:1].isdigit():
            return bytes([int(s, 8)])
        return {b'n': b'\n', b'r': b'\r', b't': b'\t', b'b': b'\b', b'f': b'\f', b'\n': b'', b'\r\n': b''}.get(s, s)
    return re.sub(rb'\\([0-7]{1,3}|\r\n|[\s\S])', escape, value)


class EdgePdf:
    def __init__(self, data):
        require(data.startswith(b'%PDF-') and data.rstrip().endswith(b'%%EOF'), 'pdf: header or EOF missing')
        require(b'/Encrypt' not in data and b'/ObjStm' not in data, 'pdf: unsupported encrypted/object stream')
        self.objects = {}
        # Edge 155 writes an ordinary xref table, providing exact object bounds instead of searching stream bytes.
        m = re.search(rb'startxref\s+(\d+)\s+%%EOF\s*$', data)
        require(m is not None, 'pdf: missing startxref')
        xref = int(m[1]); require(data[xref:xref + 4] == b'xref', 'pdf: unsupported xref format')
        lines = data[xref:].splitlines(); i = 1
        while i < len(lines) and not lines[i].startswith(b'trailer'):
            require(re.fullmatch(rb'\d+ \d+', lines[i]) is not None, 'pdf: malformed xref subsection')
            first, count = map(int, lines[i].split()); i += 1
            for n in range(first, first + count):
                row = lines[i].split(); i += 1
                require(len(row) == 3, 'pdf: malformed xref entry')
                if row[2] == b'f':
                    continue
                require(row[2] == b'n' and row[1] == b'00000', 'pdf: unsupported generation')
                offset = int(row[0]); prefix = f'{n} 0 obj'.encode()
                require(data[offset:offset + len(prefix)] == prefix, 'pdf: bad xref offset')
                start = offset + len(prefix); end = data.find(b'endobj', start)
                require(end >= start, 'pdf: unterminated object')
                body = data[start:end].strip(); stream = b''
                marker = re.search(rb'stream\r?\n', body)
                if marker:
                    head = body[:marker.start()]
                    length = re.search(rb'/Length (\d+)\s*(?=[/>])', head)
                    require(length is not None, 'pdf: indirect/absent stream length')
                    stream = data[start + len(data[start:end]) - len(data[start:end].lstrip()) + marker.end():]
                    stream = stream[:int(length[1])]
                    filters = re.findall(rb'/Filter\s*/(\w+)', head)
                    require(filters in ([], [b'FlateDecode'], [b'DCTDecode']), 'pdf: unsupported filter')
                    require(b'/Filter' not in head or filters, 'pdf: unsupported filter form')
                    if filters == [b'DCTDecode']:
                        from ui_annex import jpeg_size
                        require(re.search(rb'/Subtype\s*/Image\b', head), 'pdf: DCT outside image')
                        width, height = jpeg_size(stream)
                        require(re.search(rb'/Width\s+' + str(width).encode() + rb'\b', head) and re.search(rb'/Height\s+' + str(height).encode() + rb'\b', head), 'pdf: JPEG canvas differs')
                    if filters == [b'FlateDecode']:
                        try:
                            stream = zlib.decompress(stream)
                        except zlib.error as error:
                            raise ValueError('pdf: corrupt Flate stream') from error
                else:
                    head = body
                require(not re.search(rb'/S\s*/(?:URI|Launch|GoToR|JavaScript)\b', head), 'pdf: outgoing action')
                self.objects[n] = (head, stream)
        require(any(re.search(rb'/Type\s*/Catalog\b', h) for h, _ in self.objects.values()), 'pdf: no catalog')
        root = re.search(rb'/Root (\d+) 0 R', data[xref:])
        require(root is not None and int(root[1]) in self.objects, 'pdf: missing root reference')
        catalog = self.objects[int(root[1])][0]
        tree = re.search(rb'/Pages (\d+) 0 R', catalog)
        require(tree is not None, 'pdf: missing page tree')
        visited = set()
        def walk(ref, parent=None):
            require(ref not in visited and ref in self.objects, 'pdf: cyclic/missing page reference')
            visited.add(ref); head = self.objects[ref][0]
            if parent is not None:
                owner = re.search(rb'/Parent (\d+) 0 R', head)
                require(owner is not None and int(owner[1]) == parent, 'pdf: page parent differs')
            if re.search(rb'/Type\s*/Page\b', head):
                require(b'/MediaBox' in head, 'pdf: missing page bounds')
                return [(ref, head)]
            require(re.search(rb'/Type\s*/Pages\b', head) is not None, 'pdf: invalid page-tree node')
            kids = re.search(rb'/Kids\s*\[([^]]*)\]', head); count = re.search(rb'/Count (\d+)', head)
            require(kids is not None and count is not None, 'pdf: malformed page tree')
            pages = [p for kid in re.findall(rb'(\d+) 0 R', kids[1]) for p in walk(int(kid), ref)]
            require(len(pages) == int(count[1]), 'pdf: page-tree count differs')
            return pages
        self.pages = walk(int(tree[1]))
        require(self.pages and {n for n, _ in self.pages} ==
                {n for n, (h, _) in self.objects.items() if re.search(rb'/Type\s*/Page\b', h)}, 'pdf: unreachable/no pages')
        self.maps = self.font_maps()

    def font_maps(self):
        maps = {}
        self.code_bytes = {}
        for n, (head, _) in self.objects.items():
            ref = re.search(rb'/ToUnicode (\d+) 0 R', head)
            if not ref:
                continue
            cmap = self.objects[int(ref[1])][1]; mapping = {}
            space = re.search(rb'begincodespacerange\s*<([0-9a-fA-F]+)>', cmap)
            # Synthetic fixtures may omit the optional code-space declaration; Type 0 defaults to two bytes.
            width = len(space[1]) // 2 if space else (1 if b'/Type3' in head else 2)
            require(width in (1, 2), 'pdf: unsupported CMap code width')
            self.code_bytes[n] = width
            for block in re.findall(rb'beginbfchar(.*?)endbfchar', cmap, re.S):
                for a, b in re.findall(rb'<([0-9a-fA-F]+)>\s*<([0-9a-fA-F]+)>', block):
                    mapping[int(a, 16)] = bytes.fromhex(b.decode()).decode('utf-16-be')
            for block in re.findall(rb'beginbfrange(.*?)endbfrange', cmap, re.S):
                pattern = rb'<([0-9a-fA-F]+)>\s*<([0-9a-fA-F]+)>\s*(<([0-9a-fA-F]+)>|\[(.*?)\])'
                for a, b, _, base, array in re.findall(pattern, block, re.S):
                    first, last = int(a, 16), int(b, 16)
                    require(last - first < 65536, 'pdf: oversized CMap range')
                    if base:
                        for k in range(first, last + 1):
                            mapping[k] = (int(base, 16) + k - first).to_bytes(len(base) // 2, 'big').decode('utf-16-be')
                    else:
                        values = re.findall(rb'<([0-9a-fA-F]+)>', array)
                        require(len(values) == last - first + 1, 'pdf: CMap range length')
                        mapping.update({first + j: bytes.fromhex(s.decode()).decode('utf-16-be') for j, s in enumerate(values)})
            maps[n] = mapping
        return maps

    def page_text(self, head):
        resource = re.search(rb'/Resources (\d+) 0 R', head)
        resources = self.objects[int(resource[1])][0] if resource else head
        fonts = {k.decode(): int(v) for k, v in re.findall(rb'/(F\d+) (\d+) 0 R', resources)}
        contents = re.search(rb'/Contents\s*(?:(\d+) 0 R|\[([^]]*)\])', head)
        require(contents is not None, 'pdf: page has no contents')
        refs = [int(contents[1])] if contents[1] else [int(n) for n in re.findall(rb'(\d+) 0 R', contents[2])]
        data = b'\n'.join(self.objects[n][1] for n in refs); font, result = '', []
        token = (rb'/ActualText\s*<(?P<actual>[0-9a-fA-F]+)>\s*>>\s*BDC|(?P<end>\bEMC\b)|'
                 rb'/(?P<font>F\d+)\s+[\d.]+\s+Tf|<(?P<hex>[0-9a-fA-F]+)>|\((?P<lit>(?:\\[\s\S]|[^()\\])*)\)')
        actual = False
        for m in re.finditer(token, data):
            if m['actual']:
                require(not actual, 'pdf: nested ActualText unsupported')
                raw = bytes.fromhex(m['actual'].decode())
                require(raw.startswith(b'\xfe\xff'), 'pdf: ActualText must be UTF-16BE')
                result.append(raw[2:].decode('utf-16-be')); actual = True; continue
            if m['end']:
                actual = False; continue
            if m['font']:
                font = m['font'].decode(); continue
            if actual:
                continue
            raw = bytes.fromhex(m['hex'].decode()) if m['hex'] else pdf_literal(m['lit'])
            font_id = fonts.get(font)
            require(font_id in self.maps, 'pdf: text without mapped font')
            mapping, width = self.maps[font_id], self.code_bytes[font_id]
            require(len(raw) % width == 0, 'pdf: incomplete text code')
            for i in range(0, len(raw), width):
                code = int.from_bytes(raw[i:i + width], 'big')
                require(code in mapping, 'pdf: unmapped text code')
                result.append(mapping[code])
        require(not actual, 'pdf: unterminated ActualText')
        return ''.join(result)

    def texts(self):
        return [self.page_text(head) for _, head in self.pages]

    def check_evidence(self, doc):
        """Exact JPEG streams and every annex caption, including repeated placements of a deduplicated image."""
        from collections import Counter
        from software_model import plain
        frames = [b for b in doc if b['kind'] == 'image']
        if not any(b['source'].endswith('/frames/manifest.json') for b in doc):
            return
        compact = lambda value: re.sub(r'\s+', '', value)
        text = compact(' '.join(self.texts()))
        for block in doc:
            if block['source'].endswith('/frames/manifest.json'):
                caption = block['caption'] if block['kind'] == 'image' else plain(block['runs']) if 'runs' in block else ''
                require(compact(caption) in text, 'pdf: evidence caption/case missing')
        placements = Counter()
        for _, head in self.pages:
            resource = re.search(rb'/Resources (\d+) 0 R', head)
            resources = self.objects[int(resource[1])][0] if resource else head
            references = {name: int(ref) for name, ref in re.findall(rb'/(\w+) (\d+) 0 R', resources)}
            contents = re.search(rb'/Contents\s*(?:(\d+) 0 R|\[([^]]*)\])', head)
            refs = [int(contents[1])] if contents[1] else [int(n) for n in re.findall(rb'(\d+) 0 R', contents[2])]
            content = b'\n'.join(self.objects[n][1] for n in refs)
            for name in re.findall(rb'/(\w+)\s+Do\b', content):
                reference = references.get(name)
                if reference is None:
                    continue
                image_head, data = self.objects[reference]
                if re.search(rb'/Subtype\s*/Image\b', image_head) and b'/DCTDecode' in image_head:
                    placements[sha(data)] += 1
        expected = Counter(b['sha256'] for b in frames)
        require(all(placements[digest] == count for digest, count in expected.items()), 'pdf: evidence JPEG hash/count differs')

    def check(self, banner, document_id):
        texts = self.texts()
        flatten = lambda s: re.sub(r'\s+', '', s)
        require(flatten(banner) in flatten(texts[0]) and document_id in texts[0], 'pdf: missing cover banner/ID')
        privacy('\n'.join(texts))
        for head, _ in self.objects.values():
            privacy(head.decode('latin1'))
        return len(self.pages)
