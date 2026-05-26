import { useState, useEffect, useRef } from "react";
import { motion } from "framer-motion";
import { Camera, Activity, RefreshCw, Eye } from "lucide-react";

export default function LiveEyeTracking() {
  const [isActive, setIsActive] = useState(true);
  const [fps, setFps] = useState(30);
  const [confidence, setConfidence] = useState(94.2);
  const [gazeCoords, setGazeCoords] = useState({ x: 150, y: 150 });
  const [blinkCount, setBlinkCount] = useState(12);
  const [isBlinking, setIsBlinking] = useState(false);
  const [heatmapPoints, setHeatmapPoints] = useState<{ x: number; y: number; intensity: number }[]>([]);
  const containerRef = useRef<HTMLDivElement>(null);

  // Simulare date dinamice
  useEffect(() => {
    if (!isActive) return;

    const interval = setInterval(() => {
      // FPS fluctuează ușor în jur de 30 FPS
      setFps(prev => Math.max(28, Math.min(30, +(prev + (Math.random() - 0.5) * 2).toFixed(1))));
      
      // Confidența fluctuează ușor
      setConfidence(prev => Math.max(88, Math.min(98, +(prev + (Math.random() - 0.5) * 1.5).toFixed(1))));

      // Mișcarea privirii (gaze estimation)
      if (containerRef.current) {
        const width = containerRef.current.clientWidth;
        const height = containerRef.current.clientHeight;
        
        // Generăm coordonate noi în apropierea punctului curent pentru a simula fixarea privirii
        setGazeCoords(prev => {
          const dx = (Math.random() - 0.5) * 80;
          const dy = (Math.random() - 0.5) * 60;
          const newX = Math.max(50, Math.min(width - 50, prev.x + dx));
          const newY = Math.max(50, Math.min(height - 50, prev.y + dy));
          
          // Adăugăm punct în heatmap
          setHeatmapPoints(prevPoints => {
            const updated = [...prevPoints, { x: newX, y: newY, intensity: 1 }];
            if (updated.length > 25) updated.shift(); // Păstrăm doar ultimele puncte
            return updated;
          });

          return { x: newX, y: newY };
        });
      }

      // Simulare clipit aleatoriu
      if (Math.random() > 0.92) {
        setIsBlinking(true);
        setBlinkCount(prev => prev + 1);
        setTimeout(() => setIsBlinking(false), 150);
      }
    }, 200);

    return () => clearInterval(interval);
  }, [isActive]);

  const resetHeatmap = () => {
    setHeatmapPoints([]);
    setBlinkCount(0);
  };

  return (
    <div className="relative glass-panel rounded-3xl overflow-hidden glow-cyan border-white/10 flex flex-col h-[500px]">
      {/* Header-ul simulatorului */}
      <div className="flex items-center justify-between px-6 py-4 bg-white/5 border-b border-white/10">
        <div className="flex items-center gap-3">
          <div className="w-3 h-3 rounded-full bg-emerald-500 animate-pulse" />
          <span className="text-sm font-semibold tracking-wider font-mono text-emerald-400 uppercase">
            LIVE EYE-TRACKING FEED
          </span>
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={() => setIsActive(!isActive)}
            className={`p-1.5 rounded-lg border text-xs font-mono transition-all ${
              isActive 
                ? "bg-primary/20 border-primary/30 text-primary" 
                : "bg-white/5 border-white/10 text-slate-400"
            }`}
          >
            {isActive ? "ACTIVE" : "PAUSED"}
          </button>
          <button
            onClick={resetHeatmap}
            className="p-1.5 rounded-lg bg-white/5 border border-white/10 text-slate-400 hover:text-white transition-all"
            title="Resetează datele"
          >
            <RefreshCw className="w-4 h-4" />
          </button>
        </div>
      </div>

      {/* Ecranul principal de urmărire */}
      <div 
        ref={containerRef}
        className="relative flex-1 bg-slate-950/80 overflow-hidden flex items-center justify-center"
      >
        {/* Imagine de fundal simulată - ochi stilizați și repere faciale (Landmarks) */}
        <div className="absolute inset-0 opacity-40 flex items-center justify-center pointer-events-none">
          <svg className="w-full h-full" viewBox="0 0 600 400" fill="none" xmlns="http://www.w3.org/2000/svg">
            {/* Grilă de fundal */}
            <defs>
              <pattern id="grid" width="30" height="30" patternUnits="userSpaceOnUse">
                <path d="M 30 0 L 0 0 0 30" fill="none" stroke="rgba(255,255,255,0.03)" strokeWidth="1" />
              </pattern>
            </defs>
            <rect width="100%" height="100%" fill="url(#grid)" />

            {/* Contur față simulat */}
            <path 
              d="M150 100 C150 50, 450 50, 450 100 C450 250, 380 350, 300 350 C220 350, 150 250, 150 100 Z" 
              stroke="rgba(0, 210, 255, 0.15)" 
              strokeWidth="2" 
              strokeDasharray="4 4"
            />

            {/* Ochiul Stâng */}
            <ellipse cx="230" cy="160" rx="35" ry="20" stroke="rgba(0, 210, 255, 0.3)" strokeWidth="1.5" />
            <circle cx="230" cy="160" r="15" stroke="rgba(0, 210, 255, 0.5)" strokeWidth="1" />
            {!isBlinking && <circle cx="230" cy="160" r="6" fill="#00d2ff" className="animate-pulse" />}

            {/* Ochiul Drept */}
            <ellipse cx="370" cy="160" rx="35" ry="20" stroke="rgba(0, 210, 255, 0.3)" strokeWidth="1.5" />
            <circle cx="370" cy="160" r="15" stroke="rgba(0, 210, 255, 0.5)" strokeWidth="1" />
            {!isBlinking && <circle cx="370" cy="160" r="6" fill="#00d2ff" className="animate-pulse" />}

            {/* Landmark Lines (Repere faciale) */}
            <path d="M230 160 L300 210 L370 160" stroke="rgba(0, 210, 255, 0.2)" strokeWidth="1" />
            <path d="M300 110 L300 280" stroke="rgba(0, 210, 255, 0.2)" strokeWidth="1" />
            
            {/* Puncte de landmark */}
            <circle cx="230" cy="160" r="3" fill="#00d2ff" />
            <circle cx="370" cy="160" r="3" fill="#00d2ff" />
            <circle cx="300" cy="210" r="3" fill="#00d2ff" />
            <circle cx="300" cy="110" r="3" fill="#00d2ff" />
            <circle cx="300" cy="280" r="3" fill="#00d2ff" />
            <circle cx="200" cy="240" r="3" fill="#00d2ff" />
            <circle cx="400" cy="240" r="3" fill="#00d2ff" />
          </svg>
        </div>

        {/* Heatmap animat */}
        {heatmapPoints.map((pt, idx) => (
          <div
            key={idx}
            className="absolute rounded-full pointer-events-none blur-xl transition-all duration-500"
            style={{
              left: pt.x - 40,
              top: pt.y - 40,
              width: 80,
              height: 80,
              background: `radial-gradient(circle, rgba(0, 210, 255, ${0.15 * pt.intensity}) 0%, rgba(0, 102, 255, 0) 70%)`,
            }}
          />
        ))}

        {/* Reticulul privirii (Gaze Indicator) */}
        {isActive && (
          <motion.div
            className="absolute pointer-events-none z-10"
            animate={{ x: gazeCoords.x - 20, y: gazeCoords.y - 20 }}
            transition={{ type: "spring", stiffness: 120, damping: 15 }}
          >
            <div className="relative w-10 h-10 flex items-center justify-center">
              <div className="absolute inset-0 rounded-full border border-primary animate-ping opacity-70" />
              <div className="absolute inset-1 rounded-full border border-primary/50" />
              <div className="w-2 h-2 rounded-full bg-primary" />
              {/* Linii reticul */}
              <div className="absolute w-4 h-[1px] bg-primary/70 -left-1" />
              <div className="absolute w-4 h-[1px] bg-primary/70 -right-1" />
              <div className="absolute h-4 w-[1px] bg-primary/70 -top-1" />
              <div className="absolute h-4 w-[1px] bg-primary/70 -bottom-1" />
            </div>
          </motion.div>
        )}

        {/* Status de clipit (Blink Notification) */}
        {isBlinking && (
          <div className="absolute top-4 left-1/2 -translate-x-1/2 px-4 py-1.5 bg-primary/90 text-background font-bold text-xs rounded-full font-mono shadow-lg shadow-primary/30 z-20">
            CLIPIT DETECTAT
          </div>
        )}
      </div>

      {/* Bara de telemetrie de jos */}
      <div className="grid grid-cols-4 gap-4 px-6 py-4 bg-white/5 border-t border-white/10 font-mono text-xs">
        <div className="flex flex-col gap-1">
          <span className="text-slate-400 uppercase text-[10px]">FPS Urmărire</span>
          <span className="text-white font-bold text-sm">{fps} Hz</span>
        </div>
        <div className="flex flex-col gap-1">
          <span className="text-slate-400 uppercase text-[10px]">Acuratețe (Confidență)</span>
          <span className="text-primary font-bold text-sm">{confidence}%</span>
        </div>
        <div className="flex flex-col gap-1">
          <span className="text-slate-400 uppercase text-[10px]">Coordonate Privire</span>
          <span className="text-white font-bold text-sm">
            X: {Math.round(gazeCoords.x)}, Y: {Math.round(gazeCoords.y)}
          </span>
        </div>
        <div className="flex flex-col gap-1">
          <span className="text-slate-400 uppercase text-[10px]">Total Clipește</span>
          <span className="text-secondary font-bold text-sm">{blinkCount} / min</span>
        </div>
      </div>
    </div>
  );
}
