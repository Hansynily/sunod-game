/**
 * Stardew Valley-style animated pixel-art background.
 * Sky gradient + drifting clouds are static; the sun's light rays rotate.
 * Pure canvas 2D, no assets.
 *
 * Usage:
 *   const canvas = document.getElementById('bg');
 *   startStardewBackground(canvas);          // starts the animation loop
 *
 *   // or, to draw one frame yourself (e.g. inside your own game loop):
 *   drawStardewBackground(ctx, width, height, performance.now());
 */

function fillRect(ctx, x, y, w, h, color) {
  ctx.fillStyle = color;
  ctx.fillRect(Math.round(x), Math.round(y), Math.round(w), Math.round(h));
}

// simple deterministic pseudo-random, so stars/clouds stay fixed between frames
function hashRand(seed) {
  const x = Math.sin(seed * 999.9) * 43758.5453;
  return x - Math.floor(x);
}

function drawSky(ctx, w, h) {
  const grad = ctx.createLinearGradient(0, 0, 0, h);
  grad.addColorStop(0, '#0b2a5c');
  grad.addColorStop(0.45, '#2f6fb0');
  grad.addColorStop(0.75, '#8fc7e8');
  grad.addColorStop(1, '#d9f0e6');
  ctx.fillStyle = grad;
  ctx.fillRect(0, 0, w, h);
}

function drawStars(ctx, w, h, t) {
  const n = 40;
  for (let i = 0; i < n; i++) {
    const x = hashRand(i) * w;
    const y = hashRand(i + 100) * h * 0.5;
    const twinkle = 0.5 + 0.5 * Math.sin(t / 600 + i * 12.9);
    ctx.globalAlpha = 0.3 + twinkle * 0.5;
    fillRect(ctx, x, y, 2, 2, '#ffffff');
  }
  ctx.globalAlpha = 1;
}

// one puffy pixel cloud made of stacked blocks
function drawCloud(ctx, cx, cy, s) {
  const blocks = [
    [-4, 0, 10, 2], [-3, -1, 8, 2], [-1, -2, 5, 2],
    [-5, 1, 12, 2], [-6, 2, 14, 2],
  ];
  for (const [dx, dy, bw, bh] of blocks) {
    fillRect(ctx, cx + dx * s, cy + dy * s, bw * s, bh * s, '#ffffff');
  }
  // soft shadow underside
  fillRect(ctx, cx - 6 * s, cy + 3 * s, 12 * s, 1.5 * s, '#d7e9f2');
}

const CLOUD_LAYOUT = [
  { xf: 0.12, yf: 0.18, s: 1.6 },
  { xf: 0.32, yf: 0.10, s: 1.1 },
  { xf: 0.62, yf: 0.22, s: 1.8 },
  { xf: 0.82, yf: 0.12, s: 1.3 },
  { xf: 0.48, yf: 0.30, s: 1.0 },
  { xf: 0.05, yf: 0.55, s: 1.4 },
  { xf: 0.78, yf: 0.62, s: 1.6 },
  { xf: 0.92, yf: 0.42, s: 1.0 },
];

function drawClouds(ctx, w, h) {
  for (const c of CLOUD_LAYOUT) drawCloud(ctx, c.xf * w, c.yf * h, c.s * (w / 220));
}

// the sun disc with rotating light rays behind it
function drawSun(ctx, cx, cy, r, angle) {
  ctx.save();
  ctx.translate(cx, cy);

  // rotating rays
  const rayCount = 8;
  ctx.rotate(angle);
  for (let i = 0; i < rayCount; i++) {
    ctx.save();
    ctx.rotate((i / rayCount) * Math.PI * 2);
    ctx.globalAlpha = 0.35;
    ctx.fillStyle = '#fff4c2';
    ctx.beginPath();
    ctx.moveTo(0, 0);
    ctx.lineTo(-r * 0.35, -r * 2.4);
    ctx.lineTo(r * 0.35, -r * 2.4);
    ctx.closePath();
    ctx.fill();
    ctx.restore();
  }
  ctx.globalAlpha = 1;
  ctx.restore();

  // sun body (not rotated) - pixel rings for a blocky look
  fillRect(ctx, cx - r, cy - r, r * 2, r * 2, '#ffe28a');
  fillRect(ctx, cx - r * 0.7, cy - r * 0.7, r * 1.4, r * 1.4, '#fff1b8');
  fillRect(ctx, cx - r * 0.35, cy - r * 0.35, r * 0.7, r * 0.7, '#ffffff');
}

/**
 * Draws one full frame of the background.
 * @param {CanvasRenderingContext2D} ctx
 * @param {number} w  canvas width
 * @param {number} h  canvas height
 * @param {number} t  timestamp in ms (e.g. from performance.now())
 */
function drawStardewBackground(ctx, w, h, t) {
  ctx.imageSmoothingEnabled = false;
  drawSky(ctx, w, h);
  drawStars(ctx, w, h, t);
  drawSun(ctx, w * 0.78, h * 0.22, Math.min(w, h) * 0.05, t / 6000);
  drawClouds(ctx, w, h);
}

/**
 * Starts a requestAnimationFrame loop that continuously redraws the
 * background onto the given canvas, resizing to fill it.
 * Returns a stop() function.
 */
function startStardewBackground(canvas) {
  const ctx = canvas.getContext('2d');
  let running = true;

  function frame(t) {
    if (!running) return;
    drawStardewBackground(ctx, canvas.width, canvas.height, t);
    requestAnimationFrame(frame);
  }
  requestAnimationFrame(frame);

  return () => { running = false; };
}

if (typeof document !== 'undefined') {
  const canvas = document.getElementById('bg');
  if (canvas) startStardewBackground(canvas);
}

if (typeof module !== 'undefined' && module.exports) {
  module.exports = { drawStardewBackground, startStardewBackground };
}
