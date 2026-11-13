# Référence réelle de sérialisation

Les 511 XML de `yadis2-xml-reference.zip` viennent directement de l'archive
`yaDIS2-workspace-20261113-1012.zip` fournie par l'utilisateur.

SHA-256 de l'archive source :
`bfc32a83cb0b810946760a585ab66c438b07f3caf6559493c01f46b41697e820`.

La fixture est un conteneur ZIP réempaqueté pour rendre ses noms Unicode
portables. L'original mélange noms CP437 et champs Unicode Path 0x7075,
interprétés différemment par les lecteurs ZIP Python et .NET. Les 511 chemins
et les 511 contenus ont été comparés au conteneur original : aucune modification
des octets XML. Les SHA-256 individuels sont dans docs/LEGACY-XML-COMPARISON.csv.

473 fiches de films, 35 fiches de séries et trois configurations : `folders.xml`,
`groups.xml`, `shortcuts.xml`. Tous les contenus XML sont UTF-8 avec BOM.

Les tests LegacyXmlCorpusTests utilisent ces octets réels. Les scénarios
absents de l'archive sont fondés sur LegacyXmlReference, un oracle issu du code
historique, et ne sont pas présentés comme des captures réelles de l'application.
