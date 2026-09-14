const { test } = require('node:test');
const assert = require('node:assert/strict');
const { selectRelease, enhance, endpoint } = require('../site/assets/site.js');
const base = 'https://github.com/chenfenghao/codex-weekly-pet-hud';
function fixture() {
  return { tag_name: 'v1.1.0', draft: false, prerelease: false, assets:
    ['Codex-Weekly-Pet-HUD-v1.1.0-Windows-x64.zip', 'SHA256SUMS.txt'].map(name =>
      ({ name, browser_download_url: `${base}/releases/download/v1.1.0/${name}` })) };
}
test('selects the matching stable Windows archive and checksums', () => {
  const result = selectRelease(fixture());
  assert.equal(result.download, fixture().assets[0].browser_download_url);
  assert.equal(result.checksum, fixture().assets[1].browser_download_url);
  assert.equal(result.release, `${base}/releases/tag/v1.1.0`);
  assert.equal(endpoint, 'https://api.github.com/repos/chenfenghao/codex-weekly-pet-hud/releases/latest');
});
test('rejects incomplete, draft, preview and mismatched-platform releases', () => {
  for (const data of [null, {}, { ...fixture(), draft: true }, { ...fixture(), prerelease: true },
    { ...fixture(), tag_name: 'v1.2.0' }, { ...fixture(), assets: fixture().assets.slice(0, 1) },
    { ...fixture(), assets: [{ name: 'macOS.zip' }] }]) assert.equal(selectRelease(data), null);
});
test('rejects upstream, lookalike domains, HTTP, URL credentials and wrong checksum URLs', () => {
  for (const url of [fixture().assets[0].browser_download_url.replace('chenfenghao/codex-weekly-pet-hud', 'himomohi/codex-pet-hud'),
    fixture().assets[0].browser_download_url.replace('github.com', 'github.com.example.org'),
    fixture().assets[0].browser_download_url.replace('https:', 'http:'),
    fixture().assets[0].browser_download_url.replace('github.com', 'user:password@github.com'),
    fixture().assets[0].browser_download_url + '?download=elsewhere', 'javascript:alert(1)']) {
    const data = fixture(); data.assets[0].browser_download_url = url;
    assert.equal(selectRelease(data), null);
  }
  const data = fixture(); data.assets[1].browser_download_url = 'https://example.org/SHA256SUMS.txt';
  assert.equal(selectRelease(data), null);
});
function documentFixture(lang) {
  const links = Object.fromEntries(['data-download', 'data-checksum', 'data-release-link'].map(key =>
    [`[${key}]`, [{ href: `${base}/releases/latest` }]]));
  const status = { textContent: '' };
  return { links, status, documentElement: { lang }, querySelector: () => status,
    querySelectorAll: selector => links[selector] };
}
test('network failures preserve static release links in both languages', async () => {
  for (const lang of ['zh-CN', 'en']) {
    const doc = documentFixture(lang);
    await enhance(doc, async () => { throw new Error('offline'); });
    for (const links of Object.values(doc.links)) assert.equal(links[0].href, `${base}/releases/latest`);
    assert.ok(doc.status.textContent.includes('GitHub Releases'));
  }
});
test('successful enhancement updates every download link without sending credentials', async () => {
  const doc = documentFixture('en');
  await enhance(doc, async (url, options) => {
    assert.equal(url, endpoint); assert.equal(options.credentials, 'omit');
    return { ok: true, json: async () => fixture() };
  });
  assert.equal(doc.links['[data-download]'][0].href, fixture().assets[0].browser_download_url);
  assert.match(doc.status.textContent, /v1\.1\.0.*Latest stable/);
});
