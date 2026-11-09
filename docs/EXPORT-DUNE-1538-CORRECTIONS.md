# Export Dune — corrections après l’audit du 9 novembre 2026

## Périmètre et preuves

Branche : `feature/redesign`. Référence fonctionnelle : dépôt `marvins67/yaDIS`,
`yaDIS/yaDIS/ProcessForm.cs` (commit de référence
`6a985ea2e515800061e1808db24fc71e5958e233`), méthodes `ProcessMovie`, `bgwExport_DoWork`,
`CreateIndexItem` et `SyncInterface`.

Le CSV fourni contient bien 908 lignes : 333 fichiers communs différents,
35 présents seulement dans yaDIS2 et 540 présents seulement dans yaDIS3.
Cette vérification porte sur le CSV, pas sur une nouvelle extraction des ZIP.
Le CSV des références contient 4 192 signalements yaDIS2 et 4 771 yaDIS3.
Les médias situés sur le stockage Dune et les icônes facultatives ne sont pas
assimilés à des liens cassés vérifiés localement.
Le CSV signale aussi deux écarts sous `01` (`background.jpg` et `icon.aai`) :
l’affirmation « tout le dossier 01 est identique » ne s’applique donc pas
à ces deux fichiers de menu.

Les deux archives dépassent la limite de transfert de 32 Mio de l’outil de
pièces jointes. Ni le workspace de traitement (XML, illustrations), ni les
options effectives de l’export du 15 h 38 ne sont disponibles dans le checkout.
Aucun nouvel export de cette médiathèque ni comparaison pixel à pixel des
archives n’est revendiqué.

## Fiches 00/99

`ProcessMovie` produit la fiche et ses images pendant le traitement dans
`workspace/interface/00/99`. `bgwExport_DoWork` génère les menus, puis
`SyncInterface` copie les fichiers présents. Il ne traite pas les films.

Dans yaDIS3, `ExportLegacy` appelait `WriteMedia` pour chaque définition absente,
sans exiger d’images : ce chemin peut effectivement fabriquer les coquilles
signalées par l’audit. Cela établit le défaut du code, sans prouver à lui seul
l’historique de chacune des 447 fiches des archives inaccessibles.

Le traitement produisait déjà les images dans cette branche. Il écrit désormais
également les définitions Dune dans le cache rendu **avant** la publication vers
`workspace/interface`. Un export peut restaurer un film depuis ce cache complet.
Une fiche complète déjà publiée est conservée. Un ancien cache d’images sans
sa définition n’est pas transformé artificiellement en fiche pendant l’export :
le film doit être retraité.

L’export ne génère plus de définition sans rendu. Un répertoire de film existant
sans aucune des trois images (`icon.aai`, `background.jpg`, `background.aai`)
est renommé avec le préfixe `_`, qui l’exclut de `SyncInterface`, et un
avertissement invite au traitement. Les fichiers restent récupérables dans le
workspace. Un répertoire contenant encore des images est conservé pour ne pas
supprimer un rendu récupérable ; un avertissement indique s’il est incomplet.
Une collision avec une sauvegarde existante interrompt l’opération sans écraser
cette sauvegarde. Ce mécanisme de réparation des anciennes coquilles est un
ajout explicite de migration ; il ne prétend pas être une opération de yaDIS2.

Cette correction ne doit pas être confondue avec la régénération de Ballerina :
il faut encore exécuter **Traiter** sur sa fiche avec ses données et illustrations.
Aucune suppression globale des 452 fiches ni exception par titre n’a été ajoutée.

La synchronisation conserve ses règles yaDIS2 (horodatages à la seconde,
exclusion des dossiers `_`, suppression des fichiers obsolètes, poursuite après
une erreur de copie). Ses erreurs sont désormais remontées dans les avertissements
de l’export au lieu d’être entièrement silencieuses. Aucune atomicité globale de
l’export n’est revendiquée.

Fichiers : `DuneExporter.cs`, `DuneExporter.Legacy.cs`,
`LegacyExportSynchronizer.cs` dans `src/YaDIS3.Rendering`.

## Classement et navigation

Le compteur de yaDIS2 avance pour chaque membre d’une sélection, y compris
quand sa saga est déjà représentée. `WriteMovieListing` compactait les indices
en sautant ces doublons. Il conserve maintenant ces indices historiques ; les
libellés restent `sort_title` / `sort_name`, dans l’ordre d’entrée reçu.
La liste générale et l’ordre XML des films d’une saga ne sont pas modifiés.

Pour les 26 sous-menus d’acteurs et les 26 de réalisateurs,
`icon_sel_path` était ajouté après les propriétés de présentation.
Il est maintenant inséré immédiatement après `icon_path`, comme dans yaDIS2.

Fichiers : `LegacyDuneCatalogWriter.cs`,
`LegacyDuneCatalogWriter.Navigation.cs`, `LegacyDuneCatalogWriter.Indexes.cs`.

Les différences d’appartenance aux catégories (genres, personnes, années,
pays, formats) ne sont pas attribuables au seul classement. Les index de yaDIS2
sont persistants et alimentés pendant le traitement. Sans XML et paramètres
identiques, leurs différences ne justifient pas de règles particulières par film
ni de reconstruction arbitraire des index existants.

## Rendu : correction limitée, parité stricte non atteinte

La mesure des bandeaux employait des avances fractionnaires ensuite arrondies
vers le haut glyphe par glyphe. Les avances ajustées aux pixels sont maintenant
utilisées pour la mesure des étiquettes de navigation. Avec le template livré
GLASS Revisited, les largeurs des exemples `0-9` et `R` deviennent respectivement
54 et 32 pixels. La hauteur utilise `Math.Round` comme `CreateIndexItem`,
au lieu d’un plafond systématique.

**Écart restant :** la métrique Skia livrée pour ce template produit une hauteur
de 38 pixels, contre 35 annoncés pour les exemples yaDIS2 dans le rapport.
Le test de caractérisation documente cette hauteur actuelle ; il ne valide pas
la parité verticale. Aucun facteur arbitraire n’a été ajouté pour forcer 35.
Les métriques de police, styles et options effectifs de l’export original restent
à comparer. Le rendu des glyphes reste assuré par SkiaSharp.

L’encodage AAI et JPEG n’a pas été modifié sans preuve : les différences
binaires, de dimensions et de pixels ne sont pas interchangeables.
Les 192 différences de dimensions et les 45 différences JPEG du rapport ne
sont donc **pas déclarées entièrement résolues**. Une comparaison visuelle
exhaustive exige les images de référence accessibles et les entrées du rendu.

Fichier : `TemplateRenderer.Navigation.cs`.

## Tests

Ajouts :

- `LegacyMovieCardPublicationTests` : aucun rendu / statut non traité,
  conservation des coquilles exclues, traitement JPEG et AAI, présence et
  référence des images, restauration du cache et export répété, erreur de copie
  observable et reprise.
- `LegacyExportMovieListTests` : indices de sélection avec plusieurs membres
  d’une saga et film indépendant, titre de tri et URL correspondante.
- `LegacyPersonMenuOrderTests` : ordre des propriétés des sous-menus de personnes.
- `LegacyIndexLabelMetricsTests` : largeurs mesurées des deux exemples et
  dimensions normales / sélectionnées.

Les tests existants sont conservés. L’assertion d’un raccourci dans
`LegacyExportWorkflowTests` décodait auparavant le fichier comme une chaîne
avec `|` sans en-tête ; elle décode désormais le format CreateDF réel
(en-tête, CRLF) avant de comparer les mêmes propriétés. L’échec était également
présent lors de l’exécution initiale avant modification.

Un second test de bureau cherchait encore `EpisodeChoice`, alors que le XAML
de départ n’expose plus ce contrôle. `SelectionBindingTests` vérifie désormais
la désactivation de la navigation précédent/suivant et de la sauvegarde ainsi
que l’absence de sélection, tout en conservant les assertions sur les éditeurs
vides. Aucune logique d’interface n’a été modifiée.

Aucune dépendance ajoutée. Les tests existants de suppression, changement de
titre, synchronisation, références et traitement restent dans la solution.

## Validation

Validation finale exécutée sur l’ensemble des modifications :

| Commande | Résultat |
| --- | --- |
| `dotnet restore` | Réussi |
| `dotnet build yaDIS3.sln -c Release -m:1` | Réussi, 0 avertissement, 0 erreur |
| `dotnet test yaDIS3.sln -c Release -m:1` | Réussi : 429 tests cœur/export + 131 tests bureau = 560, 0 échec, 0 ignoré |
| `dotnet format yaDIS3.sln --verify-no-changes` | Réussi |

Les 11 nouveaux cas s’ajoutent aux tests existants. La dernière commande complète
inclut bien les deux assertions historiques corrigées. Les premières exécutions
ont permis de diagnostiquer leurs échecs ; elles ne sont pas confondues avec
cette relance finale réussie. Les tests d’interface utilisent Avalonia Headless,
sans lecteur Dune physique ni nouvel export de la médiathèque de référence.

## Commits de correction

- Fiches et synchronisation : `cc8c38181ced285deb4acfb148957268e0f841e9`.
- Indexation et ordre des propriétés : `e538dc68ff33fb0fc2a62f4b53c24a7a516341e5`.
- Mesure des étiquettes (correction partielle du rendu) :
  `b9eceb3aa8bca7d2b2abcc018b7e42e65085318b`.

La maintenance des deux tests obsolètes et ce rapport sont isolés dans un
commit complémentaire, sans modification supplémentaire du comportement produit.
