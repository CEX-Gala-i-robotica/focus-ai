import { motion } from 'motion/react';
import { Eye, Timer, Target } from 'lucide-react';

export default function HowItWorks() {
  const tests = [
    {
      icon: Eye,
      title: 'Eye Tracking',
      subtitle: 'Atenție vizuală',
      description: 'Urmărirea mișcărilor ochilor pentru evaluarea capacității de concentrare vizuală și menținerea atenției asupra unui punct focal',
      features: [
        'Detectarea direcției privirii',
        'Măsurarea stabilității atenției',
        'Identificarea distracțiilor'
      ],
      color: 'from-cyan-400 to-blue-500',
      diagram: (
        <div className="relative w-full h-48 bg-gradient-to-br from-cyan-500/10 to-blue-500/10 rounded-xl flex items-center justify-center border border-cyan-500/30">
          <div className="relative">
            <motion.div
              className="w-20 h-12 border-2 border-cyan-400 rounded-full"
              animate={{ scale: [1, 1.1, 1] }}
              transition={{ duration: 2, repeat: Infinity }}
            />
            <motion.div
              className="absolute top-1/2 left-1/2 w-3 h-3 bg-cyan-400 rounded-full"
              style={{ x: '-50%', y: '-50%' }}
              animate={{
                x: ['-50%', '0%', '-50%', '-100%', '-50%'],
                y: ['-50%', '-100%', '-50%', '0%', '-50%']
              }}
              transition={{ duration: 4, repeat: Infinity }}
            />
          </div>
        </div>
      )
    },
    {
      icon: Timer,
      title: 'Timp de reacție',
      subtitle: 'Viteză de procesare',
      description: 'Măsurarea rapidității cu care creierul procesează informații și răspunde la stimuli vizuali sau auditivi',
      features: [
        'Teste de reacție vizuală',
        'Evaluarea vitezei cognitive',
        'Măsurarea consistenței'
      ],
      color: 'from-blue-500 to-purple-500',
      diagram: (
        <div className="relative w-full h-48 bg-gradient-to-br from-blue-500/10 to-purple-500/10 rounded-xl flex items-center justify-center border border-blue-500/30">
          <div className="flex items-center gap-8">
            <motion.div
              className="w-16 h-16 bg-gradient-to-br from-blue-400 to-purple-500 rounded-lg"
              animate={{ scale: [1, 1.2, 1], rotate: [0, 180, 360] }}
              transition={{ duration: 2, repeat: Infinity }}
            />
            <div className="flex flex-col gap-2">
              {[0, 1, 2].map((i) => (
                <motion.div
                  key={i}
                  className="w-24 h-2 bg-gradient-to-r from-blue-400 to-purple-500 rounded-full"
                  initial={{ scaleX: 0 }}
                  animate={{ scaleX: 1 }}
                  transition={{ duration: 0.5, delay: i * 0.3, repeat: Infinity, repeatDelay: 1 }}
                  style={{ transformOrigin: 'left' }}
                />
              ))}
            </div>
          </div>
        </div>
      )
    },
    {
      icon: Target,
      title: 'Controlul atenției',
      subtitle: 'Test verde/roșu',
      description: 'Exercițiu de inhibiție cognitivă care evaluează capacitatea de a ignora informații irelevante și de a menține focusul pe sarcina principală',
      features: [
        'Control inhibitor',
        'Rezistență la distragere',
        'Flexibilitate cognitivă'
      ],
      color: 'from-purple-500 to-pink-500',
      diagram: (
        <div className="relative w-full h-48 bg-gradient-to-br from-purple-500/10 to-pink-500/10 rounded-xl flex items-center justify-center border border-purple-500/30">
          <div className="flex gap-4">
            <motion.div
              className="w-20 h-20 bg-green-500 rounded-xl flex items-center justify-center text-2xl"
              animate={{ scale: [1, 1.1, 1], opacity: [1, 0.7, 1] }}
              transition={{ duration: 1.5, repeat: Infinity }}
            >
              ✓
            </motion.div>
            <motion.div
              className="w-20 h-20 bg-red-500 rounded-xl flex items-center justify-center text-2xl"
              animate={{ scale: [1, 1.1, 1], opacity: [1, 0.7, 1] }}
              transition={{ duration: 1.5, repeat: Infinity, delay: 0.75 }}
            >
              ✗
            </motion.div>
          </div>
        </div>
      )
    }
  ];

  return (
    <div className="py-20 px-6 bg-gradient-to-b from-black via-gray-900/50 to-black">
      <div className="max-w-6xl mx-auto">
        <motion.div
          initial={{ opacity: 0, y: 30 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true }}
          transition={{ duration: 0.8 }}
          className="text-center mb-16"
        >
          <h2 className="text-5xl mb-4 bg-gradient-to-r from-cyan-400 to-purple-600 bg-clip-text text-transparent" style={{ fontWeight: 700 }}>
            Cum funcționează
          </h2>
          <p className="text-xl text-gray-400 max-w-3xl mx-auto">
            Trei teste științifice pentru evaluarea completă a atenției
          </p>
        </motion.div>

        <div className="space-y-12">
          {tests.map((test, index) => (
            <motion.div
              key={index}
              initial={{ opacity: 0, x: index % 2 === 0 ? -30 : 30 }}
              whileInView={{ opacity: 1, x: 0 }}
              viewport={{ once: true }}
              transition={{ duration: 0.8 }}
              className="relative group"
            >
              <div className="absolute inset-0 bg-gradient-to-br from-cyan-500/5 to-purple-500/5 rounded-3xl blur-xl" />
              <div className="relative bg-gray-900/50 backdrop-blur-sm rounded-3xl border border-gray-700 hover:border-cyan-500/40 transition-all overflow-hidden">
                <div className="grid md:grid-cols-2 gap-8 p-8">
                  <div className="space-y-6">
                    <div className="flex items-center gap-4">
                      <div className={`w-16 h-16 bg-gradient-to-br ${test.color} rounded-xl flex items-center justify-center`}>
                        <test.icon size={32} className="text-white" />
                      </div>
                      <div>
                        <h3 className="text-3xl text-white" style={{ fontWeight: 700 }}>
                          {test.title}
                        </h3>
                        <p className="text-cyan-400">
                          {test.subtitle}
                        </p>
                      </div>
                    </div>

                    <p className="text-gray-300 leading-relaxed">
                      {test.description}
                    </p>

                    <div className="space-y-3">
                      {test.features.map((feature, i) => (
                        <div key={i} className="flex items-center gap-3">
                          <div className={`w-2 h-2 bg-gradient-to-r ${test.color} rounded-full`} />
                          <span className="text-gray-400">{feature}</span>
                        </div>
                      ))}
                    </div>
                  </div>

                  <div className="flex items-center justify-center">
                    {test.diagram}
                  </div>
                </div>
              </div>
            </motion.div>
          ))}
        </div>
      </div>
    </div>
  );
}
