const fs = require('fs');
const path = require('path');

function replaceDollars(dir) {
  if (!fs.existsSync(dir)) return;
  const files = fs.readdirSync(dir, { withFileTypes: true });
  for (const f of files) {
    const full = path.join(dir, f.name);
    if (f.isDirectory()) {
      replaceDollars(full);
    } else if (f.name.endsWith('.html')) {
      let content = fs.readFileSync(full, 'utf8');
      const updated = content.replace(/\$([0-9]+[0-9,.]*)/g, '₱$1');
      if (updated !== content) {
        fs.writeFileSync(full, updated, 'utf8');
        console.log('Updated to ₱ in:', full);
      }
    }
  }
}

replaceDollars(path.resolve('./_site'));
replaceDollars(path.resolve('./site'));
console.log('Currency replacement to Philippine Peso completed!');

