# Rendu AAI : corrections et limites démontrées

Référence historique : yaDIS `6a985ea2e515800061e1808db24fc71e5958e233`.
Base yaDIS3 : `77134f501a224e260666a60f7cf345e00dbeb32a`.

## Méthodes examinées

`ProcessForm.CreateAlphabet` (6474), `CreateIndexItem` (6157), génération des
bandeaux (784 et 988), `CreateMenuBackground` (6575), `CreateSagaBackground` ;
`MainModule.GetFontIndex`, `LoadFonts`, `GetTextStyle`, `GetTextAlignment`,
`ChangeTextCase`, `SaveJPG` ; `AAImageLib.AAI.SaveAAI/OpenAAI`, lus dans la DLL
historique avec ILSpy. L’outil n’est pas une dépendance du projet.

## Règles historiques

- Police issue de `PrivateFontCollection`, style déclaré par le template ; taille
  en points convertie à 96 dpi sur les bitmaps de mesure. La sélection historique
  compare les noms de familles et utilise la première famille en cas d’échec.
  Skia associe aussi les noms de fichiers ; cette différence de résolution n’a
  pas été remplacée à l’aveugle sans connaître les familles effectives Windows.
- `Graphics.MeasureString` sur un bitmap 16 × 16, avant le passage en
  `TextRenderingHint.AntiAlias` pour le dessin. `Math.Round` (milieux vers le pair)
  de la largeur mesurée + 8 ; si le résultat est impair, ajout de 1. Hauteur :
  `Math.Round` de la hauteur mesurée. Ce n’est pas un arrondi systématique au
  nombre pair, ni une soustraction de trois pixels.
- `StringFormat.Alignment = Center` pour les étiquettes ; alignement vertical
  par défaut en haut. Les pays utilisent 128 × 64, drapeau 64 × 64 et rectangle
  texte (64, 2, 64, 64), aligné à droite et centré verticalement.
- Bandeaux : espacement et décalage provenant du template, centrage avec
  `Math.Round`. La casse est appliquée avant la mesure et le dessin.
- Images : `InterpolationMode.HighQualityBicubic`, composition SourceOver et
  `CompositingQuality.Default`, texte `TextRenderingHint.AntiAlias`.
- Masques : minimum des alphas, déjà reproduit dans `ApplyMask`. Les cadres,
  contours et ombres fournis en images restent des calques du template.
- AAI : largeur et hauteur int32 little-endian, puis pixels BGRA non
  prémultipliés, ligne par ligne ; alpha 255 remplacé par 254, autres alphas
  conservés. Le codec yaDIS3 respecte cette règle : aucune modification requise.
  Le lecteur historique omet le dernier pixel ; ce défaut de lecture n’a pas
  été propagé dans le codec portable, qui conserve tous les pixels.

## Correction appliquée

`NavigationWidth` ignorait `case="lower"` alors que `DrawText` l’appliquait.
Les étiquettes et bandeaux pouvaient donc réserver la largeur d’une autre chaîne.
La mesure applique désormais les trois cas upper/lower/inchangé. Aucun titre,
chemin, ordre, règle de dossier ou fichier de navigation n’est modifié.

`LegacyRenderingParityTests` compare les octets des icônes normales et
sélectionnées pour des légendes accentuées équivalentes après conversion en
minuscules, sur Origins et GLASS Revisited. Un second test vérifie les octets
BGRA, le canal alpha, le header et le dernier pixel décodé du codec AAI.
Le cas Origins échouait avant correction. Aucun test existant n’a été modifié.

## Dimensions mesurées et écarts persistants

Le fichier `AAI-DIMENSIONS-SCENARIO-GLASS.csv` confronte les 158 lignes fournies
aux étiquettes régénérées avec le template GLASS Revisited distribué, 24 pt,
normal/sélectionné. Toutes les largeurs correspondent déjà aux largeurs yaDIS2
annoncées : A/B/H = 32, J/T = 30, 0-9 = 54. La hauteur reste 38 pour les
158 lignes, contre 35 annoncés. Ces mesures ne prouvent pas que les polices et
paramètres du workspace inaccessible sont identiques à ceux du scénario.

Skia expose ici un interligne de 38,4 pixels pour ScoutCond Bold à 32 pixels,
issu des métriques hhea (800 + 200 + 200 unités pour un em de 1000). Origins
utilise Santana-Black : interligne Skia 36,512, arrondi à 37. Le résultat effectif
Windows de MeasureString ne peut pas être remplacé par une formule déduite d’un
seul CSV. Les hauteurs n’ont donc pas été forcées et le test existant à 38 reste
une caractérisation Skia, pas une preuve de parité GDI+.

Le filtrage bilinéaire actuel diffère de HighQualityBicubic. Aucun noyau Skia
n’a été déclaré équivalent sans référence pixel à pixel : choisir un noyau
cubique particulier sans mesure ne démontrerait pas l’identité GDI+.
Les différences de hinting, graisse synthétique, crénage, baseline, soulignement
et rasterisation restent également à confronter à une référence Windows.

## Limites de l’audit

Les ZIP dépassent la limite de transfert de 32 Mio ; les CSV n’incluent pas les
pixels ni les sources du workspace. On ne peut donc pas classer individuellement
les 176 AAI annoncés entre identité visuelle, antialiasing mineur et différence
réelle, ni recalculer le nombre global restant après correction. Les empreintes
et dimensions ne suffisent pas à calculer une RMSE ou un écart visuel.
Les 158 écarts de hauteur du scénario restent explicitement ouverts.

Documentation métrique :
https://learn.microsoft.com/en-us/windows/win32/gdiplus/-gdiplus-obtaining-font-metrics-use

## Validation finale

`dotnet restore`, `dotnet build yaDIS3.sln -c Release -m:1`,
`dotnet test yaDIS3.sln -c Release -m:1` et
`dotnet format yaDIS3.sln --verify-no-changes` réussissent.
574 tests réussis (443 métier/rendu, 131 interface), aucun échec ni test ignoré ;
compilation sans avertissement ni erreur. Les sept nouveaux cas passent également
séparément ; cinq échouaient avant correction. Aucun test existant n’a été affaibli.
