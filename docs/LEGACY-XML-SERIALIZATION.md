# Fidélité de la sérialisation XML yaDIS2

## Périmètre et références

Audit du 13 novembre 2026 sur `feature/redesign`, à partir de yaDIS3
`b00737b3fe96717e9929e95d061366f047c8fc34` et de
[yaDIS2 `6a985ea`](https://github.com/marvins67/yaDIS/tree/6a985ea2e515800061e1808db24fc71e5958e233).

Les deux archives ont été extraites directement ; aucun CSV préexistant n'a
servi de substitut aux fichiers. Elles contiennent chacune 511 fichiers XML :
473 films, 35 séries, `folders.xml`, `groups.xml`, `shortcuts.xml`.

| Archive originale | SHA-256 |
|---|---|
| yaDIS2-workspace-20261113-1012.zip | `bfc32a83cb0b810946760a585ab66c438b07f3caf6559493c01f46b41697e820` |
| yaDIS3-workspace-20261113-1012.zip | `ffbb95586d6a6db130bd4e8e88de931d0c4ba93fbc03572d35b3526068fc89fc` |

Les 511 contenus XML originaux sont inclus sans modification dans la fixture
`yadis2-xml-reference.zip`. Ce conteneur est réempaqueté avec des noms Unicode
UTF-8 portables : l'archive originale comporte des noms CP437 et des champs
supplémentaires Unicode Path (0x7075), que le lecteur ZIP .NET n'exploite pas
comme celui de Python. Cela corrige uniquement le transport des noms dans le
conteneur de test. L'égalité des 511 chemins et contenus avec l'archive originale
a été vérifiée avant utilisation ; les SHA-256 par fichier figurent dans le CSV.
Tous les noms d'entrées correspondent au champ `filename` de leur fiche.

## Mécanismes historiques

| Opération | Méthode yaDIS2 | Construction et sauvegarde |
|---|---|---|
| Workspace vide | `MainModule.LoadSettings`, lignes 580–609 | `XmlDocument.LoadXml` avec déclaration UTF-8 et paires de balises explicites ; `Save(filename)` seulement si fichier absent |
| Ajout ou modification de sources | `SetupForm.SaveFolders`, lignes 3232–3239 | Reconstruit `<folders></folders>` avec déclaration UTF-8, ajoute les nœuds dans l'ordre de la liste ; `Folders.Save(filename)` |
| Raccourcis | `SetupForm.SaveShortcuts`, lignes 3268–3284 | Reconstruit `<shortcuts></shortcuts>` avec déclaration UTF-8 ; attributs `type`, puis `path` pour type 2 ; `Shortcuts.Save(filename)` |
| Découverte des films et séries | `MainForm.bgwScanMedias_DoWork`, lignes 5673–5862 | Littéraux `data/item` avec déclaration UTF-8 et `<date></date>` ; affectations `InnerText` ; `XmlDocument.Save(filename)` |
| Groupes | `MainForm.CreateSaga`, `CreateSelection`, `RemoveSaga`, `RemoveSelection` ; `SagaForm` | `CreateElement`, `AppendChild`, `RemoveChild`, `InnerText` ; sauvegarde du `XmlGroups` chargé |
| Édition et traitement | `MainForm` autour de 7314/7396 et 8265/8266 ; `ProcessForm` autour de 566/585 et 5987 | Reconstruit une déclaration UTF-8 devant `OuterXml`, modifie le DOM, puis `XmlInfo.Save(filename)` |
| Épisodes TVDB v4 | `YaDIS.TvdbV4/EpisodeData.Edit` et `Save` | DOM issu de `LoadXml(item.OuterXml)` sans déclaration, modifications DOM, puis `doc.Save(temp)` ; UTF-8 sans BOM ni déclaration |
| Préférences globales | `MySettingsProperty.Settings.Save()` | Fournisseur .NET des paramètres applicatifs ; fichier `user.config` hors workspace, mécanisme différent de `XmlDocument.Save` |

Les classes XML historiques conservent `PreserveWhitespace=false`. La surcharge
`Save(filename)` utilise l'écrivain DOM fondé sur `XmlTextWriter` avec
`Formatting.Indented`. L'indentation vaut deux espaces. Sous Windows, les sauts
d'indentation sont CRLF. Aucun saut final n'est ajouté après la racine.

L'encodage provient de la déclaration XML : `utf-8` produit le BOM EF BB BF.
Sans déclaration, le mécanisme DOM écrit en UTF-8 sans BOM et sans déclaration.
Une déclaration UTF-16 est respectée lors de la sauvegarde du DOM chargé.
La construction initiale des fichiers et les formulaires des fiches imposent,
eux, une déclaration UTF-8. Il ne faut donc pas appliquer une règle universelle
« tous les XML avec BOM » à toutes les catégories et à tous les documents importés.

Le DOM conserve la distinction `XmlElement.IsEmpty` : un élément issu de
`<date></date>` n'est pas équivalent, pour l'écriture, à un élément issu de
`<date />`. L'écrivain historique place la fermeture d'une paire vide sur la
ligne suivante. `RemoveChild` conserve la paire de balises du parent devenu vide.
L'ordre des enfants et des attributs est celui des insertions dans le DOM.

L'écrivain gère lui-même `&`, `<`, `>` et les guillemets d'attributs. Les accents
sont encodés directement selon l'encodage déclaré. Les textes non vides, espaces
significatifs, nœuds mixtes et `xml:space="preserve"` suivent le comportement DOM.
La relecture utilise le comportement historique de `XmlTextReader` avec
`Normalization=false`, y compris pour les CR des textes. Aucun nettoyage des
données n'est ajouté.

Les informations des films, des séries et des épisodes sont enregistrées dans
les mêmes `data.xml`, sous `info` et ses enfants. Le portage n'a pas d'écrivain
XML de fichier d'épisode indépendant. Les XML de langues et de templates sont
lus, pas sauvegardés par ces opérations. L'inventaire de tous les appels XML
persistants de `src` laisse `UserConfigurationStore` comme seul mécanisme distinct.

## Écarts avant correction

Les 511 comparaisons binaires entre archives échouent ; les 511 comparaisons
logiques réussissent en ignorant les espaces de formatage comme le DOM historique.

| Fichier | Règle yaDIS3 avant | Différence démontrée |
|---|---|---|
| `folders.xml` | `DuneConfigurationStore.XmlBytes`, `XmlWriter`, UTF-8 sans BOM | BOM absent ; contenu restant identique à l'archive yaDIS2 |
| `groups.xml` | Même écrivain ; conteneurs LINQ autofermés | BOM absent et `<sagas />`, `<selections />` |
| `shortcuts.xml` | Même écrivain ; racine LINQ autofermée | BOM absent et `<shortcuts />` |
| 508 `data.xml` | `SourceFolderScanner.Write`, `XDocument.Save` | BOM déjà présent ; `<date></date>` sur une ligne au lieu de la paire indentée historique |
| Sauvegarde d'une fiche éditée | `WorkspaceRepository.SerializeMetadata`, `XmlWriter`, UTF-8 sans BOM, sans indentation | Mécanisme distinct découvert dans le code ; non exercé par les archives d'ajout initial |
| Sélection automatique | `LegacyAutoSelection.Add`, `XDocument.Save` | Troisième chemin de sauvegarde des groupes, différent de leur sauvegarde depuis la configuration |

Les archives originales ont des sauts CRLF dans les deux versions. La
caractérisation Linux révèle aussi que `XDocument.Save` prenait les sauts de ligne
de l'OS ; la correction rend les sauts d'indentation Windows explicites.

## Corrections appliquées

`LegacyWorkspaceXml` emploie le DOM et l'écrivain historiques. Les lecteurs du
workspace utilisent `XmlTextReader`, `Normalization=false` et
`WhitespaceHandling.Significant`, pour retirer les nœuds d'indentation et conserver
les textes comme `XmlDocument.Load` dans yaDIS2, y compris leurs CR. Les textes
restant dans LINQ sont alors significatifs. Leur conversion vers le DOM utilise
`XDocument.CreateReader`, puis rétablit la déclaration non exposée par ce lecteur.
Les CR saisis et les textes composés uniquement d'espaces restent ainsi des textes,
sans relecture intermédiaire qui pourrait les transformer.

Le DOM appelle `WriteTo` sur un `XmlTextWriter` indenté. Un `StreamWriter` fixe
CRLF pour reproduire Windows également sous Linux. `WriteTo` permet de respecter
aussi l'absence de déclaration d'un DOM importé. Aucun remplacement de chaînes
après sérialisation, aucune normalisation des valeurs métier. Les tests vérifient
les octets d'un synopsis saisi avec CRLF ainsi que sa relecture historique.

Seuls les chemins démontrés comme relevant du DOM historique sont raccordés :

- `WorkspaceRepository` : création initiale et sauvegarde des fiches ; lecture
  avec `IgnoreWhitespace=true`, comme le DOM historique, pour que les sauts
  d'indentation de `<date>` ne deviennent pas une date non vide en mémoire.
  Un test vérifie l'attribution de la date de traitement après relecture de la
  paire vide historique, sans nettoyage de valeurs métier.
- `Configuration/SourceFolderScanner` : fiches découvertes et mises à jour.
- `Configuration/DuneConfigurationStore` : dossiers, groupes, raccourcis.
- `Configuration/LegacyAutoSelection` : sauvegarde des sélections automatiques.
- `MediaCard`, `MediaCard.Treatment`, `Episode`, `ImageImporter` : distinguent la
  déclaration des éditions générales et celle de l'éditeur d'épisodes ; aucune
  valeur métier n'est modifiée par ce choix.
- `LegacyMovieGroups` : conserve les paires de balises des conteneurs devenus
  vides après suppression, sans modifier les membres ni leur ordre.

Les racines initiales et conteneurs historiques sont construits comme paires
vides explicites. Les documents `folders.xml` et `shortcuts.xml` reconstruits
reçoivent leur déclaration UTF-8 comme dans `SetupForm` ; les groupes chargés
conservent leur déclaration. Les éditions de fiches, vérifications et traitements rétablissent le préfixe UTF-8
des formulaires historiques. L'édition d'un épisode ou de son image retire au
contraire la déclaration, comme `EpisodeData.Edit`, puis écrit UTF-8 sans BOM.
La déclaration fait partie du document en mémoire : les sauvegardes répétées
restent stables et un traitement ultérieur rétablit sa déclaration historique. Les sauvegardes transactionnelles, contrôles de
conflit et sauvegardes de sécurité existants sont conservés.

`UserConfigurationStore` n'est pas raccordé à cet écrivain : les paramètres
utilisateur historiques relèvent de `ApplicationSettingsBase`, pas du DOM du
workspace. Les archives ne contiennent aucun `user.config`. Sa parité binaire
n'est donc pas certifiée par cet audit. Aucun changement n'est apporté au rendu,
à la navigation Dune, aux `dune_folder.txt`, aux chemins ou aux statuts métier.

## Tests et mesure reproductible

`LegacyXmlCorpusTests` utilise les octets réels de l'archive. Le test de génération
crée un workspace vierge, enregistre les quatre sources d'origine puis appelle
`SourceFolderScanner.Scan`. Les écritures ont lieu sur disque. Seule la présence
des sources `Z:\…` et leur énumération sont simulées par `LegacyXmlSourceFileSystem`,
à partir des champs `path`, `filename` et `folder` des XML de référence. Les chemins
source et Dune enregistrés restent exactement ceux des archives ; aucune fiche
complète de référence n'est copiée dans le workspace du test de génération.
Les préférences globales utilisent un `user.config` injecté et isolé pour ce
test ; ce fichier de test ne fait pas partie des 511 XML du workspace comparés.

Chaque XML généré est comparé octet par octet à la référence. Les 508 fiches sont
relues par `WorkspaceRepository` ; les 511 XML sont relus avec `XmlDocument`, API
utilisée par yaDIS2. Un deuxième test part des fichiers yaDIS2 existants et vérifie
la stabilité binaire après sauvegarde des 508 fiches sans modification.

Les classes ajoutées sont `LegacyXmlCorpusTests` (4 cas),
`LegacyGroupsXmlSerializationTests` (6 cas) et
`LegacyMetadataXmlSerializationTests` (9 cas). La fixture `LegacyXmlCorpus`
charge les octets ; `LegacyXmlSourceFileSystem` simule uniquement les sources ;
`LegacyXmlReference` fournit l'oracle des scénarios sans capture.

Les scénarios absents de l'archive sont explicitement fondés sur le code historique
et sur un oracle DOM (`LegacyXmlReference`), pas sur des captures inventées :
workspace vide, cycle création/modification/suppression de saga et de sélection,
sélection automatique, métadonnées de films et séries avec accents, apostrophes,
guillemets, `&`, texte multiligne, raccourcis avec attributs, sauvegarde répétée,
éléments autofermés importés, `xml:space`, déclaration absente, UTF-8 et UTF-16.

`MainForm` réécrit les fiches lors d'un scan ; yaDIS3 conserve son optimisation
existante qui évite de réécrire une fiche inchangée. La création initiale garde le
contrôle historique d'existence des fichiers. Les sauvegardes explicites restent
des écritures, avec des octets stables ; aucune règle métier de réécriture n'est modifiée.

Le test vérifie les API de relecture historiques ; il ne lance pas l'application
WinForms yaDIS2 sous Linux. Le rendu et les métadonnées issues de services distants
ne sont pas reproduits par les archives d'ajout initial. Leurs scénarios d'édition
sont couverts par les tests fondés sur le code, sans prétendre à un nouveau constat
binaire sur des sorties complètes de yaDIS2 non fournies.

## Résultats et validation

| Fichier | Identiques avant (archives) | Identiques après (générés par yaDIS3) | Cause résiduelle |
|---|---:|---:|---|
| `folders.xml` | 0/1 | 1/1 | Aucune |
| `groups.xml` | 0/1 | 1/1 | Aucune |
| `shortcuts.xml` | 0/1 | 1/1 | Aucune |
| `data.xml` | 0/508 | 508/508 | Aucune |
| **Total** | **0/511** | **511/511** | **Aucune sur ce corpus** |

Les comparaisons logiques donnent également 511/511 avant et après. Aucun chemin
XML supplémentaire n'est présent dans le workspace régénéré. La compilation
Release réussit sans erreur ni avertissement.

Le détail des 511 chemins est dans [LEGACY-XML-COMPARISON.csv](LEGACY-XML-COMPARISON.csv).
Il contient les égalités binaires et logiques, le premier écart en octets (index
commençant à zéro), la cause, et les trois SHA-256 par fichier : référence,
archive yaDIS3 avant correction, XML régénéré après correction. Les références
originales restent inchangées. La comparaison après correction porte sur les
fichiers écrits sur disque par yaDIS3, conservés par le test de génération.

Pour reproduire la génération sous Linux :

```bash
YADIS_XML_AUDIT_DIR=/tmp/yadis-xml-audit dotnet test tests/YaDIS3.Tests/YaDIS3.Tests.csproj -c Release -m:1 --filter FullyQualifiedName~Scan_QuatreSourcesReelles_RegenereLes511XmlOctetPourOctet
```

Le workspace apparaît dans `/tmp/yadis-xml-audit/xml-regenerated-<identifiant>`.
Le script fonctionne avec les archives originales ou leurs dossiers extraits :

```bash
python3 scripts/audit_legacy_xml.py yaDIS2-workspace-20261113-1012.zip yaDIS3-workspace-20261113-1012.zip --after "/tmp/yadis-xml-audit/xml-regenerated-<identifiant>" --output docs/LEGACY-XML-COMPARISON.csv
```

Les tests sous Windows utilisent la même fixture et les mêmes assertions.
Les 19 nouveaux cas de test couvrent les références réelles et les scénarios
DOM décrits ci-dessus, dont l'édition de l'image d'épisode et le retour au
format UTF-8 avec BOM après une édition générale de la série.

Validation complète sous Linux, SDK .NET 10.0.404, runtime 10.0.15 :

| Commande | Résultat |
|---|---|
| `dotnet restore` | Réussite |
| `dotnet build yaDIS3.sln -c Release -m:1` | Réussite, 0 erreur et 0 avertissement |
| `dotnet test yaDIS3.sln -c Release -m:1` | **633 tests réussis**, 502 métier et 131 Desktop ; aucun échec |
| `dotnet format yaDIS3.sln --verify-no-changes` | Réussite, aucune modification requise |

La régénération conservée pour la comparaison a été exécutée après compilation
du code final. Les 511 SHA-256 de sortie sont identiques à leurs références.
Aucune différence binaire résiduelle n'est constatée sur les XML de ce corpus.
Aucune dépendance n'a été ajoutée.

## Commit de correction

Correctif : [`9dc24b6ad3f4f402081438748b4501f6ae3be2bc`](https://github.com/marvins67/yaDIS3/commit/9dc24b6ad3f4f402081438748b4501f6ae3be2bc),
message `fix legacy xml serialization`, branche `feature/redesign`.

Le correctif, les tests, les fixtures et les mesures figurent dans ce commit.
Un second commit documentaire ajoute cette référence après sa création pour
consigner son SHA exact ; aucun code ni résultat de validation n'est modifié.
Publication directe sur `feature/redesign`, sans autre branche ni pull request.
