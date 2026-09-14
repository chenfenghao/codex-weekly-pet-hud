"""Build crawlable Chinese and English project pages without a browser runtime."""
import html
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SITE = ROOT / 'site'
URL = 'https://chenfenghao.github.io/codex-weekly-pet-hud/'
REPO = 'https://github.com/chenfenghao/codex-weekly-pet-hud'

COPY = {
    'zh': {
        'lang': 'zh-CN', 'locale': 'zh_CN', 'title': 'Codex Weekly Pet HUD · 周额度节奏挂件',
        'description': 'Codex Pet 上方的 Windows 周额度监控挂件。查看剩余额度、使用节奏和每日建议，支持中英文界面、可调刷新与社区重置信号提醒。开源、免费、便携。',
        'nav': ['功能', '使用方法', '下载'], 'switch': 'English', 'eyebrow': 'WINDOWS · 开源桌面挂件',
        'heading': 'Codex 周额度，<br><span>一眼看清节奏。</span>',
        'intro': '剩下多少，应该用多快。把周额度、使用节奏和重置信号放在你的 Codex Pet 上方，专心做事，也心里有数。',
        'download': '下载 Windows x64', 'source': '查看源码 ↗', 'release': '从 GitHub 获取最新稳定版',
        'figure': '真实界面 · 示例数据', 'pet': '跟随你现有的 Codex Pet', 'caption': '剩余额度 / 匀速应剩 / 每日建议 / 重置雷达',
        'stats': [('190 × 72', '默认尺寸 · 可缩放'), ('1–60 min', '更新间隔 · 默认 5 分钟'), ('中文 / EN', '界面语言 · 随时切换')],
        'featureTitle': '需要的信息，刚好够用。',
        'features': [
            ('01', '看懂使用节奏', '比较本周时间进度与额度消耗，显示偏慢、稳健、偏快或超速，估算耗尽时间和后续每日额度。'),
            ('02', '重置信号到来时提醒', '从 codex-reset.com 读取公开社区信号。新强信号或重置公告高亮并可通知；历史公告静默，同一状态不重复提醒。'),
            ('03', '跟随宠物，也会让路', '拖动宠物或状态条一起移动。打开受支持的系统托盘面板时，状态条和拖拽层暂时隐藏，让蓝牙等按钮正常可点。'),
        ],
        'paceTitle': '「剩余」是现状，<br>「应剩」是参照。',
        'paceBody': '速率 R = 已用额度百分比 ÷ 本周已过时间百分比。例如时间已过 40%、额度已用 60%，R = 1.5，代表超速。',
        'paceLabels': ['偏慢', '稳健', '偏快', '超速'], 'paceNote': '按固定 7 天窗口与平均速率估算。到达重置时间后等待真实数据，不会擅自补满额度。',
        'setupTitle': '解压，打开，就位。',
        'steps': [('完整解压下载包', '运行 CodexWeeklyPetHud.exe，保留旁边的 DLL 文件；无需单独安装 .NET。'), ('在 Codex 中打开 Pet', '挂件自动跟随。单击状态条查看详情，进入设置调整间隔、大小和位置。'), ('选好语言与输入方式', '设置 → 语言 / Language 切换中英文。自动读取不可用时，可以粘贴重置时间和剩余百分比。')],
        'manual': '阅读完整中文说明 ↗', 'downloadTitle': '给 Pet 加一条状态栏。',
        'downloadBody': 'Windows 10/11 x64 · .NET 8 运行时已包含 · 便携版不注册开机启动',
        'checksum': 'SHA-256 校验文件 ↗', 'changes': '版本记录 ↗',
        'unsigned': '当前构建未签名，Windows 可能提示未知发布者。升级前先从托盘退出旧版，并核对同一 Release 的 SHA256SUMS.txt。',
        'privacyTitle': '数据边界清楚，状态也清楚。',
        'privacy': '账户额度通过本机 Codex 登录读取，不记录 token；社区雷达使用独立的公开接口，不发送 OpenAI 凭据。宠物隐藏后雷达仍检查，账户用量读取暂停。网络失败会显示延迟，不会伪装成没有信号。',
        'community': '社区公告不代表你的账户已重置。雷达不会修改额度，也不会兑换重置次数。',
        'faq': [('没看到状态条？', '先检查系统托盘，并在 Codex 中打开 Pet。系统面板展开时状态条会暂时隐藏。'), ('为什么显示连接延迟？', '检查网站访问与系统代理。公开雷达接口拒绝原生客户端时，可使用本机已安装的 Python 作为兼容方式；程序不自动安装 Python。'), ('支持 macOS 或 ARM64 吗？', '此分支目前只发布和验证 Windows x64，其他平台的上游代码保留作参考。')],
        'attribution': '基于', 'license': '改造，遵循 MIT 许可。', 'independent': '独立社区项目，非 OpenAI 官方产品。',
        'footerDocs': '使用说明', 'skip': '跳到正文',
    },
    'en': {
        'lang': 'en', 'locale': 'en_US', 'title': 'Codex Weekly Pet HUD — Windows quota monitor',
        'description': 'A compact Windows quota monitor above your Codex Pet. Track weekly quota, usage pace and daily budget, with Chinese/English UI, adjustable refresh and community reset alerts. Free, open source and portable.',
        'nav': ['Features', 'Get started', 'Download'], 'switch': '简体中文', 'eyebrow': 'WINDOWS · OPEN-SOURCE DESKTOP WIDGET',
        'heading': 'Your Codex quota,<br><span>at a glance.</span>',
        'intro': 'Know what is left and how fast to use it. Keep weekly quota, usage pace and reset signals above your existing Codex Pet, so you can focus on the work.',
        'download': 'Download Windows x64', 'source': 'View source ↗', 'release': 'Get the latest stable build on GitHub',
        'figure': 'Real interface · Sample data', 'pet': 'Follows your existing Codex Pet', 'caption': 'Quota left / Target remaining / Daily budget / Reset radar',
        'stats': [('190 × 72', 'Default size · Adjustable scale'), ('1–60 min', 'Refresh interval · 5 by default'), ('中文 / EN', 'Switch language anytime')],
        'featureTitle': 'The information you need. Just enough.',
        'features': [
            ('01', 'Understand your pace', 'Compare quota consumed with the week elapsed. See Slow, On track, Fast or Too fast, plus an exhaustion estimate and daily budget.'),
            ('02', 'Hear about reset signals', 'Reads public community signals from codex-reset.com. New strong signals and reset announcements can alert you. History syncs silently; repeated states do not notify again.'),
            ('03', 'Follows your Pet. Makes room.', 'Drag the Pet or status bar together. When supported system tray flyouts open, the HUD and drag layer hide temporarily so buttons such as Bluetooth remain clickable.'),
        ],
        'paceTitle': '“Left” is your quota.<br>“Target” is your reference.',
        'paceBody': 'Pace R = percentage of quota used ÷ percentage of the week elapsed. If 40% of the week has passed and 60% of quota is used, R = 1.5: Too fast.',
        'paceLabels': ['Slow', 'On track', 'Fast', 'Too fast'], 'paceNote': 'Estimates assume a fixed seven-day window and average usage pace. Reaching the reset time does not automatically refill your quota.',
        'setupTitle': 'Extract. Open. You are set.',
        'steps': [('Extract the entire archive', 'Run CodexWeeklyPetHud.exe and keep its DLL files alongside it. No separate .NET installation is needed.'), ('Open Pet in Codex', 'The HUD follows automatically. Click it for details; Settings adjusts the interval, size and placement.'), ('Choose your language and input', 'Use 设置 → 语言 / Language → English. If automatic quota reads fail, paste a reset time and remaining percentage instead.')],
        'manual': 'Read the full English guide ↗', 'downloadTitle': 'Give your Pet a status bar.',
        'downloadBody': 'Windows 10/11 x64 · .NET 8 runtime included · No automatic startup registration',
        'checksum': 'SHA-256 checksums ↗', 'changes': 'Release notes ↗',
        'unsigned': 'This build is unsigned; Windows may show an unknown-publisher prompt. Exit the old version before upgrading and verify against SHA256SUMS.txt from the same release.',
        'privacyTitle': 'Clear boundaries. Clear status.',
        'privacy': 'Quota uses your local Codex login without logging tokens. Radar uses a separate public client without OpenAI credentials. Radar keeps checking while the Pet is hidden; personal quota polling pauses. Failed reads show a delay, not “no signal.”',
        'community': 'A community announcement does not confirm a reset for your account. Radar never changes quota or redeems reset credits.',
        'faq': [('No status bar?', 'Check the tray icon and open Pet in Codex. The HUD temporarily hides while supported system flyouts are open.'), ('Why does radar show Delayed?', 'Check site access and your system proxy. If public endpoints reject the native client, an installed Python interpreter can provide a compatibility fallback. The app does not install Python.'), ('macOS or ARM64?', 'This fork currently publishes and verifies Windows x64 only. Other platform code is retained from upstream for reference.')],
        'attribution': 'Based on', 'license': 'under the MIT License.', 'independent': 'Independent community project. Not an official OpenAI product.',
        'footerDocs': 'Documentation', 'skip': 'Skip to content',
    },
}

def esc(text): return html.escape(text, quote=True)

def render(language):
    c = COPY[language]
    english = language == 'en'
    prefix = '../' if english else ''
    canonical = URL + ('en/' if english else '')
    alternate = '../' if english else 'en/'
    readme = REPO + '/blob/main/README' + ('.en' if english else '') + '.md'
    image = 'capsule.en.png' if english else 'capsule.png'
    schema = json.dumps({'@context':'https://schema.org','@type':'SoftwareApplication','name':'Codex Weekly Pet HUD','url':canonical,'description':c['description'],'operatingSystem':'Windows 10, Windows 11 (x64)','applicationCategory':'DeveloperApplication','inLanguage':['zh-CN','en'],'license':REPO+'/blob/main/LICENSE','downloadUrl':REPO+'/releases/latest','offers':{'@type':'Offer','price':'0','priceCurrency':'USD'}},ensure_ascii=False)
    cards = ''.join(f'<article><span class="index">{n}</span><h3>{esc(title)}</h3><p>{esc(body)}</p></article>' for n,title,body in c['features'])
    stats = ''.join(f'<div><strong>{esc(number)}</strong><span>{esc(label)}</span></div>' for number,label in c['stats'])
    bands = ''.join(f'<div><i></i><strong>{esc(label)}</strong><span>{rule}</span></div>' for label,rule in zip(c['paceLabels'],['R &lt; 0.85','0.85 ≤ R ≤ 1','1 &lt; R ≤ 1.3','R &gt; 1.3']))
    steps = ''.join(f'<li><span>0{i}</span><div><h3>{esc(title)}</h3><p>{esc(body)}</p></div></li>' for i,(title,body) in enumerate(c['steps'],1))
    questions = ''.join(f'<details><summary>{esc(question)}</summary><p>{esc(answer)}</p></details>' for question,answer in c['faq'])
    return f'''<!doctype html>
<html lang="{c['lang']}">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta name="theme-color" content="#101612">
  <title>{esc(c['title'])}</title>
  <meta name="description" content="{esc(c['description'])}">
  <meta name="robots" content="index, follow">
  <link rel="canonical" href="{canonical}">
  <link rel="alternate" hreflang="zh-CN" href="{URL}">
  <link rel="alternate" hreflang="en" href="{URL}en/">
  <link rel="alternate" hreflang="x-default" href="{URL}">
  <meta property="og:type" content="website">
  <meta property="og:site_name" content="Codex Weekly Pet HUD">
  <meta property="og:title" content="{esc(c['title'])}">
  <meta property="og:description" content="{esc(c['description'])}">
  <meta property="og:url" content="{canonical}">
  <meta property="og:locale" content="{c['locale']}">
  <meta property="og:image" content="{URL}assets/images/{image}">
  <meta property="og:image:alt" content="{esc(c['caption'])}">
  <meta name="twitter:card" content="summary">
  <link rel="icon" type="image/svg+xml" href="{prefix}assets/icon.svg">
  <link rel="stylesheet" href="{prefix}assets/site.css">
  <script type="application/ld+json">{schema}</script>
  <script src="{prefix}assets/site.js" defer></script>
</head>
<body>
  <a class="skip" href="#main">{esc(c['skip'])}</a>
  <header class="header wrap">
    <a class="brand" href="{prefix or './'}"><img src="{prefix}assets/icon.svg" width="28" height="28" alt="">Codex Weekly Pet HUD</a>
    <nav aria-label="{'Main navigation' if english else '主导航'}"><a href="#features">{c['nav'][0]}</a><a href="#setup">{c['nav'][1]}</a><a href="#download">{c['nav'][2]}</a></nav>
    <a class="language" href="{alternate}" lang="{'zh-CN' if english else 'en'}" hreflang="{'zh-CN' if english else 'en'}">{c['switch']} ↗</a>
  </header>
  <main id="main">
    <section class="hero wrap">
      <div class="hero-copy"><p class="eyebrow"><span></span>{c['eyebrow']}</p><h1>{c['heading']}</h1><p class="intro">{esc(c['intro'])}</p>
        <div class="actions"><a class="button primary" data-download href="{REPO}/releases/latest">{c['download']} <span aria-hidden="true">↓</span></a><a class="text-link" href="{REPO}">{c['source']}</a></div>
        <p class="release-status" data-release-status aria-live="polite">{c['release']}</p>
      </div>
      <figure class="preview"><figcaption><span class="preview-dot"></span>{c['figure']}</figcaption><div class="preview-inner"><img src="{prefix}assets/images/{image}" width="380" height="144" alt="{esc(c['caption'])}"><div class="tether"></div><div class="pet-label">{esc(c['pet'])}</div></div><p class="preview-caption">{esc(c['caption'])}</p></figure>
    </section>
    <div class="stats wrap">{stats}</div>
    <section class="section wrap" id="features"><p class="eyebrow">01 / FEATURES</p><h2>{esc(c['featureTitle'])}</h2><div class="features">{cards}</div></section>
    <section class="pace wrap"><div><p class="eyebrow">02 / YOUR PACE</p><h2>{c['paceTitle']}</h2><p>{esc(c['paceBody'])}</p><p class="small">{esc(c['paceNote'])}</p></div><div class="pace-bands">{bands}</div></section>
    <section class="section wrap setup" id="setup"><div><p class="eyebrow">03 / GET STARTED</p><h2>{esc(c['setupTitle'])}</h2><a class="text-link" href="{readme}">{c['manual']}</a></div><ol>{steps}</ol></section>
    <section class="download wrap" id="download"><p class="eyebrow">WINDOWS x64 · MIT</p><h2>{esc(c['downloadTitle'])}</h2><p>{esc(c['downloadBody'])}</p><div class="actions"><a class="button primary" data-download href="{REPO}/releases/latest">{c['download']} <span aria-hidden="true">↓</span></a><a class="text-link" data-checksum href="{REPO}/releases/latest">{c['checksum']}</a><a class="text-link" data-release-link href="{REPO}/releases/latest">{c['changes']}</a></div><p class="small unsigned">{esc(c['unsigned'])}</p></section>
    <section class="section wrap privacy"><div><p class="eyebrow">04 / CLEAR BOUNDARIES</p><h2>{esc(c['privacyTitle'])}</h2><p>{esc(c['privacy'])}</p><p class="notice">{esc(c['community'])}</p></div><div class="faq">{questions}</div></section>
  </main>
  <footer class="wrap"><div><strong>Codex Weekly Pet HUD</strong><p>{c['attribution']} <a href="https://github.com/himomohi/codex-pet-hud">himomohi/codex-pet-hud</a> {c['license']}</p><p>{c['independent']}</p></div><div class="footer-links"><a href="{REPO}">GitHub ↗</a><a href="{readme}">{c['footerDocs']} ↗</a><a href="{REPO}/blob/main/LICENSE">MIT License ↗</a></div></footer>
</body>
</html>
'''

if __name__ == '__main__':
    (SITE / 'en').mkdir(parents=True, exist_ok=True)
    (SITE / 'index.html').write_text(render('zh'), encoding='utf-8')
    (SITE / 'en/index.html').write_text(render('en'), encoding='utf-8')
    print('Generated Chinese and English HTML pages.')
