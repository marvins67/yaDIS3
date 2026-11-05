# Caractérisation de l'export — référence yaDIS2

Branche de travail : `feature/redesign`.

Ce document décrit les comportements observés **dans les sources** des dépôts yaDIS2 et yaDIS3. Les résultats de tests automatisés sur yaDIS3 ne prouvent pas à eux seuls une identité des exports réels. Aucun code de production n'est modifié dans cette étape.

## Entrée de l'opération

| Référence yaDIS2 | Comportement historique | État yaDIS3 avant migration |
| --- | --- | --- |
| `MainForm.EnableExportButton` | Bouton désactivé pour profil non principal, mode basique du profil principal ou chemin vide | `MainWindowViewModel.CanExport` teste mode basique et chemin vide, sans condition de profil |
| `MainForm.SplitButtonExport_Click` | Refuse une destination absente avant d'ouvrir `ProcessForm` | `ExportDuneAsync` teste un chemin vide, et `DuneExporter.Export` peut créer la destination |
| `ProcessForm.ProcessForm_Load`, tâche 2 | Lance `bgwExport.RunWorkerAsync` | `ExportCommand` lance `RunAsync(ExportDuneAsync)` |

## Construction et publication

| Référence yaDIS2 | Comportement historique | État yaDIS3 avant migration |
| --- | --- | --- |
| `ProcessForm.bgwExport_DoWork` | Régénère menus et index sous `Workspace\\interface`, puis appelle `SyncInterface` | `DuneExporter.Export` rend chaque fiche retenue et génère les menus dans un dossier de stage |
| `ProcessForm.SyncInterface` | Compare les dates de modification UTC avec une précision à la seconde après `Fix`, sans comparer le contenu | `CommitExportStage` remplace la destination entière |
| `ProcessForm.SyncInterface` | Copie les fichiers absents ou datés différemment ; supprime les éléments de destination sans équivalent source | `CommitExportStage` garde la destination précédente sous `.previous` |
| `ProcessForm.SyncInterface` | Ne parcourt pas les répertoires source dont le nom commence par `_` | Aucun équivalent général établi dans la publication du stage |

## Effets secondaires et erreurs

- `MainModule.GetVideoFiles` crée `playlist.m3u` pour un film de plusieurs vidéos lors de la récupération des fichiers. La playlist est triée avec `Array.Sort`, produite avec `Encoding.Default` et des fins de ligne du système.
- `LegacyDuneMediaWriter.WriteMovie` écrit également `playlist.m3u`, mais **pendant l'export** et en UTF-8 avec CRLF. L'ordre et le moment de génération doivent être comparés à la référence.
- `ProcessForm.SyncInterface` ignore certaines erreurs de copie, de suppression et de synchronisation. Son annulation est testée en début d'appel récursif : elle n'implique pas de restauration du contenu précédemment synchronisé.
- `DuneExporter.Export` construit un stage, vérifie l'annulation avant publication et essaie de conserver le catalogue précédent en cas d'échec. Cela est intentionnellement différent de yaDIS2 à ce stade.
- `MainWindowViewModel.ExportDuneAsync` sauvegarde les fiches modifiées et les préférences avant d'appeler l'exporteur. Il faut caractériser les conséquences historiques des modifications non enregistrées avant d'aligner ce flux.

## Couverture actuelle des tests yaDIS3

- `LegacyExportWorkflowCharacterizationTests` : destination absente, deux exports successifs, annulation avant publication, bibliothèque vide et création d'une playlist dans le dossier vidéo.
- `LegacyExportSafetyTests` : protection d'une destination étrangère, sauvegarde de la destination précédente et annulation sans publication partielle.
- `LegacyExportIntegrationTests` : structure du catalogue, définitions Dune, groupes, raccourcis, image corrompue et fanart vide.
- `LegacyExportCharacterizationTests` : noms de dossiers, saisons, tri et encodage `dune_folder.txt`.
- `LegacyAaiExportTests` : fonds AAI et références dans le catalogue.

Les tests de flux ajoutés caractérisent **yaDIS3 tel qu'il fonctionne aujourd'hui**. Après la migration, certains devront être remplacés par les attentes yaDIS2 : ils ne doivent pas être conservés artificiellement si le but est de changer ce comportement.

## Points non attestés sans exécutions comparées

- Fichiers réellement conservés dans `workspace\\interface` après des traitements puis des exports successifs.
- Images AAI/JPG et aperçus pixel par pixel ; identité de l'encodage JPEG GDI+/Skia.
- Effet exact de chaque exception particulière sous Windows, des attributs en lecture seule et des dates de modification.
- État de la destination après interruption au milieu de la synchronisation.
- Traitement des caractères accentués dans `playlist.m3u` sous différentes pages de code Windows.

## Prochaine étape

Étape 2 : reproduire dans la commande Avalonia les conditions d'activation et le refus de la destination inexistante de yaDIS2, en ajoutant des tests **de conformité** distincts des tests **de caractérisation du comportement actuel**. Ne pas modifier les étapes d'audit `Configurer → Interface` déjà validées.
