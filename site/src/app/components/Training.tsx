import { motion } from 'motion/react';
import { Brain, Eye, Target, Repeat, Calculator } from 'lucide-react';

export default function Training() {
  const games = [
    {
      icon: Brain,
      title: 'Memorie',
      subtitle: 'Identificarea perechilor',
      description: 'Dezvoltă memoria și atenția vizuală',
      color: 'from-cyan-400 to-blue-500'
    },
    {
      icon: Target,
      title: 'Stroop Test',
      subtitle: 'Ignorarea sensului cuvântului',
      description: 'Îmbunătățește controlul cognitiv',
      color: 'from-blue-500 to-purple-500'
    },
    {
      icon: Eye,
      title: 'Visual Search',
      subtitle: 'Găsirea elementelor diferite',
      description: 'Crește viteza de procesare vizuală',
      color: 'from-purple-500 to-pink-500'
    },
    {
      icon: Repeat,
      title: 'Secvențe',
      subtitle: 'Memorarea și reproducerea pattern-urilor',
      description: 'Dezvoltă memoria de lucru',
      color: 'from-pink-500 to-red-500'
    },
    {
      icon: Calculator,
      title: 'Matematică rapidă',
      subtitle: 'Calcul sub presiune de timp',
      description: 'Stimulează gândirea logică și focusul',
      color: 'from-cyan-500 to-teal-500'
    }
  ];

  return (
    <div className="py-20 px-6 bg-gradient-to-b from-black via-purple-950/20 to-black">
      <div className="max-w-6xl mx-auto">
        <motion.div
          initial={{ opacity: 0, y: 30 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true }}
          transition={{ duration: 0.8 }}
          className="text-center mb-16"
        >
          <h2 className="text-5xl mb-4 bg-gradient-to-r from-cyan-400 to-purple-600 bg-clip-text text-transparent" style={{ fontWeight: 700 }}>
            Training
          </h2>
          <p className="text-xl text-gray-400 max-w-3xl mx-auto">
            Exerciții cognitive pentru îmbunătățirea atenției și concentrării
          </p>
        </motion.div>

        <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-6">
          {games.map((game, index) => (
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
              <div className="relative bg-gray-900/50 backdrop-blur-sm rounded-2xl border border-gray-700 hover:border-cyan-500/40 transition-all overflow-hidden h-full">
                <div className="p-6 flex flex-col h-full">
                  <div className={`w-14 h-14 bg-gradient-to-br ${game.color} rounded-xl flex items-center justify-center mb-4`}>
                    <game.icon size={28} className="text-white" />
                  </div>

                  <h3 className="text-2xl mb-2 text-white" style={{ fontWeight: 700 }}>
                    {game.title}
                  </h3>

                  <p className="text-cyan-400 mb-3">
                    {game.subtitle}
                  </p>

                  <p className="text-gray-400 text-sm flex-grow">
                    {game.description}
                  </p>

                  <div className={`mt-6 h-1 w-full bg-gradient-to-r ${game.color} rounded-full`} />
                </div>
              </div>
            </motion.div>
          ))}
        </div>
      </div>
    </div>
  );
}
