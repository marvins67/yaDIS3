# Parité de l’index alphabétique Dune

## Sources historiques

Référence yaDIS2 : `6a985ea2e515800061e1808db24fc71e5958e233`.
Base yaDIS3 : `ba4676c7cf75385db79703c3b26b7782e8d89a0e`.

- `yaDIS/yaDIS/MainForm.cs:5863-5886` charge `MainModule.Medias`
  depuis `DirectoryInfo.GetDirectories()`, sans tri intermédiaire.
- `yaDIS/yaDIS/ProcessForm.cs:676` supprime `interface/00/03`
  avant chaque export.
- `ProcessForm.cs:6474` (`CreateAlphabet`) prépare `0-9` et `A` à `Z`,
  leurs icônes et les définitions des rubriques.
- `ProcessForm.cs:1607-1650` parcourt les films de la collection,
  de type 0 et de statut différent de 3. Les films appartenant à
  une saga ne sont pas ajoutés, même s’ils appartiennent à plusieurs sagas.
- `ProcessForm.cs:1665-1698` ajoute ensuite chaque saga dans l’ordre XML,
  indépendamment de l’ordre et de la présence de ses membres dans la collection.
- `yaDIS/yaDIS/MainModule.cs:1074` (`CreateDF`) ouvre directement le fichier,
  sans créer le dossier parent.

## Algorithme

Pour les films, le libellé et l’initiale proviennent de `sort_title` ; pour les
sagas, de `sort_name`. Le premier caractère UTF-16 est comparé à `9` par
`Operators.CompareString(..., TextCompare: false)` : comparaison binaire,
reproduite par `LegacyExportPaths.Alphabet` avec `CompareOrdinal`.
Tout caractère inférieur ou égal à `9`, ponctuation comprise, va dans `0-9`.
Les autres caractères restent inchangés. Il n’y a ni suppression d’accent,
ni comparaison linguistique dépendant de la culture Windows. Le système de
fichiers Windows réunit les initiales ASCII minuscules et majuscules.

Les films hors saga sont ajoutés dans l’ordre reçu ; les sagas suivent dans
l’ordre XML. Aucun tri par titre, par nom de saga ou par culture n’intervient.
Le dernier indice écrit dans chaque rubrique détermine le suivant. Les membres
de sagas exclus ne consomment pas d’indice. Cet index reconstruit produit donc
des indices continus depuis zéro, contrairement à certaines listes de sélections.
Les libellés identiques ne sont pas dédupliqués dans cette boucle historique.

L’ordre reçu dépend du workspace et de son système de fichiers : deux exports
ne sont comparables exactement que s’ils ont les mêmes métadonnées, le même
`groups.xml` et le même ordre de collection. Trier les fichiers sur Linux pour
imiter un ordre supposé de Windows modifierait la règle historique.

## Cas des accents

`À` reste `À`, sans devenir `A`. `CreateAlphabet` ne prépare aucun dossier `À` ;
`CreateDF` échoue alors et `ProcessForm` intercepte l’exception. Le film conserve
sa fiche dans `00/99` et ses autres appartenances, mais n’a pas d’entrée dans
l’index alphabétique. Il en va de même pour `É` et les autres initiales non
préparées. yaDIS3 reproduit désormais cette restriction sans créer de dossier
supplémentaire, sans exception propre à un titre et sans modifier ses métadonnées.

## Écarts corrigés

yaDIS3 regroupait les films selon le titre de leur première saga, puis utilisait
`WriteMovieListing` pour remplacer un membre par sa saga. Cela insérait les sagas
au milieu des films, pouvait omettre une seconde saga partageant un membre et
avançait le compteur pour les membres déjà représentés. Il créait également une
rubrique pour toute initiale accentuée rencontrée.

`LegacyDuneCatalogWriter.Indexes.cs` utilise maintenant deux passes indépendantes
et un compteur par rubrique. Les listes générales, nouveautés, sélections, fiches,
sagas et séries gardent leurs implémentations existantes.

## Tests et limites de l’audit

`LegacyAlphabetOrderTests` impose un ordre de collection non alphabétique,
plusieurs films et sagas, des membres partagés, la casse, les chiffres et la
ponctuation, les accents, plusieurs genres et une sélection, les cibles de
navigation et un réexport avec une ancienne rubrique `À` résiduelle.
Sur l’implémentation initiale, quatre des sept cas échouent : les trois initiales
accentuées et l’ordre/complétude des sagas. Les autres cas passent déjà.

Le test préexistant `Export_AccentedSortTitle_PreservesLegacyAlphabetLetter`
attendait à tort que `00/03/À/dune_folder.txt` existe. Son attente est corrigée
et son nom explicite désormais l’absence du dossier non préparé. La fonction
`LegacyExportPaths.Alphabet` conserve toujours les accents ; ses tests restent
inchangés. Cette correction d’attente découle de `CreateDF`, et non d’une
normalisation ajoutée pour faire passer les tests.

`Export_SansTraitement_NeReconstruitPasLesIndex` utilisait aussi implicitement
une rubrique `É`. Son jeu de données utilise maintenant `Ete d’Adèle` et
vérifie toujours positivement la création de la rubrique `E`, ainsi que
l’absence de reconstruction des genres. Son objectif n’est donc pas affaibli.

Le CSV fourni contient 232 différences : les onze menus alphabétiques annoncés,
la rubrique supplémentaire `À` et d’autres fichiers hors périmètre. Il contient
des tailles et empreintes, pas les entrées ni leur ordre. Les deux ZIP dépassent
la limite de transfert de 32 Mio de l’environnement. Le workspace source complet
n’est pas fourni. La comparaison octet par octet après régénération des 355 films,
des 29 sagas et des quatre sélections de cette médiathèque reste donc non vérifiée.
La validation porte sur le code historique et les exports de caractérisation.

## Validation finale

SDK .NET 10.0.403, conformément au `rollForward` de `global.json` :

- `dotnet restore` : réussi.
- `dotnet build yaDIS3.sln -c Release -m:1` : réussi,
  zéro avertissement et zéro erreur.
- `dotnet test yaDIS3.sln -c Release -m:1` : 567 tests réussis,
  dont 436 dans `YaDIS3.Tests` et 131 dans `YaDIS3.Desktop.Tests` ;
  aucun échec et aucun test ignoré.
- `dotnet format yaDIS3.sln --verify-no-changes` : réussi.
- Les sept nouveaux cas et le test de persistance ajusté ont également
  été exécutés séparément : huit réussites.

Aucune dépendance de projet n’a été ajoutée.
