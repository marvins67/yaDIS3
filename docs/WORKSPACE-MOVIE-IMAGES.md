# Préservation des images sources et génération de l'aperçu film

## Référence yaDIS2

Dans `ProcessForm.GetMovie`, les images locales `*cover.jpg`, `*poster.jpg`
et `*fanart.jpg` sont copiées avec `File.Copy`. En l'absence d'une
image locale, les originaux TMDB sont téléchargés directement vers
`poster.jpg` et `fanart.jpg`. Ces deux chemins ne passent pas par
`MainModule.SaveJPG`.

En revanche, le traitement du film compose l'aperçu à partir de son
template et des images sources, puis appelle `MainModule.SaveJPG`
avec la qualité JPEG configurée. Cet aperçu est un *rendu*, pas un
original à préserver octet pour octet.

## Écart et correction yaDIS3

L'import passait systématiquement par `SkiaJpegEncoder`. Un JPEG
déjà valide était décodé puis encodé à nouveau en qualité 95, ce qui
changeait inutilement son contenu. L'encodeur vérifie toujours les
dimensions maximales et la lisibilité de l'image, mais recopie désormais
les octets lorsque le codec reconnaît un JPEG. Les sources PNG et les
autres formats pris en charge continuent à être convertis vers un JPEG
opaque comme avant.

Cette règle couvre les téléchargements d'originaux, les images locales
et les imports manuels effectués par `ImageImporter`. Les traitements
ne réécrivent pas les images existantes hors remplacement explicitement
demandé ; les sauvegardes de secours existantes restent en place.

`TemplateRenderer` conserve le parcours de dessin de l'aperçu de film
et son unique encodage final à `RenderOptions.JpegQuality` ; l'aperçu
n'est pas recopié comme s'il était une image source. Les fonds AAI/JPEG
et les fichiers source ne sont pas modifiés par ce correctif.

## Vérification

`SkiaJpegEncoderTests` contrôle la conservation des octets d'un JPEG,
la conversion d'une image PNG vers JPEG et le rejet d'une image invalide.

Les encodeurs GDI+ (yaDIS2) et Skia (yaDIS3) peuvent produire des
rendus JPEG différents à qualité numérique égale. Sans comparaisons
d'images sources et de pixels sur un même workspace traité, une égalité
visuelle ou binaire des aperçus ne peut pas être affirmée.
