# Documentația proiectului FOCUS AI

## Date de identificare

- **Titlul proiectului și acronimul:** F.O.C.U.S AI  
- **Categorie:** Seniori  
- **Secțiune:** Tehnologia Informației  

### Mentorul echipei
- prof. Apostol Valeriu  
- Liceul cu Program Sportiv Galați / Centrul Județean de Excelență Galați  
- Secțiunea: Robotică  
- Telefon: 0764273636  

### Echipa de proiect
- **Condrici Mihai**  
  - Clasa a X-a  
  - Colegiul Național „Vasile Alecsandri” Galați  
  - CJE Galați (C++, C#, Robotică)  
  - Rol: aplicație WPF + hardware  

- **Pătrașc Matteo**  
  - Clasa a X-a  
  - Colegiul Național „Costache Negri” Galați  
  - CJE Galați (C++, C#, Robotică)  
  - Rol: modul AI Python  

### Colaboratori
- Dinu Elena – Colegiul Național „Costache Negri” Galați  

---

## Rezumatul proiectului

Proiectul **F.O.C.U.S.** propune un sistem digital bimodal pentru evaluarea obiectivă a concentrării și comportamentului cognitiv.

Sistemul include:
- aplicație software în **WPF (.NET/C#)**
- stocare cloud prin **Firebase**
- modul AI în **Python (OpenCV, dlib)**
- modul hardware bazat pe **Arduino Giga R1 WiFi**

Funcționalități:
- eye-tracking
- monitorizare postură
- măsurare reacții
- analiză ECG (stres)

Rezultatul: generarea unui **profil cognitiv precis**.

---

## Scop

Dezvoltarea unei platforme hibride care:
- măsoară concentrarea
- analizează comportamentul cognitiv
- antrenează atenția utilizatorului

---

## Obiective

- Aplicație WPF + Firebase (Auth + Realtime DB)
- Modul AI (Python, OpenCV, dlib)
- Sistem hardware (Arduino Giga R1)
- Algoritm pentru **Indicele de Concentrare**

---

## Problema identificată

- Mediul digital reduce capacitatea de concentrare
- Metodele actuale sunt:
  - subiective
  - imprecise

---

## Studiu de piață

Soluții existente:
- eye-tracking (ex: Tobii)
- dispozitive hardware separate

Limitări:
- cost ridicat
- lipsă integrare

➡️ F.O.C.U.S oferă:
- integrare completă
- cost redus
- scalabilitate

---

## Etapele parcurse

1. Arhitectură sistem
2. Dezvoltare AI (eye-tracking)
3. Dezvoltare hardware
4. UI/UX WPF
5. Integrare și sincronizare

---

## Descrierea sistemului

### 1. Monitorizare vizuală (AI)
- detectare față și ochi
- urmărire privire
- detectare oboseală

### 2. Hardware (Arduino)
- matrice LED 8x8 (stimuli vizuali)
- motor vibrație (stimuli auditivi)
- buton tactil (reacție)
- senzor ultrasonic (postură)
- senzor ECG (stres)

### 3. Aplicație desktop (WPF)
- interfață grafică
- grafice în timp real
- Firebase:
  - autentificare
  - bază de date

---

## Date experimentale

- **AI:** 30 FPS, acuratețe 85–90%
- **Hardware:** latență < 10 ms
- **Cloud:** latență ~200 ms

---

## Concluzie

F.O.C.U.S reprezintă o platformă completă de analiză neuro-cognitivă care combină:
- AI
- hardware interactiv
- analiză biometrică

➡️ O soluție modernă pentru evaluarea atenției în era digitală.

---

## Bibliografie

- Holmqvist et al. (2011) – Eye Tracking  
- Posner & Petersen (1990) – Attention System  
- Bradski (2000) – OpenCV  
- King (2009) – dlib  
- Arduino Docs  
- Firebase Docs  
- Microsoft WPF Docs  

---
