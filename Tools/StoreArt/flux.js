// Generates images with FLUX.1 [schnell] through the local ComfyUI API (http://127.0.0.1:8188).
//
//   node flux.js <out folder> <width> <height> <seed,seed,...> "<prompt>"
//
// schnell is distilled for 1-4 steps at CFG 1; negative prompts do nothing, so there is none.
// Writes <out>/<seed>.png for each seed. Licence: FLUX.1 [schnell] is Apache-2.0, so the images
// can be used commercially (store art). FLUX.1 [dev] would not allow that.
const fs = require('fs');
const path = require('path');

const API = 'http://127.0.0.1:8188';
const [, , out, w, h, seeds, prompt] = process.argv;
if (!prompt) { console.error('usage: node flux.js <out> <w> <h> <seeds> "<prompt>"'); process.exit(1); }
fs.mkdirSync(out, { recursive: true });

function graph(seed) {
  return {
    '1': { class_type: 'CheckpointLoaderSimple', inputs: { ckpt_name: 'flux1-schnell-fp8.safetensors' } },
    '2': { class_type: 'CLIPTextEncode', inputs: { text: prompt, clip: ['1', 1] } },
    '3': { class_type: 'CLIPTextEncode', inputs: { text: '', clip: ['1', 1] } },
    '4': { class_type: 'EmptySD3LatentImage', inputs: { width: +w, height: +h, batch_size: 1 } },
    '5': { class_type: 'KSampler', inputs: { model: ['1', 0], seed: +seed, steps: 4, cfg: 1.0, sampler_name: 'euler', scheduler: 'simple', positive: ['2', 0], negative: ['3', 0], latent_image: ['4', 0], denoise: 1.0 } },
    '6': { class_type: 'VAEDecode', inputs: { samples: ['5', 0], vae: ['1', 2] } },
    '7': { class_type: 'SaveImage', inputs: { images: ['6', 0], filename_prefix: 'risingway_' + seed } },
  };
}

const sleep = ms => new Promise(r => setTimeout(r, ms));

(async () => {
  for (const seed of seeds.split(',')) {
    const res = await fetch(API + '/prompt', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ prompt: graph(seed) }) });
    const { prompt_id, error, node_errors } = await res.json();
    if (!prompt_id) { console.error('rejected:', JSON.stringify(error || node_errors)); process.exit(1); }
    const t0 = Date.now();
    let outputs;
    for (;;) {
      await sleep(1500);
      const hist = await (await fetch(API + '/history/' + prompt_id)).json();
      const entry = hist[prompt_id];
      if (entry && entry.status && entry.status.status_str === 'error') { console.error('failed:', JSON.stringify(entry.status.messages).slice(0, 800)); process.exit(1); }
      if (entry && entry.outputs && entry.outputs['7']) { outputs = entry.outputs['7'].images; break; }
      if (Date.now() - t0 > 15 * 60 * 1000) { console.error('timed out'); process.exit(1); }
    }
    for (const img of outputs) {
      const q = new URLSearchParams({ filename: img.filename, subfolder: img.subfolder, type: img.type });
      const buf = Buffer.from(await (await fetch(API + '/view?' + q)).arrayBuffer());
      const file = path.join(out, seed + '.png');
      fs.writeFileSync(file, buf);
      console.log('seed ' + seed + ' -> ' + file + ' (' + Math.round((Date.now() - t0) / 1000) + ' s)');
    }
  }
})();
