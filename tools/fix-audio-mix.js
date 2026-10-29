const { execSync } = require('child_process');
const path = require('path');
const fs = require('fs');

const rootDir = path.resolve(__dirname, '..');
const buildDir = path.join(rootDir, 'build-video');
const audioNormDir = path.join(buildDir, 'audio_normalized');
const paddedDir = path.join(buildDir, 'audio_padded');
const ffmpeg = path.join(rootDir, '.video-tools/node_modules/@ffmpeg-installer/win32-x64/ffmpeg.exe');
const whooshSrc = path.join(buildDir, 'sfx_whoosh.wav');
const bgMusicSrc = path.join(buildDir, 'bg_music.wav');
const finalOutput = path.join(rootDir, 'FCMS_Pro_Project_Demo.mp4');
const tempOutput = path.join(rootDir, 'FCMS_Pro_Project_Demo_temp.mp4');

if (!fs.existsSync(paddedDir)) {
  fs.mkdirSync(paddedDir, { recursive: true });
}

// Exact scene durations from slides_concat.txt (matching the already encoded video)
const sceneSpecs = [
  { id: 'scene_01_hook_web', duration: 18.420 },
  { id: 'scene_02_web_features', duration: 19.910 },
  { id: 'scene_03_download_page', duration: 14.220 },
  { id: 'scene_04_install_walkthrough', duration: 17.540 },
  { id: 'scene_05_dashboard', duration: 15.330 },
  { id: 'scene_06_commissions', duration: 17.370 },
  { id: 'scene_07_clients', duration: 16.410 },
  { id: 'scene_08_finances', duration: 14.730 },
  { id: 'scene_09_analytics', duration: 13.310 },
  { id: 'scene_10_search', duration: 13.600 },
  { id: 'scene_11_reports', duration: 16.580 },
  { id: 'scene_12_themes', duration: 13.410 },
  { id: 'scene_13_privacy', duration: 17.200 },
  { id: 'scene_14_outro', duration: 27.820 }
];

const totalVideoDuration = sceneSpecs.reduce((sum, s) => sum + s.duration, 0);
console.log(`Target Video Duration: ${totalVideoDuration.toFixed(3)}s`);

console.log('\n--- Step 1: Padding Scene Audio to Exact Target Durations ---');
const paddedFiles = [];
let cumulativeTime = 0;
const transitionTimes = [];

for (let i = 0; i < sceneSpecs.length; i++) {
  const s = sceneSpecs[i];
  if (i > 0) transitionTimes.push(cumulativeTime);
  cumulativeTime += s.duration;

  const srcWav = path.join(audioNormDir, `${s.id}.wav`);
  const dstWav = path.join(paddedDir, `${s.id}.wav`);
  const targetSamples = Math.round(s.duration * 48000);

  // apad=whole_len sets exact sample count at 48kHz
  execSync(`"${ffmpeg}" -y -i "${srcWav}" -af "apad=whole_len=${targetSamples}" -ar 48000 -ac 2 -c:a pcm_s16le "${dstWav}"`, { stdio: 'ignore' });
  paddedFiles.push(dstWav);
  console.log(`  Padded ${s.id} -> ${s.duration.toFixed(3)}s (${targetSamples} samples)`);
}

console.log('\n--- Step 2: Concatenating Master Voiceover with ZERO Attenuation ---');
const concatInputs = paddedFiles.map(f => `-i "${f}"`).join(' ');
const concatFilter = paddedFiles.map((_, i) => `[${i}:a]`).join('') + `concat=n=${paddedFiles.length}:v=0:a=1[vmaster]`;
const masterVoiceWav = path.join(buildDir, 'master_voice.wav');

execSync(`"${ffmpeg}" -y ${concatInputs} -filter_complex "${concatFilter}" -map "[vmaster]" -ar 48000 -ac 2 -c:a pcm_s16le "${masterVoiceWav}"`, { stdio: 'ignore' });
console.log('  Master voice assembled via concat!');

console.log('\n--- Step 3: Preparing SFX and Background Music Tracks ---');
const whoosh48 = path.join(buildDir, 'whoosh_48k.wav');
execSync(`"${ffmpeg}" -y -i "${whooshSrc}" -ar 48000 -ac 2 -c:a pcm_s16le "${whoosh48}"`, { stdio: 'ignore' });

// Build SFX track by delaying whoosh at each transition
const sfxInputs = transitionTimes.map(() => `-i "${whoosh48}"`).join(' ');
const sfxDelays = transitionTimes.map((t, idx) => {
  const delayMs = Math.round(t * 1000);
  return `[${idx}:a]adelay=${delayMs}|${delayMs}[sfx${idx}]`;
}).join(';');
const sfxMix = transitionTimes.map((_, idx) => `[sfx${idx}]`).join('');
const sfxFilter = `${sfxDelays};${sfxMix}amix=inputs=${transitionTimes.length}:duration=longest:dropout_transition=0,volume=${transitionTimes.length}[sfxout]`;
const masterSfxWav = path.join(buildDir, 'master_sfx.wav');

execSync(`"${ffmpeg}" -y ${sfxInputs} -filter_complex "${sfxFilter}" -map "[sfxout]" -ar 48000 -ac 2 -c:a pcm_s16le "${masterSfxWav}"`, { stdio: 'ignore' });
console.log('  Master SFX track assembled!');

// Prepare background music
const bgMusic48 = path.join(buildDir, 'bg_music_48k.wav');
const fadeStart = (totalVideoDuration - 4.0).toFixed(2);
const musicFilter = `atrim=0:${totalVideoDuration.toFixed(2)},volume=0.08,afade=t=out:st=${fadeStart}:d=4[bgm]`;
execSync(`"${ffmpeg}" -y -i "${bgMusicSrc}" -filter_complex "${musicFilter}" -map "[bgm]" -ar 48000 -ac 2 -c:a pcm_s16le "${bgMusic48}"`, { stdio: 'ignore' });
console.log('  Background music track prepared!');

console.log('\n--- Step 4: Final 3-Stem Mix with Normalized Levels ---');
// Voice: full 1.0 volume
// Music: 0.08 volume
// SFX: 0.28 volume
// With 3 inputs into amix, we multiply by 3 to counteract amix 1/3 scaling
const mixFilter = `[0:a]volume=1.0[v];[1:a]volume=0.08[m];[2:a]volume=0.28[s];[v][m][s]amix=inputs=3:duration=first:dropout_transition=0,volume=3[final]`;
const masterSoundtrackWav = path.join(buildDir, 'master_soundtrack.wav');

execSync(`"${ffmpeg}" -y -i "${masterVoiceWav}" -i "${bgMusic48}" -i "${masterSfxWav}" -filter_complex "${mixFilter}" -map "[final]" -ar 48000 -ac 2 -c:a pcm_s16le "${masterSoundtrackWav}"`, { stdio: 'ignore' });

// Check volume
const volOut = execSync(`"${ffmpeg}" -i "${masterSoundtrackWav}" -af "volumedetect" -f null NUL 2>&1`, { encoding: 'utf8' });
const maxMatch = volOut.match(/max_volume: ([-\d.]+) dB/);
const meanMatch = volOut.match(/mean_volume: ([-\d.]+) dB/);
console.log(`  Soundtrack Audio Levels: Max=${maxMatch ? maxMatch[1] : '?'} dB | Mean=${meanMatch ? meanMatch[1] : '?'} dB`);

console.log('\n--- Step 5: Fast Muxing Perfect Audio into Video ---');
// Fast stream copy for video (-c:v copy), re-encode pristine audio track (-c:a aac)
execSync(`"${ffmpeg}" -y -i "${finalOutput}" -i "${masterSoundtrackWav}" -map 0:v:0 -map 1:a:0 -c:v copy -c:a aac -b:a 192k -ar 48000 -shortest -movflags +faststart "${tempOutput}"`, { stdio: 'inherit' });

// Replace final video
fs.copyFileSync(tempOutput, finalOutput);
fs.unlinkSync(tempOutput);

console.log(`\n🎉 Successfully restored crystal-clear master audio in ${finalOutput}!`);
