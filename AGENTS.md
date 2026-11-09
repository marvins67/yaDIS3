# Règles de code (C# / .NET 10)

## Objectif
Le code doit être lisible par un humain et facile à maintenir : préfère toujours la
clarté à la concision ou à l'astuce.

## Plateforme et projet
- Cible : .NET 10, C# 14. Nullable activé, ImplicitUsings activé.
- Active les analyseurs : `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`,
  `<AnalysisLevel>latest-recommended</AnalysisLevel>`. Aucun avertissement toléré.
- Un fichier `.editorconfig` à la racine fait foi pour le style. Respecte-le.
- Paquets NuGet gérés de façon centralisée (`Directory.Packages.props`).
  N'ajoute aucune dépendance sans la justifier dans ta réponse.

## Architecture
- Sépare strictement la logique (bibliothèque `*.Core`, sans aucune référence à une
  interface graphique) de l'interface utilisateur.
- Dépendances injectées par constructeur (`Microsoft.Extensions.DependencyInjection`).
  Pas d'état statique global, pas de singleton « maison ».
- Une responsabilité par classe. Une méthode fait une chose, ~30 lignes maximum.
- Interface graphique (Avalonia) : MVVM, aucune logique métier dans le code-behind.

## Style
- Namespaces file-scoped, un type par fichier, nom du fichier = nom du type.
- Noms d'identifiants en anglais, explicites et sans abréviation obscure.
  Commentaires et documentation XML en français.
- Commente le *pourquoi*, jamais le *quoi*. Pas de code commenté laissé en place.
- Pas de nombres ou de chaînes « magiques » : constantes nommées ou options typées.
- Préfère `record` pour les données immuables, `readonly` et `init` quand c'est possible.
- Évite l'héritage profond, la réflexion, `dynamic` et les `#region`.

## Fiabilité
- `async/await` de bout en bout, jamais `.Result` ni `.Wait()`.
  Les méthodes asynchrones acceptent un `CancellationToken`.
- Entrées/sorties via des abstractions testables (`TimeProvider`, `IFileSystem`, `HttpClient`
  obtenu par `IHttpClientFactory`).
- Journalisation avec `ILogger<T>` et messages structurés. Aucune exception avalée.
- Valide les arguments publics, utilise des exceptions précises.
- Chemins avec `Path.Combine`, jamais de `\` en dur. Encodage UTF-8 explicite.

## Tests
- xUnit, un test par comportement, nommé `Methode_Condition_ResultatAttendu`.
- Toute logique nouvelle ou modifiée est accompagnée de tests. Pour du code existant
  mal compris, écris d'abord des tests de caractérisation, puis refactorise.

## Méthode de travail
- Petits changements, une intention par commit. Ne mélange pas refactor et nouvelle fonction.
- Ne change pas le comportement quand on te demande un refactor.
- Résume ce que tu as changé et signale tes hypothèses.
- Si une consigne est ambiguë, pose la question au lieu de deviner.
