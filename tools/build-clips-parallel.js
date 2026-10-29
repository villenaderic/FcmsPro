const { exec, execSync } = require('child_process');
const path = require('path');
const fs = require('fs');

const rootDir = path.resolve(__dirname, '..');
const buildDir = path.join(rootDir, 'build-video');
const audioDir = path.join(buildDir, 'audio');
const framesDir = path.join(buildDir, 'frames');
const clipsDir = path.join(buildDir, 'clips');
const ffmpeg = path.join(rootDir, '.video-tools/node_modules/@ffmpeg-installer/win32-x64/ffmpeg.exe');
const whoosh = path.join(buildDir, 'sfx_whoosh.wav');
const bgMusic = path.join(buildDir, 'bg_music.wav');
const tempMerged = path.join(buildDir, 'merged_raw.mp4');
const outputVideo = path.join(rootDir, 'FCMS_Pro_Project_Demo.mp4');

if (!fs.existsSync(clipsDir)) {
  fs.mkdirSync(clipsDir, { recursive: true });
}

const scenes = [
  "scene_01_hook_web",
  "scene_02_web_features",
  "scene_03_download_page",
  "scene_04_install_walkthrough",
  "scene_05_dashboard",
  "scene_06_commissions",
  "scene_07_clients",
  "scene_08_finances",
  "scene_09_analytics",
  "scene_10_search",
  "scene_11_reports",
  "scene_12_themes",
  "scene_13_privacy",
  "scene_14_outro"
];

function execPromise(cmd) {
  return new Promise((resolve, reject) => {
    exec(cmd, { maxBuffer: 10 * 1024 * 1024 }, (err, stdout, stderr) => {
      if (err) reject(err);
      else resolve({ stdout, stderr });
    });
  });
}

async function encodeScene(sceneId, index) {
  const framePath = path.join(framesDir, `${sceneId}.png`);
  const audioPath = path.join(audioDir, `${sceneId}.mp3`);
  const clipPath = path.join(clipsDir, `${sceneId}.mp4`);

  const frameStat = fs.statSync(framePath);
  if (fs.existsSync(clipPath)) {
    const clipStat = fs.statSync(clipPath);
    // If clip was modified after the frame and is > 100KB, it's fresh
    if (clipStat.mtimeMs > frameStat.mtimeMs && clipStat.size > 100000) {
      console.log(`[${index + 1}/${scenes.length}] Clip ${sceneId}.mp4 is already up-to-date, skipping.`);
      return clipPath;
    }
  }

  console.log(`[${index + 1}/${scenes.length}] Encoding ${sceneId}.mp4...`);
  let cmd = '';
  if (index > 0 && fs.existsSync(whoosh)) {
    cmd = `"${ffmpeg}" -y -loop 1 -framerate 24 -i "${framePath}" -i "${audioPath}" -i "${whoosh}" -vf "fade=t=in:st=0:d=0.3" -filter_complex "[1:a]volume=1.0[voice];[2:a]volume=0.30[sfx];[voice][sfx]amix=inputs=2:duration=first:dropout_transition=0[aout]" -map 0:v -map "[aout]" -c:v libx264 -preset ultrafast -tune stillimage -c:a aac -b:a 192k -pix_fmt yuv420p -shortest "${clipPath}"`;
  } else {
    cmd = `"${ffmpeg}" -y -loop 1 -framerate 24 -i "${framePath}" -i "${audioPath}" -vf "fade=t=in:st=0:d=0.3" -c:v libx264 -preset ultrafast -tune stillimage -c:a aac -b:a 192k -pix_fmt yuv420p -shortest "${clipPath}"`;
  }

  await execPromise(cmd);
  const size = fs.statSync(clipPath).size;
  console.log(`[${index + 1}/${scenes.length}] Completed ${sceneId}.mp4 (${(size / 1024 / 1024).toFixed(2)} MB)`);
  return clipPath;
}

// Pool executor with concurrency = 3
async function runPool(items, concurrency, fn) {
  const results = new Array(items.length);
  let currentIndex = 0;

  async function worker() {
    while (currentIndex < items.length) {
      const idx = currentIndex++;
      results[idx] = await fn(items[idx], idx);
    }
  }

  const workers = [];
  for (let i = 0; i < concurrency; i++) {
    workers.push(worker());
  }
  await Promise.all(workers);
  return results;
}

async function main() {
  console.log(`=== Parallel Encoding 14 Video Clips (Concurrency = 3) ===`);
  const startTime = Date.now();
  const clipPaths = await runPool(scenes, 3, encodeScene);

  console.log(`\n=== Concatenating 14 Clips into merged_raw.mp4 ===`);
  const concatTxt = path.join(buildDir, 'concat.txt');
  const lines = clipPaths.map(p => `file '${p.replace(/\\/g, '/')}'`).join('\n');
  fs.writeFileSync(concatTxt, lines, 'utf8');

  execSync(`"${ffmpeg}" -y -f concat -safe 0 -i "${concatTxt}" -c copy "${tempMerged}"`, { stdio: 'inherit' });

  console.log(`\n=== Mixing Audio (Voiceover + Transition SFX + Ambient Tech Music) ===`);
  let probeOut = '';
  try {
    probeOut = execSync(`"${ffmpeg}" -i "${tempMerged}" 2>&1`, { encoding: 'utf8' });
  } catch (e) {
    probeOut = (e.stdout || '') + (e.stderr || '');
  }

  const match = probeOut.match(/Duration: (\d{2}):(\d{2}):(\d{2}\.\d{2})/);
  let totalSeconds = 240.0;
  if (match) {
    totalSeconds = parseInt(match[1]) * 3600 + parseInt(match[2]) * 60 + parseFloat(match[3]);
  }
  const min = Math.floor(totalSeconds / 60);
  const sec = Math.floor(totalSeconds % 60);
  console.log(`Total video duration: ${totalSeconds.toFixed(1)}s (${min}m ${sec}s)`);

  const filter = `[0:a]volume=1.0[voice];[1:a]atrim=0:${totalSeconds.toFixed(2)},volume=0.08,afade=t=out:st=${(totalSeconds - 4).toFixed(2)}:d=4[music];[voice][music]amix=inputs=2:duration=first:dropout_transition=0[outa]`;
  const mixCmd = `"${ffmpeg}" -y -i "${tempMerged}" -i "${bgMusic}" -filter_complex "${filter}" -map 0:v -map "[outa]" -c:v copy -c:a aac -b:a 192k -shortest "${outputVideo}"`;

  execSync(mixCmd, { stdio: 'inherit' });

  const finalStats = fs.statSync(outputVideo);
  const totalElapsed = ((Date.now() - startTime) / 1000).toFixed(1);
  console.log(`\n🎉 Final video successfully created in ${totalElapsed}s:`);
  console.log(`Path: ${outputVideo}`);
  console.log(`Size: ${(finalStats.size / 1024 / 1024).toFixed(2)} MB`);
  console.log(`Duration: ${min}m ${sec}s`);
}

main().catch(err => {
  console.error('Build failed:', err);
  process.exit(1);
});

