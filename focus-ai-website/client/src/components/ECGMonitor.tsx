import { useState, useEffect, useRef } from "react";
import { Activity, Heart } from "lucide-react";

export default function ECGMonitor() {
  const [bpm, setBpm] = useState(72);
  const [points, setPoints] = useState<number[]>([]);
  const containerRef = useRef<HTMLDivElement>(null);
  const maxPoints = 60;

  // Generare semnal ECG (P, Q, R, S, T wave)
  useEffect(() => {
    let tick = 0;
    
    // Inițializăm cu zerouri
    setPoints(Array(maxPoints).fill(100));

    const interval = setInterval(() => {
      tick = (tick + 1) % 20; // Un ciclu cardiac complet la fiecare 20 de pași (aprox. 1 sec)
      
      let val = 100; // Linie de bază (mijlocul containerului de înălțime 200)

      if (tick === 2) {
        val = 90; // P wave (mică depolarizare atrială)
      } else if (tick === 4) {
        val = 105; // Q wave (mică deviație negativă)
      } else if (tick === 5) {
        val = 30; // R wave (depolarizare ventriculară masivă - spike-ul principal)
      } else if (tick === 6) {
        val = 160; // S wave (deviație negativă profundă)
      } else if (tick === 8) {
        val = 80; // T wave (repolarizare ventriculară)
      } else {
        // Zgomot mic de fond pe linia izoelectrică
        val = 100 + (Math.random() - 0.5) * 4;
      }

      setPoints(prev => {
        const next = [...prev.slice(1), val];
        return next;
      });

      // Fluctuație naturală BPM
      if (tick === 0) {
        setBpm(prev => {
          const change = Math.random() > 0.5 ? 1 : -1;
          return Math.max(60, Math.min(95, prev + change));
        });
      }
    }, 50);

    return () => clearInterval(interval);
  }, []);

  // Convertim punctele în path SVG
  const pathData = points
    .map((val, idx) => {
      const x = (idx / (maxPoints - 1)) * 300;
      return `${idx === 0 ? "M" : "L"} ${x} ${val}`;
    })
    .join(" ");

  return (
    <div className="glass-panel rounded-3xl p-6 glow-blue border-white/10 flex flex-col h-[280px]">
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          <Activity className="w-5 h-5 text-secondary animate-pulse" />
          <span className="text-sm font-semibold tracking-wider font-mono text-secondary uppercase">
            MONITORIZARE ECG (BIOMETRIE)
          </span>
        </div>
        <div className="flex items-center gap-2 bg-red-500/10 border border-red-500/20 px-3 py-1 rounded-full">
          <Heart className="w-4 h-4 text-red-500 animate-bounce" />
          <span className="text-xs font-mono font-bold text-red-400">{bpm} BPM</span>
        </div>
      </div>

      <div ref={containerRef} className="flex-1 bg-slate-950/50 rounded-2xl border border-white/5 overflow-hidden p-4 relative">
        {/* Grilă osciloscopică fundal */}
        <div className="absolute inset-0 opacity-10">
          <svg className="w-full h-full" xmlns="http://www.w3.org/2000/svg">
            <defs>
              <pattern id="ecg-grid" width="20" height="20" patternUnits="userSpaceOnUse">
                <rect width="20" height="20" fill="none" stroke="rgba(0, 102, 255, 0.5)" strokeWidth="0.5" />
              </pattern>
            </defs>
            <rect width="100%" height="100%" fill="url(#ecg-grid)" />
          </svg>
        </div>

        {/* Semnalul ECG propriu-zis */}
        <svg className="w-full h-full overflow-visible" viewBox="0 0 300 200" preserveAspectRatio="none">
          <path
            d={pathData}
            fill="none"
            stroke="#0066ff"
            strokeWidth="2.5"
            strokeLinecap="round"
            strokeLinejoin="round"
            className="drop-shadow-[0_0_8px_rgba(0,102,255,0.8)]"
          />
        </svg>
      </div>
    </div>
  );
}
