import { motion } from 'motion/react';
import { Github } from 'lucide-react';
import LogoIcon from './LogoIcon';

interface NavbarProps {
    activeSection: string;
}

export default function Navbar({ activeSection }: NavbarProps) {
    const menuItems = [
        { id: 'acasa', label: 'Acasă' },
        { id: 'cum-functioneaza', label: 'Cum funcționează' },
        { id: 'tehnologie', label: 'Tehnologie' },
        { id: 'training', label: 'Training' },
        { id: 'impact', label: 'Impact' },
        { id: 'echipa', label: 'Echipă' },
    ];

    const scrollToSection = (id: string) => {
        const element = document.getElementById(id);
        if (element) {
            const navbarHeight = 80;
            const elementPosition = element.getBoundingClientRect().top + window.pageYOffset;
            const offsetPosition = elementPosition - navbarHeight;

            window.scrollTo({
                top: offsetPosition,
                behavior: 'smooth'
            });
        }
    };

    return (
        <motion.nav
            className="fixed top-0 left-0 right-0 z-50 bg-black/80 backdrop-blur-xl border-b border-cyan-500/20"
            initial={{ y: -100 }}
            animate={{ y: 0 }}
            transition={{ duration: 0.5 }}
        >
            <div className="max-w-7xl mx-auto px-6 py-4 flex items-center justify-between">
                <motion.div
                    className="flex items-center gap-3"
                    whileHover={{ scale: 1.05 }}
                >
                    <div className="w-10 h-10 bg-gradient-to-br from-cyan-400 to-purple-600 rounded-lg flex items-center justify-center">
                        <LogoIcon />
                    </div>
                    <span className="text-xl font-bold bg-gradient-to-r from-cyan-400 to-purple-600 bg-clip-text text-transparent">
            FOCUS AI
          </span>
                </motion.div>

                <div className="hidden md:flex items-center gap-8">
                    {menuItems.map((item) => (
                        <button
                            key={item.id}
                            onClick={() => scrollToSection(item.id)}
                            className={`relative transition-colors ${
                                activeSection === item.id
                                    ? 'text-cyan-400'
                                    : 'text-gray-400 hover:text-white'
                            }`}
                        >
                            {item.label}
                            {activeSection === item.id && (
                                <motion.div
                                    className="absolute -bottom-1 left-0 right-0 h-0.5 bg-gradient-to-r from-cyan-400 to-purple-600"
                                    layoutId="navbar-indicator"
                                />
                            )}
                        </button>
                    ))}

                    <a
                        href="https://github.com/CEX-Gala-i-robotica/focus-ai/"
                        target="_blank"
                        rel="noopener noreferrer"
                        className="flex items-center gap-2 px-4 py-2 bg-gradient-to-r from-cyan-500 to-purple-600 rounded-lg hover:shadow-lg hover:shadow-cyan-500/50 transition-all"
                    >
                        <Github size={18} />
                        <span>GitHub</span>
                    </a>
                </div>
            </div>
        </motion.nav>
    );
}