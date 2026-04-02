import { useState, useEffect } from 'react';
import { Routes, Route, useLocation } from 'react-router-dom';
import Navbar from './components/Navbar';
import Hero from './components/Hero';
import Problem from './components/Problem';
import Solution from './components/Solution';
import Features from './components/Features';
import HowItWorks from './components/HowItWorks';
import Technology from './components/Technology';
import Training from './components/Training';
import Impact from './components/Impact';
import Team from './components/Team';
import Footer from './components/Footer';
import TestResult from './components/TestResult';

export default function App() {
    const [activeSection, setActiveSection] = useState('acasa');
    const location = useLocation();

    useEffect(() => {
        const handleScroll = () => {
            // Only handle scroll on main page
            if (location.pathname !== '/') return;

            const sections = ['acasa', 'cum-functioneaza', 'tehnologie', 'training', 'impact', 'echipa', 'github'];
            const scrollPosition = window.scrollY + 100;

            for (const section of sections) {
                const element = document.getElementById(section);
                if (element) {
                    const offsetTop = element.offsetTop;
                    const offsetBottom = offsetTop + element.offsetHeight;

                    if (scrollPosition >= offsetTop && scrollPosition < offsetBottom) {
                        setActiveSection(section);
                        break;
                    }
                }
            }
        };

        window.addEventListener('scroll', handleScroll);
        return () => window.removeEventListener('scroll', handleScroll);
    }, [location.pathname]);

    // Landing page component
    const LandingPage = () => (
        <>
            <Navbar activeSection={activeSection} />
            <main>
                <section id="acasa">
                    <Hero />
                    <Problem />
                    <Solution />
                    <Features />
                </section>

                <section id="cum-functioneaza">
                    <HowItWorks />
                </section>

                <section id="tehnologie">
                    <Technology />
                </section>

                <section id="training">
                    <Training />
                </section>

                <section id="impact">
                    <Impact />
                </section>

                <section id="echipa">
                    <Team />
                </section>
            </main>
            <Footer />
        </>
    );

    return (
        <div className="min-h-screen bg-black text-white">
            <Routes>
                <Route path="/" element={<LandingPage />} />
                <Route path="/testresult/:uid/:test_id" element={<TestResult />} />
            </Routes>
        </div>
    );
}