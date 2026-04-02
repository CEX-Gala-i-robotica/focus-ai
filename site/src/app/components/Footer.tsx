import { motion } from 'motion/react';
import { Mail, Github } from 'lucide-react';
import LogoIcon from './LogoIcon';

export default function Footer() {
    return (
        <footer className="py-12 px-6 bg-black border-t border-gray-800">
            <div className="max-w-6xl mx-auto">
                <div className="grid md:grid-cols-3 gap-8 mb-8">
                    <div>
                        <div className="flex items-center gap-3 mb-4">
                            <div className="w-10 h-10 bg-gradient-to-br from-cyan-400 to-purple-600 rounded-lg flex items-center justify-center">
                                <LogoIcon />
                            </div>
                            <span className="text-xl font-bold bg-gradient-to-r from-cyan-400 to-purple-600 bg-clip-text text-transparent">
                FOCUS AI
              </span>
                        </div>
                        <p className="text-gray-400 text-sm">
                            Platformă inteligentă pentru evaluarea și antrenarea atenției, dezvoltată de elevi pasionați de tehnologie și AI.
                        </p>
                    </div>

                    <div>
                        <h4 className="text-white mb-4" style={{ fontWeight: 600 }}>
                            Contact
                        </h4>
                        <a
                            href="mailto:binaryteam.galati@gmail.com"
                            className="flex items-center gap-2 text-gray-400 hover:text-cyan-400 transition-colors mb-2"
                        >
                            <Mail size={18} />
                            <span>binaryteam.galati@gmail.com</span>
                        </a>
                    </div>

                    <div>
                        <h4 className="text-white mb-4" style={{ fontWeight: 600 }}>
                            Resurse
                        </h4>
                        <a
                            href="https://github.com/CEX-Gala-i-robotica/focus-ai/"
                            target="_blank"
                            rel="noopener noreferrer"
                            className="flex items-center gap-2 text-gray-400 hover:text-cyan-400 transition-colors"
                        >
                            <Github size={18} />
                            <span>Repository GitHub</span>
                        </a>
                    </div>
                </div>

                <motion.div
                    className="pt-8 border-t border-gray-800 text-center text-gray-500 text-sm"
                    initial={{ opacity: 0 }}
                    whileInView={{ opacity: 1 }}
                    viewport={{ once: true }}
                    transition={{ duration: 0.8 }}
                >
                    <p>© 2026 FOCUS AI - Centrul Județean de Excelență Galați</p>
                    <p className="mt-2">Proiect pentru Olimpiada Națională de Creativitate Științifică dezvoltat cu pasiune pentru tehnologie și educație</p>
                </motion.div>
            </div>
        </footer>
    );
}