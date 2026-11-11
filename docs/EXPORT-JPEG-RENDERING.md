# Composition des fonds JPEG : correctifs démontrés

## Origine historique et écarts

`ProcessForm` compose les images et bandeaux dans un bitmap puis appelle
`MainModule.SaveJPG`. Cette méthode sélectionne le codec `image/jpeg` et transmet
uniquement `Encoder.Quality`, depuis le réglage utilisateur. Les fonds habituels
sont de 1920 × 1080. GDI+ et Skia n’utilisent pas nécessairement les mêmes tables
JPEG, sous-échantillonnages ou profils ; une égalité de qualité numérique ne
prouve pas une identité binaire. Aucun paramètre arbitraire d’encodage n’est ajouté.

Auparavant, `RenderMenu` encodait un JPEG, puis chaque appel
`OverlayNavigation` décodait le résultat et le réencodait. Plusieurs bandeaux
accumulaient donc des pertes et pouvaient ajouter des métadonnées de couleur du
décodeur Skia. La même succession en AAI pouvait aussi réutiliser un alpha déjà
abaissé à 254 au lieu du bitmap original en mémoire.

`CreateMenuBackground` parcourt également les images statiques puis les textes
statiques correspondant à la langue active. `RenderMenu` omettait ces textes.

## Correctifs

Le renderer conserve désormais une recette de composition par fond de menu,
sans garder un bitmap plein écran pour chaque dossier. Chaque ajout de bandeau
repart des sources et redessine les calques antérieurs ; l’encodage final ne
réutilise plus les pixels d’un JPEG intermédiaire. La recette est aussi copiée
pour `background2` des index de personnes avant le remplacement du panneau.
Les chemins, les dimensions et le contenu fonctionnel des dune_folder.txt
restent inchangés. Les callbacks sont propres à chaque instance du renderer.

Les textes statiques sont dessinés dans l’ordre historique, après les images,
avec la langue, police, couleur, coordonnées et dimensions du template.

Fichiers : `TemplateRenderer.Menus.cs`, `TemplateRenderer.Navigation.cs`,
`LegacyDuneCatalogWriter.Indexes.cs` (copie de la recette uniquement).

## Tests

`LegacyMenuCompositionTests`, sur Origins et GLASS Revisited :

- Deux ajouts d’icônes entièrement transparentes ne changent aucun octet du
  JPEG initial : les pertes et changements de profil ne s’accumulent plus.
- Un texte statique d’une autre langue laisse le fond inchangé ; le même texte
  dans la langue active modifie réellement les pixels AAI.

Les quatre cas échouaient avant correction et passent après correction.
Aucun test existant n’a été modifié, aucune dépendance de projet ajoutée.

## État des 44 JPEG du rapport fourni

Le CSV de comparaison antérieur recense 12 fonds d’index, 29 fonds de sagas et
3 fonds racine (`background.jpg`, `00/background.jpg`, `01/background.jpg`).
La suppression des cycles avec pertes concerne les menus composés par bandeaux.
Elle ne démontre pas la résolution des 29 fonds de sagas, dont les fanarts,
textes et sources doivent être comparés séparément.

Sans ZIP ni workspace complet, les dimensions effectives de chaque JPEG de
référence, les pixels, profils ICC et tables de compression ne sont pas
accessibles. Les dimensions 1920 × 1080 sont une règle du code, pas une mesure
de ces 44 fichiers. Leurs causes individuelles et leur écart visuel restent
non mesurables. Le total des différences restantes ne peut pas être recalculé.
Les paramètres de qualité existants sont conservés ; l’identité binaire entre
encodeurs n’est pas présentée comme un objectif atteint.

## Validation finale

`dotnet restore`, `dotnet build yaDIS3.sln -c Release -m:1`,
`dotnet test yaDIS3.sln -c Release -m:1` et
`dotnet format yaDIS3.sln --verify-no-changes` réussissent.
574 tests réussis (443 métier/rendu, 131 interface), aucun échec ni test ignoré ;
compilation sans avertissement ni erreur. Les sept nouveaux cas passent également
séparément ; cinq échouaient avant correction. Aucun test existant n’a été affaibli.
