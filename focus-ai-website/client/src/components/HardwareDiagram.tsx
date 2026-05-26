import { useState } from "react";
import { motion, AnimatePresence } from "framer-motion";
import { Cpu, Monitor, Zap, Radio, Bell, Eye, Database } from "lucide-react";

interface ComponentDetail {
  id: string;
  name: string;
  icon: any;
  category: "core" | "display" | "biometrics" | "io";
  description: string;
  specs: string[];
}

const hardwareComponents: ComponentDetail[] = [
  {
    id: "lenovo",
    name: "Lenovo ThinkCenter neo 50q Gen 4",
    icon: Cpu,
    category: "core",
    description: "Unitatea centrală de procesare a stației F.O.C.U.S. AI. Rulează algoritmii grei de computer vision și procesează fluxurile de date biometrice.",
    specs: ["Procesor Intel Core i5", "16GB RAM DDR4", "Stocare SSD NVMe rapidă", "Sistem de operare optimizat pentru latență minimă"]
  },
  {
    id: "yodoit",
    name: "Sistem Dual-Monitor YoDoIt 16.5”",
    icon: Monitor,
    category: "display",
    description: "Sistemul principal de afișaj. Un ecran prezintă mediul de lucru sau stimulul de atenție, iar celălalt oferă dashboard-ul de monitorizare în timp real.",
    specs: ["Rezoluție Full HD 1080p", "Rată de refresh 60Hz", "Panou IPS cu unghiuri largi de vizualizare", "Conexiune USB-C plug-and-play"]
  },
  {
    id: "arduino",
    name: "Arduino UNO R3",
    icon: Zap,
    category: "core",
    description: "Microcontrolerul responsabil de achiziția datelor de la senzorii analogici și controlul componentelor fizice de feedback.",
    specs: ["Interfață USB serială", "Convertor ADC pe 10 biți", "Comunicație I2C cu senzorii", "Latență de achiziție sub 1ms"]
  },
  {
    id: "myoware",
    name: "Senzor MyoWare ECG",
    icon: HeartIcon, // definit local
    category: "biometrics",
    description: "Senzor medical de ultimă generație folosit pentru măsurarea activității electrice a inimii (ECG) și extragerea variabilității ritmului cardiac (HRV).",
    specs: ["Electrozi cu gel de unică folosință", "Amplificare analogică integrată", "Filtru de zgomot activ de 50/60Hz", "Ieșire analogică directă către Arduino"]
  },
  {
    id: "max30100",
    name: "MAX30100 Pulse Oximeter",
    icon: ActivityIcon, // definit local
    category: "biometrics",
    description: "Senzor integrat pentru măsurarea pulsului și a nivelului de oxigen din sânge (SpO2) prin fotopletismografie (PPG).",
    specs: ["Comunicație I2C", "Leduri integrate Roșu și IR", "Consum ultra-redus de energie", "Rată de eșantionare configurabilă"]
  },
  {
    id: "nfc",
    name: "Cititor NFC & RFID",
    icon: Radio,
    category: "io",
    description: "Modul utilizat pentru autentificarea rapidă și securizată a utilizatorilor la stația de lucru prin card sau brățară inteligentă.",
    specs: ["Frecvență de operare 13.56 MHz", "Suport carduri MIFARE", "Antenă integrată în PCB", "Autentificare instantanee sub 100ms"]
  },
  {
    id: "touch",
    name: "Senzor Capacitiv de Atingere",
    icon: TouchIcon, // definit local
    category: "io",
    description: "Senzor de atingere fizic utilizat ca buton de urgență sau pentru calibrarea rapidă a stației neuro-cognitive.",
    specs: ["Detecție prin atingere capacitivă", "Fără elemente mecanice în mișcare", "Timp de răspuns ultra-rapid", "Led indicator de stare integrat"]
  },
  {
    id: "buzzer",
    name: "Buzzer Piezoelectric & NumPad",
    icon: Bell,
    category: "io",
    description: "Componente pentru interacțiune fizică. Buzzerul oferă feedback audio discret pentru atenționări, iar NumPad-ul permite introducerea rapidă de coduri.",
    specs: ["Buzzer cu frecvență variabilă", "Feedback sonor neuro-sensorial", "NumPad mecanic rezistent", "Conexiune directă pe pini GPIO"]
  }
];

// Helper icons care nu sunt în lucide standard sau necesită customizare
function HeartIcon(props: any) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...props}>
      <path d="M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7Z" />
    </svg>
  );
}

function ActivityIcon(props: any) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...props}>
      <path d="M22 12h-4l-3 9L9 3l-3 9H2" />
    </svg>
  );
}

function TouchIcon(props: any) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...props}>
      <path d="M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20Z" />
      <path d="M12 16a4 10 0 0 0 0-8" />
      <path d="M12 8v8" />
    </svg>
  );
}

export default function HardwareDiagram() {
  const [selectedComp, setSelectedComp] = useState<ComponentDetail>(hardwareComponents[0]);

  return (
    <div className="grid grid-cols-1 lg:grid-cols-12 gap-8 items-stretch">
      {/* Selectorul de componente (stânga/centru) */}
      <div className="lg:col-span-7 flex flex-col gap-4">
        <h3 className="text-lg font-semibold font-mono text-primary uppercase tracking-wider mb-2">
          Interacționează cu componentele stației
        </h3>
        
        <div className="grid grid-cols-2 sm:grid-cols-4 lg:grid-cols-2 xl:grid-cols-4 gap-3">
          {hardwareComponents.map((comp) => {
            const Icon = comp.icon;
            const isSelected = selectedComp.id === comp.id;
            return (
              <button
                key={comp.id}
                onClick={() => setSelectedComp(comp)}
                className={`p-4 rounded-2xl border text-left transition-all duration-300 flex flex-col gap-3 group relative overflow-hidden ${
                  isSelected
                    ? "bg-primary/10 border-primary text-white shadow-lg shadow-primary/10"
                    : "bg-white/5 border-white/5 text-slate-400 hover:bg-white/10 hover:border-white/10 hover:text-white"
                }`}
              >
                {/* Glow de fundal pentru butonul selectat */}
                {isSelected && (
                  <div className="absolute -right-6 -bottom-6 w-16 h-16 bg-primary/20 rounded-full blur-xl" />
                )}
                
                <div className={`p-2.5 rounded-xl w-fit transition-colors duration-300 ${
                  isSelected 
                    ? "bg-primary text-background" 
                    : "bg-white/5 text-slate-300 group-hover:bg-primary/20 group-hover:text-primary"
                }`}>
                  <Icon className="w-5 h-5" />
                </div>
                
                <span className="font-semibold text-xs leading-tight tracking-wide font-display">
                  {comp.name.split(" ")[0]} {comp.name.split(" ").slice(1).join(" ").substring(0, 15)}...
                </span>
              </button>
            );
          })}
        </div>

        {/* Conexiuni animate simulate */}
        <div className="hidden sm:block glass-panel rounded-2xl p-4 border-white/5 bg-slate-950/40 text-xs font-mono text-slate-400">
          <div className="flex items-center gap-3">
            <span className="text-primary font-bold">STATUS SISTEM:</span>
            <div className="flex items-center gap-2">
              <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse" />
              <span>Arduino UNO (Connected)</span>
            </div>
            <div className="flex items-center gap-2">
              <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse" />
              <span>MyoWare ECG (Receiving)</span>
            </div>
            <div className="flex items-center gap-2">
              <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse" />
              <span>NFC Reader (Ready)</span>
            </div>
          </div>
        </div>
      </div>

      {/* Detalii componentă selectată (dreapta) */}
      <div className="lg:col-span-5">
        <AnimatePresence mode="wait">
          <motion.div
            key={selectedComp.id}
            initial={{ opacity: 0, x: 20 }}
            animate={{ opacity: 1, x: 0 }}
            exit={{ opacity: 0, x: -20 }}
            transition={{ duration: 0.3 }}
            className="glass-panel rounded-3xl p-8 border-white/10 glow-blue h-full flex flex-col justify-between relative overflow-hidden"
          >
            {/* Decorație de fundal */}
            <div className="absolute -right-10 -top-10 w-40 h-40 bg-secondary/5 rounded-full blur-3xl pointer-events-none" />

            <div>
              <div className="flex items-center gap-4 mb-6">
                <div className="p-4 rounded-2xl bg-secondary/10 border border-secondary/20 text-secondary">
                  <selectedComp.icon className="w-8 h-8" />
                </div>
                <div>
                  <span className="text-xs font-mono font-bold text-secondary uppercase tracking-widest">
                    {selectedComp.category} component
                  </span>
                  <h4 className="text-2xl font-bold text-white tracking-tight font-display mt-1">
                    {selectedComp.name}
                  </h4>
                </div>
              </div>

              <p className="text-slate-300 text-sm leading-relaxed mb-8">
                {selectedComp.description}
              </p>

              <div>
                <h5 className="text-xs font-mono font-bold text-slate-400 uppercase tracking-wider mb-4">
                  Specificații Tehnice:
                </h5>
                <ul className="space-y-3">
                  {selectedComp.specs.map((spec, idx) => (
                    <li key={idx} className="flex items-start gap-3 text-sm text-slate-300">
                      <span className="w-1.5 h-1.5 rounded-full bg-secondary mt-2 flex-shrink-0" />
                      <span>{spec}</span>
                    </li>
                  ))}
                </ul>
              </div>
            </div>

            <div className="mt-8 pt-6 border-t border-white/5 flex items-center justify-between text-xs font-mono text-slate-500">
              <span>F.O.C.U.S. AI HARDWARE</span>
              <span>v1.0-R3</span>
            </div>
          </motion.div>
        </AnimatePresence>
      </div>
    </div>
  );
}
