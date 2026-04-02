import { motion } from 'motion/react';
import { User } from 'lucide-react';

export default function Team() {
  const teamMembers = [
    {
      name: 'Condrici Mihai',
      role: 'clasa a X-a, Colegiul Național "Vasile Alecsandri" Galați, profil matematică-informatică / Centrul Judeţean de Excelență Galați',
      description: 'Realizator al aplicației WPF și modului Hardware',
      color: 'from-cyan-400 to-blue-500'
    },
    {
      name: 'Pătrașc Matteo',
      role: 'clasa a X-a, Colegiul Național "Costache Negri" Galați, profil matematică-informatică / Centrul Judeţean de Excelență Galați',
      description: 'Realizator al modului AI Python',
      color: 'from-blue-500 to-purple-500'
    },
    {
      name: 'Apostol Valeriu',
      role: 'Profesor coordonator',
      description: 'profesor de robotică la Liceul cu Program Sportiv Galați / Centrul Judeţean de Excelenţa Galaţi',
      color: 'from-purple-500 to-pink-500'
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
            Echipă
          </h2>
          <p className="text-xl text-gray-400 max-w-3xl mx-auto">
            Echipa din spatele proiectului FOCUS AI
          </p>
        </motion.div>

        <div className="grid md:grid-cols-3 gap-8">
          {teamMembers.map((member, index) => (
            <motion.div
              key={index}
              initial={{ opacity: 0, y: 30 }}
              whileInView={{ opacity: 1, y: 0 }}
              viewport={{ once: true }}
              transition={{ duration: 0.5, delay: index * 0.1 }}
              whileHover={{ scale: 1.05 }}
              className="relative group"
            >
              <div className="absolute inset-0 bg-gradient-to-br from-cyan-500/10 to-purple-500/10 rounded-3xl blur-xl group-hover:blur-2xl transition-all" />
              <div className="relative bg-gray-900/50 backdrop-blur-sm rounded-3xl border border-gray-700 hover:border-cyan-500/40 transition-all overflow-hidden">
                <div className="p-8">
                  <div className="mb-6 flex justify-center">
                    <div className={`w-24 h-24 bg-gradient-to-br ${member.color} rounded-full flex items-center justify-center`}>
                      <User size={48} className="text-white" />
                    </div>
                  </div>

                  <h3 className="text-2xl mb-3 text-center text-white" style={{ fontWeight: 700 }}>
                    {member.name}
                  </h3>

                  <p className="text-cyan-400 text-sm text-center mb-4">
                    {member.role}
                  </p>

                  <p className="text-gray-400 text-sm text-center">
                    {member.description}
                  </p>
                </div>
              </div>
            </motion.div>
          ))}
        </div>
      </div>
    </div>
  );
}
