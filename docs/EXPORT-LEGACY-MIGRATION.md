# Migration de l'export central vers le flux yaDIS2

Branche : `feature/redesign`. Référence : `marvins67/yaDIS`, `master`.

## Entrée et destination (étape 2)

`MainWindowViewModel.ExportDuneAsync` refuse désormais une destination inexistante avant l'écriture des fiches. La sauvegarde générale automatique précédant l'export a été retirée. L'activation conserve les critères disponibles du mode basique et du chemin configuré. La notion de profil de yaDIS2 ne possède pas d'équivalent complet dans `DuneConfiguration`.

## Workspace et images (étapes 3 et 4)

Le traitement via `DuneExporter.Process` écrit directement les images et définitions
Dune dans `workspace/interface/00/99/<nom>` ou `workspace/interface/01/<nom>`.
Ce dossier est l'unique stockage actif : il n'existe plus de copie intermédiaire
ni de mécanisme de restauration depuis un second cache de rendu.

Les aperçus d'épisodes et le manifeste technique restent dans cette arborescence
pour le retraitement et l'interface utilisateur, mais `preview.jpg` et `render.json`
sont exclus de la synchronisation vers le lecteur. Le traitement complet utilise
un dossier temporaire masqué, remplacé atomiquement ; sa sauvegarde transitoire
est supprimée après succès. Les dossiers yaDIS2 possédant une définition reconnue
peuvent être retraités sans manifeste yaDIS3 préalable.

Un ancien dossier `interface-yaDIS3` déjà présent n'est ni lu ni effacé : aucun
rendu existant n'est supprimé automatiquement lors de la mise à jour. Il peut être
retiré après vérification des rendus disponibles dans `interface`.

`ExportCommand` utilise `ExportLegacy` plutôt que `Export`. L'ancienne méthode `Export` reste temporairement dans le code pour les tests de caractérisation de l'ancienne implémentation.

## Menus et index (étape 5)

`ExportLegacy` supprime les dossiers de menus recalculés par yaDIS2 (`00/03`, `00/98`, `02_*`, `05_*`) puis exécute `LegacyDuneCatalogWriter` directement sur `workspace/interface`.

`LegacyMovieIndexStore.CopyCategory` évite de copier un dossier sur lui-même, car source et cible sont maintenant identiques lorsque l'on travaille dans le workspace.

## Synchronisation (étape 6)

`LegacyExportSynchronizer` reproduit la logique de `ProcessForm.SyncInterface` : dates UTC avec troncature à la seconde, copie des fichiers nouveaux ou plus récents/différents de date, suppression des fichiers sans correspondant source, exclusion des sous-dossiers commençant par `_`, tentative de suppression des fichiers en lecture seule et continuation après certaines erreurs. Le remplacement global avec sauvegarde `.previous` ne sert plus au bouton Exporter.

## Erreurs et annulation (étape 7)

Le générateur de menus peut maintenant échouer sans bloquer la synchronisation. Les erreurs de copie/suppression reproduisent en partie les traitements silencieux de yaDIS2. L'annulation de la synchronisation est vérifiée au début de chaque appel récursif, sans restauration des fichiers déjà copiés.

Écarts résiduels : les messages et la fenêtre de progression diffèrent toujours de l'ancienne interface WinForms ; la correspondance exacte de toutes les exceptions et leurs conséquences n'est pas encore démontrée par une exécution réelle.

## Comparaison des sorties (étape 8)

`tools/Compare-YaDISInterface.ps1` accepte `-ComparePixels` pour comparer les pixels décodés des JPEG et PNG sur Windows, en plus des fichiers et des empreintes SHA-256. L'AAI reste contrôlé octet par octet.

Exemple :

```powershell
.\tools\Compare-YaDISInterface.ps1 `
    -Yadis2 "C:\Comparaison\yaDIS2" `
    -Yadis3 "C:\Comparaison\yaDIS3" `
    -ComparePixels *> comparaison.txt
```

Pour une preuve de conformité, effectuer des exports séparés avec la même bibliothèque, le même thème, les mêmes options, les mêmes chemins Dune et la même date. La comparaison des JPEG/PNG au pixel ne garantit pas une identité de l'encodage ; celle-ci est vérifiée séparément par SHA-256. Les images AAI ne disposent pas encore de comparaison décodée.

## Validation technique

Le workflow `.github/workflows/export-validation.yml` compile, teste et vérifie le formatage sur Windows à chaque push sur `feature/redesign`.

Tests nouveaux :
- `LegacyExportSynchronizationTests` : copie, horodatage, exclusion des répertoires temporaires, annulation.
- `LegacyExportWorkflowTests` : refus d'une destination absente, export successif sans `.previous`, réutilisation d'une icône présente dans le workspace.

## Limites du constat

Ce document décrit le nouveau flux et les écarts connus, **pas une certification d'identité stricte**. Sans les sorties réelles des deux versions et sans les essais Windows de fin de migration, il reste possible de découvrir des écarts de rendu, de playlists, de fichiers conservés ou de navigation Dune.

## Vérification du stockage unique

Les tests de traitement utilisent désormais directement les chemins historiques,
y compris pour les aperçus d'épisodes et la suppression des épisodes disparus.
Les tests dédiés vérifient deux traitements successifs de films et séries,
l'absence de second dossier de rendu et de sauvegarde transitoire persistante,
le retraitement d'une fiche yaDIS2 sans manifeste, le nettoyage lorsque le nom XML
diffère du dossier de données, et l'exclusion des fichiers internes de l'export.

L'ancienne attente de restauration automatique depuis une seconde copie est
remplacée par deux vérifications explicites : une fiche complète est exportée
sans reconstruction ; une fiche dont le rendu a été supprimé provoque un
avertissement et peut être recréée par un nouveau traitement, en AAI comme en JPEG.

Validation du 13 novembre 2026 : compilation Release sans avertissement,
614 tests réussis (483 métier et 131 Desktop), et vérification du formatage
sans modification requise. Aucune référence à l'ancien dossier ne subsiste
dans le code applicatif.
