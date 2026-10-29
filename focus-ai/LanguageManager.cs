using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
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

        // Legacy Romanian resources were written without diacritics.  Keep the
        // source map readable while rendering polished Romanian throughout the UI.
        private static readonly Dictionary<string, string> RomanianDiacritics = new(StringComparer.OrdinalIgnoreCase)
        {
            ["acasa"] = "acasă", ["actiune"] = "acțiune", ["actiuni"] = "acțiuni",
            ["activitate"] = "activitate", ["activitati"] = "activități", ["activitatilor"] = "activităților", ["activitatile"] = "activitățile",
            ["actualizeaza"] = "actualizează", ["afisate"] = "afișate", ["afiseaza"] = "afișează",
            ["aflata"] = "aflată", ["aflate"] = "aflate", ["aflati"] = "aflați",
            ["alege"] = "alege", ["analizeaza"] = "analizează", ["analiza"] = "analiză",
            ["antrenare"] = "antrenare", ["antrenamentul"] = "antrenamentul",
            ["adresa"] = "adresă", ["apasa"] = "apasă", ["asteapta"] = "așteaptă", ["atentie"] = "atenție", ["atentiei"] = "atenției",
            ["autentifica"] = "autentifică", ["autentificat"] = "autentificat", ["autentificare"] = "autentificare",
            ["bazata"] = "bazată", ["bazate"] = "bazate", ["buna"] = "bună", ["bun"] = "bun",
            ["cand"] = "când", ["cat"] = "cât", ["cauta"] = "caută", ["cautare"] = "căutare", ["clar"] = "clar", ["clara"] = "clară", ["claritate"] = "claritate", ["cognitiva"] = "cognitivă", ["cuvantului"] = "cuvântului",
            ["completa"] = "completă", ["completat"] = "completat", ["configureaza"] = "configurează",
            ["conectata"] = "conectată", ["conectate"] = "conectate", ["continua"] = "continuă", ["creaza"] = "creează", ["creste"] = "crește",
            ["datorita"] = "datorită", ["datele"] = "datele", ["dificultate"] = "dificultate",
            ["asociati"] = "asociați", ["din"] = "din", ["directia"] = "direcția", ["directii"] = "direcții", ["disponibila"] = "disponibilă",
            ["editare"] = "editare", ["editeaza"] = "editează", ["eficienta"] = "eficientă", ["esantioane"] = "eșantioane",
            ["evaluare"] = "evaluare", ["evaluari"] = "evaluări", ["evaluarilor"] = "evaluărilor",
            ["exista"] = "există", ["exercitiu"] = "exercițiu", ["exercitii"] = "exerciții",
            ["fiecarui"] = "fiecărui", ["finalizata"] = "finalizată", ["finalizate"] = "finalizate", ["finalizeaza"] = "finalizează",
            ["foloseste"] = "folosește", ["functie"] = "funcție", ["functii"] = "funcții",
            ["gaseste"] = "găsește", ["gresite"] = "greșite", ["grija"] = "grijă", ["imbunatateste"] = "îmbunătățește",
            ["ignora"] = "ignoră", ["in"] = "în", ["inca"] = "încă", ["incarca"] = "încarcă", ["inchide"] = "închide", ["incepe"] = "începe", ["inceput"] = "început",
            ["indicatiile"] = "indicațiile", ["inregistrare"] = "înregistrare", ["inregistrarile"] = "înregistrările",
            ["intelege"] = "înțelege", ["inteligenta"] = "inteligență", ["intentie"] = "intenție", ["intre"] = "între",
            ["intr"] = "într", ["invatamant"] = "învățământ", ["invata"] = "învață", ["invatati"] = "învățați",
            ["latenta"] = "latență", ["joc"] = "joc", ["jocuri"] = "jocuri", ["legatura"] = "legătură", ["lipsa"] = "lipsă",
            ["matematica"] = "matematică", ["masoara"] = "măsoară", ["masurare"] = "măsurare", ["masuratori"] = "măsurători", ["modifica"] = "modifică", ["modificari"] = "modificări",
            ["nasterii"] = "nașterii", ["necesita"] = "necesită", ["necesare"] = "necesare", ["nici"] = "nici", ["noua"] = "nouă",
            ["obiective"] = "obiective", ["opreste"] = "oprește", ["optimizata"] = "optimizată",
            ["operatii"] = "operații", ["pacient"] = "pacient", ["pacienti"] = "pacienți", ["pacientii"] = "pacienții", ["pacientilor"] = "pacienților",
            ["pastreaza"] = "păstrează", ["pentru"] = "pentru", ["poti"] = "poți", ["practica"] = "practică",
            ["porneste"] = "pornește", ["pregatit"] = "pregătit", ["prelucrate"] = "prelucrate", ["primeste"] = "primește", ["privire"] = "privire",
            ["procesare"] = "procesare", ["procent"] = "procent", ["progres"] = "progres", ["proprietati"] = "proprietăți",
            ["performanta"] = "performanță", ["poti"] = "poți", ["ramai"] = "rămâi", ["rapida"] = "rapidă", ["raspunde"] = "răspunde", ["raspuns"] = "răspuns", ["raspunsului"] = "răspunsului", ["raspunsuri"] = "răspunsuri", ["reincarca"] = "reîncarcă",
            ["reimprospateaza"] = "reîmprospătează", ["reprezinta"] = "reprezintă", ["rezolva"] = "rezolvă", ["rezultate"] = "rezultate", ["rezultat"] = "rezultat",
            ["ridicata"] = "ridicată", ["rosu"] = "roșu", ["rotunjita"] = "rotunjită", ["salveaza"] = "salvează", ["sanatate"] = "sănătate", ["scaneaza"] = "scanează", ["scadere"] = "scădere",
            ["scazuta"] = "scăzută", ["schimba"] = "schimbă", ["scor"] = "scor", ["secventa"] = "secvență", ["secvente"] = "secvențe", ["selecteaza"] = "selectează", ["selectiva"] = "selectivă", ["semnale"] = "semnale", ["setari"] = "setări",
            ["sesiune"] = "sesiune", ["sesiuni"] = "sesiuni", ["siguranta"] = "siguranță", ["spatiu"] = "spațiu", ["spatiul"] = "spațiul", ["spatiului"] = "spațiului",
            ["si"] = "și", ["stanga"] = "stânga", ["structurata"] = "structurată", ["structurate"] = "structurate", ["sunt"] = "sunt",
            ["tau"] = "tău", ["tine"] = "ține", ["tinta"] = "țintă", ["tinte"] = "ținte", ["timp"] = "timp",
            ["toate"] = "toate", ["trebuie"] = "trebuie", ["urmaresti"] = "urmărești", ["urmatoarea"] = "următoarea",
            ["urmareste"] = "urmărește", ["usor"] = "ușor", ["utilizator"] = "utilizator", ["valabila"] = "valabilă", ["verifica"] = "verifică",
            ["viata"] = "viață", ["vieti"] = "vieți", ["vizuala"] = "vizuală", ["vizuale"] = "vizuale", ["zona"] = "zonă"
        };
        private static readonly Dictionary<string, string> EnToRo = new(StringComparer.Ordinal)
        {
            ["Focus AI - Dashboard"] = "Focus AI - Panou",
            ["Focus AI - Login"] = "Focus AI - Autentificare",
            ["Focus AI - Sign Up"] = "Focus AI - Inregistrare",
            ["Wait for the doctor's instructions"] = "Asteapta indicatiile medicului",
            ["SESSION SUMMARY"] = "REZUMATUL SESIUNII",
            ["Error:"] = "Eroare:",
            ["Focus AI configuration"] = "Configurare Focus AI",
            ["Configure the FOCUSAI_FIREBASE_API_KEY environment variable and restart Focus AI."] = "Configureaza variabila de mediu FOCUSAI_FIREBASE_API_KEY si reporneste Focus AI.",
            ["The script did not return coordinates.\nCheck the eye tracker."] = "Scriptul nu a returnat coordonate.\nVerifica dispozitivul de urmarire a privirii.",
            ["Set up profile"] = "Configureaza profilul",
            ["Wait for the sound cue..."] = "Așteaptă semnalul sonor...",
            ["PRESS THE SENSOR NOW!"] = "APASĂ SENZORUL ACUM!",
            ["N/A"] = "Indisponibil",
            ["points!"] = "puncte!",
            ["completed in"] = "finalizată în",
            ["Wrong!"] = "Greșit!",
            ["Time is up!"] = "Timpul a expirat!",
            ["life"] = "viață",
            ["left"] = "rămase",

            // Main workspace, sign-in and account setup
            ["Focus AI · Clinical workspace"] = "Focus AI · Spatiu clinic",
            ["Focus AI · Sign in"] = "Focus AI · Autentificare",
            ["Focus AI · Create account"] = "Focus AI · Creeaza un cont",
            ["↗  Cognitive activities"] = "↗  Activitati cognitive",
            ["↻  Refresh"] = "↻  Reimprospateaza",
            ["↻  Refresh data"] = "↻  Actualizeaza datele",
            ["＋  New activity"] = "＋  Activitate noua",
            ["＋  New assessment"] = "＋  Evaluare noua",
            ["4 stages · connected"] = "4 etape · conectate",
            ["A clearer view of every patient."] = "O imagine mai clara a fiecarui pacient.",
            ["A considered overview of the people in your care."] = "O privire de ansamblu asupra persoanelor aflate in grija ta.",
            ["A snapshot across your patient workspace"] = "O privire de ansamblu asupra spatiului pacientilor tai",
            ["ACCOUNT"] = "CONT",
            ["Account details and workspace access"] = "Detaliile contului si accesul la spatiul de lucru",
            ["ACTIVITY"] = "ACTIVITATE",
            ["ACTIVITY SCORE"] = "SCOR ACTIVITATI",
            ["ASSESSMENT"] = "EVALUARE",
            ["ASSESSMENT FLOW"] = "FLUXUL EVALUARII",
            ["Assessment history"] = "Istoricul evaluarilor",
            ["ASSESSMENT SCORE"] = "SCOR EVALUARE",
            ["Average completed"] = "Finalizate in medie",
            ["Average per patient"] = "Media per pacient",
            ["Bring assessments, cognitive activities, and patient progress into one focused workspace."] = "Reuneste evaluarile, activitatile cognitive si progresul pacientilor intr-un singur spatiu de lucru.",
            ["Clinical assessments"] = "Evaluari clinice",
            ["CLINICAL INTELLIGENCE"] = "INTELIGENTA CLINICA",
            ["Cognitive activities"] = "Activitati cognitive",
            ["Cognitive activity"] = "Activitate cognitiva",
            ["Cognitive activity records are being refreshed."] = "Inregistrarile activitatilor cognitive se actualizeaza.",
            ["Completed cognitive activities will be listed here."] = "Activitatile cognitive finalizate vor fi afisate aici.",
            ["DATE"] = "DATA",
            ["Edit profile  ↗"] = "Editeaza profilul  ↗",
            ["Explore activities  ↗"] = "Exploreaza activitatile  ↗",
            ["FOCUS AI  /  CLINICAL WORKSPACE"] = "FOCUS AI  /  SPATIU CLINIC",
            ["FOCUS AI  /  DESKTOP"] = "FOCUS AI  /  DESKTOP",
            ["FORECAST"] = "PREVIZIUNE",
            ["LAST ASSESSMENT"] = "ULTIMA EVALUARE",
            ["LIVE OVERVIEW"] = "PREZENTARE LIVE",
            ["Loading activity history"] = "Se incarca istoricul activitatilor",
            ["Loading assessment history"] = "Se incarca istoricul evaluarilor",
            ["Loading patient records"] = "Se incarca inregistrarile pacientilor",
            ["NEEDS REVIEW"] = "NECESITA REVIZUIRE",
            ["No activity sessions yet"] = "Inca nu exista sesiuni de activitate",
            ["No assessments to show"] = "Nu exista evaluari de afisat",
            ["No patients yet"] = "Inca nu exista pacienti",
            ["Patient summaries are being refreshed."] = "Rezumatul pacientilor se actualizeaza.",
            ["Patients associated with your account will appear here."] = "Pacientii asociati contului tau vor aparea aici.",
            ["Practice metrics"] = "Indicatori de practica",
            ["Practice overview"] = "Prezentare generala",
            ["QUICK ACTIONS"] = "ACTIUNI RAPIDE",
            ["Ready for your next session"] = "Pregatit pentru urmatoarea sesiune",
            ["Select a patient and complete an assessment to start building a history."] = "Selecteaza un pacient si finalizeaza o evaluare pentru a incepe istoricul.",
            ["Select a patient to view their activity history"] = "Selecteaza un pacient pentru a vedea istoricul activitatilor",
            ["Select a patient to view their assessments"] = "Selecteaza un pacient pentru a vedea evaluarile",
            ["SIGNAL SNAPSHOT"] = "REZUMAT SEMNALE",
            ["SIGNED IN AS"] = "AUTENTIFICAT CA",
            ["TESTS / PATIENT"] = "TESTE / PACIENT",
            ["The latest results will appear here."] = "Cele mai recente rezultate vor aparea aici.",
            ["TYPE"] = "TIP",
            ["Under your care"] = "In grija ta",
            ["View trends  ↗"] = "Vezi tendintele  ↗",
            ["WORKSPACE"] = "SPATIU DE LUCRU",
            ["Workspace ready"] = "Spatiul de lucru este pregatit",
            ["Your profile"] = "Profilul tau",

            ["A focused space for patient assessments, cognitive training and meaningful progress over time."] = "Un spatiu clar pentru evaluari, antrenament cognitiv si progres relevant in timp.",
            ["COGNITIVE HEALTH WORKSPACE"] = "SPATIU PENTRU SANATATE COGNITIVA",
            ["Cognitive practice"] = "Exersare cognitiva",
            ["Continue  →"] = "Continua  →",
            ["Continue to your clinical workspace."] = "Continua in spatiul tau clinic.",
            ["Email address"] = "Adresa de e-mail",
            ["FOCUS AI  ·  DESIGNED FOR CLINICAL CLARITY"] = "FOCUS AI  ·  CREAT PENTRU CLARITATE CLINICA",
            ["Four complementary signals, one workflow."] = "Patru semnale complementare, un singur flux.",
            ["Keep me signed in"] = "Pastreaza-ma autentificat",
            ["Longitudinal insight"] = "Perspectiva in timp",
            ["Make training part of the care journey."] = "Integreaza antrenamentul in parcursul de ingrijire.",
            ["New to Focus AI?  "] = "Nou in Focus AI?  ",
            ["OR CONTINUE WITH"] = "SAU CONTINUA CU",
            ["PRIVATE WORKSPACE  ·  SECURE SESSION"] = "SPATIU PRIVAT  ·  SESIUNE SECURIZATA",
            ["Review patient results across sessions."] = "Analizeaza rezultatele pacientului intre sesiuni.",
            ["Sign in to Focus AI"] = "Autentifica-te in Focus AI",
            ["Structured assessments"] = "Evaluari structurate",
            ["Turn every signal into understanding."] = "Transforma fiecare semnal in intelegere.",
            ["WELCOME BACK"] = "BINE AI REVENIT",

            ["A few details to set up your clinical workspace."] = "Cateva detalii pentru a configura spatiul tau clinic.",
            ["A more connected way to care."] = "O modalitate mai conectata de a oferi ingrijire.",
            ["Already have an account?  "] = "Ai deja un cont?  ",
            ["By continuing, you agree to use patient data responsibly."] = "Continuand, esti de acord sa folosesti responsabil datele pacientilor.",
            ["Cabinet address"] = "Adresa cabinetului",
            ["Create a secure workspace for structured cognitive assessment and patient progress."] = "Creeaza un spatiu securizat pentru evaluari cognitive structurate si progresul pacientilor.",
            ["Create account  →"] = "Creeaza contul  →",
            ["Create your account"] = "Creeaza-ti contul",
            ["Date of birth"] = "Data nasterii",
            ["Eye movement, response and attention tasks."] = "Sarcini de miscare a ochilor, raspuns si atentie.",
            ["GET STARTED"] = "INCEPE",
            ["Keep contact and clinical history together."] = "Pastreaza impreuna datele de contact si istoricul clinic.",
            ["Multidimensional assessments"] = "Evaluari multidimensionale",
            ["Patient profiles"] = "Profiluri de pacienti",
            ["PRIVATE WORKSPACE  ·  DESIGNED FOR CLINICAL CLARITY"] = "SPATIU PRIVAT  ·  CREAT PENTRU CLARITATE CLINICA",
            ["Progress over time"] = "Progres in timp",
            ["Turn results into a clearer longitudinal picture."] = "Transforma rezultatele intr-o imagine mai clara in timp.",
            ["YOUR WORKSPACE INCLUDES"] = "SPATIUL TAU INCLUDE",

            // Assessment flow and training library
            ["Focus AI · Cognitive assessment"] = "Focus AI · Evaluare cognitiva",
            ["Focus AI · Cognitive activities"] = "Focus AI · Activitati cognitive",
            ["4 STAGES"] = "4 ETAPE",
            ["A connected picture of attention."] = "O imagine conectata a atentiei.",
            ["Assess response inhibition and selective attention."] = "Evalueaza inhibarea raspunsului si atentia selectiva.",
            ["Auditory response"] = "Raspuns auditiv",
            ["Close session"] = "Inchide sesiunea",
            ["Cognitive assessment"] = "Evaluare cognitiva",
            ["Complete four stages to build a structured cognitive profile."] = "Finalizeaza patru etape pentru a construi un profil cognitiv structurat.",
            ["Each stage captures a different dimension of cognitive response."] = "Fiecare etapa surprinde o dimensiune diferita a raspunsului cognitiv.",
            ["ELAPSED TIME"] = "TIMP SCURS",
            ["FOCUS AI  /  ASSESSMENT PROTOCOL"] = "FOCUS AI  /  PROTOCOL DE EVALUARE",
            ["Four distinct tasks work together to give context to reaction, gaze, and response inhibition."] = "Patru sarcini distincte ofera context pentru reactie, privire si inhibarea raspunsului.",
            ["Go / No-Go"] = "Go / No-Go",
            ["Measure reaction time to controlled sound cues."] = "Masurati timpul de reactie la semnale sonore controlate.",
            ["Observe how the gaze follows a moving spatial target."] = "Observa cum privirea urmareste o tinta spatiala in miscare.",
            ["PROTOCOL MAP"] = "HARTA PROTOCOLULUI",
            ["SESSION GUIDE"] = "GHIDUL SESIUNII",
            ["Start stage  →"] = "Porneste etapa  →",
            ["The protocol"] = "Protocolul",
            ["The timer starts with the first stage and ends when the full protocol is complete."] = "Cronometrul porneste la prima etapa si se opreste cand protocolul este finalizat.",
            ["Track eye movement and gaze accuracy across a guided visual path."] = "Urmareste miscarea ochilor si precizia privirii pe o traiectorie vizuala ghidata.",
            ["Visual trajectory"] = "Traiectorie vizuala",
            ["Waiting to begin"] = "Asteapta sa incepi",

            ["5 TRAINING MODULES"] = "5 MODULE DE ANTRENAMENT",
            ["A CONSISTENT ROUTINE"] = "O RUTINA CONSTANTA",
            ["Choose one exercise, stay focused, and return to the results to follow performance over time."] = "Alege un exercitiu, ramai concentrat si revino la rezultate pentru a urmari performanta in timp.",
            ["Each activity focuses on a different cognitive skill. Keep sessions intentional, then review the results alongside the assessment history."] = "Fiecare activitate se concentreaza pe o abilitate cognitiva diferita. Pastreaza sesiunile intentionate, apoi analizeaza rezultatele alaturi de istoricul evaluarilor.",
            ["EXERCISE"] = "EXERCITIU",
            ["FOCUS AI  /  TRAINING LIBRARY"] = "FOCUS AI  /  BIBLIOTECA DE ANTRENAMENT",
            ["FOCUS AREA"] = "ZONA DE FOCUS",
            ["Identify the ink color and resist word interference."] = "Identifica culoarea cernelii si rezista interferentei cuvantului.",
            ["INHIBITION"] = "INHIBITIE",
            ["Locate a target among similar visual distractors."] = "Gaseste tinta printre distractori vizuali similari.",
            ["Match pairs and hold visual information in mind."] = "Potriveste perechi si retine informatia vizuala.",
            ["NO."] = "NR.",
            ["PROCESSING SPEED"] = "VITEZA DE PROCESARE",
            ["Progress through practice, not noise."] = "Progreseaza prin practica, nu prin zgomot.",
            ["Quick math"] = "Matematica rapida",
            ["Remember and repeat a growing sequence of cues."] = "Retine si repeta o secventa crescatoare de indicii.",
            ["SELECTIVE ATTENTION"] = "ATENTIE SELECTIVA",
            ["SEQUENTIAL MEMORY"] = "MEMORIE SECVENTIALA",
            ["Short, intentional exercises for memory, attention and response control."] = "Exercitii scurte si intentionate pentru memorie, atentie si controlul raspunsului.",
            ["Solve timed operations with accuracy and speed."] = "Rezolva operatii contra timp cu precizie si viteza.",
            ["Stroop test"] = "Test Stroop",
            ["THE TRAINING APPROACH"] = "ABORDAREA ANTRENAMENTULUI",
            ["Visual search"] = "Cautare vizuala",
            ["WORKING MEMORY"] = "MEMORIE DE LUCRU",

            ["Focus AI · Auditory reaction"] = "Focus AI · Reactie auditiva",
            ["ASSESSMENT STAGE"] = "ETAPA EVALUARII",
            ["FOCUS AI  ·  RESPONSE TIME MEASUREMENT"] = "FOCUS AI  ·  MASURAREA TIMPULUI DE REACTIE",
            ["Follow the instructions and keep your hands ready to respond."] = "Urmeaza instructiunile si tine mainile pregatite pentru raspuns.",
            ["Measure the moment between signal and response."] = "Masura momentul dintre semnal si raspuns.",
            ["Press Start to begin the test"] = "Apasa Porneste pentru a incepe testul",
            ["PROTOCOL STATUS"] = "STAREA PROTOCOLULUI",
            ["REACTION LATENCY"] = "LATENTA REACTIEI",
            ["REACTION PROTOCOL"] = "PROTOCOL DE REACTIE",
            ["REACTION TIMER"] = "CRONOMETRU REACTIE",
            ["Ready"] = "Pregatit",
            ["Response latency"] = "Latenta raspunsului",
            ["SOUND CUE"] = "SEMNAL SONOR",
            ["STAGE 03  /  AUDITORY RESPONSE"] = "ETAPA 03  /  RASPUNS AUDITIV",
            ["Start test  →"] = "Porneste testul  →",
            ["Stay ready for the cue."] = "Ramai pregatit pentru semnal.",
            ["Wait for the sound cue. Respond as quickly and consistently as possible. The timer captures your response latency."] = "Asteapta semnalul sonor. Raspunde cat mai repede si consecvent. Cronometrul masoara latenta raspunsului.",

            // Cognitive exercises and assessment detail screens
            ["Focus AI – Stroop Test"] = "Focus AI – Test Stroop",
            ["Focus AI - Memory"] = "Focus AI - Memorie",
            ["Focus AI - Sequences"] = "Focus AI - Secvente",
            ["Focus AI - Visual Search"] = "Focus AI - Cautare vizuala",
            ["Focus AI - Quick Math"] = "Focus AI - Matematica rapida",
            ["Focus AI – GO/NO-GO"] = "Focus AI – GO/NO-GO",
            ["Focus AI - Hybrid CPT"] = "Focus AI - CPT hibrid",
            ["Focus AI · Assessment details"] = "Focus AI · Detalii evaluare",
            ["Focus AI · Profile settings"] = "Focus AI · Setari profil",
            ["Focus AI · Trend analysis"] = "Focus AI · Analiza tendinte",

            ["3 in a row"] = "3 la rand",
            ["30 TRIALS"] = "30 RUNDE",
            ["A color word appears on screen."] = "Pe ecran apare un cuvant colorat.",
            ["Begin assessment  →"] = "Incepe evaluarea  →",
            ["BLUE"] = "ALBASTRU",
            ["COGNITIVE PROTOCOL  /  02"] = "PROTOCOL COGNITIV  /  02",
            ["Complete 30 trials with speed and accuracy."] = "Finalizeaza 30 de runde cu viteza si precizie.",
            ["CORRECT  /  "] = "CORECT  /  ",
            ["INCORRECT  /  "] = "INCORECT  /  ",
            ["Name the ink color, not the word."] = "Numeste culoarea cernelii, nu cuvantul.",
            ["Play again  →"] = "Joaca din nou  →",
            ["Respond to the ink color, not the word's meaning."] = "Raspunde la culoarea cernelii, nu la sensul cuvantului.",
            ["SAMPLE STIMULUS"] = "STIMUL EXEMPLU",
            ["See the color. Ignore the word."] = "Priveste culoarea. Ignora cuvantul.",
            ["SELECTIVE ATTENTION  /  PROTOCOL 02"] = "ATENTIE SELECTIVA  /  PROTOCOL 02",
            ["SESSION COMPLETE"] = "SESIUNE FINALIZATA",
            ["SESSION SETUP"] = "CONFIGURARE SESIUNE",
            ["THE PROTOCOL"] = "PROTOCOLUL",
            ["This task measures how consistently you can suppress an automatic response and focus on the relevant visual signal."] = "Aceasta sarcina masoara cat de consecvent poti inhiba un raspuns automat si te poti concentra pe semnalul vizual relevant.",
            ["Your performance is recorded in the assessment history."] = "Performanta ta este inregistrata in istoricul evaluarilor.",

            ["01  /  GAZE TRAJECTORY"] = "01  /  TRAIECTORIA PRIVIRII",
            ["02  /  ECG"] = "02  /  ECG",
            ["03  /  SPO₂"] = "03  /  SPO₂",
            ["04  /  HEART RATE"] = "04  /  RITM CARDIAC",
            ["05  /  DISTRACTION EVENTS"] = "05  /  EVENIMENTE DE DISTRACTIE",
            ["A detailed view of measured movement and physiological signals."] = "O vedere detaliata a miscarii masurate si a semnalelor fiziologice.",
            ["Attention events"] = "Evenimente de atentie",
            ["Cardiac waveform"] = "Unda cardiaca",
            ["Horizontal and vertical gaze displacement"] = "Deplasare orizontala si verticala a privirii",
            ["Oxygen saturation"] = "Saturatie de oxigen",
            ["Pulse response"] = "Raspunsul pulsului",
            ["SCATTER MAP"] = "HARTA DE DISPERSIE",
            ["SESSION DIAGNOSTICS"] = "DIAGNOSTIC SESIUNE",
            ["Signal explorer"] = "Explorator de semnale",
            ["Visual path"] = "Traiectorie vizuala",
            ["XY COORDINATES"] = "COORDONATE XY",

            ["Addition and subtraction · values 1–20"] = "Adunare si scadere · valori 1–20",
            ["Addition, subtraction and multiplication · values 1–50"] = "Adunare, scadere si inmultire · valori 1–50",
            ["Advanced reasoning"] = "Rationament avansat",
            ["All operations including division · values 1–100"] = "Toate operatiile, inclusiv impartirea · valori 1–100",
            ["COGNITIVE TRAINING  /  05"] = "ANTRENAMENT COGNITIV  /  05",
            ["Expert  ↗"] = "Expert  ↗",
            ["Fluency"] = "Fluenta",
            ["Foundation"] = "Baza",
            ["HIGH LOAD"] = "DIFICULTATE RIDICATA",
            ["Intermediate  ↗"] = "Intermediar  ↗",
            ["LOW LOAD"] = "DIFICULTATE REDUSA",
            ["MODERATE LOAD"] = "DIFICULTATE MEDIE",
            ["Recommended start  ↗"] = "Inceput recomandat  ↗",
            ["SELECT YOUR CHALLENGE"] = "ALEGE PROVOCAREA",
            ["STREAK "] = "SERIE ",
            ["TIME "] = "TIMP ",
            ["Work through mental calculations with a balance of speed and accuracy. Each round lasts 60 seconds."] = "Rezolva calcule mentale echilibrand viteza si precizia. Fiecare runda dureaza 60 de secunde.",

            ["30-second protocol · sensor response"] = "Protocol de 30 de secunde · raspuns senzor",
            ["AVERAGE RESPONSE TIME"] = "TIMP MEDIU DE RASPUNS",
            ["Begin test  →"] = "Incepe testul  →",
            ["CORRECT GO"] = "GO CORECTE",
            ["FALSE POSITIVES"] = "POZITIVE FALSE",
            ["GO"] = "GO",
            ["Green means you should trigger a response."] = "Verde inseamna ca trebuie sa raspunzi.",
            ["INHIBITORY CONTROL  /  PROTOCOL 06"] = "CONTROL INHIBITOR  /  PROTOCOL 06",
            ["NO"] = "NU",
            ["Red means do not respond; false positives are penalized."] = "Rosu inseamna sa nu raspunzi; raspunsurile false sunt penalizate.",
            ["Respond"] = "Raspunde",
            ["Respond only to green GO stimuli. Withhold your response when red NO-GO stimuli appear."] = "Raspunde doar la stimulii GO verzi. Retine raspunsul cand apar stimulii NO-GO rosii.",
            ["RESPONSE INHIBITION  /  06"] = "INHIBAREA RASPUNSULUI  /  06",
            ["SIGNAL GUIDE"] = "GHID SEMNALE",
            ["Withhold"] = "Retine raspunsul",

            ["↻  Refresh analysis"] = "↻  Actualizeaza analiza",
            ["Close analysis"] = "Inchide analiza",
            ["Cognitive trajectory"] = "Traiectorie cognitiva",
            ["Direction of change"] = "Directia schimbarii",
            ["FOCUS AI  /  PATIENT INSIGHTS"] = "FOCUS AI  /  PERSPECTIVE PACIENT",
            ["INTERPRETATION"] = "INTERPRETARE",
            ["k-NN (k=3)"] = "k-NN (k=3)",
            ["MODEL COMPARISON"] = "COMPARATIE MODELE",
            ["MODEL OUTPUT"] = "REZULTAT MODEL",
            ["Observed performance and next-session estimate"] = "Performanta observata si estimarea urmatoarei sesiuni",
            ["PHYSIOLOGICAL ALERTS"] = "ALERTE FIZIOLOGICE",
            ["Predicted score"] = "Scor estimat",
            ["Prediction interval"] = "Interval de predictie",
            ["PREDICTION SUMMARY"] = "REZUMAT PREDICTIE",
            ["PREDICTIVE ANALYTICS  /  FOCUS AI"] = "ANALITICA PREDICTIVA  /  FOCUS AI",
            ["SCORE EVOLUTION"] = "EVOLUTIA SCORULUI",
            ["Sessions analyzed"] = "Sesiuni analizate",

            ["Easy · 25s"] = "Usor · 25s",
            ["Eight rounds with increasing difficulty"] = "Opt runde cu dificultate crescatoare",
            ["Find the signal in the noise."] = "Gaseste semnalul in zgomot.",
            ["Hard · 15s"] = "Dificil · 15s",
            ["Medium · 20s"] = "Mediu · 20s",
            ["PREVIEW / 01"] = "PREVIZUALIZARE / 01",
            ["Scan a field of similar symbols and locate the one item that breaks the pattern. The visual field expands as you progress."] = "Scaneaza un camp de simboluri similare si gaseste elementul care rupe tiparul. Campul vizual se extinde pe masura ce progresezi.",
            ["SELECTIVE ATTENTION  /  03"] = "ATENTIE SELECTIVA  /  03",
            ["Start visual search  →"] = "Porneste cautarea vizuala  →",
            ["Stay systematic. The target could appear in any position."] = "Ramai metodic. Tinta poate aparea in orice pozitie.",
            ["TARGET RECOGNITION"] = "RECUNOASTEREA TINTEI",
            ["Three lives — choose carefully, not randomly"] = "Trei vieti — alege cu grija, nu la intamplare",
            ["VISUAL FIELD"] = "CAMP VIZUAL",

            ["A cue sequence is shown one step at a time. Reproduce it in the same order; each new level adds another item to working memory."] = "O secventa de indicii este afisata pe rand. Reprodu-o in aceeasi ordine; fiecare nivel adauga un element in memoria de lucru.",
            ["Begin sequence test  →"] = "Incepe testul de secvente  →",
            ["COGNITIVE PROTOCOL  /  04"] = "PROTOCOL COGNITIV  /  04",
            ["Final score: 0"] = "Scor final: 0",
            ["Hold the sequence. Rebuild the pattern."] = "Retine secventa. Reconstruieste tiparul.",
            ["MEMORY LOAD"] = "INCARCARE MEMORIE",
            ["Observe the highlighted cue sequence."] = "Observa secventa de indicii evidentiata.",
            ["Progress through up to 20 levels; three errors end the session."] = "Parcurge pana la 20 de niveluri; trei erori incheie sesiunea.",
            ["Repeat the cues in the exact same order."] = "Repeta indiciile exact in aceeasi ordine.",
            ["SEQUENTIAL MEMORY  /  PROTOCOL 04"] = "MEMORIE SECVENTIALA  /  PROTOCOL 04",
            ["SESSION STRUCTURE"] = "STRUCTURA SESIUNII",
            ["UP TO 20 LEVELS"] = "PANA LA 20 DE NIVELURI",

            ["ACCOUNT DETAILS"] = "DETALII CONT",
            ["Contact"] = "Contact",
            ["Height"] = "Inaltime",
            ["Identity"] = "Identitate",
            ["Keep your professional profile accurate and ready for the next session."] = "Pastreaza profilul profesional corect si pregatit pentru urmatoarea sesiune.",
            ["Name and date of birth"] = "Nume si data nasterii",
            ["PERSONAL INFORMATION"] = "INFORMATII PERSONALE",
            ["Phone and office address"] = "Telefon si adresa cabinetului",
            ["Profile settings"] = "Setari profil",
            ["Save changes  →"] = "Salveaza modificarile  →",
            ["Update the fields below, then save your changes."] = "Actualizeaza campurile de mai jos, apoi salveaza modificarile.",
            ["Your name, contact information and office address are used to identify your clinical workspace."] = "Numele, datele de contact si adresa cabinetului sunt folosite pentru identificarea spatiului tau clinic.",

            ["- when X appears after A"] = "- cand X apare dupa A",
            ["ATTENTION  /  INHIBITORY CONTROL"] = "ATENTIE  /  CONTROL INHIBITOR",
            ["Hybrid CPT - Attention &amp; Inhibitory Control Test"] = "CPT hibrid - Test de atentie si control inhibitor",
            ["Letters appear one at a time. Respond only when X follows A, and withhold responses to all other combinations."] = "Literele apar pe rand. Raspunde doar cand X urmeaza dupa A si retine raspunsul pentru toate celelalte combinatii.",
            ["Respond to the target sequence."] = "Raspunde la secventa tinta.",
            ["Start"] = "Porneste",
            ["Trial 0 / 50"] = "Runda 0 / 50",

            ["FOCUS AI  /  ASSESSMENT DATA"] = "FOCUS AI  /  DATE EVALUARE",
            ["SESSION SETTINGS"] = "SETARI SESIUNE",
            ["WORKING MEMORY  /  01"] = "MEMORIE DE LUCRU  /  01",

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
            ["Select a patient to view trend analysis"] = "Selecteaza un pacient pentru analiza trendului",

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
            ["Cognitive trend analysis"] = "Analiza trend cognitiv",
            ["Patient:"] = "Pacient:",
            ["Patient: loading..."] = "Pacient: se incarca...",
            ["ML-based cognitive trend analysis"] = "Analiza trend cognitiv bazata pe ML",
            ["⟳ Refresh"] = "⟳ Reimprospateaza",
            ["Test score evolution"] = "Evolutie scoruri test",
            ["PREDICTED SCORE"] = "SCOR PREVIZIONAT",
            ["TREND"] = "TENDINTA",
            ["PREDICTION INTERVAL STATUS"] = "STATUTUL INTERVALULUI DE PREDICTIE",
            ["SESSIONS ANALYZED"] = "SESIUNI ANALIZATE",
            ["Prediction model details"] = "Detalii model de predictie",
            ["Linear regression"] = "Regresie liniara",
            ["Holt smoothing"] = "Holt (netezire)",
            ["Physiological alerts"] = "Avertizari fiziologice",
            ["⚠️ Physiological alerts"] = "⚠️ Avertizari fiziologice",
            ["Close"] = "Inchide",
            ["At least 2 test sessions are needed for analysis."] = "Este nevoie de cel putin 2 sesiuni de testare pentru analiza.",
            ["Not enough data could be extracted for prediction."] = "Nu s-au putut extrage suficiente date pentru predictie.",
            ["Error loading data:"] = "Eroare la incarcarea datelor:",
            ["Date & time"] = "Data si ora",
            ["Test score"] = "Scor test",
            ["Actual score"] = "Scor real",
            ["Prediction (next session)"] = "Predictie (urmatoarea sesiune)",
            ["Prediction interval (low)"] = "Interval de predictie (limita inferioara)",
            ["Prediction interval (high)"] = "Interval de predictie (limita superioara)",
            ["Empirical 90% interval"] = "Interval empiric 90%",
            ["Rolling-origin empirical interval"] = "Interval empiric rolling-origin",
            ["Uncalibrated interval"] = "Interval necalibrat",
            ["Prediction based on {0} sessions. The interval is empirical only when sufficient historical evaluation data are available. Last score: {1:F1} -> expected score: {2:F1}."] = "Predictie bazata pe {0} sesiuni. Intervalul este empiric doar daca exista suficiente date de evaluare istorica. Ultimul scor: {1:F1} -> scor anticipat: {2:F1}.",
            ["points/session"] = "puncte/sesiune",
            ["Improving"] = "In crestere",
            ["Declining"] = "In scadere",
            ["Stable"] = "Stabil",
            ["High"] = "Ridicata",
            ["Low"] = "Scazuta",
            ["Low SpO2"] = "SpO2 scazut",
            ["Abnormal heart rate"] = "Ritm cardiac anormal",
            ["High dispersion in heart-rate samples"] = "Dispersie crescuta a masuratorilor ritmului cardiac",
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
            EnToRo.GroupBy(kv => DiacritizeRomanian(kv.Value), StringComparer.Ordinal)
                  .ToDictionary(g => g.Key, g => g.First().Key, StringComparer.Ordinal);

        private static readonly ConditionalWeakTable<DependencyObject, LocalizedElementState> LocalizedValues = new();
        private static readonly Dictionary<Window, WindowRegistration> RegisteredWindows = new();
        private static bool _isInitialized;

        private sealed class LocalizedElementState
        {
            public Dictionary<string, LocalizedValue> Values { get; } = new(StringComparer.Ordinal);
        }

        private sealed class LocalizedValue
        {
            public LocalizedValue(string source, string lastRendered)
            {
                Source = source;
                LastRendered = lastRendered;
            }

            public string Source { get; set; }
            public string LastRendered { get; set; }
        }

        private sealed class WindowRegistration
        {
            public Button? LanguageButton { get; set; }
            public RoutedEventHandler? ToggleHandler { get; set; }
        }

        public static AppLanguage Current { get; private set; } = Load();

        public static event EventHandler? LanguageChanged;

        /// <summary>
        /// Enables localization for every WPF window, including windows opened after
        /// the language was changed from the dashboard.
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized) return;

            _isInitialized = true;
            EventManager.RegisterClassHandler(
                typeof(Window),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnWindowLoaded));
        }

        public static string T(string english)
        {
            if (Current == AppLanguage.English) return english;
            return EnToRo.TryGetValue(english, out var translated) ? DiacritizeRomanian(translated) : english;
        }

        public static string TranslateLiteral(string value)
        {
            return Current == AppLanguage.Romanian
                ? TranslateToRomanian(value)
                : TranslateToEnglish(value);
        }

        public static void Toggle()
        {
            Current = Current == AppLanguage.English ? AppLanguage.Romanian : AppLanguage.English;
            Save();
            ApplyToOpenWindows();
            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void Register(Window window, Button? languageButton = null)
        {
            Initialize();

            if (!RegisteredWindows.TryGetValue(window, out var registration))
            {
                registration = new WindowRegistration();
                RegisteredWindows.Add(window, registration);
                window.Closed += OnWindowClosed;
            }

            if (languageButton != null && !ReferenceEquals(registration.LanguageButton, languageButton))
            {
                if (registration.LanguageButton != null && registration.ToggleHandler != null)
                    registration.LanguageButton.Click -= registration.ToggleHandler;

                registration.LanguageButton = languageButton;
                registration.ToggleHandler = (_, _) => Toggle();
                languageButton.Click += registration.ToggleHandler;
            }

            ApplyWindow(window, registration);
        }

        public static void Apply(DependencyObject root)
        {
            if (root is Window window)
                window.Title = Localize(window, nameof(Window.Title), window.Title);

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
                        textBlock.Text = Localize(textBlock, nameof(TextBlock.Text), textBlock.Text);
                    }
                }

                if (element is ContentControl contentControl && contentControl.Content is string content)
                    contentControl.Content = Localize(contentControl, nameof(ContentControl.Content), content);

                if (element is HeaderedContentControl headered && headered.Header is string header)
                    headered.Header = Localize(headered, nameof(HeaderedContentControl.Header), header);

                if (element is FrameworkElement frameworkElement && frameworkElement.ToolTip is string toolTip)
                    frameworkElement.ToolTip = Localize(frameworkElement, nameof(FrameworkElement.ToolTip), toolTip);
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
                run.Text = Localize(run, nameof(Run.Text), run.Text);
                return;
            }

            if (inline is Span span)
            {
                foreach (var child in span.Inlines.ToList())
                    TranslateInline(child);
            }
        }

        private static void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is Window window)
                Register(window);
        }

        private static void OnWindowClosed(object? sender, EventArgs e)
        {
            if (sender is not Window window || !RegisteredWindows.Remove(window, out var registration))
                return;

            if (registration.LanguageButton != null && registration.ToggleHandler != null)
                registration.LanguageButton.Click -= registration.ToggleHandler;
        }

        private static void ApplyToOpenWindows()
        {
            if (Application.Current == null) return;

            foreach (var window in Application.Current.Windows.Cast<Window>().ToArray())
            {
                if (!RegisteredWindows.TryGetValue(window, out var registration))
                {
                    registration = new WindowRegistration();
                    RegisteredWindows[window] = registration;
                }

                ApplyWindow(window, registration);
            }
        }

        private static void ApplyWindow(Window window, WindowRegistration registration)
        {
            Apply(window);
            UpdateLanguageButton(registration.LanguageButton);
        }

        private static string Localize(DependencyObject owner, string propertyName, string currentValue)
        {
            if (string.IsNullOrWhiteSpace(currentValue)) return currentValue;

            var state = LocalizedValues.GetOrCreateValue(owner);
            if (!state.Values.TryGetValue(propertyName, out var value))
            {
                value = new LocalizedValue(TranslateToEnglish(currentValue), currentValue);
                state.Values[propertyName] = value;
            }
            else if (!string.Equals(currentValue, value.LastRendered, StringComparison.Ordinal))
            {
                // The screen changed this text since the last language pass (for
                // example a game score or a dynamically-created row). Capture the
                // fresh source instead of translating an old value back and forth.
                value.Source = TranslateToEnglish(currentValue);
            }

            string rendered = Current == AppLanguage.Romanian
                ? TranslateToRomanian(value.Source)
                : value.Source;
            value.LastRendered = rendered;
            return rendered;
        }

        private static string TranslateToRomanian(string value)
        {
            string translated = TranslateWithMap(value, EnToRo);
            return string.Equals(translated, value, StringComparison.Ordinal)
                ? value
                : DiacritizeRomanian(translated);
        }

        private static string TranslateToEnglish(string value)
        {
            string translated = TranslateWithMap(value, RoToEn);
            if (!string.Equals(translated, value, StringComparison.Ordinal))
                return translated;

            string normalizedRomanian = DiacritizeRomanian(value);
            string normalizedTranslation = TranslateWithMap(normalizedRomanian, RoToEn);
            return string.Equals(normalizedTranslation, normalizedRomanian, StringComparison.Ordinal)
                ? value
                : normalizedTranslation;
        }

        private static string DiacritizeRomanian(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;

            return Regex.Replace(value, @"\p{L}+", match =>
            {
                if (!RomanianDiacritics.TryGetValue(match.Value, out string? translated))
                    return match.Value;

                if (match.Value.All(char.IsUpper))
                    return translated.ToUpperInvariant();

                if (char.IsUpper(match.Value[0]))
                    return char.ToUpperInvariant(translated[0]) + translated[1..];

                return translated;
            });
        }

        private static string TranslateWithMap(
            string value,
            IReadOnlyDictionary<string, string> translations)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;

            if (translations.TryGetValue(value, out var translated))
                return translated;

            int firstTextIndex = IndexOfFirstTextCharacter(value);
            if (firstTextIndex > 0)
            {
                string prefix = value[..firstTextIndex];
                string core = value[firstTextIndex..];
                if (translations.TryGetValue(core, out var coreTranslated))
                    return prefix + coreTranslated;
            }

            return value;
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
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegPath);
                string value = key?.GetValue(RegKey)?.ToString() ?? "";
                return string.Equals(value, "ro", StringComparison.OrdinalIgnoreCase)
                    ? AppLanguage.Romanian
                    : AppLanguage.English;
            }
            catch
            {
                return AppLanguage.English;
            }
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
