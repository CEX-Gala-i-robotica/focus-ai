import { motion } from "framer-motion";
import { 
  Brain, 
  Cpu, 
  Activity, 
  ChevronRight, 
  Play, 
  AlertTriangle, 
  Smartphone, 
  Server, 
  Layers, 
  Users, 
  BookOpen, 
  ExternalLink, 
  Github, 
  Mail,
  Zap,
  ArrowRight,
  TrendingUp,
  Award,
  Eye,
  CheckCircle2,
  Clock
} from "lucide-react";
import Navbar from "@/components/Navbar";
import LiveEyeTracking from "@/components/LiveEyeTracking";
import ECGMonitor from "@/components/ECGMonitor";
import MathFormulas from "@/components/MathFormulas";
import HardwareDiagram from "@/components/HardwareDiagram";
import { Button } from "@/components/ui/button";

const projectImages = {
  circuit: "/project-images/circuit-natio.png",
  cad: "/project-images/cad-station.png",
  formula: "/project-images/performance-formula.png",
  mobileProfile: "/project-images/mobile-profile-nfc.png",
  mobileGames: "/project-images/mobile-games.png",
  mobileLogin: "/project-images/mobile-login.png"
};

export default function Home() {
  // Animații reutilizabile
  const fadeIn = {
    initial: { opacity: 0, y: 20 },
    whileInView: { opacity: 1, y: 0 },
    viewport: { once: true },
    transition: { duration: 0.6 }
  };

  const staggerContainer = {
    initial: {},
    whileInView: {
      transition: {
        staggerChildren: 0.1
      }
    },
    viewport: { once: true }
  };

  return (
    <div className="min-h-screen bg-background text-foreground flex flex-col relative overflow-hidden">
      {/* Navbar */}
      <Navbar />

      {/* 1. HERO SECTION */}
      <section className="relative min-h-screen flex items-center justify-center pt-24 pb-16 overflow-hidden">
        <div className="absolute inset-0 z-0">
          <div className="absolute inset-0 bg-[radial-gradient(circle_at_20%_20%,rgba(0,210,255,0.16),transparent_32%),radial-gradient(circle_at_78%_26%,rgba(20,255,170,0.12),transparent_28%),linear-gradient(180deg,rgba(5,11,23,0.45),rgba(5,11,23,1))]" />
          <div className="absolute inset-x-0 bottom-0 h-2/3 bg-gradient-to-t from-background via-background/70 to-transparent" />
        </div>

        <div className="container relative z-10 grid grid-cols-1 lg:grid-cols-12 gap-12 items-center">
          {/* Conținut Hero */}
          <div className="lg:col-span-7 flex flex-col gap-6 text-left">
            <motion.div 
              initial={{ opacity: 0, scale: 0.95 }}
              animate={{ opacity: 1, scale: 1 }}
              transition={{ duration: 0.5 }}
              className="inline-flex items-center gap-2 px-3 py-1.5 rounded-full bg-primary/10 border border-primary/20 text-primary text-xs font-mono font-bold w-fit uppercase tracking-widest"
            >
              <Zap className="w-3.5 h-3.5" />
              STATION NEURO-COGNITIVĂ INTELIGENTĂ
            </motion.div>

            <motion.h1 
              initial={{ opacity: 0, y: 30 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.6, delay: 0.1 }}
              className="text-5xl sm:text-6xl xl:text-7xl font-extrabold tracking-tight leading-none font-display text-white"
            >
              F.O.C.U.S. <span className="bg-clip-text text-transparent bg-gradient-to-r from-primary to-secondary">AI</span>
            </motion.h1>

            <motion.p 
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.6, delay: 0.2 }}
              className="text-lg sm:text-xl text-slate-300 max-w-2xl leading-relaxed"
            >
              Stație neuro-cognitivă inteligentă pentru analiza și îmbunătățirea concentrării umane. Folosește AI avansat, analiză biometrică și eye-tracking în timp real.
            </motion.p>

            <motion.div 
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.6, delay: 0.3 }}
              className="flex flex-wrap items-center gap-4 mt-4"
            >
              <Button 
                size="lg" 
                className="rounded-full bg-gradient-to-r from-primary to-secondary text-background font-bold px-8 hover:opacity-90 transition-all duration-300"
                onClick={() => {
                  document.getElementById("despre")?.scrollIntoView({ behavior: "smooth" });
                }}
              >
                Descoperă proiectul
                <ChevronRight className="w-5 h-5 ml-1" />
              </Button>
              <Button 
                size="lg" 
                variant="outline" 
                className="rounded-full border-white/10 text-white hover:bg-white/5 px-8 transition-all duration-300"
                onClick={() => {
                  document.getElementById("ai-tracking")?.scrollIntoView({ behavior: "smooth" });
                }}
              >
                <Play className="w-4 h-4 mr-2 fill-white" />
                Vezi demonstrația
              </Button>
            </motion.div>
          </div>

          {/* Schema hardware reala */}
          <motion.div 
            initial={{ opacity: 0, x: 50 }}
            animate={{ opacity: 1, x: 0 }}
            transition={{ duration: 0.8, delay: 0.2 }}
            className="lg:col-span-5 relative flex justify-center"
          >
            <div className="relative w-full max-w-[450px] aspect-square rounded-3xl overflow-hidden glass-panel glow-blue border-white/10">
              <img 
                src={projectImages.circuit} 
                alt="Schema circuitului F.O.C.U.S. AI" 
                className="w-full h-full object-cover"
              />
              <div className="absolute inset-0 bg-gradient-to-t from-background/80 via-transparent to-transparent" />
              
              {/* Overlay telemetry databox */}
              <div className="absolute bottom-6 left-6 right-6 p-4 rounded-2xl bg-black/60 backdrop-blur-md border border-white/10 flex justify-between items-center font-mono text-xs">
                <div>
                  <span className="text-slate-400 block">SISTEM DE OPERARE</span>
                  <span className="text-white font-bold">Arduino + senzori biometrici</span>
                </div>
                <div className="text-right">
                  <span className="text-slate-400 block">STATUS</span>
                  <span className="text-emerald-400 font-bold flex items-center gap-1.5 justify-end">
                    <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse" />
                    ONLINE
                  </span>
                </div>
              </div>
            </div>
          </motion.div>
        </div>
      </section>

      {/* 2. DESPRE PROIECT */}
      <section id="despre" className="py-24 relative z-10 border-t border-white/5">
        <div className="container">
          <div className="text-center max-w-3xl mx-auto mb-16 flex flex-col gap-4">
            <span className="text-xs font-mono font-bold text-primary uppercase tracking-widest">Despre Proiect</span>
            <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-white font-display">
              Sinergia perfectă între Biometrie și Inteligență Artificială
            </h2>
            <p className="text-slate-400">
              F.O.C.U.S. AI reprezintă o paradigmă revoluționară în evaluarea atenției. Combinăm senzori fizici cu algoritmi sofisticați de computer vision pentru a oferi o imagine completă a stării tale cognitive.
            </p>
          </div>

          <motion.div 
            variants={staggerContainer}
            initial="initial"
            whileInView="whileInView"
            viewport={{ once: true }}
            className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6"
          >
            {/* Card 1 */}
            <motion.div variants={fadeIn} className="glass-panel glass-panel-hover rounded-2xl p-6 border-white/5 flex flex-col gap-4">
              <div className="p-3 rounded-xl bg-primary/10 border border-primary/20 text-primary w-fit">
                <Eye className="w-6 h-6" />
              </div>
              <h3 className="text-lg font-bold text-white font-display">Eye-Tracking Avansat</h3>
              <p className="text-sm text-slate-400 leading-relaxed">
                Urmărirea privirii în timp real și generarea de heatmaps pentru a înțelege exact unde îți este distribuită atenția pe ecran.
              </p>
            </motion.div>

            {/* Card 2 */}
            <motion.div variants={fadeIn} className="glass-panel glass-panel-hover rounded-2xl p-6 border-white/5 flex flex-col gap-4">
              <div className="p-3 rounded-xl bg-secondary/10 border border-secondary/20 text-secondary w-fit">
                <Activity className="w-6 h-6" />
              </div>
              <h3 className="text-lg font-bold text-white font-display">Senzori Biometrici</h3>
              <p className="text-sm text-slate-400 leading-relaxed">
                Monitorizarea activității electrice a inimii prin ECG (MyoWare) și determinarea saturației de oxigen cu MAX30100.
              </p>
            </motion.div>

            {/* Card 3 */}
            <motion.div variants={fadeIn} className="glass-panel glass-panel-hover rounded-2xl p-6 border-white/5 flex flex-col gap-4">
              <div className="p-3 rounded-xl bg-primary/10 border border-primary/20 text-primary w-fit">
                <Brain className="w-6 h-6" />
              </div>
              <h3 className="text-lg font-bold text-white font-display">Indice de Concentrare</h3>
              <p className="text-sm text-slate-400 leading-relaxed">
                Un scor dinamic unic calculat matematic pe baza corelării tuturor datelor biometrice și a comportamentului vizual.
              </p>
            </motion.div>

            {/* Card 4 */}
            <motion.div variants={fadeIn} className="glass-panel glass-panel-hover rounded-2xl p-6 border-white/5 flex flex-col gap-4">
              <div className="p-3 rounded-xl bg-secondary/10 border border-secondary/20 text-secondary w-fit">
                <Server className="w-6 h-6" />
              </div>
              <h3 className="text-lg font-bold text-white font-display">Sincronizare Cloud</h3>
              <p className="text-sm text-slate-400 leading-relaxed">
                Stocarea securizată a progresului în Firebase Realtime Database pentru analize pe termen lung și trenduri de evoluție.
              </p>
            </motion.div>
          </motion.div>

          {/* Timeline de dezvoltare */}
          <div className="mt-24">
            <h3 className="text-xl font-bold font-display text-white text-center mb-12">Timeline Dezvoltare Proiect</h3>
            <div className="relative border-l border-white/10 max-w-3xl mx-auto pl-6 space-y-12">
              <div className="relative">
                <div className="absolute -left-[31px] top-1.5 w-4 h-4 rounded-full bg-primary border-4 border-background" />
                <span className="text-xs font-mono font-bold text-primary">FAZA 1 - CONCEPT</span>
                <h4 className="text-lg font-bold text-white mt-1">Cercetare & Arhitectură Hardware</h4>
                <p className="text-sm text-slate-400 mt-2">
                  Identificarea senzorilor necesari (MyoWare ECG, MAX30100) și proiectarea schemei de conexiuni cu Arduino UNO R3.
                </p>
              </div>
              <div className="relative">
                <div className="absolute -left-[31px] top-1.5 w-4 h-4 rounded-full bg-secondary border-4 border-background" />
                <span className="text-xs font-mono font-bold text-secondary">FAZA 2 - DEZVOLTARE AI</span>
                <h4 className="text-lg font-bold text-white mt-1">Algoritm de Eye-Tracking în Python</h4>
                <p className="text-sm text-slate-400 mt-2">
                  Implementarea OpenCV și dlib pentru detecția de landmark-uri faciale, estimarea privirii și detecția clipitului.
                </p>
              </div>
              <div className="relative">
                <div className="absolute -left-[31px] top-1.5 w-4 h-4 rounded-full bg-primary border-4 border-background" />
                <span className="text-xs font-mono font-bold text-primary">FAZA 3 - ECOSYSTEM SOFTWARE</span>
                <h4 className="text-lg font-bold text-white mt-1">Aplicații Desktop WPF & Mobil Kotlin</h4>
                <p className="text-sm text-slate-400 mt-2">
                  Construirea interfeței grafice pe desktop pentru utilizator și conectarea la baza de date Firebase pentru sincronizare.
                </p>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* 3. PROBLEMA IDENTIFICATĂ */}
      <section id="problema" className="py-24 relative z-10 bg-slate-950/40 border-t border-white/5">
        <div className="container">
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-12 items-center">
            {/* Stânga: Text */}
            <div className="lg:col-span-6 flex flex-col gap-6">
              <span className="text-xs font-mono font-bold text-destructive uppercase tracking-widest flex items-center gap-2">
                <AlertTriangle className="w-4 h-4" />
                CRIZA ATENȚIEI MODERNE
              </span>
              <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-white font-display">
                Suntem bombardați de stimuli digitali. Atenția noastră este fragmentată.
              </h2>
              <p className="text-slate-300 leading-relaxed">
                În era TikTok, Instagram Reels și YouTube Shorts, atenția umană medie a scăzut dramatic. Suntem supuși unui fenomen de <strong>ADHD indus digital</strong>, în care economia atenției ne forțează creierul să caute recompense rapide de dopamină.
              </p>
              
              <div className="space-y-4 mt-2">
                <div className="flex gap-4">
                  <div className="p-2 rounded-xl bg-destructive/10 border border-destructive/20 text-destructive h-fit">
                    <TrendingUp className="w-5 h-5" />
                  </div>
                  <div>
                    <h4 className="font-bold text-white font-display">Scăderea capacității de concentrare</h4>
                    <p className="text-sm text-slate-400 mt-1">Atenția medie a scăzut sub 8 secunde, mai puțin decât a unui peștișor auriu.</p>
                  </div>
                </div>
                <div className="flex gap-4">
                  <div className="p-2 rounded-xl bg-destructive/10 border border-destructive/20 text-destructive h-fit">
                    <Clock className="w-5 h-5" />
                  </div>
                  <div>
                    <h4 className="font-bold text-white font-display">Lipsa instrumentelor de măsurare</h4>
                    <p className="text-sm text-slate-400 mt-1">Metodele clasice de evaluare sunt subiective, chestionarele fiind ușor de influențat.</p>
                  </div>
                </div>
              </div>
            </div>

            {/* Dreapta: Grafic comparativ */}
            <div className="lg:col-span-6">
              <div className="glass-panel rounded-3xl p-8 border-white/10 glow-blue flex flex-col gap-6">
                <h3 className="text-lg font-bold font-display text-white">Comparație Metode de Evaluare</h3>
                
                <div className="space-y-6">
                  {/* Metoda 1 */}
                  <div className="space-y-2">
                    <div className="flex justify-between text-xs font-mono">
                      <span className="text-slate-400">METODE CLASICE (CHESTIONARE)</span>
                      <span className="text-destructive font-bold">35% Acuratețe</span>
                    </div>
                    <div className="h-2 bg-white/5 rounded-full overflow-hidden">
                      <div className="h-full bg-destructive rounded-full" style={{ width: "35%" }} />
                    </div>
                    <p className="text-xs text-slate-500">Subiective, evaluate post-eveniment, susceptibile la erori umane.</p>
                  </div>

                  {/* Metoda 2 */}
                  <div className="space-y-2">
                    <div className="flex justify-between text-xs font-mono">
                      <span className="text-primary font-bold">F.O.C.U.S. AI SYSTEM</span>
                      <span className="text-emerald-400 font-bold">90% Acuratețe</span>
                    </div>
                    <div className="h-2 bg-white/5 rounded-full overflow-hidden">
                      <div className="h-full bg-gradient-to-r from-primary to-secondary rounded-full" style={{ width: "90%" }} />
                    </div>
                    <p className="text-xs text-slate-400">Obiectiv, în timp real, corelează eye-tracking cu biometria ECG.</p>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* 4. HARDWARE ARCHITECTURE */}
      <section id="hardware" className="py-24 relative z-10 border-t border-white/5">
        <div className="container">
          <div className="text-center max-w-3xl mx-auto mb-16 flex flex-col gap-4">
            <span className="text-xs font-mono font-bold text-secondary uppercase tracking-widest">Hardware Architecture</span>
            <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-white font-display">
              Sistemul Hardware de Achiziție Date
            </h2>
            <p className="text-slate-400">
              O stație neuro-cognitivă completă, construită pe o arhitectură robustă care îmbină puterea de calcul a unui mini PC industrial cu precizia senzorilor microcontrolerului Arduino.
            </p>
          </div>

          <HardwareDiagram />
        </div>
      </section>

      {/* 5. AI & EYE TRACKING */}
      <section id="ai-tracking" className="py-24 relative z-10 bg-slate-950/40 border-t border-white/5">
        <div className="container">
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-12 items-center">
            {/* Stânga: Live Eye Tracking Simulator */}
            <div className="lg:col-span-6">
              <LiveEyeTracking />
            </div>

            {/* Dreapta: Text explicativ algoritmi */}
            <div className="lg:col-span-6 flex flex-col gap-6">
              <span className="text-xs font-mono font-bold text-primary uppercase tracking-widest">AI & Computer Vision</span>
              <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-white font-display">
                Algoritm de Urmărire Oculară de Mare Precizie
              </h2>
              <p className="text-slate-300 leading-relaxed">
                Inima software a sistemului folosește <strong>Python, OpenCV și dlib</strong> pentru a detecta 68 de repere faciale (landmarks) în timp real. Prin izolarea regiunilor ochilor și calcularea raportului de aspect al ochiului (EAR), sistemul detectează clipitul și estimează direcția privirii.
              </p>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 mt-2">
                <div className="p-5 rounded-2xl bg-white/5 border border-white/5 flex flex-col gap-2">
                  <span className="text-xs font-mono text-primary font-bold">LANDMARK DETECTION</span>
                  <p className="text-xs text-slate-400">Urmărirea a 68 de puncte faciale cheie pentru stabilizarea imaginii și izolarea ochilor.</p>
                </div>
                <div className="p-5 rounded-2xl bg-white/5 border border-white/5 flex flex-col gap-2">
                  <span className="text-xs font-mono text-primary font-bold">BLINK DETECTION</span>
                  <p className="text-xs text-slate-400">Calcularea EAR (Eye Aspect Ratio) pentru determinarea precisă a oboselii oculare.</p>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* 6. SOFTWARE ECOSYSTEM */}
      <section id="software" className="py-24 relative z-10 border-t border-white/5">
        <div className="container">
          <div className="text-center max-w-3xl mx-auto mb-16 flex flex-col gap-4">
            <span className="text-xs font-mono font-bold text-secondary uppercase tracking-widest">Software Ecosystem</span>
            <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-white font-display">
              Ecosistem Software Multiplatformă
            </h2>
            <p className="text-slate-400">
              Sincronizare fluidă între stația de lucru locală, baza de date securizată din cloud și aplicația mobilă a utilizatorului pentru monitorizarea progresului.
            </p>
          </div>

          <div className="mb-10 grid grid-cols-1 lg:grid-cols-[1fr_0.72fr] gap-6 items-stretch">
            <div className="glass-panel rounded-3xl overflow-hidden border-white/10 glow-cyan">
              <div className="aspect-video bg-slate-950">
                <img
                  src={projectImages.mobileGames}
                  alt="Captura aplicatie mobila cu sectiunea de jocuri cognitive"
                  className="w-full h-full object-cover"
                />
              </div>
            </div>
            <div className="glass-panel rounded-3xl overflow-hidden border-white/10 glow-blue max-h-[520px]">
              <img
                src={projectImages.mobileProfile}
                alt="Captura aplicatie mobila cu profil si NFC"
                className="w-full h-full object-cover object-top"
              />
            </div>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
            {/* Card 1: Desktop App */}
            <div className="glass-panel rounded-3xl p-8 border-white/10 glow-cyan flex flex-col justify-between">
              <div>
                <div className="p-3 rounded-xl bg-primary/10 border border-primary/20 text-primary w-fit mb-6">
                  <Layers className="w-6 h-6" />
                </div>
                <h3 className="text-xl font-bold text-white font-display mb-3">Aplicație Desktop WPF</h3>
                <p className="text-sm text-slate-400 leading-relaxed">
                  Dezvoltată în C# și WPF, aplicația locală se conectează la Arduino, preia feed-ul camerei web, rulează scriptul Python și afișează interfața interactivă de antrenament.
                </p>
              </div>
              <div className="mt-8 pt-4 border-t border-white/5 flex justify-between text-xs font-mono text-slate-500">
                <span>C# / WPF</span>
                <span>DESKTOP CLIENT</span>
              </div>
            </div>

            {/* Card 2: Firebase Cloud */}
            <div className="glass-panel rounded-3xl p-8 border-white/10 glow-blue flex flex-col justify-between">
              <div>
                <div className="p-3 rounded-xl bg-secondary/10 border border-secondary/20 text-secondary w-fit mb-6">
                  <Server className="w-6 h-6" />
                </div>
                <h3 className="text-xl font-bold text-white font-display mb-3">Firebase Cloud Database</h3>
                <p className="text-sm text-slate-400 leading-relaxed">
                  Bază de date în timp real care stochează istoricul sesiunilor de concentrare, indicii biometrici și preferințele de configurare ale fiecărui utilizator.
                </p>
              </div>
              <div className="mt-8 pt-4 border-t border-white/5 flex justify-between text-xs font-mono text-slate-500">
                <span>REALTIME DB</span>
                <span>CLOUD SYNC</span>
              </div>
            </div>

            {/* Card 3: Mobile App */}
            <div className="glass-panel rounded-3xl p-8 border-white/10 glow-cyan flex flex-col justify-between">
              <div>
                <div className="p-3 rounded-xl bg-primary/10 border border-primary/20 text-primary w-fit mb-6">
                  <Smartphone className="w-6 h-6" />
                </div>
                <h3 className="text-xl font-bold text-white font-display mb-3">Aplicație Mobilă Kotlin</h3>
                <p className="text-sm text-slate-400 leading-relaxed">
                  Construită nativ în Kotlin, oferă utilizatorului acces rapid la rapoarte statistice, evoluția Indicelui de Concentrare în timp și notificări inteligente de antrenament.
                </p>
              </div>
              <div className="mt-8 pt-4 border-t border-white/5 flex justify-between text-xs font-mono text-slate-500">
                <span>KOTLIN / KMP</span>
                <span>MOBILE COMPANION</span>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* 7. ANALIZA MATEMATICĂ */}
      <section id="matematica" className="py-24 relative z-10 bg-slate-950/40 border-t border-white/5">
        <div className="container">
          <div className="text-center max-w-3xl mx-auto mb-16 flex flex-col gap-4">
            <span className="text-xs font-mono font-bold text-primary uppercase tracking-widest">Analiză Matematică</span>
            <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-white font-display">
              Algoritmi și Fundamentare Matematică
            </h2>
            <p className="text-slate-400">
              Acuratețea sistemului F.O.C.U.S. AI se bazează pe modele matematice riguroase și analize spectrale ale semnalelor biologice.
            </p>
          </div>

          <MathFormulas />

          {/* Mini ECG Live încorporat ca suport vizual secundar */}
          <div className="mt-12 max-w-3xl mx-auto">
            <ECGMonitor />
          </div>
        </div>
      </section>

      {/* 8. REZULTATE EXPERIMENTALE */}
      <section id="rezultate" className="py-24 relative z-10 border-t border-white/5">
        <div className="container">
          <div className="text-center max-w-3xl mx-auto mb-16 flex flex-col gap-4">
            <span className="text-xs font-mono font-bold text-secondary uppercase tracking-widest">Rezultate Experimentale</span>
            <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-white font-display">
              Performanță Validată în Laborator
            </h2>
            <p className="text-slate-400">
              Testele riguroase efectuate pe prototipul F.O.C.U.S. AI demonstrează o latență extrem de redusă și o acuratețe remarcabilă a algoritmilor de detecție.
            </p>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6">
            {/* Rezultat 1 */}
            <div className="glass-panel rounded-2xl p-6 border-white/5 text-center flex flex-col gap-2">
              <span className="text-4xl font-extrabold text-primary font-display">30 FPS</span>
              <span className="text-xs font-mono text-slate-400 uppercase tracking-wider">Cadre pe secundă stabil</span>
              <p className="text-xs text-slate-500 mt-2">Procesare fluidă a fluxului video fără drop-uri de cadre pe hardware local.</p>
            </div>

            {/* Rezultat 2 */}
            <div className="glass-panel rounded-2xl p-6 border-white/5 text-center flex flex-col gap-2">
              <span className="text-4xl font-extrabold text-secondary font-display">85-90%</span>
              <span className="text-xs font-mono text-slate-400 uppercase tracking-wider">Acuratețe detecție</span>
              <p className="text-xs text-slate-500 mt-2">Precizie ridicată în estimarea privirii și corelarea cu semnalele ECG.</p>
            </div>

            {/* Rezultat 3 */}
            <div className="glass-panel rounded-2xl p-6 border-white/5 text-center flex flex-col gap-2">
              <span className="text-4xl font-extrabold text-primary font-display">&lt; 10 ms</span>
              <span className="text-xs font-mono text-slate-400 uppercase tracking-wider">Latență de achiziție</span>
              <p className="text-xs text-slate-500 mt-2">Timp de răspuns instantaneu de la senzori către unitatea centrală.</p>
            </div>

            {/* Rezultat 4 */}
            <div className="glass-panel rounded-2xl p-6 border-white/5 text-center flex flex-col gap-2">
              <span className="text-4xl font-extrabold text-secondary font-display">&lt; 200 ms</span>
              <span className="text-xs font-mono text-slate-400 uppercase tracking-wider">Sincronizare Cloud</span>
              <p className="text-xs text-slate-500 mt-2">Transmisia rapidă a datelor către Firebase pentru dashboard-ul mobil.</p>
            </div>
          </div>
        </div>
      </section>

      {/* 9. ECHIPA */}
      <section className="py-24 relative z-10 bg-slate-950/40 border-t border-white/5">
        <div className="container">
          <div className="text-center max-w-3xl mx-auto mb-16 flex flex-col gap-4">
            <span className="text-xs font-mono font-bold text-primary uppercase tracking-widest">Echipa Noastră</span>
            <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-white font-display">
              Creatorii F.O.C.U.S. AI
            </h2>
            <p className="text-slate-400">
              O echipă pasionată de ingineri, dezvoltatori și mentori care au colaborat pentru a aduce la viață acest proiect neuro-cognitiv inovator.
            </p>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-8 max-w-4xl mx-auto">
            {/* Membru 1 */}
            <div className="glass-panel rounded-3xl p-8 border-white/10 glow-cyan flex flex-col sm:flex-row gap-6 items-center">
              <div className="w-24 h-24 rounded-2xl bg-primary/10 border border-primary/20 flex items-center justify-center flex-shrink-0">
                <Users className="w-10 h-10 text-primary" />
              </div>
              <div>
                <h3 className="text-xl font-bold text-white font-display">Condrici Mihai</h3>
                <span className="text-xs font-mono text-primary font-bold block mt-1 uppercase">Lead Hardware Engineer & AI Developer</span>
                <p className="text-sm text-slate-400 mt-3">
                  Responsabil de proiectarea stației hardware, integrarea senzorilor Arduino și dezvoltarea algoritmului de eye-tracking în Python.
                </p>
              </div>
            </div>

            {/* Membru 2 */}
            <div className="glass-panel rounded-3xl p-8 border-white/10 glow-blue flex flex-col sm:flex-row gap-6 items-center">
              <div className="w-24 h-24 rounded-2xl bg-secondary/10 border border-secondary/20 flex items-center justify-center flex-shrink-0">
                <Users className="w-10 h-10 text-secondary" />
              </div>
              <div>
                <h3 className="text-xl font-bold text-white font-display">Pătrașc Matteo</h3>
                <span className="text-xs font-mono text-secondary font-bold block mt-1 uppercase">Lead Software Architect</span>
                <p className="text-sm text-slate-400 mt-3">
                  Responsabil de arhitectura ecosistemului software, dezvoltarea aplicației desktop WPF și integrarea bazei de date Firebase Cloud.
                </p>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* 10. GALERIE */}
      <section className="py-24 relative z-10 border-t border-white/5">
        <div className="container">
          <div className="text-center max-w-3xl mx-auto mb-16 flex flex-col gap-4">
            <span className="text-xs font-mono font-bold text-secondary uppercase tracking-widest">Galerie Proiect</span>
            <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-white font-display">
              Imagini și Scheme Tehnice
            </h2>
            <p className="text-slate-400">
              Vizualizează detaliile constructive ale stației hardware, randările CAD și capturile din aplicațiile software dezvoltate.
            </p>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
            {/* Imagine 1 */}
            <div className="glass-panel rounded-2xl overflow-hidden border-white/5 group">
              <div className="aspect-video bg-slate-950 relative overflow-hidden">
                <img 
                  src={projectImages.circuit} 
                  alt="Schema electronica F.O.C.U.S. AI" 
                  className="w-full h-full object-cover object-center group-hover:scale-105 transition-transform duration-500"
                />
              </div>
              <div className="p-4">
                <h4 className="font-bold text-white font-display">Schema Electronică Completă</h4>
                <p className="text-xs text-slate-400 mt-1">Arduino UNO, display LCD, MyoWare, MAX30100 și modulele de interacțiune.</p>
              </div>
            </div>

            {/* Imagine 2 */}
            <div className="glass-panel rounded-2xl overflow-hidden border-white/5 group">
              <div className="aspect-video bg-slate-950 relative overflow-hidden">
                <img
                  src={projectImages.mobileProfile}
                  alt="Interfata mobila F.O.C.U.S. AI pentru profil si NFC"
                  className="w-full h-full object-cover object-top group-hover:scale-105 transition-transform duration-500"
                />
              </div>
              <div className="p-4">
                <h4 className="font-bold text-white font-display">Profil Utilizator & NFC</h4>
                <p className="text-xs text-slate-400 mt-1">Ecranul de cont pentru identificare rapidă și configurarea tagului NFC.</p>
              </div>
            </div>

            {/* Imagine 3 */}
            <div className="glass-panel rounded-2xl overflow-hidden border-white/5 group">
              <div className="aspect-video bg-slate-950 relative overflow-hidden">
                <img
                  src={projectImages.mobileGames}
                  alt="Interfata mobila F.O.C.U.S. AI pentru jocuri cognitive"
                  className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-500"
                />
              </div>
              <div className="p-4">
                <h4 className="font-bold text-white font-display">Jocuri Cognitive</h4>
                <p className="text-xs text-slate-400 mt-1">Module de testare pentru reacție, inhibiție vizuală și antrenament atențional.</p>
              </div>
            </div>

            {/* Imagine 4 */}
            <div className="glass-panel rounded-2xl overflow-hidden border-white/5 group">
              <div className="aspect-video bg-slate-950 relative overflow-hidden">
                <img
                  src={projectImages.cad}
                  alt="Randare CAD pentru statia F.O.C.U.S. AI"
                  className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-500"
                />
              </div>
              <div className="p-4">
                <h4 className="font-bold text-white font-display">Randare CAD Stație</h4>
                <p className="text-xs text-slate-400 mt-1">Modelul carcasei și al suportului pentru componentele fizice.</p>
              </div>
            </div>

            {/* Imagine 5 */}
            <div className="glass-panel rounded-2xl overflow-hidden border-white/5 group">
              <div className="aspect-video bg-slate-950 relative overflow-hidden">
                <img
                  src={projectImages.formula}
                  alt="Formula scorului de performanta F.O.C.U.S. AI"
                  className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-500"
                />
              </div>
              <div className="p-4">
                <h4 className="font-bold text-white font-display">Formula Scorului</h4>
                <p className="text-xs text-slate-400 mt-1">Modelul de calcul pentru performanță, precizia privirii, reacție și HRV.</p>
              </div>
            </div>

            {/* Imagine 6 */}
            <div className="glass-panel rounded-2xl overflow-hidden border-white/5 group">
              <div className="aspect-video bg-slate-950 relative overflow-hidden">
                <img
                  src={projectImages.mobileLogin}
                  alt="Interfata de autentificare F.O.C.U.S. AI"
                  className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-500"
                />
              </div>
              <div className="p-4">
                <h4 className="font-bold text-white font-display">Autentificare Mobilă</h4>
                <p className="text-xs text-slate-400 mt-1">Ecranul de acces pentru aplicația companion a utilizatorului.</p>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* 11. TEHNOLOGII UTILIZATE */}
      <section className="py-24 relative z-10 bg-slate-950/40 border-t border-white/5">
        <div className="container">
          <div className="text-center max-w-3xl mx-auto mb-16 flex flex-col gap-4">
            <span className="text-xs font-mono font-bold text-primary uppercase tracking-widest">Tehnologii</span>
            <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-white font-display">
              Stack-ul Tehnologic Utilizat
            </h2>
            <p className="text-slate-400">
              Am selectat cu atenție cele mai performante tehnologii și limbaje de programare pentru a asigura stabilitate, viteză și precizie.
            </p>
          </div>

          <div className="flex flex-wrap justify-center gap-4 max-w-4xl mx-auto">
            {["Python", "OpenCV", "dlib", "Arduino", "Firebase", "Kotlin", "WPF", "C#", "C++", "AI / Machine Learning"].map((tech) => (
              <span 
                key={tech} 
                className="px-5 py-2.5 rounded-full bg-white/5 border border-white/10 text-slate-300 font-mono text-sm font-semibold hover:border-primary/50 hover:text-white transition-all duration-300"
              >
                {tech}
              </span>
            ))}
          </div>
        </div>
      </section>

      {/* 12. BIBLIOGRAFIE */}
      <section className="py-24 relative z-10 border-t border-white/5">
        <div className="container">
          <div className="text-center max-w-3xl mx-auto mb-16 flex flex-col gap-4">
            <span className="text-xs font-mono font-bold text-secondary uppercase tracking-widest">Bibliografie</span>
            <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-white font-display">
              Referințe Științifice și Documentații
            </h2>
            <p className="text-slate-400">
              Fundamentarea teoretică a proiectului F.O.C.U.S. AI se bazează pe lucrări academice și documentații tehnice oficiale de prestigiu.
            </p>
          </div>

          <div className="max-w-3xl mx-auto space-y-4">
            <div className="glass-panel rounded-2xl p-6 border-white/5 flex gap-4 items-start">
              <BookOpen className="w-5 h-5 text-secondary flex-shrink-0 mt-1" />
              <div>
                <h4 className="font-bold text-white">Gaze Estimation and Eye-Tracking Methods</h4>
                <p className="text-xs text-slate-400 mt-1">Studiu privind algoritmii moderni de estimare a privirii folosind repere faciale (OpenCV & dlib).</p>
              </div>
            </div>
            <div className="glass-panel rounded-2xl p-6 border-white/5 flex gap-4 items-start">
              <BookOpen className="w-5 h-5 text-secondary flex-shrink-0 mt-1" />
              <div>
                <h4 className="font-bold text-white">Heart Rate Variability (HRV) Analysis in Cognitive Stress</h4>
                <p className="text-xs text-slate-400 mt-1">Cercetare privind corelarea semnalelor ECG și a indicelui HRV cu nivelul de stres și atenție mentală.</p>
              </div>
            </div>
            <div className="glass-panel rounded-2xl p-6 border-white/5 flex gap-4 items-start">
              <BookOpen className="w-5 h-5 text-secondary flex-shrink-0 mt-1" />
              <div>
                <h4 className="font-bold text-white">Arduino & Biometric Sensors Official Documentation</h4>
                <p className="text-xs text-slate-400 mt-1">Ghidurile tehnice oficiale pentru integrarea senzorului MyoWare și a pulsoximetrului MAX30100.</p>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* 13. FOOTER */}
      <footer className="relative z-10 border-t border-white/10 bg-slate-950/80 py-12 mt-auto">
        <div className="container flex flex-col md:flex-row items-center justify-between gap-6">
          <div className="flex items-center gap-2">
            <Brain className="w-6 h-6 text-primary" />
            <span className="text-lg font-bold font-display text-white">
              F.O.C.U.S. <span className="text-primary">AI</span>
            </span>
          </div>
          
          <p className="text-xs text-slate-500 font-mono">
            © 2026 F.O.C.U.S. AI. Toate drepturile rezervate. Proiect realizat de Condrici Mihai & Pătrașc Matteo.
          </p>

          <div className="flex items-center gap-4">
            <a href="https://github.com/CEX-Gala-i-robotica/focus-ai" target="_blank" rel="noopener noreferrer" className="p-2 rounded-lg bg-white/5 border border-white/10 text-slate-400 hover:text-white transition-colors" title="GitHub Repository">
              <Github className="w-5 h-5" />
            </a>
            <a href="https://www.instagram.com/focuss._.ai?utm_source=ig_web_button_share_sheet&igsh=ZDNlZDc0MzIxNw==" target="_blank" rel="noopener noreferrer" className="p-2 rounded-lg bg-white/5 border border-white/10 text-slate-400 hover:text-white transition-colors" title="Instagram">
              <svg className="w-5 h-5" fill="currentColor" viewBox="0 0 24 24"><path d="M12 2.163c3.204 0 3.584.012 4.85.07 3.252.148 4.771 1.691 4.919 4.919.058 1.266.069 1.646.069 4.85 0 3.204-.012 3.584-.07 4.85-.149 3.225-1.664 4.771-4.919 4.919-1.266.058-1.644.07-4.85.07-3.204 0-3.584-.012-4.849-.07-3.26-.149-4.771-1.699-4.919-4.92-.058-1.265-.07-1.644-.07-4.849 0-3.204.013-3.583.07-4.849.149-3.227 1.664-4.771 4.919-4.919 1.266-.057 1.645-.069 4.849-.069zm0-2.163c-3.259 0-3.667.014-4.947.072-4.358.2-6.78 2.618-6.98 6.98-.059 1.281-.073 1.689-.073 4.948 0 3.259.014 3.668.072 4.948.2 4.358 2.618 6.78 6.98 6.98 1.281.058 1.689.072 4.948.072 3.259 0 3.668-.014 4.948-.072 4.354-.2 6.782-2.618 6.979-6.98.059-1.28.073-1.689.073-4.948 0-3.259-.014-3.667-.072-4.947-.196-4.354-2.617-6.78-6.979-6.98-1.281-.059-1.69-.073-4.949-.073zM5.838 12a6.162 6.162 0 1 1 12.324 0 6.162 6.162 0 0 1-12.324 0zM12 16a4 4 0 1 1 0-8 4 4 0 0 1 0 8zm4.965-10.322a1.44 1.44 0 1 1 2.881.001 1.44 1.44 0 0 1-2.881-.001z"/></svg>
            </a>
            <a href="mailto:contact@focusai.ro" className="p-2 rounded-lg bg-white/5 border border-white/10 text-slate-400 hover:text-white transition-colors" title="Email">
              <Mail className="w-5 h-5" />
            </a>
          </div>
        </div>
      </footer>
    </div>
  );
}
