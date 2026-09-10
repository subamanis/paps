# PowerShell setup

Το PowerShell 7 setup μου, μαζεμένο ώστε ένα καινούριο μηχάνημα να στήνεται με μία εντολή.

```powershell
git clone <this repo> D:\dev\Powershell
D:\dev\Powershell\setup.ps1
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

| Το token | Υπάρχει κάτω από το cwd; | Απόφαση |
|---|---|---|
| `.\target\release\b.exe` | όχι | έξω |
| `.\target\release\a.exe` | ναι | μέσα, και πρώτο |
| `cargo build --release` | δεν έχει path | μέσα, ισχύει παντού |
| `feature/new-thing` | δεν φαίνεται σίγουρα path | μέσα, μπορεί να είναι git branch |

Η τελευταία γραμμή είναι σκόπιμη. Ένα token με forward slash που δεν ξεκινάει με `./` και δεν έχει backslash μένει, ώστε τα ονόματα των branch να μη θεωρούνται χαμένα paths.

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

Κόστος: 1 ms σε σωστή εντολή, 20 ms σε typo.
