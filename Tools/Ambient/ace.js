// Generates instrumental audio with ACE-Step v1 (3.5B) through the local ComfyUI API
// (http://127.0.0.1:8188). ACE-Step is Apache-2.0, so what it makes can ship in the game.
//
//   node ace.js <out folder> <seconds> <seed,seed,...> "<tags>"
//
// Writes <out>/<seed>.flac for each seed.
const fs = require('fs');
const path = require('path');

const API = 'http://127.0.0.1:8188';
const [, , out, seconds, seeds, tags] = process.argv;
if (!tags) { console.error('usage: node ace.js <out> <seconds> <seeds> "<tags>"'); process.exit(1); }
fs.mkdirSync(out, { recursive: true });

function graph(seed) {
  return {
    '1': { class_type: 'CheckpointLoaderSimple', inputs: { ckpt_name: 'ace_step_v1_3.5b.safetensors' } },
    '2': { class_type: 'ModelSamplingSD3', inputs: { model: ['1', 0], shift: 5.0 } },
    '3': { class_type: 'TextEncodeAceStepAudio', inputs: { clip: ['1', 1], tags, lyrics: '[instrumental]', lyrics_strength: 1.0 } },
    '4': { class_type: 'ConditioningZeroOut', inputs: { conditioning: ['3', 0] } },
    '5': { class_type: 'EmptyAceStepLatentAudio', inputs: { seconds: +seconds, batch_size: 1 } },
    '6': { class_type: 'KSampler', inputs: { model: ['2', 0], seed: +seed, steps: 50, cfg: 5.0, sampler_name: 'euler', scheduler: 'simple', positive: ['3', 0], negative: ['4', 0], latent_image: ['5', 0], denoise: 1.0 } },
    '7': { class_type: 'VAEDecodeAudio', inputs: { samples: ['6', 0], vae: ['1', 2] } },
    '8': { class_type: 'SaveAudio', inputs: { audio: ['7', 0], filename_prefix: 'audio/risingway_' + seed } },
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
      await sleep(2000);
      const hist = await (await fetch(API + '/history/' + prompt_id)).json();
      const entry = hist[prompt_id];
      if (entry && entry.status && entry.status.status_str === 'error') { console.error('failed:', JSON.stringify(entry.status.messages).slice(0, 1200)); process.exit(1); }
      if (entry && entry.outputs && entry.outputs['8']) { outputs = entry.outputs['8'].audio; break; }
      if (Date.now() - t0 > 30 * 60 * 1000) { console.error('timed out'); process.exit(1); }
    }
    for (const a of outputs) {
      const q = new URLSearchParams({ filename: a.filename, subfolder: a.subfolder, type: a.type });
      const buf = Buffer.from(await (await fetch(API + '/view?' + q)).arrayBuffer());
      const file = path.join(out, seed + path.extname(a.filename));
      fs.writeFileSync(file, buf);
      console.log('seed ' + seed + ' -> ' + file + ' (' + Math.round((Date.now() - t0) / 1000) + ' s)');
    }
  }
})();
