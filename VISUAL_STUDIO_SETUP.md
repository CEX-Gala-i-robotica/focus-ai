# Deschiderea soluției Focus AI

1. Extrage complet arhiva într-un folder local (nu deschide `.sln` direct din ZIP).
2. Deschide `focus-ai.sln` în Visual Studio 2022.
3. Asigură-te că este instalat workload-ul **.NET desktop development** și un .NET SDK 9.0 sau mai nou.
4. În Visual Studio, folosește **Build > Restore NuGet Packages**, apoi **Build > Rebuild Solution**.
5. Pentru teste, selectează **Test > Run All Tests**.

Soluția conține proiectul WPF `focus-ai/focus-ai.csproj` și proiectul xUnit `focus-ai/tests/FocusAI.Tests/FocusAI.Tests.csproj`.

Dacă Visual Studio continuă să afișeze `Solution load finished with warnings`, verifică fereastra **View > Output**, selectează `Solution` sau `Project System`, și păstrează prima eroare completă. Mesajul generic din bara de stare nu identifică singur cauza.
