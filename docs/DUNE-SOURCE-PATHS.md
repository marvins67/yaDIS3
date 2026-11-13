# Conservation du chemin Dune saisi manuellement

## Cause de l'écart

Le sélecteur de dossiers recalculait systématiquement le champ Chemin Dune,
même lorsqu'il contenait déjà `/Séries`. Pour le disque indiqué par son adresse
IP, ce calcul proposait `smb://192.168.1.10/DuneHDD_26b6a642067197ba/Séries`.
La configuration et le scan enregistraient ensuite cette nouvelle valeur.

La correction conserve le chemin saisi ou déjà enregistré. Elle n'introduit
aucune règle de détection du disque ni conversion générale de SMB en chemin
local. Le calcul historique de `LegacyDunePathResolver` reste inchangé.

## Comportement corrigé

- `/Séries` saisi manuellement reste `/Séries` lors du choix du dossier.
- Une configuration chargée avec ce chemin le conserve aussi.
- Une saisie effectuée pendant une résolution DNS prime sur sa réponse tardive.
- Un champ vide reçoit toujours la proposition automatique historique.
- Une proposition automatique non modifiée suit les prochains choix de dossiers.
- Effacer le champ réactive la proposition automatique au prochain choix.

Le comportement vaut aussi pour un URI NFS, SMB ou UUID explicitement configuré,
et en mode basique. Il ne dépend ni de l'adresse IP ni de l'identifiant du disque.

## Application au workspace existant

Si `/Séries` a déjà été remplacé par un URI SMB, saisir de nouveau `/Séries` dans
le champ Chemin Dune de la source, puis appliquer la configuration. Le choix du
dossier n'écrasera plus cette saisie. Lorsque la source est accessible, le scan
reporte cette valeur dans les fiches selon les règles existantes de mise à jour
et de statut. Les fichiers déjà modifiés ne permettent pas de retrouver
spontanément une ancienne saisie qui n'y figure plus.

## Vérification

Les tests couvrent le chemin chargé, la saisie avant sélection, la saisie pendant
une résolution DNS, le mode basique, le suivi d'une proposition automatique,
l'effacement et la réactivation du calcul. Un test de bout en bout vérifie
`<dunepath>/Séries</dunepath>` dans `folders.xml` et dans le `data.xml` créé.

La sérialisation XML et les règles d'export Dune ne sont pas modifiées.
Validation du 13 novembre 2026 :

- Compilation de la solution en Release : 0 erreur, 0 avertissement.
- Suite Desktop complète : 138 tests réussis, dont les 7 nouveaux cas.
- Régressions Core ciblées chemins Dune, scan et XML : 55 tests réussis.
- Corpus XML : les 511 fichiers régénérés restent identiques aux références.
- `dotnet format yaDIS3.sln --verify-no-changes` : conforme.

Seul le code du sélecteur Desktop est modifié ; le Core et le rendu restent
inchangés. Les tests de configuration ont été mis à jour pour vérifier la
conservation demandée au lieu de l'ancien écrasement systématique.
