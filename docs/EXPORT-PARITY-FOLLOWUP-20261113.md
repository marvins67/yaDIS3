# Poursuite de la parité fonctionnelle — 13 novembre 2026

## Résultat et périmètre

Deux divergences supplémentaires ont été reproduites avant correction puis
vérifiées sur les mêmes données synthétiques après correction. Les attendus
sont établis à partir du code historique yaDIS2 référencé ci-dessous :

1. La recherche des sagas déjà représentées dans les sélections et les récents.
2. La recherche des doublons dans les index persistants, à la fin de la dernière
   propriété d'une entrée.

Le rapport transmis est identique au
[rapport du 12 novembre](EXPORT-REDUCED-PARITY-20261112.md). La base de cette
intervention est `6497b937859b369f438429209adab6fec5e37b59`, sur
`feature/redesign`. La référence historique reste yaDIS2
`6a985ea2e515800061e1808db24fc71e5958e233`.

Ces corrections portent sur des règles de génération démontrées dans le code.
Les nouvelles fixtures ne reconstituent pas les workspaces d'origine absents.
Elles ne permettent pas de déclarer résolus les 52 écarts initiaux encore à
élucider : ordre Alien, appartenances Papa/Enfants, variantes de noms, historiques
d'index et chemins d'épisodes restent tributaires des données et réglages ayant
produit les ZIP. Les archives, leurs CSV et les métadonnées d'origine sont
inchangés. Les moteurs AAI/JPEG et le correctif alphabétique ne sont pas modifiés.

## Sélections et récents : recherche littérale des sagas

### Règle historique

Dans `ProcessForm.bgwExport_DoWork`, yaDIS2 recherche littéralement
`nom_de_saga + "|"` dans toute la définition du menu déjà construite :

- [Récents, ligne 1517](https://github.com/marvins67/yaDIS/blob/6a985ea2e515800061e1808db24fc71e5958e233/yaDIS/yaDIS/ProcessForm.cs#L1517).
- [Sélections, ligne 1587](https://github.com/marvins67/yaDIS/blob/6a985ea2e515800061e1808db24fc71e5958e233/yaDIS/yaDIS/ProcessForm.cs#L1587).
- La conversion du fond AAI en JPEG intervient ensuite dans
  [`MainModule.CreateDF`](https://github.com/marvins67/yaDIS/blob/6a985ea2e515800061e1808db24fc71e5958e233/yaDIS/yaDIS/MainModule.cs#L1074).

La recherche est sensible à la casse. Elle peut rencontrer le nom de la saga
à la fin d'une caption, d'une URL ou d'une propriété de l'en-tête. Il faut
conserver cette règle historique même lorsqu'une comparaison des seuls chemins
semblerait plus naturelle.

yaDIS3 utilisait un `HashSet` de chemins de sagas, qui ne reproduisait pas ces
collisions littérales. Le nouveau code réutilise `ContainsDefinitionSuffix`,
déjà employé pour la liste générale, sur la définition encore munie de son fond
AAI. La référence finale au fond configuré est rétablie avant l'écriture du TXT.
Aucune image n'est retouchée.

### Exemples de contenu généré vérifiés

Collection : un film de titre de tri « Une quête », deux membres de la saga
« quête », puis « Après ». Les éléments vérifiés dans les TXT sont :

| Rubrique | Avant correction yaDIS3 | Référence yaDIS2 et sortie corrigée |
|---|---|---|
| Sélection | `item.0=Une quête`, `item.1=Tri saga`, `item.3=Après` | `item.0=Une quête`, `item.3=Après` |
| Récents sans préfixe de date | `item.0=Une quête`, `item.1=Tri saga`, `item.2=Après` | `item.0=Une quête`, `item.1=Après` |

Le format `item.N=titre` du tableau abrège la paire `caption` / `media_url`.
Les tests lisent les fichiers écrits et comparent ces deux propriétés avec leurs
indices exacts, ainsi que le nombre total de propriétés des entrées. Les liens
vers les films et sagas sont vérifiés intégralement.

Autres cas contrôlés :

- « Une Quête » avec une majuscule et « Une quête parallèle » ne bloquent pas la
  saga « quête » : comparaison ordinale et suffixe exact conservés.
- Une URL vers la saga « Long Quest » peut déjà satisfaire la recherche de
  « Quest » ; le test ne se limite donc pas aux captions.
- Les noms `aai` et `background.aai` sont rencontrés dans l'en-tête AAI brut.
- Les noms `jpg` et `background.jpg`, lorsqu'ils sont examinés en premier,
  ne doivent pas être bloqués par une conversion prématurée du fond en JPEG.
- Le mode AAI et le mode JPEG écrivent chacun leur référence de fond finale
  correcte. Aucun traitement particulier n'est codé pour ces noms.

Avant la première entrée, le dernier champ `system_files=*.aai,*.jpg` n'est pas
encore suivi de `|` : le motif `jpg|` n'y est donc pas trouvé à cet instant. Les
tests préservent aussi cette particularité de l'ordre de construction.

La distinction de numérotation reste intacte : les sélections consomment un
indice pour chaque membre admissible, tandis que les récents n'avancent pas le
compteur pour une saga déjà représentée. Le correctif `dfff01c` est conservé.

## Index persistants : absence de séparateur terminal

### Règle historique

[`ProcessForm.CreateIndexItem`, lignes 6183–6193](https://github.com/marvins67/yaDIS/blob/6a985ea2e515800061e1808db24fc71e5958e233/yaDIS/yaDIS/ProcessForm.cs#L6183)
relit les entrées, retire le CRLF terminal, transforme les autres CRLF en `|`,
puis recherche `caption + "|"`. La dernière valeur n'est donc pas suivie d'un
séparateur ajouté artificiellement. L'en-tête du menu est exclu de cette recherche.

yaDIS3 ajoutait `|` après chaque propriété pendant sa recherche. Un premier film
se terminant par `item.0.icon_valign=center` suffisait alors à exclure à tort un
second film dont le titre de tri était `center`, `enter` ou `ter`.

`DuneFile.ContainsIndexCaption` reconstruit désormais le texte des seules entrées,
avec le préfixe historique et les séparateurs internes, sans séparateur final.
L'ajout d'entrées et la numérotation existante ne sont pas réécrits.

### Contenu généré vérifié

| Entrées traitées dans cet ordre | Avant correction | Référence yaDIS2 et sortie corrigée |
|---|---|---|
| « Premier film », puis `center` | uniquement `item.0.caption=Premier film` | ajout de `item.1.caption=center` et du lien vers `Second.mkv` |
| « Premier film », puis `enter` ou `ter` | second titre absent | second titre présent à l'indice 1 |
| « Premier film », puis `CENTER` | second titre présent | comportement conservé |
| « Premier film », puis `1`, `Premier film` ou `film` | second titre exclu par la recherche littérale historique | comportement conservé |

Les sept cas sont contrôlés après deux mises à jour successives, dans les huit
familles d'index : genres, acteurs, réalisateurs, durées, pays, années, notes et
formats. Les captions, indices, liens relatifs exacts et nombres de propriétés
sont comparés dans les TXT produits. Les premiers liens restent inchangés et le
retraitement n'ajoute pas de doublon.

Aucun tri supplémentaire n'est introduit et la règle de conservation de la
première version déjà indexée reste en place.

## Tests et validation

- `LegacyExportSagaDeduplicationTests` : 13 nouveaux cas. Avant correction :
  **7 échecs et 6 réussites**. Après correction : **13 réussites**.
- `LegacyIndexTerminalSeparatorTests` : 7 nouveaux cas. Avant correction :
  **3 échecs et 4 réussites**. Après correction : **7 réussites**.
- Le lot ciblé final de 21 tests comprend ces vingt cas et la comparaison de
  tous les octets de `00/02/dune_folder.txt` avec le véritable fichier yaDIS2
  fourni : **21 réussites**.
- Un premier lot de 25 tests a également vérifié les sélections et la
  numérotation existantes après la correction des sagas : **25 réussites**.
- Les six tests Python de l'outil d'audit restent réussis.

| Contrôle global | Résultat |
|---|---|
| `dotnet restore` | Réussite |
| `dotnet build yaDIS3.sln -c Release -m:1` | Réussite, 0 avertissement, 0 erreur |
| `dotnet test yaDIS3.sln -c Release -m:1` | **477 tests principaux + 131 tests Desktop = 608 réussis**, 0 échec, 0 ignoré |
| `dotnet format yaDIS3.sln --verify-no-changes` | Réussite |
| Tests Python de l'outil de comparaison | **6 réussites** |

La suite complète a été exécutée après stabilisation des deux corrections et
des vingt nouveaux cas. Aucune assertion antérieure n'a été changée pour masquer
un écart. Aucune dépendance applicative n'a été ajoutée ; .NET 10 et C# 14 sont
conservés.

## Commits des corrections

- [`83f53ff3ac868b30ecbe3215c7a6559fe1540a04` — fix export grouped saga membership](https://github.com/marvins67/yaDIS3/commit/83f53ff3ac868b30ecbe3215c7a6559fe1540a04).
- [`7f9f8d93a20b22addf234158a81ff305cfec3729` — fix export index terminal separator](https://github.com/marvins67/yaDIS3/commit/7f9f8d93a20b22addf234158a81ff305cfec3729).

Le rapport de poursuite est publié séparément, sans branche supplémentaire ni PR.
