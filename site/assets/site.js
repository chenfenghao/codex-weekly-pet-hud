/* Static HTML remains usable if the public GitHub API is unavailable. */
(function () {
  'use strict';
  const repo = 'chenfenghao/codex-weekly-pet-hud';
  const base = `https://github.com/${repo}`;
  const endpoint = `https://api.github.com/repos/${repo}/releases/latest`;

  function assetUrl(asset, tag) {
    if (!asset || typeof asset.name !== 'string') return null;
    try {
      const url = new URL(asset.browser_download_url);
      const expected = `/${repo}/releases/download/${tag}/${asset.name}`;
      return url.protocol === 'https:' && url.hostname === 'github.com' &&
        !url.port && !url.username && !url.password && !url.search && !url.hash &&
        decodeURIComponent(url.pathname) === expected ? url.href : null;
    } catch { return null; }
  }

  function selectRelease(data) {
    if (!data || data.draft !== false || data.prerelease !== false ||
        typeof data.tag_name !== 'string' || !/^v\d+\.\d+\.\d+$/.test(data.tag_name) ||
        !Array.isArray(data.assets)) return null;
    const name = `Codex-Weekly-Pet-HUD-${data.tag_name}-Windows-x64.zip`;
    const zip = data.assets.find(asset => asset && asset.name === name);
    const checksum = data.assets.find(asset => asset && asset.name === 'SHA256SUMS.txt');
    const download = assetUrl(zip, data.tag_name);
    const sums = assetUrl(checksum, data.tag_name);
    // Keep both download and checksums on the reviewable release page if incomplete.
    if (!download || !sums) return null;
    return { tag: data.tag_name, download, checksum: sums,
      release: `${base}/releases/tag/${data.tag_name}` };
  }

  async function enhance(doc, fetcher) {
    const english = doc.documentElement.lang === 'en';
    const status = doc.querySelector('[data-release-status]');
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), 8000);
    try {
      const response = await fetcher(endpoint, { signal: controller.signal,
        credentials: 'omit', headers: { Accept: 'application/vnd.github+json' } });
      if (!response.ok) throw new Error('Release request failed');
      const release = selectRelease(await response.json());
      if (!release) throw new Error('No complete stable Windows release');
      for (const link of doc.querySelectorAll('[data-download]')) link.href = release.download;
      for (const link of doc.querySelectorAll('[data-checksum]')) link.href = release.checksum;
      for (const link of doc.querySelectorAll('[data-release-link]')) link.href = release.release;
      if (status) status.textContent = english ? `${release.tag} · Latest stable release · Windows x64` : `${release.tag} · 最新稳定版 · Windows x64`;
    } catch {
      if (status) status.textContent = english ? 'Open GitHub Releases to download the latest build' : '前往 GitHub Releases 下载最新版本';
    } finally { clearTimeout(timer); }
  }

  if (typeof module !== 'undefined' && module.exports) module.exports = { selectRelease, enhance, endpoint };
  if (typeof document !== 'undefined' && typeof fetch !== 'undefined') enhance(document, fetch);
}());
