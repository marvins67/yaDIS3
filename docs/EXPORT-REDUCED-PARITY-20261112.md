# Parité fonctionnelle de l’export réduit — 12 novembre 2026

## Résultat et limites

Audit effectué sur les **véritables fichiers des deux ZIP**, extraits dans le
répertoire de travail, puis comparés intégralement. Les CSV fournis servent de
contre-vérification et n’alimentent pas l’outil de comparaison.

**Un défaut de génération est démontré et corrigé : les indices de Récents après
regroupement des sagas.** Le test reproduit d’abord l’échec (indice 2 au lieu de 1),
puis vérifie deux exports successifs. Un second test reconstruit une collection
synthétique de 24 films à partir des liens du ZIP yaDIS2 et compare **tous les
octets** de `00/02/dune_folder.txt` régénéré au véritable fichier de référence.
Cette reconstruction est documentée dans `tests/YaDIS3.Tests/Fixtures/ReducedExport` ;
elle ne remplace pas le workspace original.

Les archives ne contiennent **ni `data.xml`, ni `groups.xml`, ni `shortcuts.xml`,
ni configuration des chemins, ni historique complet du workspace**. L’identité des XML, index persistants et réglages lors du traitement
n’est pas confirmée. Il est donc
impossible d’établir l’égalité des données sources ou de régénérer honnêtement
l’intégralité du jeu original. Les causes amont des autres écarts ne sont pas
présentées comme prouvées lorsque seules leurs manifestations sont disponibles.
Aucune appartenance, métadonnée, image ou sortie du ZIP n’a été retouchée.

Branche : `feature/redesign`, sans branche supplémentaire ni PR.
La version du programme ayant produit chaque ZIP n’est pas enregistrée dans les
archives : la conformité du code actuel ne prouve donc pas la provenance de
chaque fichier fourni.

Base yaDIS3 : `02d25113b58a92f7a1f86537b6e5894053523163`.
Référence yaDIS2 : `6a985ea2e515800061e1808db24fc71e5958e233`.
Le SDK .NET 10.0.404 a été installé localement pour les contrôles ; aucune dépendance
applicative ajoutée. Cible .NET 10 / C# 14 conservée.

## Inventaire vérifié

| Indicateur | Mesuré | Audit initial |
|---|---:|---:|
| Fichiers yaDIS2 | 977 | 977 |
| Fichiers yaDIS3 | 980 | 980 |
| Identiques | 279 | 279 |
| Communs différents | 689 | 689 |
| Uniquement yaDIS2 | 9 | 9 |
| Uniquement yaDIS3 | 12 | 12 |
| AAI différents | 584 | 584 |
| JPEG différents | 48 | 48 |
| TXT différents | 57 | 57 |

Aucune divergence numérique. Les dossiers ZIP ne sont pas comptés comme fichiers.
Les 57 chemins, nombres d’entrées et ordres de captions du CSV de menus ont aussi
été contre-vérifiés sur les fichiers réels : aucune divergence.
Les différences graphiques sont inventoriées uniquement, sans modification du rendu.
Les 57 TXT communs différents se décomposent en **53 fichiers avec valeurs de
propriétés différentes et 4 fichiers avec seulement des lignes vides différentes**.
Il existe aussi trois TXT uniquement yaDIS2 et trois uniquement yaDIS3, associés
aux personnes : ils s’ajoutent aux 57 et sont traités ci-dessous.

## Méthodes historiques et règles contrôlées

Référence : `yaDIS/yaDIS/ProcessForm.cs` dans le dépôt yaDIS2.

| Domaine | Référence yaDIS2 | yaDIS3 / conclusion |
|---|---|---|
| Liste générale | `bgwExport_DoWork`, lignes 1463–1495 | `WriteGeneralMovieListing` / `WriteGeneralMovie` : ordre de `MainModule.Medias`, type film, statut différent de 3, compteur consommé même pour une saga déjà représentée. Fichier général identique dans les ZIP. |
| Récents | `bgwExport_DoWork`, lignes 1496–1557 | `WriteRecent` / `WriteMovieListing` : exclusion des anciennes fiches, ordre de collection conservé, préfixe âge optionnel. **Un doublon de saga n’incrémente pas le compteur**, contrairement aux sélections. Correction limitée à ce point. |
| Sélections | `bgwExport_DoWork`, lignes 1565–1607 | `WriteSelections` : `selection enabled=yes`, appartenance par égalité de `filename`, parcours de collection, séries/statut 3 exclus. Les doublons de saga consomment un indice. Un film peut appartenir à plusieurs sélections. |
| Sagas | `bgwExport_DoWork`, lignes 1408–1454 | `WriteSaga` / `WriteSagaDefinition` : ordre des éléments `movie` du XML, captions 00, 01… et indices 0, 1… ; aucun tri par date ou titre. |
| Alphabet | `CreateAlphabet`, lignes 6474–6570 ; ajout des sagas dans `bgwExport_DoWork` | `WriteAlphabetEntries` : films dans l’ordre de collection puis sagas dans l’ordre XML. Pas de normalisation des accents. Correctif `77134f5` conservé ; fichiers alphabétiques du jeu identiques. |
| Genres, acteurs, réalisateurs, durées, pays, années, notes, formats | `AddToIndex`, lignes 5818–5975 ; `CreateIndexItem`, lignes 6159–6246 ; `SyncInterface`, ligne 6798 | `LegacyMovieIndexes.Create`, `LegacyMovieIndexStore.Update/CopyCategory`, `DuneFile.AddIndexMovie` : listes **persistantes**, ajoutées au traitement, recopiées à l’export. Aucun tri alphabétique ne doit être ajouté. |
| Personnes | `AddToIndex`, lignes 5834–5881 | Valeur littérale des nœuds `actor` (cinq premiers) et `director`, initiale après le dernier espace. Ni nom de tri ni translittération. |
| Épisodes | `ProcessTVShow`, ligne 4931 puis 5726–5733 | yaDIS2 lit `item/dunepath`, enlève `item/path` du nom du fichier, remplace les antislashs. yaDIS3 `DuneExporter.Plan` → `DunePaths.Resolve` applique les correspondances explicites puis le chemin hérité. |
| Raccourcis | `bgwExport_DoWork`, lignes 715–746 | `WriteShortcuts` : `../00/01_<nom>` pour une sélection activée. Les quatre TXT `05_*` sont identiques. |
| Encodage | `MainModule.CreateDF`, ligne 1080 | UTF-8 avec BOM, CRLF ; pas de remplacement arbitraire des noms Unicode. |

### Index persistants et versions de Dune

Les différences d’ordre des index 04–11 suivent des historiques d’ajout différents.
Le code historique **ne reconstruit pas ces listes à partir de la collection lors
de l’export**. Les lignes vides yaDIS2 sont compatibles avec l’effacement historique
d’anciennes entrées, mais l’historique exact n’est pas présent dans les ZIP.
Les tests existants `LegacyIndexPersistenceTests` et `LegacyIndexMembershipTests`
caractérisent la conservation du premier libellé, des indices et du fichier
antérieurement enregistré ; ils ne sont pas modifiés.

Cinq index pointent vers `Dune (2021).mkv` côté yaDIS2 et vers
`Dune (1984) Redux.mkv` côté yaDIS3 : Aventure, Science-Fiction, 3h 00, US et
Matroska (voir détail brut pour chaque catégorie ; une catégorie peut contenir les
deux versions à un autre rang). Les deux films portent le même titre « Dune ».
`CreateIndexItem` conserve le premier libellé déjà présent, sans remplacer le lien.
**Aucun correctif de tri ou de choix de version ne serait justifié sur ce constat.**
Il faut comparer les index persistants et l’ordre de traitement des deux workspaces.

### Sagas

- Alien : ordre yaDIS2 **2, 3, 4, 1**, ordre yaDIS3 **1, 2, 3, 4**. Les captions
  numérotées, la grille et toutes les autres clés sont identiques. Les deux codes
  lisent l’ordre XML. Sans les deux `groups.xml`, impossible de prouver si les
  données différaient lors de la production des ZIP. Aucun nouveau correctif de
  classement et aucune exception Alien.
- Batman : même ordre **1, 2, 3**, TXT intégralement identiques.
- X-Men : même ordre **1, 2, 3**, TXT intégralement identiques.
- Les tests existants `Export_SagaCustomOrder_UsesXmlOrderNotTitleOrChronology`,
  `Export_SagaOrderEditedAndExportedAgain_PreservesUpdatedOrder` et
  `Export_SagaMissingFilm_PreservesXmlMemberOrderAndLinks` couvrent la persistance,
  les exports successifs et un membre absent de la collection. Le XML lui-même
  reste la source de vérité, y compris pour un membre absent.

### Sélections

- Papa : « Le Fabuleux Destin d’Amélie Poulain » présent uniquement dans yaDIS2 ;
  les deux entrées suivantes se décalent de 15/16 à 14/15 dans yaDIS3.
- Enfants : ce même film apparaît uniquement dans yaDIS3, à l’indice 4.
- Disney et Ghibli : TXT intégralement identiques.
- Le film existe dans les deux exports. Les règles de lecture/export comparées
  ne déplacent aucun film entre sélections. La manifestation est cohérente avec
  des appartenances différentes, **mais les appartenances originales ne sont pas
  disponibles et cette cause amont reste à confirmer**. Aucune modification des
  appartenances pour forcer l’égalité.
- Le nouveau test `Export_MultipleMemberships_PreservesCollectionOrderAndSourceGroups`
  couvre les quatre sélections, un film partagé, les doublons XML, une série et un
  film en erreur, l’ordre inverse du XML par rapport à la collection, les quatre
  raccourcis et deux exports successifs sans modification de `groups.xml`.

### Noms Unicode

Les différences sont des chaînes distinctes, pas une corruption UTF-8 :
`Shigesato Itoi` / `糸井重里`, `Hitoshi Takagi` / `高木均`, `Eric Summer` /
`Éric Summer`. Les trois anciens dossiers et les trois nouveaux contiennent des
listes vers les mêmes films correspondants ; leurs suites de propriétés sont
strictement identiques pour chaque paire. Les clés de personne sont les noms
littéraux issus des XML au traitement, ou les noms déjà persistés dans l’index.
L’origine exacte (scraping, modification XML, index ancien) est indécidable sans
ces sources ; aucune règle historique n’autorise à substituer ces noms.
Les nouveaux cas de `Create_ValidPersonName_PreservesLegacySpelling` couvrent ces
six formes et É décomposé, sans translittération ni normalisation Unicode.

### Séries, saisons et chemins

Les **28 épisodes** diffèrent uniquement par un préfixe de `media_url` :
`/Séries/...` contre
`smb://192.168.1.10/DuneHDD_26b6a642067197ba/Séries/...`.
Les suffixes (noms, espaces, accents, extensions) sont identiques ; aucune
transformation en encodage URL n’est constatée. Les 24 chemins de lecture des
films sont au contraire identiques, avec des racines absolues `/Films/`,
`/Disney/` ou `/Ghibli/` : l’écart SMB ne concerne donc pas toutes les fiches.
Les définitions sont produites au traitement (`DuneExporter.Process` →
`WriteMedia` / `LegacyDuneMediaWriter.Write` → `LegacyProcessedMediaPublisher.Publish`).
L’export central `ExportLegacy` synchronise les définitions déjà publiées :
changer un réglage de chemin sans retraiter les fiches ne prouve pas l’origine
ou l’actualité du TXT présent dans le cache. Les trois séries, les deux saisons
de From, les clés d’épisodes et les autres propriétés sont identiques.

Il n’est pas établi que les deux exports utilisent la même configuration.
Un `dunepath` vide côté yaDIS2 associé au remplacement du chemin source peut
produire le chemin absolu observé ; une correspondance SMB côté yaDIS3 produit le
second. Ce sont des hypothèses à vérifier sur `item/path`, `item/dunepath`, les
nœuds `episode/file` et la configuration des racines, pas un motif pour enlever SMB.
`DunePaths.Resolve` applique les correspondances explicites les plus précises,
puis les données héritées ; cette différence de mécanisme ne prouve pas que le
préfixe observé soit erroné. Aucun lien externe n’est modifié.

Le jeu n’expose pas d’épisode multiple dans un même fichier. Les contrôles de
saisons et fichiers partagés utilisent le nouveau test synthétique
`Export_SeasonsAndSharedFile_PreservesConfiguredPlaybackRoot` (racines absolue, SMB
et storage_uuid) ; cela ne
prouve pas la configuration du jeu réel. Les chemins relatifs `../` des menus
sont vérifiés séparément des chemins externes de lecture.

## Tableau des 57 fichiers initialement différents

Chaque fichier est comparé intégralement dans
[`txt-differences.diff`](audit-reduit-20261112/txt-differences.diff).
Toutes les propriétés dans leur ordre, clés modifiées, entrées et numérotations
avant/après sont conservées dans
[`audit.json`](audit-reduit-20261112/audit.json) et
[`menus.csv`](audit-reduit-20261112/menus.csv).

Codes de suivi :
- **R** : défaut de code démontré, corrigé, sortie de référence régénérée et comparée.
- **I** : ordre/choix de version persisté dans les index ; cause amont exacte à
  confirmer sur workspace/historique. Règles historiques déjà reproduites, pas de tri ajouté.
- **S** : différence d’appartenance observée, origine amont non vérifiable sans XML.
- **A** : ordre de saga différent, origine amont non vérifiable sans XML.
- **E** : préfixe externe différent, configuration équivalente non démontrée.
- **P** : présentation seulement, valeurs et ordre utiles identiques.

Les tests de caractérisation établissent les règles, ils ne résolvent pas à eux
seuls les différences de données des ZIP figés.

| Fichier | Clés modifiées | Cause / correction | Test associé | Résultat |
|---|---:|---|---|---|
| `00/01_Enfants/dune_folder.txt` | 8 | S : Amélie dans une sélection différente ; XML absent | Export_MultipleMemberships… ; Export_SelectionAvecSagaRepetee… | cause amont à confirmer ; ZIP inchangés |
| `00/01_Papa/dune_folder.txt` | 16 | S : Amélie dans une sélection différente ; XML absent | Export_MultipleMemberships… ; Export_SelectionAvecSagaRepetee… | cause amont à confirmer ; ZIP inchangés |
| `00/02/dune_folder.txt` | 104 | R : indices après doublons de saga ; compteur corrigé | Export_RecentSagaRepeated… ; Export_ReducedArchiveRecent… | corrigé ; fichier régénéré identique octet par octet |
| `00/04/Action/dune_folder.txt` | 24 | I : ordre persistant ; historique absent | LegacyIndexPersistenceTests ; LegacyIndexMembershipTests | règle conservée ; origine amont à confirmer |
| `00/04/Animation/dune_folder.txt` | 8 | I : ordre persistant ; historique absent | LegacyIndexPersistenceTests ; LegacyIndexMembershipTests | règle conservée ; origine amont à confirmer |
| `00/04/Aventure/dune_folder.txt` | 3 | I : ordre persistant et version Dune retenue ; historique absent | LegacyIndexPersistenceTests ; LegacyIndexMembershipTests | règle conservée ; origine amont à confirmer |
| `00/04/Comédie/dune_folder.txt` | 12 | I : ordre persistant ; historique absent | LegacyIndexPersistenceTests ; LegacyIndexMembershipTests | règle conservée ; origine amont à confirmer |
| `00/04/Crime/dune_folder.txt` | 8 | I : ordre persistant ; historique absent | LegacyIndexPersistenceTests ; LegacyIndexMembershipTests | règle conservée ; origine amont à confirmer |
| `00/04/Familial/dune_folder.txt` | 8 | I : ordre persistant ; historique absent | LegacyIndexPersistenceTests ; LegacyIndexMembershipTests | règle conservée ; origine amont à confirmer |
| `00/04/Science-Fiction/dune_folder.txt` | 28 | I : ordre persistant et version Dune retenue ; historique absent | LegacyIndexPersistenceTests ; LegacyIndexMembershipTests | règle conservée ; origine amont à confirmer |
| `00/04/Thriller/dune_folder.txt` | 8 | I : ordre persistant ; historique absent | LegacyIndexPersistenceTests ; LegacyIndexMembershipTests | règle conservée ; origine amont à confirmer |
| `00/05/P/Ron Perlman/dune_folder.txt` | 0 | P : lignes vides historiques ; aucune correction | comparaison intégrale + propriétés ordonnées | aucun écart de valeurs |
| `00/05/R/Winona Ryder/dune_folder.txt` | 0 | P : lignes vides historiques ; aucune correction | comparaison intégrale + propriétés ordonnées | aucun écart de valeurs |
| `00/05/S/Patrick Stewart/dune_folder.txt` | 0 | P : lignes vides historiques ; aucune correction | comparaison intégrale + propriétés ordonnées | aucun écart de valeurs |
| `00/06/J/Jean-Pierre Jeunet/dune_folder.txt` | 8 | I : ordre persistant ; historique absent | LegacyIndexPersistenceTests ; LegacyIndexMembershipTests | règle conservée ; origine amont à confirmer |
| `00/07/2h 00/dune_folder.txt` | 0 | P : lignes vides historiques ; aucune correction | comparaison intégrale + propriétés ordonnées | aucun écart de valeurs |
| `00/07/2h 30/dune_folder.txt` | 24 | I : ordre persistant ; historique absent | LegacyIndexPersistenceTests ; LegacyIndexMembershipTests | règle conservée ; origine amont à confirmer |
| `00/07/3h 00/dune_folder.txt` | 8 | I : ordre persistant et version Dune retenue ; historique absent | LegacyIndexPersistenceTests ; LegacyIndexMembershipTests | règle conservée ; origine amont à confirmer |
| `00/08/GB/dune_folder.txt` | 12 | I : ordre persistant ; historique absent | LegacyIndexPersistenceTests ; LegacyIndexMembershipTests | règle conservée ; origine amont à confirmer |
| `00/08/US/dune_folder.txt` | 44 | I : ordre persistant et version Dune retenue ; historique absent | LegacyIndexPersistenceTests ; LegacyIndexMembershipTests | règle conservée ; origine amont à confirmer |
| `00/09/1970/dune_folder.txt` | 8 | I : ordre persistant ; historique absent | LegacyIndexPersistenceTests ; LegacyIndexMembershipTests | règle conservée ; origine amont à confirmer |
| `00/09/1980/dune_folder.txt` | 12 | I : ordre persistant ; historique absent | LegacyIndexPersistenceTests ; LegacyIndexMembershipTests | règle conservée ; origine amont à confirmer |
| `00/09/2000/dune_folder.txt` | 8 | I : ordre persistant ; historique absent | LegacyIndexPersistenceTests ; LegacyIndexMembershipTests | règle conservée ; origine amont à confirmer |
| `00/09/2010/dune_folder.txt` | 12 | I : ordre persistant ; historique absent | LegacyIndexPersistenceTests ; LegacyIndexMembershipTests | règle conservée ; origine amont à confirmer |
| `00/10/6/dune_folder.txt` | 12 | I : ordre persistant ; historique absent | LegacyIndexPersistenceTests ; LegacyIndexMembershipTests | règle conservée ; origine amont à confirmer |
| `00/10/7/dune_folder.txt` | 40 | I : ordre persistant ; historique absent | LegacyIndexPersistenceTests ; LegacyIndexMembershipTests | règle conservée ; origine amont à confirmer |
| `00/10/8/dune_folder.txt` | 8 | I : ordre persistant ; historique absent | LegacyIndexPersistenceTests ; LegacyIndexMembershipTests | règle conservée ; origine amont à confirmer |
| `00/11/Matroska/dune_folder.txt` | 44 | I : ordre persistant et version Dune retenue ; historique absent | LegacyIndexPersistenceTests ; LegacyIndexMembershipTests | règle conservée ; origine amont à confirmer |
| `00/98/Alien/dune_folder.txt` | 12 | A : Alien 2,3,4,1 / 1,2,3,4 ; XML absent | Export_SagaCustomOrder… ; Export_SagaOrderEditedAndExportedAgain… | cause amont à confirmer ; aucun correctif justifié |
| `01/And Then There Were None/01/01_001_000/dune_folder.txt` | 1 | E : préfixe / contre SMB ; configuration absente | LegacyDunePathTests ; LegacyExportIntegrationTests | configuration à confirmer ; liens externes préservés |
| `01/And Then There Were None/01/01_002_000/dune_folder.txt` | 1 | E : préfixe / contre SMB ; configuration absente | LegacyDunePathTests ; LegacyExportIntegrationTests | configuration à confirmer ; liens externes préservés |
| `01/And Then There Were None/01/01_003_000/dune_folder.txt` | 1 | E : préfixe / contre SMB ; configuration absente | LegacyDunePathTests ; LegacyExportIntegrationTests | configuration à confirmer ; liens externes préservés |
| `01/Chernobyl/01/01_001_000/dune_folder.txt` | 1 | E : préfixe / contre SMB ; configuration absente | LegacyDunePathTests ; LegacyExportIntegrationTests | configuration à confirmer ; liens externes préservés |
| `01/Chernobyl/01/01_002_000/dune_folder.txt` | 1 | E : préfixe / contre SMB ; configuration absente | LegacyDunePathTests ; LegacyExportIntegrationTests | configuration à confirmer ; liens externes préservés |
| `01/Chernobyl/01/01_003_000/dune_folder.txt` | 1 | E : préfixe / contre SMB ; configuration absente | LegacyDunePathTests ; LegacyExportIntegrationTests | configuration à confirmer ; liens externes préservés |
| `01/Chernobyl/01/01_004_000/dune_folder.txt` | 1 | E : préfixe / contre SMB ; configuration absente | LegacyDunePathTests ; LegacyExportIntegrationTests | configuration à confirmer ; liens externes préservés |
| `01/Chernobyl/01/01_005_000/dune_folder.txt` | 1 | E : préfixe / contre SMB ; configuration absente | LegacyDunePathTests ; LegacyExportIntegrationTests | configuration à confirmer ; liens externes préservés |
| `01/From/01/01_001_000/dune_folder.txt` | 1 | E : préfixe / contre SMB ; configuration absente | LegacyDunePathTests ; LegacyExportIntegrationTests | configuration à confirmer ; liens externes préservés |
| `01/From/01/01_002_000/dune_folder.txt` | 1 | E : préfixe / contre SMB ; configuration absente | LegacyDunePathTests ; LegacyExportIntegrationTests | configuration à confirmer ; liens externes préservés |
| `01/From/01/01_003_000/dune_folder.txt` | 1 | E : préfixe / contre SMB ; configuration absente | LegacyDunePathTests ; LegacyExportIntegrationTests | configuration à confirmer ; liens externes préservés |
| `01/From/01/01_004_000/dune_folder.txt` | 1 | E : préfixe / contre SMB ; configuration absente | LegacyDunePathTests ; LegacyExportIntegrationTests | configuration à confirmer ; liens externes préservés |
| `01/From/01/01_005_000/dune_folder.txt` | 1 | E : préfixe / contre SMB ; configuration absente | LegacyDunePathTests ; LegacyExportIntegrationTests | configuration à confirmer ; liens externes préservés |
| `01/From/01/01_006_000/dune_folder.txt` | 1 | E : préfixe / contre SMB ; configuration absente | LegacyDunePathTests ; LegacyExportIntegrationTests | configuration à confirmer ; liens externes préservés |
| `01/From/01/01_007_000/dune_folder.txt` | 1 | E : préfixe / contre SMB ; configuration absente | LegacyDunePathTests ; LegacyExportIntegrationTests | configuration à confirmer ; liens externes préservés |
| `01/From/01/01_008_000/dune_folder.txt` | 1 | E : préfixe / contre SMB ; configuration absente | LegacyDunePathTests ; LegacyExportIntegrationTests | configuration à confirmer ; liens externes préservés |
| `01/From/01/01_009_000/dune_folder.txt` | 1 | E : préfixe / contre SMB ; configuration absente | LegacyDunePathTests ; LegacyExportIntegrationTests | configuration à confirmer ; liens externes préservés |
| `01/From/01/01_010_000/dune_folder.txt` | 1 | E : préfixe / contre SMB ; configuration absente | LegacyDunePathTests ; LegacyExportIntegrationTests | configuration à confirmer ; liens externes préservés |
| `01/From/02/02_001_000/dune_folder.txt` | 1 | E : préfixe / contre SMB ; configuration absente | LegacyDunePathTests ; LegacyExportIntegrationTests | configuration à confirmer ; liens externes préservés |
| `01/From/02/02_002_000/dune_folder.txt` | 1 | E : préfixe / contre SMB ; configuration absente | LegacyDunePathTests ; LegacyExportIntegrationTests | configuration à confirmer ; liens externes préservés |
| `01/From/02/02_003_000/dune_folder.txt` | 1 | E : préfixe / contre SMB ; configuration absente | LegacyDunePathTests ; LegacyExportIntegrationTests | configuration à confirmer ; liens externes préservés |
| `01/From/02/02_004_000/dune_folder.txt` | 1 | E : préfixe / contre SMB ; configuration absente | LegacyDunePathTests ; LegacyExportIntegrationTests | configuration à confirmer ; liens externes préservés |
| `01/From/02/02_005_000/dune_folder.txt` | 1 | E : préfixe / contre SMB ; configuration absente | LegacyDunePathTests ; LegacyExportIntegrationTests | configuration à confirmer ; liens externes préservés |
| `01/From/02/02_006_000/dune_folder.txt` | 1 | E : préfixe / contre SMB ; configuration absente | LegacyDunePathTests ; LegacyExportIntegrationTests | configuration à confirmer ; liens externes préservés |
| `01/From/02/02_007_000/dune_folder.txt` | 1 | E : préfixe / contre SMB ; configuration absente | LegacyDunePathTests ; LegacyExportIntegrationTests | configuration à confirmer ; liens externes préservés |
| `01/From/02/02_008_000/dune_folder.txt` | 1 | E : préfixe / contre SMB ; configuration absente | LegacyDunePathTests ; LegacyExportIntegrationTests | configuration à confirmer ; liens externes préservés |
| `01/From/02/02_009_000/dune_folder.txt` | 1 | E : préfixe / contre SMB ; configuration absente | LegacyDunePathTests ; LegacyExportIntegrationTests | configuration à confirmer ; liens externes préservés |
| `01/From/02/02_010_000/dune_folder.txt` | 1 | E : préfixe / contre SMB ; configuration absente | LegacyDunePathTests ; LegacyExportIntegrationTests | configuration à confirmer ; liens externes préservés |

## Intégrité des liens

L’analyse de chaque `media_url`, `icon_path`, `icon_sel_path` et `background_path`
recense **2 248 références de chaque côté** : 2 196 références locales et
52 références externes aux vidéos. **Zéro destination de navigation absente**,
dans chacun des deux exports. Le détail est dans
`audit-reduit-20261112/references.csv`.

Il subsiste des **ressources graphiques locales absentes** :

| Export | Propriété | Cible absente |
|---|---|---|
| yaDIS2 | `00/01_Papa/dune_folder.txt` : `icon_path` | `00/01_Papa/icon.aai` |
| yaDIS2 | `00/01_Enfants/dune_folder.txt` : `icon_path` | `00/01_Enfants/icon.aai` |
| yaDIS2 | `00/01_Disney/dune_folder.txt` : `icon_path` | `00/01_Disney/icon.aai` |
| yaDIS2 et yaDIS3 | `00/01_Ghibli/dune_folder.txt` : `icon_path` | `00/01_Ghibli/icon.aai` |

La [documentation officielle Dune](https://dune-hd.com/support/misc/dune_folder_howto.txt),
section F1/F2, confirme que `icon_path` sans préfixe désigne l’icône de ce dossier
vue depuis son parent. Ce n’est pas une valeur héritée pour tous les enfants ;
les paramètres `all_children.*` possèdent une portée distincte. Le contrôleur
vérifie donc ces ressources au même titre que les icônes explicites des entrées.
La navigation via les quatre raccourcis racine `05_*` et vers la fiche Totoro
est résoluble. Les quatre dossiers `01_Papa`, `01_Enfants`, `01_Disney` et `01_Ghibli` sont
explicitement masqués par `system_files` dans `00/dune_folder.txt` des deux
exports. Les quatre raccourcis `05_*` disposent de leurs propres icônes, toutes
présentes. La route de navigation habituelle conserve donc ses ressources ;
l’absence Ghibli dans le dossier masqué est déjà présente dans la référence
yaDIS2. Aucune modification des liens ou du rendu n’est justifiée ici.

Les `../` sont normalisés uniquement pour trouver leur cible, sans modifier les
chaînes exportées. Le nouveau test xUnit
`Export_CompleteTree_ResolvesExplicitLocalReferences` contrôle un export généré
avec film, série, index, sélection et raccourci. Il caractérise explicitement
l’absence de l’icône de saison pour une fixture composée uniquement de spéciaux :
`ProcessTVShow` ne dessine cette icône que pour `season > 0` (lignes 4943–4975),
tout en la référençant dans le TXT. Le test vérifie exactement ces absences
historiques, puis régénère une saison numérotée et contrôle que seule l’icône
propre à la sélection masquée reste absente. Il supprime ensuite une icône de
film pour vérifier qu’une ressource manquante d’entrée est détectée. Ce cas de
spéciaux est synthétique ; les ZIP fournis ne présentent pas ces absences de
saison. Aucun changement du rendu ou des XML d’origine n’est effectué.
Les six tests Python distinguent également navigation et images manquantes,
numérotation, ordre, Unicode, lignes vides et médias externes.

Ce contrôle ne remplace pas un essai matériel Dune HD et n’atteste pas que les
partages SMB ou fichiers `/Séries/...` soient accessibles depuis le lecteur.

## Mesure des écarts restants

- Les ZIP fournis restent immuables : **57 TXT communs différents, dont 53 avec
  valeurs différentes**, plus trois paires de TXT de personnes sous des noms
  distincts. Quatre différences communes sont uniquement des lignes vides.
- Le défaut de numérotation de **1 fichier** est corrigé à la source et sa sortie
  synthétique équivalente est vérifiée contre le fichier réel complet.
- Il reste **52 fichiers communs avec différences de valeurs à élucider ou
  réconcilier dans les entrées originales** si l’on soustrait ce défaut identifié.
  C’est un suivi des écarts initiaux, **pas la mesure d’un nouvel export complet**.
- Le nombre exact de différences fonctionnelles après régénération complète est
  **non mesurable sans les workspaces d’origine**. Les 28 préfixes externes, les
  appartenances, l’ordre Alien et les index persistants ne sont pas déclarés résolus.
- Aucun autre défaut de code expliquant ces écarts n’a été démontré ; aucun
  correctif conjectural ni tri supplémentaire n’est appliqué.

Pour terminer la validation à données équivalentes, il faut fournir les workspaces
utilisés pour les deux exports : `data/*/data.xml`, `groups.xml`, `shortcuts.xml`,
les correspondances de chemins et réglages Dune pertinents, ainsi que les index
persistants `interface/00/04` à `interface/00/11`. Les images sources seraient
également nécessaires pour régénérer un export complet ; leur rendu reste hors
périmètre de cette intervention.

## Reproduction

```sh
python3 tools/export-audit/compare_exports.py yaDIS2-20261112-2148.zip yaDIS3-20261112-2148.zip --output work/audit --check-navigation
python3 -m unittest discover -s tools/export-audit -v
dotnet restore
dotnet build yaDIS3.sln -c Release -m:1
dotnet test yaDIS3.sln -c Release -m:1
dotnet format yaDIS3.sln --verify-no-changes
```

L’outil utilise uniquement la bibliothèque standard Python et accepte également
les dossiers directement extraits. `audit.json` inclut les SHA-256 des ZIP, la
liste exhaustive des propriétés/entrées et les différences intégrales.
Le CSV utilisateur d’inventaire et les résultats directs contiennent exactement
les mêmes 710 chemins (689 différents + 9 uniquement yaDIS2 + 12 uniquement yaDIS3).

La comparaison sur les dossiers extraits produit le même rapport que celle
réalisée directement sur les ZIP, hors noms et empreintes des entrées.

## Empreintes des archives

- `yaDIS2-20261112-2148.zip` : `a4d4987a4f35d1049ea65c2ce3b92481fdacc35aee76a195a5d13a3118948b96`
- `yaDIS3-20261112-2148.zip` : `874346fee537fd6f086611f441b96297133f7e242eec03c1f027365f917b6526`

Le contrôle strict `--check-local-links` retourne 1 sur ce jeu, car il inclut
les quatre références d’icône absentes yaDIS2 et celle de Ghibli yaDIS3.
Le contrôle `--check-navigation` retourne 0. Les deux constats sont conservés
dans le rapport ; aucune ressource manquante n’est masquée.

## Validation finale

| Contrôle | Résultat |
|---|---|
| `dotnet restore` | Réussite, cinq projets restaurés |
| `dotnet build yaDIS3.sln -c Release -m:1` | Réussite, 0 avertissement, 0 erreur |
| `dotnet test yaDIS3.sln -c Release -m:1` | 457 tests principaux + 131 tests Desktop = **588 réussis**, 0 échec, 0 ignoré |
| Contrôleur de références final, revérifié séparément après caractérisation des spéciaux | **1 réussi**, 0 échec |
| `dotnet format yaDIS3.sln --verify-no-changes` | Réussite |
| Tests Python de l’outil de comparaison | **6 réussis** |
| Comparaison directe ZIP / dossiers extraits | Rapports identiques hors métadonnées des entrées |
| `--check-navigation` | Retour 0 : aucune destination de navigation absente |
| `--check-local-links` | Retour 1 attendu et vérifié : quatre icônes absentes yaDIS2, une yaDIS3 |

La suite principale a duré 40 min 24 s dans cet environnement. Les tests existants
sont exécutés séquentiellement et génèrent leurs images ; un contrôle de pile
a confirmé une activité d’encodage JPEG pendant les tests de Récents. Le moteur
n’a pas été modifié. Le dernier contrôleur a ensuite été recompilé et revérifié
avec les absences historiques exactes et une saison numérotée.

Nouveaux cas xUnit : deux pour la numérotation et le fichier Récents réel, un
pour les appartenances multiples, un pour l’intégrité des références, trois
pour les racines d’épisodes/saisons/fichier partagé et sept formes Unicode de
personnes. Les assertions des tests antérieurs n’ont pas été neutralisées.

## Commits de code et d’outillage

- [`dfff01c77d8584c19f25dabff506a8f7898f01c8` — fix export recent saga numbering](https://github.com/marvins67/yaDIS3/commit/dfff01c77d8584c19f25dabff506a8f7898f01c8).
- [`83bc8dfc7d2f3c3a419479bf5dfeb064e4942ade` — test export reduced parity audit](https://github.com/marvins67/yaDIS3/commit/83bc8dfc7d2f3c3a419479bf5dfeb064e4942ade).

Le présent rapport et les comparaisons brutes sont publiés dans un commit
documentaire distinct, identifié dans la livraison, sur `feature/redesign`.

## Poursuite du 13 novembre 2026

Deux règles supplémentaires de recherche de doublons ont été corrigées et
validées dans le [rapport de poursuite](EXPORT-PARITY-FOLLOWUP-20261113.md).
Les inventaires et conclusions ci-dessus restent ceux des archives du
12 novembre ; les écarts liés aux sources absentes ne sont pas requalifiés
comme résolus.
