import { motion } from 'motion/react';
import { Smartphone, TrendingDown, Brain } from 'lucide-react';

export default function Problem() {
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
          <h2 className="text-5xl mb-4 bg-gradient-to-r from-red-400 to-orange-500 bg-clip-text text-transparent" style={{ fontWeight: 700 }}>
            Problema
          </h2>
          <p className="text-xl text-gray-400 max-w-3xl mx-auto">
            Tehnologia modernă a schimbat modul în care creierul procesează informația
          </p>
        </motion.div>

        <div className="grid md:grid-cols-3 gap-8">
          {[
            {
              icon: TrendingDown,
              title: 'Scăderea atenției',
              description: 'Durata medie de atenție a scăzut de la 12 secunde la 8 secunde',
              color: 'from-red-500 to-orange-500'
            },
            {
              icon: Smartphone,
              title: 'Supra-stimulare digitală',
              description: 'Notificările constante fragmentează atenția și distrug focusul',
              color: 'from-orange-500 to-yellow-500'
            },
            {
              icon: Brain,
              title: 'Impact cognitiv',
              description: 'Afectează capacitatea de concentrare și procesare a informațiilor',
              color: 'from-yellow-500 to-red-500'
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
              <div className="absolute inset-0 bg-gradient-to-br from-red-500/10 to-orange-500/10 rounded-2xl blur-xl group-hover:blur-2xl transition-all" />
              <div className="relative p-8 bg-gray-900/50 backdrop-blur-sm rounded-2xl border border-red-500/20 hover:border-red-500/40 transition-all">
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
