import { motion } from 'motion/react';
import { TrendingDown, Smartphone, DollarSign } from 'lucide-react';

export default function Impact() {
  const statistics = [
    {
      icon: TrendingDown,
      value: '-33%',
      title: 'Scăderea capacității de concentrare',
      description: 'Durata medie de atenție a scăzut de la 12 secunde la 8 secunde',
      color: 'from-red-500 to-orange-500'
    },
    {
      icon: Smartphone,
      value: '2.617',
      title: 'Supra-stimulare digitală',
      description: 'Notificările constante fragmentează atenția',
      color: 'from-orange-500 to-yellow-500'
    },
    {
      icon: DollarSign,
      value: '$650B',
      title: 'Pierderi de productivitate',
      description: 'Distragerea atenției costă economia globală miliarde',
      color: 'from-yellow-500 to-red-500'
    }
  ];

  return (
    <div className="py-20 px-6 bg-gradient-to-b from-black via-red-950/20 to-black">
      <div className="max-w-6xl mx-auto">
        <motion.div
          initial={{ opacity: 0, y: 30 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true }}
          transition={{ duration: 0.8 }}
          className="text-center mb-16"
        >
          <h2 className="text-5xl mb-4 bg-gradient-to-r from-red-400 to-orange-500 bg-clip-text text-transparent" style={{ fontWeight: 700 }}>
            Criza atenției
          </h2>
          <p className="text-xl text-gray-400 max-w-3xl mx-auto">
            Tehnologia modernă a schimbat modul în care creierul procesează informația
          </p>
        </motion.div>

        <div className="grid md:grid-cols-3 gap-8">
          {statistics.map((stat, index) => (
            <motion.div
              key={index}
              initial={{ opacity: 0, y: 30 }}
              whileInView={{ opacity: 1, y: 0 }}
              viewport={{ once: true }}
              transition={{ duration: 0.5, delay: index * 0.1 }}
              whileHover={{ scale: 1.05 }}
              className="relative group"
            >
              <div className="absolute inset-0 bg-gradient-to-br from-red-500/10 to-orange-500/10 rounded-3xl blur-xl group-hover:blur-2xl transition-all" />
              <div className="relative bg-gray-900/50 backdrop-blur-sm rounded-3xl border border-red-500/20 hover:border-red-500/40 transition-all overflow-hidden">
                <div className="p-8">
                  <div className={`w-16 h-16 bg-gradient-to-br ${stat.color} rounded-xl flex items-center justify-center mb-6`}>
                    <stat.icon size={32} className="text-white" />
                  </div>

                  <motion.div
                    className={`text-5xl mb-4 bg-gradient-to-r ${stat.color} bg-clip-text text-transparent`}
                    style={{ fontWeight: 700 }}
                    initial={{ scale: 0.5 }}
                    whileInView={{ scale: 1 }}
                    viewport={{ once: true }}
                    transition={{ duration: 0.5, delay: index * 0.1 + 0.2 }}
                  >
                    {stat.value}
                  </motion.div>

                  <h3 className="text-xl mb-3 text-white" style={{ fontWeight: 600 }}>
                    {stat.title}
                  </h3>

                  <p className="text-gray-400 text-sm">
                    {stat.description}
                  </p>
                </div>
              </div>
            </motion.div>
          ))}
        </div>

        <motion.div
          initial={{ opacity: 0, y: 30 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true }}
          transition={{ duration: 0.8, delay: 0.5 }}
          className="mt-16 text-center"
        >
          <div className="inline-block p-8 bg-gradient-to-br from-red-500/10 to-orange-500/10 backdrop-blur-sm rounded-3xl border border-red-500/20">
            <p className="text-xl text-gray-300 max-w-2xl">
              FOCUS AI oferă soluții concrete pentru combaterea acestei crize, prin evaluare precisă și antrenament personalizat
            </p>
          </div>
        </motion.div>
      </div>
    </div>
  );
}
