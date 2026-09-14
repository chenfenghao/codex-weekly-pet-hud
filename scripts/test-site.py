"""Check crawlable pages and project-subpath links before Pages deployment."""
import json
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import urlsplit, unquote
import xml.etree.ElementTree as ET

SITE = Path(__file__).resolve().parents[1] / 'site'
BASE = 'https://chenfenghao.github.io/codex-weekly-pet-hud/'
REPO = 'https://github.com/chenfenghao/codex-weekly-pet-hud'

class Page(HTMLParser):
    def __init__(self, path):
        super().__init__()
        self.tags = []
        self.schema = ''
        self.in_schema = False
        self.feed(path.read_text(encoding='utf-8'))

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        self.tags.append((tag, attrs))
        if tag == 'script': self.in_schema = attrs.get('type') == 'application/ld+json'

    def handle_endtag(self, tag):
        if tag == 'script': self.in_schema = False

    def handle_data(self, value):
        if self.in_schema: self.schema += value

for name, lang, canonical in [('index.html', 'zh-CN', BASE), ('en/index.html', 'en', BASE + 'en/')]:
    path = SITE / name
    page = Page(path)
    assert ('html', {'lang': lang}) in page.tags
    assert ('link', {'rel': 'canonical', 'href': canonical}) in page.tags
    assert ('meta', {'name': 'robots', 'content': 'index, follow'}) in page.tags
    for alternate, href in [('zh-CN', BASE), ('en', BASE+'en/'), ('x-default', BASE)]:
        assert ('link', {'rel': 'alternate', 'hreflang': alternate, 'href': href}) in page.tags
    schema = json.loads(page.schema)
    assert schema['url'] == canonical and schema['downloadUrl'] == REPO + '/releases/latest'
    assert schema['name'] == 'Codex Weekly Pet HUD'
    ids = {attrs['id'] for _, attrs in page.tags if 'id' in attrs}
    for tag, attrs in page.tags:
        for key in ['href', 'src']:
            if key not in attrs: continue
            value = attrs[key]
            if value.startswith('#'):
                assert value[1:] in ids, (name, value)
                continue
            parsed = urlsplit(value)
            if parsed.scheme:
                assert parsed.scheme == 'https', value
                if 'himomohi' in value:
                    assert value == 'https://github.com/himomohi/codex-pet-hud' and tag == 'a'
                continue
            assert not value.startswith('/'), (name, 'root-relative link breaks project prefix', value)
            target = (path.parent / unquote(parsed.path)).resolve()
            if target.is_dir(): target /= 'index.html'
            assert target.is_relative_to(SITE.resolve()) and target.is_file(), (name, value)
        if 'data-download' in attrs or 'data-checksum' in attrs:
            assert attrs['href'] == REPO + '/releases/latest'

urls = ET.parse(SITE/'sitemap.xml').findall('{*}url/{*}loc')
assert {loc.text for loc in urls} == {BASE, BASE+'en/'}
assert 'Sitemap: '+BASE+'sitemap.xml' in (SITE/'robots.txt').read_text(encoding='utf-8')
assert '/codex-pet-hud/' not in (SITE/'404.html').read_text(encoding='utf-8')
print('Both languages: metadata, schema, local links, fallback downloads and sitemap passed.')
