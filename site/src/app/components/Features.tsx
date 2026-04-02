import { motion } from 'motion/react';
import { Eye, Timer, Target, Brain, Zap, MemoryStick } from 'lucide-react';

export default function Features() {
  const features = [
    {
      icon: Eye,
      title: 'Eye Tracking',
      description: 'Tehnologie avansată de urmărire a privirii pentru evaluarea atenției vizuale',
      color: 'from-cyan-400 to-blue-500'
    },
    {
      icon: Timer,
      title: 'Măsurarea timpului de reacție',
      description: 'Teste precise pentru evaluarea vitezei de procesare a informațiilor',
      color: 'from-blue-500 to-purple-500'
    },
    {
      icon: Target,
      title: 'Control atenției',
      description: 'Exerciții pentru îmbunătățirea capacității de concentrare și focus',
      color: 'from-purple-500 to-pink-500'
    },
    {
      icon: Brain,
      title: 'Antrenament cognitiv',
      description: 'Jocuri și exerciții pentru dezvoltarea abilităților mentale',
      color: 'from-pink-500 to-red-500'
    },
    {
      icon: Zap,
      title: 'Rezultate în timp real',
      description: 'Feedback instant pentru fiecare test și exercițiu realizat',
      color: 'from-cyan-500 to-teal-500'
    },
    {
      icon: MemoryStick,
      title: 'Salvare progres',
      description: 'Toate datele sunt salvate automat pentru urmărirea evoluției',
      color: 'from-teal-500 to-green-500'
    }
  ];

  return (
    <div className="py-20 px-6">
      <div className="max-w-6xl mx-auto">
        <motion.div
          initial={{ opacity: 0, y: 30 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true }}
          transition={{ duration: 0.8 }}
          className="text-center mb-16"
        >
          <h2 className="text-5xl mb-4 bg-gradient-to-r from-cyan-400 to-purple-600 bg-clip-text text-transparent" style={{ fontWeight: 700 }}>
            Funcționalități
          </h2>
        </motion.div>

        <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-6">
          {features.map((feature, index) => (
            <motion.div
              key={index}
              initial={{ opacity: 0, y: 30 }}
              whileInView={{ opacity: 1, y: 0 }}
              viewport={{ once: true }}
              transition={{ duration: 0.5, delay: index * 0.1 }}
              whileHover={{ scale: 1.05, y: -5 }}
              className="relative group"
            >
              <div className="absolute inset-0 bg-gradient-to-br from-cyan-500/10 to-purple-500/10 rounded-2xl blur-xl group-hover:blur-2xl transition-all" />
              <div className="relative p-6 bg-gray-900/50 backdrop-blur-sm rounded-2xl border border-gray-700 hover:border-cyan-500/40 transition-all h-full">
                <div className={`w-14 h-14 bg-gradient-to-br ${feature.color} rounded-xl flex items-center justify-center mb-4`}>
                  <feature.icon size={28} className="text-white" />
                </div>
                <h3 className="text-xl mb-3 text-white" style={{ fontWeight: 600 }}>
                  {feature.title}
                </h3>
                <p className="text-gray-400 text-sm">
                  {feature.description}
                </p>
              </div>
            </motion.div>
          ))}
        </div>
      </div>
    </div>
  );
}
