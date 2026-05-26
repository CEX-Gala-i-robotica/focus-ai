import { Link } from "wouter";
import { Brain, ArrowLeft } from "lucide-react";
import { Button } from "@/components/ui/button";

export default function NotFound() {
  return (
    <div className="min-h-screen bg-background text-foreground flex flex-col items-center justify-center p-6 relative overflow-hidden">
      {/* Background glow decors */}
      <div className="absolute top-1/4 left-1/4 w-96 h-96 bg-primary/10 rounded-full blur-3xl pointer-events-none" />
      <div className="absolute bottom-1/4 right-1/4 w-96 h-96 bg-secondary/10 rounded-full blur-3xl pointer-events-none" />

      <div className="relative z-10 text-center flex flex-col items-center max-w-md">
        <div className="w-16 h-16 rounded-2xl bg-primary/10 border border-primary/20 flex items-center justify-center mb-6">
          <Brain className="w-8 h-8 text-primary animate-pulse" />
        </div>

        <h1 className="text-6xl font-extrabold font-display text-white mb-2">404</h1>
        <h2 className="text-xl font-bold font-display text-slate-200 mb-4">Pagina nu a fost găsită</h2>
        
        <p className="text-slate-400 text-sm mb-8 leading-relaxed">
          Semnalul neuro-cognitiv a fost întrerupt. Pagina pe care o cauți nu există sau a fost mutată în alt sector al rețelei F.O.C.U.S. AI.
        </p>

        <Link href="/">
          <Button className="rounded-full bg-gradient-to-r from-primary to-secondary text-background font-bold px-6 py-2 flex items-center gap-2 hover:opacity-90 transition-all">
            <ArrowLeft className="w-4 h-4" />
            Înapoi la stația principală
          </Button>
        </Link>
      </div>
    </div>
  );
}
