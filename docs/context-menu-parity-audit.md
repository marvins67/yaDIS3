# Audit final — menu contextuel yaDIS2 / yaDIS3

## Références et périmètre

- Référence historique : `marvins67/yaDIS`, `master`, `yaDIS/yaDIS/MainForm.cs` :
  `MenuMedia_Opening` (8034–8152), gestionnaires du menu (8154–8270),
  rechargements (8406–8459), suppression (5964–6020), sagas (9115–9422)
  et sélections (9429–9704).
- Changement de titre : `yaDIS/yaDIS/ChangeForm.cs`, `DoChange`
  (vers 735–772).
- Implémentation : `MainWindow.axaml`, `MainWindow.axaml.cs`,
  `LegacyMediaContextMenuState`, `MainWindowViewModel.*`,
  `LegacyMovieGroups`, `LegacyMediaDeletion`,
  `LegacyMovieIndexRemoval`, `MediaTreatment`.
- Les tests ci-dessous caractérisent les cas contrôlés, **sans remplacer**
  une comparaison interactive des applications ni une comparaison
  exhaustive des workspaces après des échecs.

## Matrice de conformité

| Entrée du menu | yaDIS2 | yaDIS3 et preuve | État |
| --- | --- | --- | --- |
| Structure, état et visibilité | `MenuMedia_Opening` selon média, statut et nombre de titres | `MainWindow.axaml`, `LegacyMediaContextMenuStateTests` | Tests d'états ; rendu pixel à pixel non vérifié |
| Ouvrir dossier des données | `workspace/data/<filename>` | `OpenDataFolder`, `MediaCard.DataDirectory` | Raccordé ; test visuel requis |
| Ouvrir dossier du média | `item/folder=yes` ouvre le dossier, sinon `item/path` | `OpenMediaFolder`, `MediaCard.MediaFolderPath`, `LegacyEditorTests` | Raccordé et testé |
| Fiche en ligne | TMDB pour films ; TVDB pour séries | `OpenInformationPage`, `MediaCard.InformationUrl` | Raccordé ; navigateurs/URL réelles à vérifier |
| Création manuelle | Film non traité, ouvre l'éditeur | `BeginManualMovieEdit`, `LegacyEditorTests` | Raccordé ; cycle complet non comparé visuellement |
| Changer de titre | Sélection du résultat, retrait de l'ancien index et des JPG, nouveau traitement | `ChangeContextTitle`, `ChangeMovieTitleAsync`, `ChangeSeriesTitleAsync`, `LegacyMovieTitleChangeCleanup`, tests d'index et de traitement | Principaux effets caractérisés ; erreurs/annulation et dialogue non identiques |
| Recharger les informations | `ForceReloadInfo=true`, `ForceReloadMediaInfo=false`, `ProcessAllEpisodes=false` | `TreatmentMode.ReloadInformation`, `MediaTreatmentTests` | Tests de traitement ; erreurs réelles à comparer |
| Recharger les informations techniques | `ForceReloadMediaInfo=true`, `ProcessAllEpisodes=true` | `TreatmentMode.ReloadTechnicalInformation`, tests de métadonnées/épisodes | Tests de traitement ; correspondance de tous les épisodes à revérifier |
| Supprimer | Suppression données, groupes, index, rendus et actualisation de la liste ; échecs partiels | `LegacyMediaDeletion`, `MainWindowViewModel.Deletion`, `LegacyMediaDeletionTests`, `StateMenuTests` | Tests temporaires ; erreurs de permissions à éprouver |
| Sagas | Clic direct = ajouter, déplacer ou retirer ; sous-menu par saga = éditer/supprimer | `LegacyMovieGroups`, `ContextGroups`, `StateMenuTests` | Comportement de menu confirmé manuellement ; éditeur différent |
| Sélections | Clic direct, état tri-state pour sélection multiple, supprimer groupe vide | `LegacyMovieGroups`, `ContextGroups`, `LegacyMovieGroupsContextTests` | Logique couverte ; état indéterminé rendu via symbole plutôt que case native |
| Basculer vérification | `item/status` 0↔1, sauvegarde `data.xml` ; multi-sélection | `MediaCard.ToggleVerification`, `ToggleVerificationAsync`, tests | Tests ciblés ; exceptions d'écriture non caractérisées |
| Raccourcis | Ctrl+M, Suppr, Ctrl+T | Indication `InputGesture`; `ListBox.KeyBindings` pour Suppr | Suppr câblé ; Ctrl+M/Ctrl+T à tester depuis le clavier |

## Écarts confirmés corrigés pendant l'audit

1. **Index Dune obsolètes après changement d'identification d'un film.**
   yaDIS2 appelle `ProcessForm.RemoveFromIndex` avant de remplacer la fiche.
   yaDIS3 passe désormais par `LegacyMovieTitleChangeCleanup.Remove`
   avant `PrepareMovieTitleChange`. Le test `ChangeTitle_AncienneIndexation_RetireLesCriteresAvantLaRecherche`
   vérifie que les anciennes entrées disparaissent.
2. **Retraitement d'autres médias lors de Changer de titre.**
   Le flux de changement d'identification envoie explicitement la
   seule fiche sélectionnée à `TreatSelectionAsync`, même lorsque
   `TreatmentSelection` conserve une sélection multiple. Les
   commandes de traitement par lots conservent leur sélection habituelle.

## Écarts restants et limites de preuve

- **Dialogues** : `ChangeForm` historique et `SagaForm` ne sont
  pas reproduits à l'identique en Avalonia : présentation, navigation,
  réorganisation des membres, validation et annulation demandent
  une comparaison fonctionnelle et visuelle dédiée.
- **Erreurs en cours de traitement** : une erreur de rendu est
  remontée par `TreatmentRenderException`, les préparations
  peuvent enregistrer un statut d'échec ; les conséquences exactes
  pour tous les états partiels de yaDIS2 ne sont pas démontrées.
- **Sélections mixtes** : la logique bascule les appartenances,
  mais l'état indéterminé est indiqué par `◩` au lieu d'une case
  indéterminée native de WinForms.
- **Raccourcis** : les `InputGesture` du menu ne garantissent pas
  à eux seuls le déclenchement de Ctrl+M et Ctrl+T. Suppr dispose
  d'un `KeyBinding` sur la liste.
- **Comparaison de workspaces réels** : aucun couple de workspaces
  yaDIS2/yaDIS3 de référence identique n'a été exécuté et comparé
  octet par octet durant cet audit. Les tests utilisent des
  fixtures temporaires et les sources des deux dépôts.

## Recette manuelle recommandée

Sur une **copie** de workspace avec films et séries :
1. Sélectionner successivement film traité (0/1), manquant (2),
   non traité (3), série puis plusieurs titres ; comparer activation,
   libellés, séparateurs et raccourcis.
2. Créer, modifier, déplacer, retirer et supprimer une saga ;
   comparer `groups.xml`, les images de référence et `interface/00/98`.
3. Ajouter et retirer plusieurs films d'une sélection ;
   vérifier les appartenances mixtes et la suppression du groupe vide.
4. Changer l'identifiant d'un film indexé ; comparer avant/après
   les répertoires `data`, `interface/00` et les critères Dune.
5. Recharger informations puis informations techniques sur un film
   et une série (avec plusieurs épisodes), puis contrôler XML,
   images et aperçus.
6. Supprimer un film en saga/sélection et une série ; contrôler
   données, index, interface, groupes, liste et **présence des vidéos**.
7. Simuler un fichier verrouillé, une panne de métadonnées et
   une erreur de rendu ; relever quels fichiers persistent, les
   statuts, les messages et la reprise.

**Conclusion :** validation de compilation et tests automatisés
nécessaire, mais non suffisante pour certifier l'identité stricte
des interfaces et de tous les scénarios d'erreur.
