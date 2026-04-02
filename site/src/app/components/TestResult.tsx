import { useEffect, useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { initializeApp } from 'firebase/app';
import { getDatabase, ref, get } from 'firebase/database';
import {
    LineChart,
    Line,
    ScatterChart,
    Scatter,
    XAxis,
    YAxis,
    CartesianGrid,
    Tooltip,
    ResponsiveContainer,
    ReferenceLine,
    ComposedChart
} from 'recharts';

// Firebase configuration
const firebaseConfig = {
    apiKey: import.meta.env.VITE_FIREBASE_API_KEY,
    authDomain: import.meta.env.VITE_FIREBASE_AUTH_DOMAIN,
    databaseURL: import.meta.env.VITE_FIREBASE_DATABASE_URL,
    projectId: import.meta.env.VITE_FIREBASE_PROJECT_ID,
    storageBucket: import.meta.env.VITE_FIREBASE_STORAGE_BUCKET,
    messagingSenderId: import.meta.env.VITE_FIREBASE_MESSAGING_SENDER_ID,
    appId: import.meta.env.VITE_FIREBASE_APP_ID,
};

const app = initializeApp(firebaseConfig);
const db = getDatabase(app);

interface UserProfile {
    name: string;
    surname: string;
    'birth-date': string;
    'doctor-email': string;
    'doctor-phone': string;
    'phone-number': string;
    setup: boolean;
}

interface TestData {
    dateTime: string;
    duration: string;
    map?: string;
    ecg?: string;
    hr?: string;
    spo2?: string;
    dist?: string;
    scor?: number;
    precizie_gonogo?: number;
    tr2?: number;
}

const parseXY = (raw: string): { x: number; y: number }[] => {
    if (!raw) return [];
    return raw.split(';')
        .map(pair => pair.trim().split(','))
        .filter(parts => parts.length === 2)
        .map(parts => ({ x: parseFloat(parts[0]), y: parseFloat(parts[1]) }))
        .filter(p => !isNaN(p.x) && !isNaN(p.y));
};

const parseValues = (raw: string): number[] => {
    if (!raw) return [];
    return raw.split(',')
        .map(v => parseFloat(v.trim()))
        .filter(v => !isNaN(v));
};

const parseECGPairs = (raw: string): { index: number; ch1: number; ch2: number }[] => {
    if (!raw) return [];
    const pairs = raw.split(';')
        .map(pair => pair.trim().split(','))
        .filter(parts => parts.length === 2)
        .map(parts => ({ ch1: parseFloat(parts[0]), ch2: parseFloat(parts[1]) }))
        .filter(p => !isNaN(p.ch1) && !isNaN(p.ch2));

    return pairs.map((p, idx) => ({ index: idx, ch1: p.ch1, ch2: p.ch2 }));
};

export default function TestResult() {
    const { uid, test_id } = useParams<{ uid: string; test_id: string }>();
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [userProfile, setUserProfile] = useState<UserProfile | null>(null);
    const [testData, setTestData] = useState<TestData | null>(null);

    useEffect(() => {
        if (!uid || !test_id) {
            setError('Missing UID or test ID');
            setLoading(false);
            return;
        }

        const fetchData = async () => {
            try {
                const profileRef = ref(db, `${uid}/profile`);
                const testRef = ref(db, `${uid}/tests/${test_id}`);
                const [profileSnap, testSnap] = await Promise.all([
                    get(profileRef),
                    get(testRef),
                ]);

                if (!profileSnap.exists()) {
                    setError('User profile not found');
                    setLoading(false);
                    return;
                }
                if (!testSnap.exists()) {
                    setError('Test not found');
                    setLoading(false);
                    return;
                }

                setUserProfile(profileSnap.val() as UserProfile);
                setTestData(testSnap.val() as TestData);
            } catch (err) {
                console.error(err);
                setError('Failed to load data. Please try again later.');
            } finally {
                setLoading(false);
            }
        };

        fetchData();
    }, [uid, test_id]);

    if (loading) {
        return (
            <div className="min-h-screen bg-black text-white flex items-center justify-center">
                <div className="text-xl">Loading...</div>
            </div>
        );
    }

    if (error) {
        return (
            <div className="min-h-screen bg-black text-white flex items-center justify-center flex-col">
                <div className="text-red-500 text-xl mb-4">{error}</div>
                <Link to="/" className="text-blue-500 hover:text-blue-400 underline">
                    Return to Home
                </Link>
            </div>
        );
    }

    if (!userProfile || !testData) {
        return (
            <div className="min-h-screen bg-black text-white flex items-center justify-center">
                <div>No data found</div>
            </div>
        );
    }

    const mapPoints = parseXY(testData.map || '');
    const ecgData = parseECGPairs(testData.ecg || '');
    const hrVals = parseValues(testData.hr || '');
    const spo2Vals = parseValues(testData.spo2 || '');
    const distVals = parseValues(testData.dist || '');

    // Prepare data for charts
    const hrData = hrVals.map((val, idx) => ({ index: idx, value: val }));
    const spo2Data = spo2Vals.map((val, idx) => ({ index: idx, value: val }));
    const distData = distVals.map((val, idx) => ({ index: idx, active: val > 0 ? 1 : 0 }));

    const mapMinX = mapPoints.length > 0 ? Math.min(...mapPoints.map(p => p.x)) : 0;
    const mapMaxX = mapPoints.length > 0 ? Math.max(...mapPoints.map(p => p.x)) : 0;
    const mapMinY = mapPoints.length > 0 ? Math.min(...mapPoints.map(p => p.y)) : 0;
    const mapMaxY = mapPoints.length > 0 ? Math.max(...mapPoints.map(p => p.y)) : 0;

    const ecgMinY = ecgData.length > 0 ? Math.min(...ecgData.flatMap(d => [d.ch1, d.ch2])) : 0;
    const ecgMaxY = ecgData.length > 0 ? Math.max(...ecgData.flatMap(d => [d.ch1, d.ch2])) : 0;

    const stats = {
        mapCount: mapPoints.length,
        ecgCount: ecgData.length,
        spo2Min: spo2Vals.filter(v => v > 0).length > 0 ? Math.min(...spo2Vals.filter(v => v > 0)) : 0,
        spo2Max: spo2Vals.length > 0 ? Math.max(...spo2Vals) : 0,
        hrMin: hrVals.filter(v => v > 0).length > 0 ? Math.min(...hrVals.filter(v => v > 0)) : 0,
        hrMax: hrVals.length > 0 ? Math.max(...hrVals) : 0,
        distActive: distVals.filter(v => v > 0).length,
        distTotal: distVals.length,
    };

    return (
        <div className="min-h-screen bg-gray-900 text-white">
            <div className="max-w-7xl mx-auto px-4 py-8">
                <h1 className="text-3xl font-bold mb-8">Test Results</h1>

                {/* User Profile */}
                <div className="bg-gray-800 rounded-lg p-6 mb-6">
                    <h2 className="text-xl font-semibold mb-4 border-b border-gray-700 pb-2">User Profile</h2>
                    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                        <div><strong>Name:</strong> {userProfile.name} {userProfile.surname}</div>
                        <div><strong>Birth Date:</strong> {userProfile['birth-date']}</div>
                        <div><strong>Phone:</strong> {userProfile['phone-number']}</div>
                        <div><strong>Doctor Email:</strong> {userProfile['doctor-email']}</div>
                        <div><strong>Doctor Phone:</strong> {userProfile['doctor-phone']}</div>
                        <div><strong>Test ID:</strong> {test_id}</div>
                        <div><strong>Date & Time:</strong> {testData.dateTime}</div>
                        <div><strong>Duration:</strong> {testData.duration}</div>
                        {testData.scor !== undefined && <div><strong>Score:</strong> {testData.scor}</div>}
                        {testData.precizie_gonogo !== undefined && <div><strong>Accuracy:</strong> {testData.precizie_gonogo}%</div>}
                        {testData.tr2 !== undefined && <div><strong>tr²:</strong> {testData.tr2}</div>}
                    </div>
                </div>

                {/* Statistics Bar */}
                <div className="bg-gray-800 rounded-lg p-4 mb-6">
                    <div className="grid grid-cols-2 md:grid-cols-5 gap-4 text-center">
                        <div>
                            <div className="text-sm text-gray-400">MAP Points</div>
                            <div className="text-xl font-bold text-cyan-400">{stats.mapCount}</div>
                        </div>
                        <div>
                            <div className="text-sm text-gray-400">ECG Points</div>
                            <div className="text-xl font-bold text-purple-400">{stats.ecgCount}</div>
                        </div>
                        <div>
                            <div className="text-sm text-gray-400">SpO₂ Range</div>
                            <div className="text-xl font-bold text-blue-400">{stats.spo2Min}–{stats.spo2Max}%</div>
                        </div>
                        <div>
                            <div className="text-sm text-gray-400">HR Range</div>
                            <div className="text-xl font-bold text-orange-400">{stats.hrMin}–{stats.hrMax} bpm</div>
                        </div>
                        <div>
                            <div className="text-sm text-gray-400">DIST Active</div>
                            <div className="text-xl font-bold text-indigo-400">{stats.distActive}/{stats.distTotal}</div>
                        </div>
                    </div>
                </div>

                {/* MAP Scatter Plot */}
                {mapPoints.length > 0 && (
                    <div className="bg-gray-800 rounded-lg p-6 mb-6">
                        <h2 className="text-xl font-semibold mb-4 border-b border-gray-700 pb-2">MAP Scatter Plot</h2>
                        <div className="h-[400px] w-full">
                            <ResponsiveContainer width="100%" height="100%">
                                <ScatterChart margin={{ top: 20, right: 20, bottom: 20, left: 20 }}>
                                    <CartesianGrid strokeDasharray="3 3" stroke="#374151" />
                                    <XAxis
                                        type="number"
                                        dataKey="x"
                                        name="X"
                                        domain={[mapMinX - 10, mapMaxX + 10]}
                                        stroke="#9CA3AF"
                                        tick={{ fill: '#9CA3AF' }}
                                    />
                                    <YAxis
                                        type="number"
                                        dataKey="y"
                                        name="Y"
                                        domain={[mapMinY - 10, mapMaxY + 10]}
                                        stroke="#9CA3AF"
                                        tick={{ fill: '#9CA3AF' }}
                                    />
                                    <Tooltip
                                        contentStyle={{ backgroundColor: '#1F2937', border: 'none', borderRadius: '8px' }}
                                        labelStyle={{ color: '#9CA3AF' }}
                                    />
                                    <ReferenceLine x={0} stroke="#4DFFDF" strokeWidth={1.5} />
                                    <ReferenceLine y={0} stroke="#4DFFDF" strokeWidth={1.5} />
                                    <Scatter
                                        name="MAP Points"
                                        data={mapPoints}
                                        fill="#4DFFDF"
                                        fillOpacity={0.8}
                                        shape="circle"
                                    />
                                </ScatterChart>
                            </ResponsiveContainer>
                        </div>
                    </div>
                )}

                {/* ECG Chart - Two Channels */}
                {ecgData.length > 0 && (
                    <div className="bg-gray-800 rounded-lg p-6 mb-6">
                        <h2 className="text-xl font-semibold mb-4 border-b border-gray-700 pb-2">ECG - Dual Channel</h2>
                        <div className="h-[400px] w-full">
                            <ResponsiveContainer width="100%" height="100%">
                                <LineChart margin={{ top: 20, right: 20, bottom: 20, left: 20 }}>
                                    <CartesianGrid strokeDasharray="3 3" stroke="#374151" />
                                    <XAxis
                                        dataKey="index"
                                        type="number"
                                        domain={[0, ecgData.length - 1]}
                                        stroke="#9CA3AF"
                                        tick={{ fill: '#9CA3AF' }}
                                    />
                                    <YAxis
                                        domain={[ecgMinY - 20, ecgMaxY + 20]}
                                        stroke="#9CA3AF"
                                        tick={{ fill: '#9CA3AF' }}
                                    />
                                    <Tooltip
                                        contentStyle={{ backgroundColor: '#1F2937', border: 'none', borderRadius: '8px' }}
                                        labelStyle={{ color: '#9CA3AF' }}
                                    />
                                    <Line
                                        type="monotone"
                                        dataKey="ch1"
                                        data={ecgData}
                                        stroke="#4DFFDF"
                                        strokeWidth={1.5}
                                        dot={false}
                                        name="Channel 1"
                                    />
                                    <Line
                                        type="monotone"
                                        dataKey="ch2"
                                        data={ecgData}
                                        stroke="#FF4DC8"
                                        strokeWidth={1.5}
                                        dot={false}
                                        name="Channel 2"
                                    />
                                </LineChart>
                            </ResponsiveContainer>
                        </div>
                        <div className="flex justify-center gap-8 mt-4 text-sm">
                            <div className="flex items-center gap-2">
                                <div className="w-4 h-0.5 bg-cyan-400"></div>
                                <span>Channel 1 (Left)</span>
                            </div>
                            <div className="flex items-center gap-2">
                                <div className="w-4 h-0.5 bg-pink-500"></div>
                                <span>Channel 2 (Right)</span>
                            </div>
                        </div>
                    </div>
                )}

                {/* SpO2 Chart */}
                {spo2Data.length > 0 && (
                    <div className="bg-gray-800 rounded-lg p-6 mb-6">
                        <h2 className="text-xl font-semibold mb-4 border-b border-gray-700 pb-2">SpO₂ (%)</h2>
                        <div className="h-[300px] w-full">
                            <ResponsiveContainer width="100%" height="100%">
                                <LineChart data={spo2Data} margin={{ top: 20, right: 20, bottom: 20, left: 20 }}>
                                    <CartesianGrid strokeDasharray="3 3" stroke="#374151" />
                                    <XAxis dataKey="index" stroke="#9CA3AF" tick={{ fill: '#9CA3AF' }} />
                                    <YAxis domain={[80, 100]} stroke="#9CA3AF" tick={{ fill: '#9CA3AF' }} />
                                    <Tooltip
                                        contentStyle={{ backgroundColor: '#1F2937', border: 'none', borderRadius: '8px' }}
                                        labelStyle={{ color: '#9CA3AF' }}
                                    />
                                    <Line
                                        type="monotone"
                                        dataKey="value"
                                        stroke="#4D9FFF"
                                        strokeWidth={2}
                                        dot={false}
                                        fill="url(#colorGradient)"
                                    />
                                </LineChart>
                            </ResponsiveContainer>
                        </div>
                    </div>
                )}

                {/* Heart Rate Chart */}
                {hrData.length > 0 && (
                    <div className="bg-gray-800 rounded-lg p-6 mb-6">
                        <h2 className="text-xl font-semibold mb-4 border-b border-gray-700 pb-2">Heart Rate (bpm)</h2>
                        <div className="h-[300px] w-full">
                            <ResponsiveContainer width="100%" height="100%">
                                <LineChart data={hrData} margin={{ top: 20, right: 20, bottom: 20, left: 20 }}>
                                    <CartesianGrid strokeDasharray="3 3" stroke="#374151" />
                                    <XAxis dataKey="index" stroke="#9CA3AF" tick={{ fill: '#9CA3AF' }} />
                                    <YAxis stroke="#9CA3AF" tick={{ fill: '#9CA3AF' }} />
                                    <Tooltip
                                        contentStyle={{ backgroundColor: '#1F2937', border: 'none', borderRadius: '8px' }}
                                        labelStyle={{ color: '#9CA3AF' }}
                                    />
                                    <Line
                                        type="monotone"
                                        dataKey="value"
                                        stroke="#FF6B4D"
                                        strokeWidth={2}
                                        dot={false}
                                        fill="url(#colorGradient)"
                                    />
                                </LineChart>
                            </ResponsiveContainer>
                        </div>
                    </div>
                )}

                {/* DIST Event Axis */}
                {distData.length > 0 && (
                    <div className="bg-gray-800 rounded-lg p-6">
                        <h2 className="text-xl font-semibold mb-4 border-b border-gray-700 pb-2">DIST - Active Moments</h2>
                        <div className="h-[100px] w-full">
                            <ResponsiveContainer width="100%" height="100%">
                                <ComposedChart data={distData} margin={{ top: 20, right: 20, bottom: 20, left: 20 }}>
                                    <CartesianGrid strokeDasharray="3 3" stroke="#374151" />
                                    <XAxis dataKey="index" stroke="#9CA3AF" tick={{ fill: '#9CA3AF' }} />
                                    <YAxis domain={[0, 1]} stroke="#9CA3AF" tick={{ fill: '#9CA3AF' }} />
                                    <Tooltip
                                        contentStyle={{ backgroundColor: '#1F2937', border: 'none', borderRadius: '8px' }}
                                        labelStyle={{ color: '#9CA3AF' }}
                                    />
                                    <ReferenceLine y={0.5} stroke="#A0A0FF" strokeDasharray="3 3" />
                                    <Scatter
                                        dataKey="active"
                                        data={distData.filter(d => d.active === 1)}
                                        fill="#A0A0FF"
                                        shape="circle"
                                    />
                                </ComposedChart>
                            </ResponsiveContainer>
                        </div>
                        <div className="text-center text-sm text-gray-400 mt-4">
                            Active events: {stats.distActive} / {stats.distTotal}
                        </div>
                    </div>
                )}
            </div>
        </div>
    );
}