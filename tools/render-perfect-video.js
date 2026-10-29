const { execSync } = require('child_process');
const path = require('path');
const fs = require('fs');

const rootDir = path.resolve(__dirname, '..');
const buildDir = path.join(rootDir, 'build-video');
const audioDir = path.join(buildDir, 'audio');
const framesDir = path.join(buildDir, 'frames');
const ffmpeg = path.join(rootDir, '.video-tools/node_modules/@ffmpeg-installer/win32-x64/ffmpeg.exe');
const whooshSrc = path.join(buildDir, 'sfx_whoosh.wav');
const bgMusicSrc = path.join(buildDir, 'bg_music.wav');
const finalOutput = path.join(rootDir, 'FCMS_Pro_Project_Demo.mp4');

const scenes = [
  { id: 'scene_01_hook_web', frame: 'scene_01_hook_web.png' },
  { id: 'scene_02_web_features', frame: 'scene_02_web_features.png' },
  { id: 'scene_03_download_page', frame: 'scene_03_download_page.png' },
  { id: 'scene_04_install_walkthrough', frame: 'scene_04_install_walkthrough.png' },
  { id: 'scene_05_dashboard', frame: 'scene_05_dashboard.png' },
  { id: 'scene_06_commissions', frame: 'scene_06_commissions.png' },
  { id: 'scene_07_clients', frame: 'scene_07_clients.png' },
  { id: 'scene_08_finances', frame: 'scene_08_finances.png' },
  { id: 'scene_09_analytics', frame: 'scene_09_analytics.png' },
  { id: 'scene_10_search', frame: 'scene_10_search.png' },
  { id: 'scene_11_reports', frame: 'scene_11_reports.png' },
  { id: 'scene_12_themes', frame: 'scene_12_themes.png' },
  { id: 'scene_13_privacy', frame: 'scene_13_privacy.png' },
  { id: 'scene_14_outro', frame: 'scene_14_outro.png' }
];

function getAudioDuration(filePath) {
  let out = '';
  try {
    out = execSync(`"${ffmpeg}" -i "${filePath}" 2>&1`, { encoding: 'utf8' });
  } catch (err) {
    out = (err.stdout || '') + (err.stderr || '');
  }
  const m = out.match(/Duration: (\d{2}):(\d{2}):(\d{2}\.\d{2})/);
  if (!m) throw new Error(`Could not parse duration for ${filePath}`);
  return parseInt(m[1]) * 3600 + parseInt(m[2]) * 60 + parseFloat(m[3]);
}

async function main() {
  console.log('=== Step 1: Normalizing Audio Tracks to 48kHz Stereo PCM WAV ===');
  const normalizedDir = path.join(buildDir, 'audio_normalized');
  if (!fs.existsSync(normalizedDir)) {
    fs.mkdirSync(normalizedDir, { recursive: true });
  }

  const sceneDurations = [];
  const pauseNormal = 0.40; // 400ms natural breathing space between scenes
  const outroHold = 2.50;   // 2.50s holding time for contact details at the end

  for (let i = 0; i < scenes.length; i++) {
    const s = scenes[i];
    const srcMp3 = path.join(audioDir, `${s.id}.mp3`);
    const dstWav = path.join(normalizedDir, `${s.id}.wav`);

    // Convert MP3 to clean 48kHz 16-bit Stereo PCM WAV
    execSync(`"${ffmpeg}" -y -i "${srcMp3}" -ar 48000 -ac 2 -c:a pcm_s16le "${dstWav}"`, { stdio: 'ignore' });
    const voiceDuration = getAudioDuration(dstWav);

    const isLast = (i === scenes.length - 1);
    const pause = isLast ? outroHold : pauseNormal;
    const totalSceneDuration = voiceDuration + pause;

    sceneDurations.push({
      id: s.id,
      frame: s.frame,
      wavPath: dstWav,
      voiceDuration,
      pause,
      totalSceneDuration
    });

    console.log(`  [${i + 1}/${scenes.length}] ${s.id}: Voice=${voiceDuration.toFixed(2)}s + Pause=${pause.toFixed(2)}s = ${totalSceneDuration.toFixed(2)}s`);
  }

  // Calculate cumulative start timestamps for each scene
  let currentStart = 0;
  for (let i = 0; i < sceneDurations.length; i++) {
    sceneDurations[i].startTime = currentStart;
    currentStart += sceneDurations[i].totalSceneDuration;
  }
  const totalVideoDuration = currentStart;
  const min = Math.floor(totalVideoDuration / 60);
  const sec = Math.floor(totalVideoDuration % 60);
  console.log(`\nExact Total Video & Audio Duration: ${totalVideoDuration.toFixed(2)}s (${min}m ${sec}s)`);

  console.log('\n=== Step 2: Assembling Continuous Master Voiceover Track ===');
  // Build a complex filter to place each scene's audio at its exact startTime
  // Using adelay and amix for mathematically sample-accurate alignment
  const filterInputs = [];
  const filterDelays = [];

  for (let i = 0; i < sceneDurations.length; i++) {
    filterInputs.push(`-i "${sceneDurations[i].wavPath}"`);
    const delayMs = Math.round(sceneDurations[i].startTime * 1000);
    filterDelays.push(`[${i}:a]adelay=${delayMs}|${delayMs}[a${i}]`);
  }

  const mixInputs = sceneDurations.map((_, i) => `[a${i}]`).join('');
  const voiceoverFilter = `${filterDelays.join(';')};${mixInputs}amix=inputs=${sceneDurations.length}:duration=longest:dropout_transition=0[outv]`;

  const masterVoiceWav = path.join(buildDir, 'master_voice.wav');
  const assembleVoiceCmd = `"${ffmpeg}" -y ${filterInputs.join(' ')} -filter_complex "${voiceoverFilter}" -map "[outv]" -ar 48000 -ac 2 -c:a pcm_s16le "${masterVoiceWav}"`;
  execSync(assembleVoiceCmd, { stdio: 'ignore' });
  console.log(`  -> Master voiceover generated: ${masterVoiceWav}`);

  console.log('\n=== Step 3: Mixing Sound Effects & Background Music ===');
  // Prepare whoosh SFX at 48kHz
  const whoosh48 = path.join(buildDir, 'whoosh_48k.wav');
  execSync(`"${ffmpeg}" -y -i "${whooshSrc}" -ar 48000 -ac 2 -c:a pcm_s16le "${whoosh48}"`, { stdio: 'ignore' });

  // Prepare background music trimmed & faded
  const bgMusic48 = path.join(buildDir, 'bg_music_48k.wav');
  const fadeStart = (totalVideoDuration - 4.0).toFixed(2);
  const musicFilter = `atrim=0:${totalVideoDuration.toFixed(2)},volume=0.08,afade=t=out:st=${fadeStart}:d=4[bgm]`;
  execSync(`"${ffmpeg}" -y -i "${bgMusicSrc}" -filter_complex "${musicFilter}" -map "[bgm]" -ar 48000 -ac 2 -c:a pcm_s16le "${bgMusic48}"`, { stdio: 'ignore' });

  // Build whoosh events for scenes 2 to 14
  const whooshInputs = [];
  const whooshDelays = [];
  let sfxIndex = 2; // input 0 is voice, input 1 is bgm, inputs 2.. are whooshes

  for (let i = 1; i < sceneDurations.length; i++) {
    whooshInputs.push(`-i "${whoosh48}"`);
    const delayMs = Math.round(sceneDurations[i].startTime * 1000);
    whooshDelays.push(`[${sfxIndex}:a]adelay=${delayMs}|${delayMs},volume=0.32[sfx${i}]`);
    sfxIndex++;
  }

  const sfxMixInputs = sceneDurations.slice(1).map((_, idx) => `[sfx${idx + 1}]`).join('');
  const totalSfxInputs = sceneDurations.length - 1;
  const soundtrackFilter = `${whooshDelays.join(';')};[0:a]volume=1.0[voice];[1:a]volume=1.0[music];${sfxMixInputs}amix=inputs=${totalSfxInputs}[sfxall];[voice][music][sfxall]amix=inputs=3:duration=first:dropout_transition=0[finala]`;

  const masterSoundtrackWav = path.join(buildDir, 'master_soundtrack.wav');
  const mixFinalAudioCmd = `"${ffmpeg}" -y -i "${masterVoiceWav}" -i "${bgMusic48}" ${whooshInputs.join(' ')} -filter_complex "${soundtrackFilter}" -map "[finala]" -ar 48000 -ac 2 -c:a pcm_s16le "${masterSoundtrackWav}"`;
  execSync(mixFinalAudioCmd, { stdio: 'ignore' });
  console.log(`  -> Master soundtrack generated: ${masterSoundtrackWav}`);

  console.log('\n=== Step 4: Generating Frame Concat Script (CFR 30fps) ===');
  const slidesConcatPath = path.join(buildDir, 'slides_concat.txt');
  let concatContent = 'ffconcat version 1.0\n';

  for (let i = 0; i < sceneDurations.length; i++) {
    const s = sceneDurations[i];
    // Relative path from project root where command executes
    concatContent += `file 'build-video/frames/${s.frame}'\n`;
    concatContent += `duration ${s.totalSceneDuration.toFixed(3)}\n`;
  }
  // Repeat last frame to prevent ffconcat truncation
  concatContent += `file 'build-video/frames/${sceneDurations[sceneDurations.length - 1].frame}'\n`;

  fs.writeFileSync(slidesConcatPath, concatContent, 'utf8');
  console.log(`  -> Saved slides concat script to ${slidesConcatPath}`);

  console.log('\n=== Step 5: Encoding Broadcast-Standard Full HD MP4 (H.264 High Profile, CFR 30fps) ===');
  const encodeCmd = `"${ffmpeg}" -y -f concat -safe 0 -i "${slidesConcatPath}" -i "${masterSoundtrackWav}" -vf "fps=30,format=yuv420p" -c:v libx264 -preset fast -crf 18 -profile:v high -level 4.1 -c:a aac -b:a 192k -ar 48000 -ac 2 -movflags +faststart "${finalOutput}"`;
  console.log('Running FFmpeg master encode (this takes ~30-40 seconds for maximum quality)...');
  const encodeStart = Date.now();
  execSync(encodeCmd, { stdio: 'inherit' });

  const stats = fs.statSync(finalOutput);
  const elapsed = ((Date.now() - encodeStart) / 1000).toFixed(1);
  console.log(`\n🎉 Success! Master Video Created in ${elapsed}s:`);
  console.log(`  File: ${finalOutput}`);
  console.log(`  Size: ${(stats.size / 1024 / 1024).toFixed(2)} MB`);

  // Probe final file
  let finalProbe = '';
  try {
    finalProbe = execSync(`"${ffmpeg}" -i "${finalOutput}" 2>&1`, { encoding: 'utf8' });
  } catch (err) {
    finalProbe = (err.stdout || '') + (err.stderr || '');
  }
  const durMatch = finalProbe.match(/Duration: (\d{2}):(\d{2}):(\d{2}\.\d{2})/);
  if (durMatch) {
    console.log(`  Verified Duration: ${durMatch[1]}:${durMatch[2]}:${durMatch[3]}`);
  }
}

main().catch(err => {
  console.error('Fatal error during video render:', err);
  process.exit(1);
});
