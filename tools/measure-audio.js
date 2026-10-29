const { execSync } = require('child_process');
const path = require('path');

const ffmpeg = path.join(__dirname, '../.video-tools/node_modules/@ffmpeg-installer/win32-x64/ffmpeg.exe');
const scenes = [
  'scene_01_hook_web', 'scene_02_web_features', 'scene_03_download_page', 'scene_04_install_walkthrough',
  'scene_05_dashboard', 'scene_06_commissions', 'scene_07_clients', 'scene_08_finances',
  'scene_09_analytics', 'scene_10_search', 'scene_11_reports', 'scene_12_themes',
  'scene_13_privacy', 'scene_14_outro'
];

let total = 0;
for (const s of scenes) {
  const p = path.join(__dirname, '../build-video/audio', `${s}.mp3`);
  let out = '';
  try {
    out = execSync(`"${ffmpeg}" -i "${p}" 2>&1`, { encoding: 'utf8' });
  } catch (err) {
    out = (err.stdout || '') + (err.stderr || '');
  }
  const m = out.match(/Duration: (\d{2}):(\d{2}):(\d{2}\.\d{2})/);
  if (m) {
    const sec = parseInt(m[1]) * 3600 + parseInt(m[2]) * 60 + parseFloat(m[3]);
    console.log(`${s}: ${sec.toFixed(3)}s`);
    total += sec;
  }
}
console.log(`--- Total: ${total.toFixed(3)}s ---`);
