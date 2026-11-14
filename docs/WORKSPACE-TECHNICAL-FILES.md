# Fichiers techniques et compatibilité du workspace yaDIS2

## Distinction entre données et mécanismes internes

Le workspace conserve les fichiers utiles à yaDIS2 : `data.xml`,
`poster.jpg`, `fanart.jpg`, `preview.jpg` dans `data/`, ainsi
que les images et `dune_folder.txt` dans `interface/`.

yaDIS3 ajoute `render.json` dans le cache de rendu sous `interface/`,
pour comparer la configuration d'un traitement et réutiliser certaines icônes
d'épisodes. Le fichier `.render.lock` dans `data/` assure l'exclusion
mutuelle pendant le traitement. L'absence de ces fichiers n'empêche pas
la lecture des fiches historiques par yaDIS3.

Ces fichiers **ne sont pas des ressources de navigation Dune**. Leur
présence dans le workspace ne justifie pas leur copie vers l'export.
Une suppression aveugle de `render.json` dans le workspace désactiverait
la réutilisation des rendus ; la suppression d'un verrou actif introduirait
une course entre traitements.

## Correction

`LegacyExportSynchronizer` exclut, à toutes les profondeurs :
- `preview.jpg` et `render.json` (déjà exclus) ;
- `.render.lock` ;
- les fichiers temporaires de publication `render.json.*.tmp`.

L'exclusion est indépendante de la casse, comme les deux exclusions
historiques. Les autres fichiers `.tmp` ne sont pas supprimés
arbitrairement. La synchronisation retire aussi les fichiers techniques
qui auraient déjà été copiés dans la destination, sans supprimer leurs
originaux dans le workspace.

Le verrou de traitement et le manifeste de cache restent dans le
workspace pour maintenir la fiabilité des traitements et des réexports.
Le nettoyage de fichiers techniques qui se trouveraient déjà dans le
workspace lui-même n'est pas effectué sans preuve qu'ils sont inactifs.

## Tests

`LegacyExportSynchronizationTests` couvre le nettoyage de la destination,
la conservation du workspace source et la non-exclusion d'un fichier
`notes.tmp` ordinaire. Les scénarios précédents (dates et annulation)
restent inchangés.
