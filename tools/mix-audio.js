const { execSync } = require('child_process');
const path = require('path');
const fs = require('fs');

const rootDir = path.resolve(__dirname, '..');
const ffmpeg = path.join(rootDir, '.video-tools/node_modules/@ffmpeg-installer/win32-x64/ffmpeg.exe');
const mergedRaw = path.join(rootDir, 'build-video/merged_raw.mp4');
const bgMusic = path.join(rootDir, 'build-video/bg_music.wav');
const output = path.join(rootDir, 'FCMS_Pro_Project_Demo.mp4');

let probeOut = '';
try {
  probeOut = execSync(`"${ffmpeg}" -i "${mergedRaw}" 2>&1`, { encoding: 'utf8' });
} catch (e) {
  probeOut = (e.stdout || '') + (e.stderr || '');
}

const match = probeOut.match(/Duration: (\d{2}):(\d{2}):(\d{2}\.\d{2})/);
let totalSeconds = 250.04;
if (match) {
  totalSeconds = parseInt(match[1]) * 3600 + parseInt(match[2]) * 60 + parseFloat(match[3]);
}
console.log(`Video duration: ${totalSeconds} seconds (4m 10s)`);

// Mix voiceover (volume 1.0) with background music (volume 0.08, trimmed to total duration)
const filter = `[0:a]volume=1.0[voice];[1:a]atrim=0:${totalSeconds.toFixed(2)},volume=0.08,afade=t=out:st=${(totalSeconds - 4).toFixed(2)}:d=4[music];[voice][music]amix=inputs=2:duration=first:dropout_transition=0[outa]`;

const cmd = `"${ffmpeg}" -y -i "${mergedRaw}" -i "${bgMusic}" -filter_complex "${filter}" -map 0:v -map "[outa]" -c:v copy -c:a aac -b:a 192k -shortest "${output}"`;

console.log('Mixing audio track...');
execSync(cmd, { stdio: 'inherit' });

const stats = fs.statSync(output);
console.log(`Final output video created: ${output} (${(stats.size / 1024 / 1024).toFixed(2)} MB)`);
