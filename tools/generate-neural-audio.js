const path = require('path');
const fs = require('fs');
const { MsEdgeTTS, OUTPUT_FORMAT } = require(path.resolve(__dirname, '../.video-tools/node_modules/msedge-tts'));

const scenes = [
  {
    id: "scene_01_hook_web",
    narration: "Hi everyone, I'm Roderic Villena! I've been a freelancer since 2023, and I've been using FCMS Pro every single day for over 4 months to run my client commissions. If you're tired of paying monthly subscriptions just to track invoices on slow, bloated web apps, you're going to love this."
  },
  {
    id: "scene_02_web_features",
    narration: "Right on the official website at villenaderic dot github dot io slash FcmsPro, you can explore the entire project. As a Filipino freelancer, having native Philippine Peso support was essential for me. Everything is local-first: no accounts, zero cloud tracking, and your data never leaves your computer."
  },
  {
    id: "scene_03_download_page",
    narration: "Installing FCMS Pro takes less than sixty seconds. The Download page automatically detects your operating system — whether you're on Windows, macOS, or Linux — and provides a lightweight installer or portable package ready to run."
  },
  {
    id: "scene_04_install_walkthrough",
    narration: "Setup is virtually effortless. Just download the installer, click run, and FCMS Pro instantly provisions an encrypted-ready local SQLite database in your user directory. No database servers to configure, no email verification, and no setup headaches."
  },
  {
    id: "scene_05_dashboard",
    narration: "Once you launch the app, you're greeted by the Dashboard. Here's my live business pulse in Philippine Pesos: tracking twenty thousand pesos in sample demo income, active project balances, overdue alerts, and an interactive income trend trajectory."
  },
  {
    id: "scene_06_commissions",
    narration: "Managing project deadlines has never been smoother. Move your commissions across customizable stages from Lead and In Progress to Delivered. View your pipeline in a detailed table or an intuitive Kanban board, complete with down payments, balances, and urgency countdowns."
  },
  {
    id: "scene_07_clients",
    narration: "Never lose track of a client again. The built-in CRM lets you store full contact records, lifetime earnings in pesos, and active project counts. You can even attach contracts, creative briefs, and project files directly to each client's profile."
  },
  {
    id: "scene_08_finances",
    narration: "Take total control of your cash flow. Record client payments across multiple methods to automatically update project balances. Categorize business expenses and track recurring subscriptions to see your true net profit."
  },
  {
    id: "scene_09_analytics",
    narration: "Gain powerful insights into your freelance business with built-in analytics. Review month-over-month revenue growth, identify your highest-earning clients, and see exactly where your expenses go without ever touching a spreadsheet."
  },
  {
    id: "scene_10_search",
    narration: "Need to find something in a split second? Just hit the forward slash key anywhere in the app to summon Global Search. Instantly search across all your clients, commissions, and transactions at lightning speed."
  },
  {
    id: "scene_11_reports",
    narration: "When tax season arrives, don't stress. FCMS Pro generates publication-quality quarterly and annual financial summary PDFs in Philippine Pesos with one click, cleanly breaking down gross revenue, deductible expenses, and net profit ready for your accountant."
  },
  {
    id: "scene_12_themes",
    narration: "Work comfortably day or night. Switch between a sleek Dark theme and a crisp Light theme with a single toggle, customize your accent color, and configure Philippine Pesos or any international currency as your default."
  },
  {
    id: "scene_13_privacy",
    narration: "Best of all, you get absolute peace of mind. Because FCMS Pro is local-first, your financial records stay exclusively on your device with automatic rotating backups every time you exit. You're never locked in, and you can export your entire database anytime."
  },
  {
    id: "scene_14_outro",
    narration: "Stop paying monthly fees for cloud trackers. Upgrade your freelance business today with FCMS Pro. Download it for free at villenaderic dot github dot io slash FcmsPro. And if you're a business or individual looking for custom websites, software applications, or specialized business tools, feel free to contact me directly on Messenger — let's build something great together. Happy freelancing!"
  }
];

async function main() {
  console.log("=== Generating High-Quality Neural Male Voiceover for Roderic Villena ===");
  const audioDir = path.resolve("./build-video/audio");
  if (!fs.existsSync(audioDir)) {
    fs.mkdirSync(audioDir, { recursive: true });
  }

  for (let i = 0; i < scenes.length; i++) {
    const s = scenes[i];
    const outPath = path.join(audioDir, `${s.id}.mp3`);
    console.log(`[${i + 1}/${scenes.length}] Synthesizing ${s.id}...`);

    const tts = new MsEdgeTTS();
    await tts.setMetadata('en-US-GuyNeural', OUTPUT_FORMAT.AUDIO_24KHZ_96KBITRATE_MONO_MP3);
    
    await new Promise((resolve, reject) => {
      const { audioStream } = tts.toStream(s.narration);
      const writeStream = fs.createWriteStream(outPath);
      audioStream.pipe(writeStream);
      writeStream.on('finish', () => {
        const stats = fs.statSync(outPath);
        console.log(`  -> Completed: ${stats.size} bytes`);
        resolve();
      });
      audioStream.on('error', reject);
      writeStream.on('error', reject);
    });
  }

  console.log("\nAll neural voiceover tracks successfully generated!");
}

main().catch(err => {
  console.error("Audio generation failed:", err);
  process.exit(1);
});
