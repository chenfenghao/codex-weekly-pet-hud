# Website / 官网维护

- 中文：https://chenfenghao.github.io/codex-weekly-pet-hud/
- English: https://chenfenghao.github.io/codex-weekly-pet-hud/en/
- Sitemap: https://chenfenghao.github.io/codex-weekly-pet-hud/sitemap.xml

## Build and publish

Edit bilingual content in `scripts/build-site.py`, styles in `site/assets/site.css`, and release selection in `site/assets/site.js`. Regenerate the checked-in HTML before committing:

```sh
python scripts/build-site.py
python scripts/test-site.py
node --test scripts/test-site.cjs
python -m http.server 8765 --directory site
```

Open `http://localhost:8765/` and `/en/` for preview. No frontend dependencies or third-party fonts are needed. Text and links work without JavaScript. JavaScript optionally resolves the latest stable Windows x64 archive and checksum file from this repository's public GitHub Releases API. If the API is unavailable or the assets are incomplete, buttons keep pointing to the release page.

GitHub Settings → Pages → Source must be **GitHub Actions**. `.github/workflows/pages.yml` builds, checks and deploys `site/` when relevant files change on `main`, or via **Run workflow**. A successful workflow, followed by a live URL check, confirms publication. Website changes do not require a new desktop release.

## 搜索引擎验证与提交 / Search verification

1. 在 Google Search Console 新增**网址前缀**资源，使用上方实际可访问的中文根地址。选择 HTML 文件验证，将 Google 给出的文件原样放到 `site/`，文件名和内容均不修改。
2. 提交文件并等待 Pages 部署成功。确认 Google 指定的完整文件网址返回原始内容，然后在 Search Console 完成验证。验证后保留文件。
3. 提交上方 `sitemap.xml`，在网址检查中分别检查首页和 `/en/`，按需请求编入索引。
4. Bing Webmaster Tools 可导入已验证的 Search Console 站点，或单独验证；然后提交 sitemap 和页面 URL。

For Google, add a **URL-prefix property** for the live project root. Put the supplied HTML verification file unchanged in `site/`, deploy it, verify its public URL, and complete verification in Search Console. Keep the file after verification. Submit `sitemap.xml` and inspect the root and `/en/` URLs. Bing can import a verified Search Console property or use its own verification process.

The site includes canonical URLs, language alternates, descriptions and SoftwareApplication structured data. These support discovery; deployment or submission does **not** establish or guarantee search-engine indexing. Verification requires the owner's Search Console/Bing account and the actual verification file; this repository does not ship a placeholder or claim verification.

`site/robots.txt` is served under the project path. Crawlers apply the host-level `https://chenfenghao.github.io/robots.txt`, so this project file does not control other projects or replace host-level rules. Submit the sitemap directly in the verified webmaster property. Public content pages permit indexing; the custom 404 page intentionally uses `noindex`.

官方参考 / Official references: [Pages workflows](https://docs.github.com/en/pages/getting-started-with-github-pages/using-custom-workflows-with-github-pages), [Google verification](https://support.google.com/webmasters/answer/9008080), [Request indexing](https://developers.google.com/search/docs/crawling-indexing/ask-google-to-recrawl).
