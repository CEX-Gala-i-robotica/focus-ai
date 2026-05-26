import { motion } from "framer-motion";
import { Brain, Sigma, Cpu, Sparkles } from "lucide-react";

export default function MathFormulas() {
  return (
    <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
      {/* Formula 1: Indicele de Concentrare */}
      <motion.div
        className="glass-panel rounded-3xl p-8 border-white/10 glow-cyan relative overflow-hidden"
        initial={{ opacity: 0, y: 30 }}
        whileInView={{ opacity: 1, y: 0 }}
        viewport={{ once: true }}
        transition={{ duration: 0.6 }}
      >
        <div className="absolute top-0 right-0 w-32 h-32 bg-primary/5 rounded-bl-full -z-10" />
        <div className="flex items-center gap-3 mb-6">
          <div className="p-2.5 rounded-xl bg-primary/10 border border-primary/20">
            <Brain className="w-5 h-5 text-primary" />
          </div>
          <h3 className="text-xl font-bold font-display text-white">Indicele de Concentrare (IC)</h3>
        </div>

        <p className="text-slate-300 text-sm mb-6 leading-relaxed">
          Algoritmul nostru calculează atenția în timp real printr-un raport dinamic între stabilitatea privirii, rata de clipit și variabilitatea ritmului cardiac (HRV).
        </p>

        {/* Formula randată elegant */}
        <div className="bg-slate-950/60 border border-white/5 rounded-2xl p-6 flex items-center justify-center my-6 font-mono overflow-x-auto">
          <div className="text-center">
            <span className="text-primary font-bold text-lg">IC</span>
            <span className="text-slate-400 mx-2">=</span>
            <span className="inline-block align-middle text-left">
              <span className="block text-center border-b border-slate-600 pb-1 text-white px-2">
                &alpha; · S<sub>privire</sub> + &beta; · (1 / f<sub>clipit</sub>)
              </span>
              <span className="block text-center pt-1 text-slate-400">
                &gamma; · (1 / HRV<sub>LF/HF</sub>)
              </span>
            </span>
            <span className="text-slate-400 mx-2">&times;</span>
            <span className="text-secondary font-bold text-lg">100</span>
          </div>
        </div>

        <div className="space-y-3 text-xs font-mono text-slate-400">
          <div className="flex justify-between border-b border-white/5 pb-2">
            <span>&alpha;, &beta;, &gamma;</span>
            <span className="text-white">Coeficienți de ponderare dinamici</span>
          </div>
          <div className="flex justify-between border-b border-white/5 pb-2">
            <span>S<sub>privire</sub></span>
            <span className="text-white">Stabilitatea fixării privirii (gaze variance)</span>
          </div>
          <div className="flex justify-between border-b border-white/5 pb-2">
            <span>f<sub>clipit</sub></span>
            <span className="text-white">Frecvența clipitului pe minut</span>
          </div>
          <div className="flex justify-between">
            <span>HRV<sub>LF/HF</sub></span>
            <span className="text-white">Raportul frecvențelor joase/înalte (stres cardiac)</span>
          </div>
        </div>
      </motion.div>

      {/* Formula 2: Analiza Spectrală ECG */}
      <motion.div
        className="glass-panel rounded-3xl p-8 border-white/10 glow-blue relative overflow-hidden"
        initial={{ opacity: 0, y: 30 }}
        whileInView={{ opacity: 1, y: 0 }}
        viewport={{ once: true }}
        transition={{ duration: 0.6, delay: 0.2 }}
      >
        <div className="absolute top-0 right-0 w-32 h-32 bg-secondary/5 rounded-bl-full -z-10" />
        <div className="flex items-center gap-3 mb-6">
          <div className="p-2.5 rounded-xl bg-secondary/10 border border-secondary/20">
            <Sigma className="w-5 h-5 text-secondary" />
          </div>
          <h3 className="text-xl font-bold font-display text-white">Analiza Spectrală ECG</h3>
        </div>

        <p className="text-slate-300 text-sm mb-6 leading-relaxed">
          Pentru a evalua stresul și oboseala mentală, analizăm intervalele R-R extrase de senzorul MyoWare prin Transformata Fourier Rapidă (FFT).
        </p>

        {/* Formula randată elegant */}
        <div className="bg-slate-950/60 border border-white/5 rounded-2xl p-6 flex items-center justify-center my-6 font-mono overflow-x-auto">
          <div className="text-center">
            <span className="text-secondary font-bold text-lg">P(f)</span>
            <span className="text-slate-400 mx-2">=</span>
            <span className="text-white text-lg">|</span>
            <span className="text-white text-lg">&int;</span>
            <span className="text-slate-400 text-xs relative -top-2">-&infin;</span>
            <span className="text-slate-400 text-xs relative -bottom-2">&infin;</span>
            <span className="text-white mx-1">RR(t) · e<sup>-i2&pi;ft</sup> dt</span>
            <span className="text-white text-lg">|</span>
            <span className="text-white text-xs relative -top-2">2</span>
          </div>
        </div>

        <div className="space-y-3 text-xs font-mono text-slate-400">
          <div className="flex justify-between border-b border-white/5 pb-2">
            <span>RR(t)</span>
            <span className="text-white">Seria temporală a intervalelor dintre bătăi</span>
          </div>
          <div className="flex justify-between border-b border-white/5 pb-2">
            <span>P(f)</span>
            <span className="text-white">Densitatea spectrală de putere</span>
          </div>
          <div className="flex justify-between border-b border-white/5 pb-2">
            <span>LF (0.04 - 0.15 Hz)</span>
            <span className="text-white">Indicator activitate Simpatică (Stres)</span>
          </div>
          <div className="flex justify-between">
            <span>HF (0.15 - 0.40 Hz)</span>
            <span className="text-white">Indicator activitate Parasimpatică (Relaxare)</span>
          </div>
        </div>
      </motion.div>
    </div>
  );
}
