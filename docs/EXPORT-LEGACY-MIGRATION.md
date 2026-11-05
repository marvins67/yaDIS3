# Migration de l'export central vers le flux yaDIS2

Branche : `feature/redesign`. Référence : `marvins67/yaDIS`, `master`.

## Entrée et destination (étape 2)

`MainWindowViewModel.ExportDuneAsync` refuse désormais une destination inexistante avant l'écriture des fiches. La sauvegarde générale automatique précédant l'export a été retirée. L'activation conserve les critères disponibles du mode basique et du chemin configuré. La notion de profil de yaDIS2 ne possède pas d'équivalent complet dans `DuneConfiguration`.

## Workspace et images (étapes 3 et 4)

Le traitement via `DuneExporter.Process` publie maintenant les images traitées dans `workspace/interface/00/99/<nom>` ou `workspace/interface/01/<nom>` par `LegacyProcessedMediaPublisher`. Les `preview.jpg` et `render.json` ne sont pas inclus dans cette publication.

Les définitions Dune des médias sont générées lors du traitement. Pour les anciens workspaces yaDIS3 n'ayant pas encore de dossier d'interface, `DuneExporter.ExportLegacy` peut importer les images existantes du cache `interface-yaDIS3` lorsque le dossier cible manque et régénérer les définitions manquantes. Cette compatibilité de migration n'est **pas** une routine historique de yaDIS2.

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
