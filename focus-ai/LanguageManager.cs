using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace focus_ai
{
    public enum AppLanguage
    {
        English,
        Romanian
    }

    public static class LanguageManager
    {
        private const string RegPath = @"Software\FocusAI";
        private const string RegKey = "Language";
        private static readonly Dictionary<string, string> EnToRo = new(StringComparer.Ordinal)
        {
            ["Focus AI - Dashboard"] = "Focus AI - Panou",
            ["Focus AI - Login"] = "Focus AI - Autentificare",
            ["Focus AI - Sign Up"] = "Focus AI - Inregistrare",
            ["Set up profile"] = "Configureaza profilul",

            ["Toggle theme"] = "Schimba tema",
            ["Switch to Romanian"] = "Schimba in romana",
            ["Switch to English"] = "Schimba in engleza",
            ["MENU"] = "MENIU",
            ["MENIU"] = "MENIU",
            ["Doctor profile"] = "Profil medic",
            ["Active account"] = "Cont activ",
            ["Doctor account"] = "Cont medic",
            ["Edit profile"] = "Editeaza profilul",
            ["✏️  Edit profile"] = "✏️  Editeaza profilul",
            ["Patients"] = "Pacienti",
            ["Patient tests"] = "Testele pacientului",
            ["Patient activities"] = "Activitatile pacientului",
            ["Connected"] = "Conectat",
            ["Sign out"] = "Deconectare",

            ["Medical account details and associated patient summary"] = "Date cont medical si sumarul pacientilor asociati",
            ["Personal details"] = "Date personale",
            ["PERSONAL DETAILS"] = "DATE PERSONALE",
            ["First name"] = "Prenume",
            ["Last name"] = "Nume",
            ["Birth date"] = "Data nasterii",
            ["Phone"] = "Telefon",
            ["Phone number"] = "Numar de telefon",
            ["Office address"] = "Adresa cabinetului",
            ["Email"] = "Email",
            ["Password"] = "Parola",
            ["Remember me"] = "Tine-ma minte",
            ["Forgot password?"] = "Ai uitat parola?",
            ["Sign in"] = "Autentificare",
            ["Sign up"] = "Inregistrare",
            ["Create account"] = "Creeaza cont",
            ["Create a doctor account"] = "Creeaza un cont de medic",
            ["Sign in to your account"] = "Autentifica-te in contul tau",
            ["Welcome back"] = "Bine ai revenit",
            ["or"] = "sau",
            ["Continue with Google"] = "Continua cu Google",
            ["No account? Sign up"] = "Nu ai cont? Inregistreaza-te",
            ["Already have an account? Sign in"] = "Ai deja cont? Autentifica-te",
            ["Processing..."] = "Se proceseaza...",
            ["Warning"] = "Avertizare",
            ["Error"] = "Eroare",
            ["Success"] = "Succes",
            ["Sign-in error"] = "Eroare autentificare",
            ["Sign-up error"] = "Eroare inregistrare",
            ["Password reset"] = "Resetare parola",
            ["Email and password are required."] = "Emailul si parola sunt obligatorii.",
            ["Incorrect email or password."] = "Email sau parola incorecta.",
            ["The account has been disabled."] = "Contul a fost dezactivat.",
            ["Google sign-in failed."] = "Autentificarea cu Google a esuat.",
            ["Enter your email address to reset your password."] = "Introdu adresa de email pentru resetarea parolei.",
            ["Password reset email sent. Check your inbox."] = "Emailul de resetare a fost trimis. Verifica inboxul.",
            ["Could not send the reset email. Check the address you entered."] = "Nu s-a putut trimite emailul de resetare. Verifica adresa introdusa.",
            ["Fill in all fields for the doctor account."] = "Completeaza toate campurile pentru contul de medic.",
            ["The email address is not valid."] = "Adresa de email nu este valida.",
            ["The phone number is not valid."] = "Numarul de telefon nu este valid.",
            ["The password must be at least 6 characters long."] = "Parola trebuie sa aiba cel putin 6 caractere.",
            ["Account created successfully. You can sign in now."] = "Contul a fost creat cu succes. Te poti autentifica acum.",
            ["Could not save data to Firebase."] = "Nu s-au putut salva datele in Firebase.",

            ["Associated patients"] = "Pacienti asociati",
            ["Patients associated with your medical account"] = "Pacienti asociati contului tau medical",
            ["TOTAL PATIENTS"] = "TOTAL PACIENTI",
            ["POSITIVE EVOLUTION"] = "EVOLUTIE POZITIVA",
            ["NEEDS ATTENTION"] = "NECESITA ATENTIE",
            ["PATIENT"] = "PACIENT",
            ["TESTS"] = "TESTE",
            ["ACTIVITIES"] = "ACTIVITATI",
            ["PREDICTION"] = "PREDICTIE",
            ["ACTION"] = "ACTIUNE",
            ["Scan NFC"] = "Scaneaza NFC",
            ["Selected"] = "Selectat",
            ["This patient does not have an NFC UID configured."] = "Acest pacient nu are un UID NFC configurat.",
            ["Scan the NFC card for {0} on {1}."] = "Scaneaza cardul NFC pentru {0} pe {1}.",
            ["Could not read NFC on {0}."] = "Nu s-a putut citi NFC pe {0}.",
            ["No NFC tag was detected. Try again and keep the tag close to the reader."] = "Nu a fost detectat niciun tag NFC. Incearca din nou si tine tagul aproape de cititor.",
            ["NFC card does not match this patient."] = "Cardul NFC nu corespunde acestui pacient.",
            ["Expected:"] = "Asteptat:",
            ["Scanned:"] = "Scanat:",
            ["Select a patient from the patient list first."] = "Selecteaza mai intai un pacient din lista.",

            ["Tests"] = "Teste",
            ["Activities"] = "Activitati",
            ["New test"] = "Test nou",
            ["New activity"] = "Activitate noua",
            ["Refresh"] = "Reincarca",
            ["BEST SCORE"] = "CEL MAI BUN SCOR",
            ["AVERAGE SCORE"] = "SCOR MEDIU",
            ["LAST TEST"] = "ULTIMUL TEST",
            ["DATE AND TIME"] = "DATA SI ORA",
            ["DURATION"] = "DURATA",
            ["SCORE"] = "SCOR",
            ["DETAILS"] = "DETALII",
            ["Details"] = "Detalii",
            ["No tests yet"] = "Nu exista teste inca",
            ["No activities yet"] = "Nu exista activitati inca",
            ["Loading..."] = "Se incarca...",
            ["Selected patient:"] = "Pacient selectat:",
            ["Doctor patients"] = "Pacientii medicului",
            ["Average tests"] = "Media testelor",
            ["Average activities"] = "Media activitatilor",
            ["Average test score"] = "Scor mediu la teste",
            ["Average activity score"] = "Scor mediu la activitati",
            ["Best activity score"] = "Cel mai bun scor la activitati",
            ["STATISTICS"] = "STATISTICI",
            ["STATISTICI"] = "STATISTICI",
            ["Reload"] = "Reincarca",
            ["⟳  Reload"] = "⟳  Reincarca",
            ["PATIENTS"] = "PACIENTI",
            ["POSITIVE TREND"] = "TENDINTA POZITIVA",
            ["ATTENTION"] = "ATENTIE",
            ["Loading patients..."] = "Se incarca pacientii...",
            ["No associated patients"] = "Nu exista pacienti asociati",
            ["Patients appear here after they are associated with the doctor in Firebase."] = "Pacientii apar aici dupa asocierea cu medicul in Firebase.",
            ["TESTS"] = "TESTE",
            ["Loading tests..."] = "Se incarca testele...",
            ["No tests recorded"] = "Nu exista teste inregistrate",
            ["Patient tests will appear here."] = "Testele pacientului vor aparea aici.",
            ["Select a patient to view test history"] = "Selecteaza un pacient pentru istoricul testelor",
            ["Select a patient to view activity history"] = "Selecteaza un pacient pentru istoricul activitatilor",
            ["🎮  New game"] = "🎮  Joc nou",
            ["New game"] = "Joc nou",
            ["LAST ACTIVITY"] = "ULTIMA ACTIVITATE",
            ["TOTAL SESSIONS"] = "TOTAL SESIUNI",
            ["Loading activities..."] = "Se incarca activitatile...",
            ["No activities recorded"] = "Nu exista activitati inregistrate",
            ["Start a cognitive training game to view history."] = "Porneste un joc de antrenament cognitiv pentru a vedea istoricul.",
            ["GAME"] = "JOC",
            ["DIFFICULTY"] = "DIFICULTATE",

            ["Cancel"] = "Anuleaza",
            ["Save changes"] = "Salveaza modificarile",
            ["Continue"] = "Continua",
            ["First name is required."] = "Prenumele este obligatoriu.",
            ["Last name is required."] = "Numele este obligatoriu.",
            ["Birth date is required."] = "Data nasterii este obligatorie.",
            ["Invalid format. Example: +40 722 000 000"] = "Format invalid. Exemplu: +40 722 000 000",
            ["Office address is required."] = "Adresa cabinetului este obligatorie.",

            ["Easy"] = "Usor",
            ["Medium"] = "Mediu",
            ["Hard"] = "Dificil",
            ["← Back"] = "← Inapoi",
            ["← Back to menu"] = "← Inapoi la meniu",
            ["🔄  Play again"] = "🔄  Joaca din nou",
            ["▶  New game"] = "▶  Joc nou",
            ["▶  Start test"] = "▶  Porneste testul",
            ["▶  Start Test"] = "▶  Porneste testul",
            ["Choose a game"] = "Alege un joc",
            ["Select a cognitive training game"] = "Selecteaza un joc de antrenament cognitiv",
            ["Memory"] = "Memorie",
            ["🧠  Memory"] = "🧠  Memorie",
            ["Train short-term memory with matching card pairs."] = "Antreneaza memoria de scurta durata cu perechi de carti.",
            ["Find all matching card pairs"] = "Gaseste toate perechile potrivite",
            ["Stroop Test"] = "Test Stroop",
            ["Identify the word color while ignoring its meaning."] = "Identifica culoarea cuvantului ignorand sensul lui.",
            ["Visual Search"] = "Cautare vizuala",
            ["Find the odd item hidden among similar items."] = "Gaseste elementul diferit ascuns intre elemente similare.",
            ["Sequences"] = "Secvente",
            ["Memorize and repeat increasingly long sequences."] = "Memoreaza si repeta secvente din ce in ce mai lungi.",
            ["Quick Math"] = "Matematica rapida",
            ["Solve math operations as quickly as possible."] = "Rezolva operatii matematice cat mai repede.",
            ["Solve as many math operations as possible in 60 seconds."] = "Rezolva cat mai multe operatii in 60 de secunde.",
            ["CHOOSE DIFFICULTY"] = "ALEGE DIFICULTATEA",
            ["Recommended start"] = "Start recomandat",
            ["Moderate challenge"] = "Provocare medie",
            ["Expert"] = "Expert",
            ["What is the result?"] = "Care este rezultatul?",
            ["Press Enter or ✓ to confirm"] = "Apasa Enter sau ✓ pentru confirmare",
            ["Correct: "] = "Corecte: ",
            ["Wrong: "] = "Gresite: ",
            ["Accuracy: "] = "Precizie: ",
            ["Excellent!"] = "Excelent!",
            ["Good!"] = "Bine!",
            ["Well done!"] = "Foarte bine!",
            ["Final score: 0 / 100"] = "Scor final: 0 / 100",
            ["Score: 0"] = "Scor: 0",
            ["Congratulations!"] = "Felicitari!",
            ["TIME"] = "TIMP",
            ["MOVES"] = "MISCARI",
            ["PAIRS"] = "PERECHI",
            ["Difficulty:"] = "Dificultate:",
            ["TIME LEFT"] = "TIMP RAMAS",
            ["Waiting"] = "In asteptare",
            ["Ready for the GO/NO-GO test?"] = "Gata pentru testul GO/NO-GO?",
            ["The test lasts 30 seconds. Press the sensor ONLY when GREEN appears (GO). Ignore RED (NO-GO). False reactions are penalized."] = "Testul dureaza 30 de secunde. Apasa senzorul DOAR cand apare VERDE (GO). Ignora ROSU (NO-GO). Reactiile false sunt penalizate.",
            ["Visual inhibition test"] = "Test de inhibitie vizuala",
            ["Press the sensor ONLY when the screen is GREEN. Ignore RED stimuli."] = "Apasa senzorul DOAR cand ecranul este VERDE. Ignora stimulii ROSII.",
            ["Sound Stimulus Reaction Test"] = "Test de reactie la stimul sonor",
            ["Press Start and react as quickly as possible to the sound cue."] = "Apasa Start si reactioneaza cat mai repede la semnalul sonor.",
            ["Ready for test"] = "Gata pentru test",
            ["✅  CORRECT GO"] = "✅  GO CORECTE",
            ["❌  FALSE POSITIVES"] = "❌  POZITIVE FALSE",
            ["⚡  AVERAGE TIME"] = "⚡  TIMP MEDIU",
            ["+, - with numbers 1-20"] = "+, - cu numere 1-20",
            ["+, -, x with numbers 1-50"] = "+, -, x cu numere 1-50",
            ["+, -, x, / with numbers 1-100"] = "+, -, x, / cu numere 1-100",
            ["✅ Correct: "] = "✅ Corecte: ",
            ["❌ Wrong: "] = "❌ Gresite: ",
            ["📊 Accuracy: "] = "📊 Precizie: ",
            ["Easy (4x4)"] = "Usor (4x4)",
            ["Medium (4x5)"] = "Mediu (4x5)",
            ["Hard (4x6)"] = "Dificil (4x6)",
            ["S S S S S S S S S S\nS S S S 5 S S S S S\nS S S S S S S S S S"] = "S S S S S S S S S S\nS S S S 5 S S S S S\nS S S S S S S S S S",
            ["Test Details"] = "Detalii test",
            ["TEST DETAILS · EVOLUTION CHARTS"] = "DETALII TEST · GRAFICE EVOLUTIE",
            ["ECG  (x1 - left · x2 - right)"] = "ECG  (x1 - stanga · x2 - dreapta)",
            ["DIST  (active moments)"] = "DIST  (momente active)",
            ["Focus AI - Reaction Test"] = "Focus AI - Test reactie",
            ["Focus AI - Quick Math"] = "Focus AI - Matematica rapida",
            ["Focus AI - Memory"] = "Focus AI - Memorie",
            ["Focus AI - Sequences"] = "Focus AI - Secvente",
            ["Focus AI - Cognitive Test"] = "Focus AI - Test cognitiv",
            ["Focus AI - Visual Search"] = "Focus AI - Cautare vizuala",
            ["Focus AI – Stroop Test"] = "Focus AI – Test Stroop",
            ["Focus AI – GO/NO-GO"] = "Focus AI – GO/NO-GO",
            ["LEVEL"] = "NIVEL",
            ["LIVES"] = "VIETI",
            ["ROUND"] = "RUNDA",
            ["PREVIEW"] = "PREVIZUALIZARE",
            ["Find The Odd One"] = "Gaseste elementul diferit",
            ["8 rounds  •  3 lives"] = "8 runde  •  3 vieti",
            ["Easy  (25s)"] = "Usor  (25s)",
            ["Medium  (20s)"] = "Mediu  (20s)",
            ["Hard  (15s)"] = "Dificil  (15s)",
            ["Round 1/8"] = "Runda 1/8",
            ["🔢  Sequences"] = "🔢  Secvente",
            ["Memorize and repeat the button sequence"] = "Memoreaza si repeta secventa de butoane",
            ["Watch the highlighted button sequence and repeat it in the same order. The sequence gets longer with each level. The game ends after 20 levels or when you run out of lives."] = "Urmareste secventa de butoane evidentiata si repet-o in aceeasi ordine. Secventa devine mai lunga la fiecare nivel. Jocul se termina dupa 20 de niveluri sau cand ramai fara vieti.",
            ["▶  Start game"] = "▶  Porneste jocul",
            ["🚀  Start game"] = "🚀  Porneste jocul",
            ["Watch the sequence..."] = "Urmareste secventa...",
            ["🏠  Main menu"] = "🏠  Meniu principal",
            ["COGNITIVE ASSESSMENT"] = "EVALUARE COGNITIVA",
            ["Cognitive test"] = "Test cognitiv",
            ["Complete all 3 stages in any order. The timer starts when the first stage begins."] = "Completeaza cele 3 etape in orice ordine. Cronometrul porneste cand incepe prima etapa.",
            ["Complete all 4 stages in any order. The timer starts when the first stage begins."] = "Completeaza cele 4 etape in orice ordine. Cronometrul porneste cand incepe prima etapa.",
            ["TOTAL TIME"] = "TIMP TOTAL",
            ["Running"] = "Ruleaza",
            ["Completed"] = "Finalizat",
            ["Not completed"] = "Nefinalizat",
            ["Eye tracking"] = "Urmarire oculara",
            ["Follow a moving target with your eyes. The system records gaze trajectory and eye movement accuracy."] = "Urmareste cu ochii o tinta in miscare. Sistemul inregistreaza traiectoria privirii si acuratetea miscarilor oculare.",
            ["Sound reaction"] = "Reactie sonora",
            ["Respond as quickly as possible to the sound cue. Measures reaction time to auditory stimuli."] = "Raspunde cat mai repede la semnalul sonor. Masoara timpul de reactie la stimuli auditivi.",
            ["GO/NO-GO visual reaction"] = "Reactie vizuala GO/NO-GO",
            ["Respond to GO stimuli and inhibit reactions to NO-GO stimuli. Evaluates inhibitory control and visual processing speed."] = "Raspunde la stimulii GO si inhiba reactiile la stimulii NO-GO. Evalueaza controlul inhibitor si viteza de procesare vizuala.",
            ["X/AX-CPT hybrid"] = "X/AX-CPT hibrid",
            ["Respond only when X appears immediately after A. Measures sustained attention, working memory and inhibitory control."] = "Raspunde doar cand X apare imediat dupa A. Masoara atentia sustinuta, memoria de lucru si controlul inhibitor.",
            ["▶  Start stage"] = "▶  Porneste etapa",
            ["0 / 3 stages completed"] = "0 / 3 etape finalizate",
            ["0 / 4 stages completed"] = "0 / 4 etape finalizate",
            ["✕  Cancel test"] = "✕  Anuleaza testul",
            ["The color matters, not the word!"] = "Conteaza culoarea, nu cuvantul!",
            ["HOW TO PLAY"] = "CUM SE JOACA",
            ["A colored word appears on screen."] = "Pe ecran apare un cuvant colorat.",
            ["Press the button matching the word color, not its meaning."] = "Apasa butonul care corespunde culorii cuvantului, nu sensului lui.",
            ["You have 30 questions. Each correct answer gives points; speed matters."] = "Ai 30 de intrebari. Fiecare raspuns corect ofera puncte; viteza conteaza.",
            ["Round 1 / 30"] = "Runda 1 / 30",
            ["sec"] = "sec",
            ["0 correct"] = "0 corecte",
            ["0 wrong"] = "0 gresite",
            ["Score "] = "Scor ",
            ["🔥 3 in a row!"] = "🔥 3 la rand!",
            ["RED"] = "ROSU",
            ["Press!"] = "Apasa!",
            ["Do not press"] = "Nu apasa",
            ["Test finalizat!"] = "Test finalizat!",
            ["✕  Close"] = "✕  Inchide",
            ["Correct GO:"] = "GO corecte:",
            ["Missed GO:"] = "GO ratate:",
            ["False positives:"] = "Pozitive false:",
            ["Correct NO-GO:"] = "NO-GO corecte:",
            ["Average reaction time:"] = "Timp mediu de reactie:",
            ["Level"] = "Nivel",
            ["Repeat the sequence!"] = "Repeta secventa!",
            ["Correct! Current score:"] = "Corect! Scor curent:",
            ["Wrong! You have"] = "Gresit! Mai ai",
            ["left. Replaying the sequence..."] = "ramase. Reluam secventa...",
            ["Congratulations! You completed all levels!"] = "Felicitari! Ai completat toate nivelurile!",
            ["Game Over!"] = "Joc terminat!",
            ["Game Over"] = "Joc terminat",
            ["Well played!"] = "Bine jucat!",
            ["Level reached:"] = "Nivel atins:",
            ["Score:"] = "Scor:",
            ["Difficulty:"] = "Dificultate:",
            ["Final score:"] = "Scor final:",
            ["stages completed"] = "etape finalizate",
            ["All stages are complete!"] = "Toate etapele sunt finalizate!",
            ["Total time:"] = "Timp total:",
            ["Do you want to save the results to Firebase?"] = "Vrei sa salvezi rezultatele in Firebase?",
            ["Test completed"] = "Test finalizat",
            ["in a row!"] = "la rand!",
            ["correct"] = "corecte",
            ["wrong"] = "gresite",
            ["Round"] = "Runda",
            ["Keep going!"] = "Continua!",
            ["Accuracy:"] = "Precizie:",
            ["Practice"] = "Practica",
            ["Test"] = "Test",
            ["Main test"] = "Test principal",
            ["Hybrid CPT - Attention & Inhibitory Control Test"] = "Hybrid CPT - Test de atentie si control inhibitor",
            ["instructions"] = "instructiuni",
            ["countdown"] = "numaratoare inversa",
            ["practice"] = "practica",
            ["practice_feedback"] = "feedback practica",
            ["main_test"] = "test principal",
            ["fixation"] = "fixare",
            ["results"] = "rezultate",
            ["PROGRESS"] = "PROGRES",
            ["Trial {0} / {1}"] = "Trial {0} / {1}",
            ["In this test, letters will appear one at a time on the screen."] = "In acest test, literele vor aparea una cate una pe ecran.",
            ["Press SPACE:"] = "Apasa SPACE:",
            ["- when you see a target X"] = "- cand vezi un X tinta",
            ["- or when X appears after A"] = "- sau cand X apare dupa A",
            ["Respond:"] = "Raspunde:",
            ["- as quickly as possible"] = "- cat mai rapid",
            ["- but as accurately as possible"] = "- dar cat mai corect",
            ["Do NOT press:"] = "NU apasa:",
            ["- for other letters"] = "- pentru alte litere",
            ["- for X after letters other than A (example: BX, CX)"] = "- pentru X dupa alte litere decat A (exemplu: BX, CX)",
            ["The test measures:"] = "Testul masoara:",
            ["- attention"] = "- atentia",
            ["- reaction speed"] = "- viteza de reactie",
            ["- impulse control"] = "- controlul impulsurilor",
            ["- working memory"] = "- memoria de lucru",
            ["Start practice"] = "Incepe practica",
            ["Fixation"] = "Fixare",
            ["Results Dashboard"] = "Panou rezultate",
            ["Mean Reaction Time"] = "Timp mediu de reactie",
            ["Total Hits"] = "Hit-uri totale",
            ["Total Misses"] = "Ratari totale",
            ["Total False Alarms"] = "Alarme false totale",
            ["Correct Rejections"] = "Respingeri corecte",
            ["Automatic Interpretation"] = "Interpretare automata",
            ["Finish"] = "Finalizeaza",
            ["Get ready"] = "Pregateste-te",
            ["Practice: feedback is shown after each response."] = "Practica: feedback-ul este afisat dupa fiecare raspuns.",
            ["Test: respond only to X after A."] = "Test: raspunde doar la X dupa A.",
            ["Main test: respond only when the target rule is met."] = "Test principal: raspunde doar cand regula tinta este indeplinita.",
            ["Correct"] = "Corect",
            ["Miss"] = "Ratare",
            ["False alarm"] = "Alarma falsa",
            ["CORRECT"] = "CORECT",
            ["TOO SLOW"] = "PREA LENT",
            ["MISSED TARGET"] = "AI RATAT TARGETUL",
            ["DO NOT PRESS"] = "NU TREBUIA SA APESI",
            ["Accuracy"] = "Precizie",
            ["Mean RT"] = "RT mediu",
            ["Hit Rate"] = "Rata raspunsurilor corecte",
            ["False Alarm Rate"] = "Rata alarmelor false",
            ["CPT completed."] = "CPT finalizat.",
            ["High accuracy + low RT: good attention and efficient processing."] = "Precizie mare + RT mic: atentie buna si procesare eficienta.",
            ["Many false alarms: increased impulsivity / reduced inhibitory control."] = "Multe alarme false: impulsivitate crescuta / control inhibitor redus.",
            ["Many misses: possible sustained attention deficit."] = "Multe ratari: posibil deficit de atentie sustinuta.",
            ["Very high RT: slow processing or hesitation in maintaining the rule."] = "RT foarte mare: procesare lenta sau ezitare in mentinerea regulii.",
            ["Performance is within a good range for this task: stable responses, low errors and adequate pace."] = "Performanta este in limite bune pentru aceasta sarcina: raspunsuri stabile, erori reduse si ritm adecvat.",
            ["Average time:"] = "Timp mediu:",
            ["Keep practicing!"] = "Continua sa exersezi!",
            ["Best streak:"] = "Cea mai buna serie:",
            ["Time:"] = "Timp:",
            ["Moves:"] = "Miscari:",
            ["You completed all"] = "Ai completat toate cele",
            ["rounds!"] = "runde!",
            ["You reached round"] = "Ai ajuns la runda",
            ["Nice! Time:"] = "Bravo! Timp:",
            ["Result:"] = "Rezultat:",
            ["seconds"] = "secunde",
            ["positive"] = "pozitiva",
            ["negative"] = "negativa",
            ["stable"] = "stabila",
            ["insufficient"] = "insuficiente"
        };

        private static readonly Dictionary<string, string> RoToEn =
            EnToRo.GroupBy(kv => kv.Value, StringComparer.Ordinal)
                  .ToDictionary(g => g.Key, g => g.First().Key, StringComparer.Ordinal);

        public static AppLanguage Current { get; private set; } = Load();

        public static event EventHandler? LanguageChanged;

        public static string T(string english)
        {
            if (Current == AppLanguage.English) return english;
            return EnToRo.TryGetValue(english, out var translated) ? translated : english;
        }

        public static string TranslateLiteral(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;

            if (TryTranslate(value, out var translated))
                return translated;

            int firstTextIndex = IndexOfFirstTextCharacter(value);
            if (firstTextIndex > 0)
            {
                string prefix = value[..firstTextIndex];
                string core = value[firstTextIndex..];
                if (TryTranslate(core, out var coreTranslated))
                    return prefix + coreTranslated;
            }

            return value;
        }

        public static void Toggle()
        {
            Current = Current == AppLanguage.English ? AppLanguage.Romanian : AppLanguage.English;
            Save();
            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void Register(Window window, Button? languageButton = null)
        {
            void ApplyHandler(object? _, EventArgs __)
            {
                Apply(window);
                UpdateLanguageButton(languageButton);
            }

            if (languageButton != null)
            {
                languageButton.Click += (_, _) => Toggle();
                UpdateLanguageButton(languageButton);
            }

            LanguageChanged += ApplyHandler;
            window.Closed += (_, _) => LanguageChanged -= ApplyHandler;
            window.Loaded += (_, _) => ApplyHandler(null, EventArgs.Empty);
            Apply(window);
        }

        public static void Apply(DependencyObject root)
        {
            if (root is Window window)
                window.Title = TranslateLiteral(window.Title);

            foreach (var element in DescendantsAndSelf(root))
            {
                if (element is TextBlock textBlock)
                {
                    if (textBlock.Inlines.Count > 0)
                    {
                        foreach (var inline in textBlock.Inlines.ToList())
                            TranslateInline(inline);
                    }
                    else
                    {
                        textBlock.Text = TranslateLiteral(textBlock.Text);
                    }
                }

                if (element is ContentControl contentControl && contentControl.Content is string content)
                    contentControl.Content = TranslateLiteral(content);

                if (element is HeaderedContentControl headered && headered.Header is string header)
                    headered.Header = TranslateLiteral(header);

                if (element is FrameworkElement frameworkElement && frameworkElement.ToolTip is string toolTip)
                    frameworkElement.ToolTip = TranslateLiteral(toolTip);
            }
        }

        public static void UpdateLanguageButton(Button? button)
        {
            if (button == null) return;
            button.Content = Current == AppLanguage.English ? BuildRomanianFlag() : BuildUkFlag();
            button.ToolTip = Current == AppLanguage.English ? T("Switch to Romanian") : T("Switch to English");
        }

        private static void TranslateInline(Inline inline)
        {
            if (inline is Run run)
            {
                run.Text = TranslateLiteral(run.Text);
                return;
            }

            if (inline is Span span)
            {
                foreach (var child in span.Inlines.ToList())
                    TranslateInline(child);
            }
        }

        private static bool TryTranslate(string value, out string translated)
        {
            if (Current == AppLanguage.Romanian)
                return EnToRo.TryGetValue(value, out translated!);

            return RoToEn.TryGetValue(value, out translated!);
        }

        private static int IndexOfFirstTextCharacter(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsLetterOrDigit(value[i]))
                    return i;
            }

            return -1;
        }

        private static Grid BuildRomanianFlag()
        {
            var flag = new Grid { Width = 24, Height = 16, ClipToBounds = true };
            flag.ColumnDefinitions.Add(new ColumnDefinition());
            flag.ColumnDefinitions.Add(new ColumnDefinition());
            flag.ColumnDefinitions.Add(new ColumnDefinition());
            AddRect(flag, "#002B7F", 0);
            AddRect(flag, "#FCD116", 1);
            AddRect(flag, "#CE1126", 2);
            return WrapFlag(flag);
        }

        private static Grid BuildUkFlag()
        {
            var flag = new Grid
            {
                Width = 24,
                Height = 16,
                ClipToBounds = true,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#012169"))
            };

            flag.Children.Add(new Rectangle
            {
                Fill = Brushes.White,
                Width = 34,
                Height = 3,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(34),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
            flag.Children.Add(new Rectangle
            {
                Fill = Brushes.White,
                Width = 34,
                Height = 3,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(-34),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
            flag.Children.Add(new Rectangle
            {
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C8102E")),
                Width = 34,
                Height = 1.4,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(34),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
            flag.Children.Add(new Rectangle
            {
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C8102E")),
                Width = 34,
                Height = 1.4,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(-34),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
            flag.Children.Add(new Rectangle
            {
                Fill = Brushes.White,
                Width = 24,
                Height = 5,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
            flag.Children.Add(new Rectangle
            {
                Fill = Brushes.White,
                Width = 7,
                Height = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
            flag.Children.Add(new Rectangle
            {
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C8102E")),
                Width = 24,
                Height = 2.8,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
            flag.Children.Add(new Rectangle
            {
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C8102E")),
                Width = 4,
                Height = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });

            return WrapFlag(flag);
        }

        private static Grid WrapFlag(Grid flag)
        {
            var wrapper = new Grid { Width = 28, Height = 20 };
            var border = new Border
            {
                Width = 26,
                Height = 18,
                CornerRadius = new CornerRadius(3),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                Child = flag,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            wrapper.Children.Add(border);
            return wrapper;
        }

        private static void AddRect(Grid grid, string color, int column)
        {
            var rect = new Rectangle
            {
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color))
            };
            Grid.SetColumn(rect, column);
            grid.Children.Add(rect);
        }

        private static IEnumerable<DependencyObject> DescendantsAndSelf(DependencyObject root)
        {
            yield return root;

            int visualCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < visualCount; i++)
            {
                foreach (var child in DescendantsAndSelf(VisualTreeHelper.GetChild(root, i)))
                    yield return child;
            }
        }

        private static AppLanguage Load()
        {
            // Always return English as default language
            return AppLanguage.English;
        }

        private static void Save()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegPath);
                key.SetValue(RegKey, Current == AppLanguage.Romanian ? "ro" : "en");
            }
            catch { }
        }
    }
}
