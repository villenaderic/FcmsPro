/*
 * search.js - Lightweight client-side search query matcher.
 * Matches multi-term queries with weighted ranking (title vs body).
 */

export function scoreSearchEntries(entries, query) {
  const terms = query.toLowerCase().split(/\s+/).filter(Boolean);
  if (!terms.length || !entries) return [];
  const scored = [];
  for (const e of entries) {
    const title = (e.t || "").toLowerCase();
    const text = (e.x || "").toLowerCase();
    let score = 0;
    let all = true;
    for (const t of terms) {
      const inTitle = title.includes(t);
      const inText = text.includes(t);
      if (!inTitle && !inText) { all = false; break; }
      score += (inTitle ? 5 : 0) + (inText ? 1 : 0);
    }
    if (all) scored.push({ e, score });
  }
  scored.sort((a, b) => b.score - a.score);
  return scored;
}
