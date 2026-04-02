import { motion } from 'motion/react';
import { Code, Cpu, Zap } from 'lucide-react';

export default function Technology() {
  const techCategories = [
    {
      icon: Code,
      title: 'Software',
      color: 'from-cyan-400 to-blue-500',
      technologies: [
        { name: '.NET WPF', description: 'Interfață desktop modernă' },
        { name: 'C#', description: 'Logică aplicație' },
        { name: 'Firebase', description: 'Bază de date cloud' },
      ]
    },
    {
      icon: Cpu,
      title: 'AI & Machine Learning',
      color: 'from-purple-500 to-pink-500',
      technologies: [
        { name: 'Python', description: 'Limbaj principal AI' },
        { name: 'OpenCV', description: 'Computer vision' },
        { name: 'MediaPipe', description: 'Detectare facială' },
        { name: 'NumPy & SciPy', description: 'Procesare date' }
      ]
    },
    {
      icon: Zap,
      title: 'Hardware',
      color: 'from-yellow-400 to-orange-500',
      technologies: [
        { name: 'Arduino Giga R1 Wifi', description: 'Microcontroler principal' },
        { name: 'Senzori de touch', description: 'Input utilizator' },
        { name: 'Buzzer', description: 'Stimul sonor' },
        { name: 'Pulsoximetru si ECG-uri', description: 'Feedback-ul corpului' }
      ]
    }
  ];

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
          <h2 className="text-5xl mb-4 bg-gradient-to-r from-cyan-400 to-purple-600 bg-clip-text text-transparent" style={{ fontWeight: 700 }}>
            Tehnologie
          </h2>
          <p className="text-xl text-gray-400 max-w-3xl mx-auto">
            Stack tehnologic modern pentru performanță maximă
          </p>
        </motion.div>

        <div className="grid md:grid-cols-3 gap-8">
          {techCategories.map((category, index) => (
            <motion.div
              key={index}
              initial={{ opacity: 0, y: 30 }}
              whileInView={{ opacity: 1, y: 0 }}
              viewport={{ once: true }}
              transition={{ duration: 0.5, delay: index * 0.1 }}
              className="relative group"
            >
              <div className="absolute inset-0 bg-gradient-to-br from-cyan-500/10 to-purple-500/10 rounded-3xl blur-xl group-hover:blur-2xl transition-all" />
              <div className="relative bg-gray-900/50 backdrop-blur-sm rounded-3xl border border-gray-700 hover:border-cyan-500/40 transition-all overflow-hidden h-full">
                <div className="p-8">
                  <div className={`w-16 h-16 bg-gradient-to-br ${category.color} rounded-xl flex items-center justify-center mb-6`}>
                    <category.icon size={32} className="text-white" />
                  </div>

                  <h3 className="text-2xl mb-6 text-white" style={{ fontWeight: 700 }}>
                    {category.title}
                  </h3>

                  <div className="space-y-4">
                    {category.technologies.map((tech, i) => (
                      <motion.div
                        key={i}
                        initial={{ opacity: 0, x: -20 }}
                        whileInView={{ opacity: 1, x: 0 }}
                        viewport={{ once: true }}
                        transition={{ duration: 0.5, delay: index * 0.1 + i * 0.05 }}
                        className="p-3 bg-gray-800/50 rounded-xl border border-gray-700/50 hover:border-cyan-500/30 transition-all"
                      >
                        <div className="flex items-start gap-3">
                          <div className={`w-2 h-2 mt-2 bg-gradient-to-r ${category.color} rounded-full`} />
                          <div>
                            <p className="text-white" style={{ fontWeight: 600 }}>
                              {tech.name}
                            </p>
                            <p className="text-sm text-gray-400">
                              {tech.description}
                            </p>
                          </div>
                        </div>
                      </motion.div>
                    ))}
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
