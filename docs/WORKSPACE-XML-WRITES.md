# Audit des écritures de data.xml : scan, traitement, MediaInfo

Référence historique : `MainForm.bgwScanMedias_DoWork`,
`ProcessForm.ProcessMovie`, `ProcessForm.GetMediaInfo` et
`MainModule.CreateMovieXML` de yaDIS2.

| Champ | yaDIS2 | yaDIS3 corrigé | Régénération |
| --- | --- | --- | --- |
| `selection` | `no` à la création, supprimé au scan existant ; `yes` déclenche la sélection automatique | `no` retiré uniquement au scan existant ; `yes` conservé pour ne pas perdre un ajout non encore consommé | scan uniquement |
| `item/date` | `Conversions.ToString(DateTime.Today)` : date courte de la culture courante | `ToString("d", CurrentCulture)` | uniquement si vide et traitement réussi |
| `info/rating` | `Conversions.ToString(vote_average)` avec séparateur régional | conversion du point JSON vers le séparateur régional | identification ou rechargement d'information |
| `images/image/@url` | `TMDB_Config.images.base_url + "original" + path` ; le corpus comparé utilise HTTP | URL HTTP historique dans les nouveaux XML, requêtes réseau toujours en HTTPS | identification ou rechargement d'information |
| `mediainfo` | `format`, `source`, `duration`, `video`, `audio`, `subtitles` | repositionnement de `duration` après `source` | nouvelle analyse uniquement |

La sérialisation historique existante est maintenue. Une sauvegarde
normale n'effectue pas de migration globale des fiches et une date non
vide n'est pas remplacée. Les URL `homepage` du film ne sont pas
forcées en HTTP : elles viennent du fournisseur et non du préfixe des
illustrations TMDB.

**Limite :** le préfixe historique des images provient de la configuration
TMDB du poste yaDIS2 ; si son serveur renvoie HTTPS, le résultat
historique peut être HTTPS. La comparaison HTTP/HTTPS du corpus fourni
ne doit pas être interprétée comme une obligation universelle de
déclasser les URL réseau.

La gestion de `selection=yes` (création éventuelle de groupes par le
scan historique) reste distincte et n'est pas supprimée sans garantir
l'ajout au groupe. Aucun XML existant n'est réécrit uniquement pour
changer ces URL ou ces nombres.
