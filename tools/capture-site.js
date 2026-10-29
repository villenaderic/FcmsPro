const puppeteer = require(require('path').resolve(__dirname, '../.video-tools/node_modules/puppeteer-core'));
const http = require('http');
const fs = require('fs');
const path = require('path');

const mimeTypes = {
  '.html': 'text/html',
  '.css': 'text/css',
  '.js': 'text/javascript',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.svg': 'image/svg+xml',
  '.ico': 'image/x-icon',
  '.woff2': 'font/woff2'
};

const server = http.createServer((req, res) => {
  let reqPath = req.url.split('?')[0];
  if (reqPath.endsWith('/')) reqPath += 'index.html';
  const filePath = path.join(path.resolve('./_site'), reqPath);
  
  if (fs.existsSync(filePath) && fs.statSync(filePath).isFile()) {
    const ext = path.extname(filePath);
    res.writeHead(200, { 'Content-Type': mimeTypes[ext] || 'application/octet-stream' });
    fs.createReadStream(filePath).pipe(res);
  } else {
    res.writeHead(404);
    res.end('Not found');
  }
});

server.listen(3829, async () => {
  console.log('Local site server running at http://localhost:3829');

  const profileDir = path.resolve('./build-video/edge-profile');
  if (!fs.existsSync(profileDir)) fs.mkdirSync(profileDir, { recursive: true });

  const browser = await puppeteer.launch({
    executablePath: 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
    headless: true,
    userDataDir: profileDir,
    args: ['--no-sandbox', '--disable-setuid-sandbox']
  });

  const page = await browser.newPage();
  await page.setViewport({ width: 1920, height: 1080, deviceScaleFactor: 1 });

  // 1. Hero with Peso mockups
  console.log('Capturing local site hero...');
  await page.goto('http://localhost:3829/', { waitUntil: 'networkidle0' });
  await page.screenshot({ path: './build-video/web_01_hero.png' });

  // 2. Features with Peso mockups
  console.log('Capturing local site features...');
  await page.evaluate(() => {
    const el = document.querySelector('.features') || document.querySelector('#features') || document.querySelector('section:nth-of-type(2)');
    if (el) el.scrollIntoView({ behavior: 'instant', block: 'start' });
    else window.scrollBy(0, 850);
  });
  await new Promise(r => setTimeout(r, 600));
  await page.screenshot({ path: './build-video/web_02_features.png' });

  // 3. Download page
  console.log('Capturing local site download page...');
  await page.goto('http://localhost:3829/download/', { waitUntil: 'networkidle0' });
  await page.screenshot({ path: './build-video/web_03_download.png' });

  await browser.close();
  server.close();
  console.log('Captures with Philippine Peso updated successfully!');
});

