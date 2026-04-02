import { motion } from 'motion/react';
import { Sparkles, Target, Brain } from 'lucide-react';

export default function Solution() {
  return (
    <div className="py-20 px-6 bg-gradient-to-b from-black via-blue-950/20 to-black">
      <div className="max-w-6xl mx-auto">
        <motion.div
          initial={{ opacity: 0, y: 30 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true }}
          transition={{ duration: 0.8 }}
          className="text-center mb-16"
        >
          <h2 className="text-5xl mb-4 bg-gradient-to-r from-cyan-400 to-blue-500 bg-clip-text text-transparent" style={{ fontWeight: 700 }}>
            Soluția: FOCUS AI
          </h2>
          <p className="text-xl text-gray-400 max-w-3xl mx-auto">
            O platformă inteligentă care evaluează și antrenează capacitatea de concentrare
          </p>
        </motion.div>

        <div className="grid md:grid-cols-3 gap-8">
          {[
            {
              icon: Target,
              title: 'Evaluare precisă',
              description: 'Măsoară nivelul actual de atenție prin teste validate științific',
              color: 'from-cyan-500 to-blue-500'
            },
            {
              icon: Brain,
              title: 'Antrenament inteligent',
              description: 'Exerciții personalizate pentru îmbunătățirea concentrării',
              color: 'from-blue-500 to-purple-500'
            },
            {
              icon: Sparkles,
              title: 'Progres măsurabil',
              description: 'Urmărește evoluția capacității de concentrare în timp',
              color: 'from-purple-500 to-pink-500'
            }
          ].map((item, index) => (
            <motion.div
              key={index}
              initial={{ opacity: 0, y: 30 }}
              whileInView={{ opacity: 1, y: 0 }}
              viewport={{ once: true }}
              transition={{ duration: 0.5, delay: index * 0.1 }}
              whileHover={{ scale: 1.05 }}
              className="relative group"
            >
              <div className="absolute inset-0 bg-gradient-to-br from-cyan-500/10 to-purple-500/10 rounded-2xl blur-xl group-hover:blur-2xl transition-all" />
              <div className="relative p-8 bg-gray-900/50 backdrop-blur-sm rounded-2xl border border-cyan-500/20 hover:border-cyan-500/40 transition-all">
                <div className={`w-16 h-16 bg-gradient-to-br ${item.color} rounded-xl flex items-center justify-center mb-6`}>
                  <item.icon size={32} className="text-white" />
                </div>
                <h3 className="text-2xl mb-4 text-white" style={{ fontWeight: 600 }}>
                  {item.title}
                </h3>
                <p className="text-gray-400">
                  {item.description}
                </p>
              </div>
            </motion.div>
          ))}
        </div>
      </div>
    </div>
  );
}
