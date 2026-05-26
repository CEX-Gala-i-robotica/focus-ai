import { useState, useEffect } from "react";
import { Link } from "wouter";
import { motion, AnimatePresence } from "framer-motion";
import { Brain, Menu, X, Shield, Cpu, Activity } from "lucide-react";
import { Button } from "@/components/ui/button";

interface NavItem {
  name: string;
  href: string;
}

const navItems: NavItem[] = [
  { name: "Despre", href: "#despre" },
  { name: "Problemă", href: "#problema" },
  { name: "Hardware", href: "#hardware" },
  { name: "AI & Tracking", href: "#ai-tracking" },
  { name: "Software", href: "#software" },
  { name: "Matematică", href: "#matematica" },
  { name: "Rezultate", href: "#rezultate" },
];

export default function Navbar() {
  const [isScrolled, setIsScrolled] = useState(false);
  const [isOpen, setIsOpen] = useState(false);

  useEffect(() => {
    const handleScroll = () => {
      setIsScrolled(window.scrollY > 20);
    };
    window.addEventListener("scroll", handleScroll);
    return () => window.removeEventListener("scroll", handleScroll);
  }, []);

  return (
    <motion.header
      className={`fixed top-0 left-0 right-0 z-50 transition-all duration-300 ${
        isScrolled 
          ? "bg-background/80 backdrop-blur-md border-b border-white/10 py-4" 
          : "bg-transparent py-6"
      }`}
      initial={{ y: -100, opacity: 0 }}
      animate={{ y: 0, opacity: 1 }}
      transition={{ duration: 0.5, ease: "easeOut" }}
    >
      <div className="container flex items-center justify-between">
        {/* Logo */}
        <Link href="/" className="flex items-center gap-2 group">
          <div className="relative flex items-center justify-center w-10 h-10 rounded-xl bg-primary/10 border border-primary/20 group-hover:border-primary/50 transition-all duration-300">
            <Brain className="w-5 h-5 text-primary animate-pulse" />
            <div className="absolute inset-0 rounded-xl bg-primary/20 blur-md opacity-0 group-hover:opacity-100 transition-opacity duration-300" />
          </div>
          <span className="text-xl font-bold tracking-wider bg-clip-text text-transparent bg-gradient-to-r from-white via-slate-200 to-primary/80 font-display">
            F.O.C.U.S. <span className="text-primary">AI</span>
          </span>
        </Link>

        {/* Desktop Navigation */}
        <nav className="hidden xl:flex items-center gap-1 bg-white/5 border border-white/5 px-2 py-1.5 rounded-full backdrop-blur-md">
          {navItems.map((item) => (
            <a
              key={item.name}
              href={item.href}
              className="px-4 py-1.5 rounded-full text-sm font-medium text-slate-300 hover:text-white hover:bg-white/5 transition-all duration-200"
            >
              {item.name}
            </a>
          ))}
        </nav>

        {/* CTA Button */}
        <div className="hidden xl:flex items-center gap-4">
          <Button 
            variant="outline" 
            className="rounded-full border-primary/30 text-primary hover:bg-primary/10 hover:text-primary transition-all duration-300"
            onClick={() => {
              document.getElementById("demo")?.scrollIntoView({ behavior: "smooth" });
            }}
          >
            Vezi Demo
          </Button>
        </div>

        {/* Mobile Menu Button */}
        <button
          onClick={() => setIsOpen(!isOpen)}
          className="xl:hidden p-2 rounded-lg bg-white/5 border border-white/10 text-slate-300 hover:text-white transition-colors"
        >
          {isOpen ? <X className="w-6 h-6" /> : <Menu className="w-6 h-6" />}
        </button>
      </div>

      {/* Mobile Navigation Menu */}
      <AnimatePresence>
        {isOpen && (
          <motion.div
            initial={{ opacity: 0, height: 0 }}
            animate={{ opacity: 1, height: "auto" }}
            exit={{ opacity: 0, height: 0 }}
            className="xl:hidden bg-background/95 backdrop-blur-lg border-b border-white/10"
          >
            <div className="container py-6 flex flex-col gap-4">
              {navItems.map((item) => (
                <a
                  key={item.name}
                  href={item.href}
                  onClick={() => setIsOpen(false)}
                  className="px-4 py-2 rounded-lg text-lg font-medium text-slate-300 hover:text-white hover:bg-white/5 transition-all"
                >
                  {item.name}
                </a>
              ))}
              <hr className="border-white/10 my-2" />
              <Button 
                variant="default" 
                className="w-full rounded-full bg-gradient-to-r from-primary to-secondary hover:opacity-90 text-background font-bold"
                onClick={() => {
                  setIsOpen(false);
                  document.getElementById("demo")?.scrollIntoView({ behavior: "smooth" });
                }}
              >
                Vezi Demo
              </Button>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </motion.header>
  );
}
