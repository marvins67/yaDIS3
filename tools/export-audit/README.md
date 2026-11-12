# Comparaison reproductible des exports Dune

Python 3, bibliothèque standard uniquement. Les entrées acceptent les ZIP
originaux ou les dossiers racines extraits (`yaDIS2`, `yaDIS3`). L'outil lit les
ZIP sans modifier les archives ni les fichiers générés.

```sh
python3 tools/export-audit/compare_exports.py yaDIS2-20261112-2148.zip yaDIS3-20261112-2148.zip --output work/audit --check-navigation
python3 -m unittest discover -s tools/export-audit -v
```

Le rapport contient inventaire et comparaison binaire, fichiers absents et
supplémentaires, différences intégrales des TXT, propriétés et entrées ordonnées
avec leurs indices et liens, et inventaire des références. `--check-local-links`
retourne 1 si une cible locale manque, 0 sinon. `--check-navigation` ne fait
échouer le contrôle que pour une destination de navigation absente, tout en
conservant toutes les images manquantes dans le rapport. Les ZIP de référence
ont quatre références d’icône absentes dans yaDIS2 et une dans yaDIS3, déjà
présente côté yaDIS2 (Ghibli). Le code de retour
n'indique pas l'égalité des deux exports : consulter `audit.json` et `audit.md`.

Les `media_url` sans `media_action=browse`, les chemins absolus et les schémas
Dune/SMB/NFS sont des références externes ; leur accessibilité ne peut être
vérifiée sans le stockage vidéo et la configuration. Les icônes du dossier, les icônes des entrées et les fonds relatifs sont tous
vérifiés comme fichiers locaux. La portée spéciale `*_children` est signalée
séparément et n’est pas développée (elle ne concerne aucune référence graphique
du jeu). Voir la documentation officielle :
https://dune-hd.com/support/misc/dune_folder_howto.txt.
Les noms sont conservés sans translittération, décodage URL ou normalisation
Unicode. Une analyse exhaustive du langage Dune (redirections réseau, variables,
archives de médias ou syntaxes non présentes dans ce jeu) dépasse cet outil.

Pour les rapports du 12 novembre, voir `docs/EXPORT-REDUCED-PARITY-20261112.md`.
Le fichier JSON conserve toutes les propriétés des 57 TXT et le diff les compare
ligne par ligne ; les CSV fournis par l'utilisateur n'interviennent pas dans
leur calcul. La régénération de l'export original requiert les XML des fiches,
`groups.xml`, `shortcuts.xml`, la configuration et `workspace/interface`.
