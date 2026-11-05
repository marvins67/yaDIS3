# Audit final — Configurer / Interface (yaDIS2 ↔ yaDIS3)

Branche : `feature/redesign`. Référence : yaDIS2 (`marvins67/yaDIS`, `yaDIS/yaDIS/SetupForm.cs`, `ProcessForm.cs`) et yaDIS3 (`marvins67/yaDIS3`).

## Périmètre et méthode

Comparaison statique des routines de configuration et d'export, complétée par les tests de caractérisation. Les étapes 1 à 6 ont été exécutées avec succès sur le poste Windows du mainteneur avant le présent audit. **Les derniers changements de l'étape 7 n'ont pas encore été compilés ni testés sur ce poste.**

Les tests de yaDIS3 prouvent des comportements attendus, pas une identité octet par octet avec un export réellement produit par yaDIS2. Aucune sortie binaire des deux programmes exécutés avec une bibliothèque identique n'a été fournie pour cette comparaison.

## Matrice de conformité

| Domaine | Éléments examinés | Conclusion |
| --- | --- | --- |
| Thèmes et aperçu | `ConfigurationViewModel.SelectedTemplate`, ressources et `TemplateRenderer` | Parcours raccordé ; rendu identique non démontré pixel par pixel |
| Boîtiers | `TemplateRenderer.Images.cs`, images `case_*.png`, absence d'image de secours | Tests et gestion du cadre manquant ; comparaison visuelle binaire non réalisée |
| Fonds AAI/JPG | `RenderOptions`, génération et choix des extensions | Tests d'export conformes aux noms et formats ; égalité des octets non démontrée |
| Qualité JPEG | `DuneConfiguration.JpegQuality`, `SaveJpeg` | Option transmise et bornée ; égalité de l'encodage entre GDI+ et Skia non démontrée |
| Menu Dune | `LegacyDuneCatalogWriter.Navigation.cs` | Directives du menu racine et cases/barre de défilement caractérisées |
| Catégories / nouveautés | `LegacyDuneCatalogWriter.Indexes.cs`, `Collections.cs` | Tests pour les dix indicateurs, la période et les deux tris |
| Sélections | `ConfigurationViewModel.SelectedGroups` et `groups.xml` | **Écart corrigé pendant l'audit :** les sélections de même nom retrouvent chacune leur propre état |
| Raccourcis | `ConfigurationViewModel.Shortcuts.cs`, `shortcuts.xml`, dossiers `05_*` | Ordre, doublons et types caractérisés ; suppression de l'instance sélectionnée corrigée à l'étape 5 |
| Enregistrement | `DuneConfigurationStore`, `DuneConfigurationService` | Sauvegarde et restauration des documents couvertes par les tests ; différences historiques ci-dessous |
| Annulation / erreurs | `ConfigurationViewModel.ApplyAsync`, `ConfigurationWindow` | Brouillon isolé et annulation vérifiée ; comportement exact de yaDIS2 non reproduit en cas d'échec |

## Écarts conservés ou non encore démontrés

1. **Fermeture sur erreur de sauvegarde.** Dans yaDIS2, `ButtonSetupOK_Click` appelle `Close()` avant les écritures. Dans yaDIS3, la fenêtre reste ouverte et affiche l'erreur ; ce comportement est plus protecteur mais **n'est pas strictement identique**. Aucun changement automatique n'a été apporté.
2. **Atomicité et restauration.** yaDIS3 sauvegarde les documents via fichiers temporaires et restaure les originaux après erreur. yaDIS2 écrit les documents individuellement, sans transaction commune. C'est un écart volontaire dans les conséquences d'une panne partielle ; il reste à arbitrer si la parité stricte d'échec prime sur la protection des données.
3. **Images et encodage.** L'égalité des pixels ou octets d'images AAI/JPG et des menus n'a pas été démontrée. Les moteurs graphiques diffèrent ; aucun résultat ne permet d'affirmer leur identité.
4. **Sorties d'export.** La correspondance complète de l'arborescence, des contenus `dune_folder.txt` et des images doit être exécutée avec une même bibliothèque, un même thème et les mêmes options dans les deux programmes.

## Correctif de l'étape 7

Dans `ConfigurationViewModel.SelectedGroups`, yaDIS3 retrouvait auparavant la case de sélection par son nom avec `FirstOrDefault`. Deux sélections homonymes récupéraient donc le même état. yaDIS2 utilise directement chaque nœud XML stocké dans le contrôle de la ligne. yaDIS3 utilise désormais l'ordre de chargement, qui maintient cette correspondance, avec un test pour les deux états opposés.

## Comparaison reproductible des exports

Le script `tools/Compare-YaDISInterface.ps1` confronte deux répertoires **interface** :

```powershell
./tools/Compare-YaDISInterface.ps1 -Yadis2 "C:\exports\yaDIS2\interface" -Yadis3 "C:\exports\yaDIS3\interface"
```

Le rapport classe chaque chemin : identique, texte équivalent avec octets différents, différent, absent dans une version. Il compare les contenus XML et `dune_folder.txt` et les empreintes SHA-256 des fichiers ; il **ne compare pas les pixels** et ne prétend pas interpréter les binaires AAI.

Pour une comparaison significative : même version de données, même thème, mêmes options de catégories, de boîtiers, de qualité JPG, de langue, même date pour les nouveautés, et mêmes chemins Dune. Conserver les répertoires exportés séparément.

## Validation après les modifications de l'étape 7

```powershell
git pull origin feature/redesign
dotnet test tests/YaDIS3.Desktop.Tests/YaDIS3.Desktop.Tests.csproj -c Release --filter "FullyQualifiedName~InterfaceConfigurationCharacterizationTests"
dotnet build yaDIS3.sln -c Release -m:1
dotnet test yaDIS3.sln -c Release -m:1
dotnet format yaDIS3.sln --verify-no-changes
```

## Décision

**Audit statique et tests de caractérisation : achevés. Parité stricte de bout en bout : non attestée.** Ne pas fermer les réserves sur les échecs ni les images sans comparaison d'exécutions réelles et décision explicite.
