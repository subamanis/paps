# paps

**P**etros' **A**wesome **P**owerShell **S**etup.

Το PowerShell 7 setup μου, μαζεμένο ώστε ένα καινούριο μηχάνημα να στήνεται με μία εντολή.

```powershell
git clone https://github.com/subamanis/paps.git D:\dev\paps
D:\dev\paps\setup.ps1
```

Το `setup.ps1` είναι idempotent, το ξανατρέχεις όποτε θες. Παραλείπει ό,τι υπάρχει ήδη.

| Flag | Τι κάνει |
|---|---|
| `-SkipTools` | Δεν αγγίζει το winget, μόνο modules και profile |
| `-CopyProfile` | Αντιγράφει το profile. Χωρίς αυτό φτιάχνει symlink, ώστε οι αλλαγές να γυρνάνε στο repo |

Το symlink θέλει Developer Mode ή elevation. Αν αποτύχει, πέφτει μόνο του σε αντιγραφή και σου το λέει.

## Τι μπαίνει

**Εργαλεία** (winget): `zoxide`, `fzf`, `fd`, `eza`, `ripgrep`, `bat`

**Modules** (gallery): `CompletionPredictor`, `Terminal-Icons`

**Modules** (χτίζονται εδώ): `ContextHistoryPredictor`

## Τα κομμάτια

### `profile/`

Το `Microsoft.PowerShell_profile.ps1`. Πηγαίνει στο `$PROFILE`.

### `predictor/`

Ένας PSReadLine predictor σε C# που φιλτράρει το ιστορικό με βάση τον φάκελο που είσαι.

Το πρόβλημα που λύνει: το ενσωματωμένο ιστορικό του PSReadLine αγνοεί το πού στέκεσαι. Στο project A γράφεις `.\ta` και σου προτείνει το `.\target\release\b.exe` του project B, που από εκεί δεν τρέχει καν.

Πώς αποφασίζει, για κάθε υποψήφια γραμμή:

| Το token | Κρίση | Απόφαση |
|---|---|---|
| `.\target\release\a.exe` | path, και υπάρχει εδώ | μέσα, και πρώτο |
| `.\target\release\b.exe` | path, λείπει από εδώ | έξω |
| `target/release/b.exe` | κατάληξη από γράμματα, λείπει | έξω |
| `target/debug/deps` | το `target` υπάρχει, το ολόκληρο όχι | έξω |
| `cargo build --release` | κανένα path | μέσα, ισχύει παντού |
| `feature/new-thing` | χωρίς κατάληξη, χωρίς πρόγονο που να λύνεται | μέσα, μπορεί να είναι git branch |
| `git@github.com:me/repo.git` | έχει `@`, είναι SSH URL | μέσα |

Ένα token θεωρείται σίγουρα path όταν ισχύει ένα από τα εξής: έχει backslash, είναι rooted, ξεκινάει με `./`, το τελευταίο του κομμάτι έχει κατάληξη από γράμματα, ή κάποιος πρόγονός του λύνεται σαν φάκελος εκεί που στέκεσαι. Μόνο τότε η απουσία του από τον δίσκο κόβει τη γραμμή.

Το `@` εξαιρείται πριν από όλα αυτά, γιατί πιάνει τα SSH URL και τα `@(...)` array literal του PowerShell που κουβαλάνε paths μέσα τους.

Το ταίριασμα ψάχνει το κείμενό σου **οπουδήποτε** μέσα στη γραμμή, όπως έκανε και η ενσωματωμένη πηγή του PSReadLine. Όσες ταιριάζουν από την αρχή έρχονται πρώτες.

Δύο ακόμα λεπτομέρειες στην ανάγνωση: ένα path μέσα σε εισαγωγικά διαβάζεται ολόκληρο ακόμα κι αν έχει κενά, και οι πολυγραμμικές εντολές με backtick ενώνονται σε μία ισοδύναμη γραμμή.

Ο cwd φτάνει στον predictor μέσω `LocationChangedAction`, γιατί το process current directory δεν ακολουθεί το `Set-Location` και ο predictor τρέχει σε δικό του νήμα.

Χτίζεται σε `net8.0` παρόλο που το PowerShell 7.6 τρέχει σε .NET 10, ώστε να μη χρειάζεται το .NET 10 SDK. Φορτώνει κανονικά.

Μετρημένο: **0.043 ms** ανά πάτημα πλήκτρου πάνω σε αρχείο 4334 γραμμών. Το PSReadLine δίνει 20 ms στα plugin predictors.

### `scripts/trim-history.ps1`

Κόβει το `ConsoleHost_history.txt`, που το PSReadLine δεν καθαρίζει ποτέ μόνο του. Πηγαίνει στο `Scripts/` δίπλα στο profile και το καλεί το profile.

Κρατάει ό,τι έτρεξες 2+ φορές, συν τις τελευταίες 1000 γραμμές. Οι επαναλήψεις μένουν μέσα, γιατί αυτές είναι η μέτρηση της συχνότητας.

Δύο φρουροί στην αρχή: δεν κάνει τίποτα κάτω από 1 MB, και δεν κάνει τίποτα αν τρέχει άλλο shell. Ο δεύτερος υπάρχει επειδή το PSReadLine κρατάει το μέγεθος του αρχείου σε μετρητή και κάνει `Seek` σε αυτό για να διαβάσει τι γράφουν τα άλλα παράθυρα. Αν το αρχείο μικρύνει από κάτω τους, το seek προσγειώνεται πέρα από το τέλος.

### `docs/cheatsheet.md`

Τι κάνει η κάθε εντολή και το κάθε shortcut.

## Το prediction setup

```powershell
Set-PSReadLineOption -PredictionSource Plugin
```

`Plugin`, ώστε να σβήσει η ενσωματωμένη πηγή ιστορικού. Η λίστα έρχεται από δύο plugins:

- `ContextHistory` για το ιστορικό, φιλτραρισμένο κατά φάκελο
- `Completion` για ό,τι είναι στον τρέχοντα φάκελο, μέσω της μηχανής του tab completion

Το `CompletionPredictor` δεν απαντάει στην πρώτη λέξη της γραμμής. Είναι σκόπιμο: εκεί το completion σαρώνει όλο το PATH και μετρήθηκε στα 53 ms, πάνω από το budget των 20 ms.

## Υγιεινή του ιστορικού

Το profile βάζει `AddToHistoryHandler` που πετάει τα typos πριν γραφτούν. Παίρνει το όνομα της εντολής από το AST και ρωτάει `Get-Command`. Αν δεν υπάρχει, η γραμμή δεν μπαίνει ούτε στη μνήμη ούτε στο αρχείο.

Χτίζεται πάνω στο `GetDefaultAddToHistoryOption`, οπότε το φίλτρο μυστικών του PSReadLine μένει ενεργό: το `$token = "ghp_..."` γίνεται `MemoryOnly` και δεν αγγίζει τον δίσκο.

Το ενσωματωμένο φίλτρο ψάχνει `password|asplaintext|token|apikey|secret` σε θέση ανάθεσης ή παραμέτρου, οπότε ένα `curl -H "Authorization: Bearer ..."` του ξεφεύγει. Από πάνω μπαίνει ένα δεύτερο πέρασμα για σχήματα διαπιστευτηρίων όπου κι αν βρίσκονται μέσα στη γραμμή: `Bearer`, `ghp_`, `github_pat_`, `sk-`, `xox[abprs]-`, `AKIA`, `AIza`, `glpat-`, και header σαν το `X-Api-Key:`. Δοκιμασμένο πάνω σε 4334 πραγματικές γραμμές ιστορικού με μηδέν ψευδώς θετικά.

Κόστος: 1 ms σε σωστή εντολή, 20 ms σε typo.
