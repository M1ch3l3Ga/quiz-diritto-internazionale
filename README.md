# Quiz Diritto internazionale

Applicazione web per esercitarsi con test a risposta multipla: 20 domande a sessione,
4 risposte per domanda, correzione e spiegazione dopo ogni risposta, statistiche personali.

- **Tecnologia:** C# / Blazor WebAssembly (.NET 10), pubblicata gratis su **GitHub Pages**.
- **Utenti e statistiche:** **Supabase** (piano gratuito): accesso con email e password,
  sessioni e risposte salvate nel database e visibili da qualsiasi dispositivo.
- **Domande:** `QuizApp/wwwroot/data/questions.json` (253 domande sugli appunti corretti di
  Diritto internazionale, fino alla lezione del 23/11/2026 sui crimini internazionali e la
  Corte penale internazionale).

## Cosa fa

| Funzione | Dettaglio |
|---|---|
| Accesso | Registrazione con nome, email e password; recupero password via email |
| Sessione | 20 domande pescate dalla banca dati, opzioni rimescolate; si può scegliere un solo argomento |
| Correzione | Subito dopo ogni risposta: giusta/sbagliata, risposta corretta e spiegazione |
| Ripasso errori | Fino all'8 domande su 20 (40%) sono quelle la cui ultima risposta era sbagliata; poi le domande mai viste, poi quelle viste da più tempo |
| Statistiche | Storico sessioni (data, punteggio, durata), media, miglior risultato, grafico dell'andamento, percentuale per argomento, errori da ripassare |

Senza Supabase configurato l'app parte in **modalità locale**: utenti e statistiche restano
solo nel browser (utile per provarla sul PC).

## 1. Database Supabase (una volta sola)

1. Crea un account gratuito su <https://supabase.com> e un nuovo progetto (regione: Europa).
2. **SQL Editor → New query**: incolla il contenuto di `supabase/schema.sql` e premi **Run**.
   Crea le tabelle `quiz_sessions` e `quiz_answers` con la Row Level Security: ogni utente
   vede solo i propri dati.
3. **Authentication → Sign In / Providers → Email**:
   - lascia attivo *Enable Email provider*;
   - *Confirm email*: se attivo, chi si registra deve cliccare il link ricevuto prima di
     accedere (consigliato); se disattivato l'accesso è immediato.
4. **Authentication → URL Configuration**: in *Site URL* e in *Redirect URLs* inserisci
   l'indirizzo dell'app su GitHub Pages (vedi sotto), es. `https://NOMEUTENTE.github.io/quiz-diritto/`.
   Serve per i link di conferma e di recupero password.
5. **Project Settings → API Keys / Data API**: copia
   - il **Project URL** (es. `https://abcdefgh.supabase.co`);
   - la chiave **anon / publishable** (pubblica: è pensata per stare nel browser,
     i dati sono protetti dalla Row Level Security). **Non** usare la chiave `service_role` / secret.

> Nota: sul piano gratuito Supabase mette in pausa il progetto dopo circa una settimana
> senza alcun accesso; si riattiva dal pannello di Supabase con un clic.

## 2. Pubblicazione su GitHub Pages

1. Crea su GitHub un repository **pubblico** (es. `quiz-diritto`) e carica il contenuto di
   questa cartella (file `QuizApp/`, `supabase/`, `.github/`, `.gitignore`, `README.md`).
2. Nel repository: **Settings → Pages → Build and deployment → Source: GitHub Actions**.
3. **Settings → Secrets and variables → Actions → Variables → New repository variable**:
   - `SUPABASE_URL` = Project URL di Supabase
   - `SUPABASE_ANON_KEY` = chiave anon/publishable
4. Ogni push sul branch `main` avvia il workflow `.github/workflows/deploy.yml`, che compila
   l'app e la pubblica su `https://NOMEUTENTE.github.io/NOME-REPOSITORY/`
   (si può avviare anche a mano da **Actions → Deploy su GitHub Pages → Run workflow**).

## Sviluppo in locale

Serve .NET SDK 10.

```bash
cd QuizApp
dotnet run
```

Per collegare la versione locale a Supabase, inserisci URL e chiave in
`QuizApp/wwwroot/appsettings.json` (e aggiungi `http://localhost:5007` alle *Redirect URLs*).
Lasciandoli vuoti l'app usa la modalità locale.

## Modificare le domande

Le domande sono in `QuizApp/wwwroot/data/questions.json`, ciascuna nel formato:

```json
{
  "id": "t4-012",
  "topic": "Fonti: i trattati",
  "question": "Testo della domanda",
  "options": ["Risposta A", "Risposta B", "Risposta C", "Risposta D"],
  "answer": 2,
  "explanation": "Perché la risposta corretta è la C."
}
```

`answer` è la posizione della risposta giusta (0 = prima). Le statistiche usano `id` e `topic`:
non cambiare l'`id` di una domanda esistente, altrimenti il suo storico viene perso.
