# SHINO // CODEX MONITOR

Widget Windows local pour afficher les **quotas Codex de l'abonnement ChatGPT**, et non les limites du chat classique ni les quotas de facturation API. Application C# WinForms discrète, quatre dispositions, jauges circulaires et colorées, resets, historique local et alertes.

> **État : V4.7.2 candidate (non publiée)**. Dernière Release publique : **v4.7.1**. V4.7.2 incrémente la version interne afin que le widget déjà installé en 4.7.1 puisse détecter une future Release plus récente. CI Windows V4.7.2 réussie (66 tests Python, 80 autotests C#), essai sur PC utilisateur encore attendu ; les binaires restent non signés. Consulter [Releases](https://github.com/shinobione/SHINO-CODEX-MONITOR/releases) pour les versions réellement publiées.

## Confidentialité

- Le programme lit les quotas via le **Codex CLI installé et déjà authentifié** (méthode `account/rateLimits/read`).
- Aucun mot de passe, cookie ou jeton Codex n'est demandé, copié dans le projet ou envoyé vers GitHub.
- Les fichiers `settings.json` et `history.jsonl` restent sur votre PC : `%LOCALAPPDATA%\SHINO-CODEX-MONITOR`.
- La CI utilise des données **simulées** pour les autotests, jamais de session utilisateur.
- Le dépôt ne contient que les sources, scripts, icônes, tests et documentation ; voir [SECURITY.md](SECURITY.md).

## Télécharger et installer (Windows)

1. Télécharger le fichier officiel `SHINO_CODEX_MONITOR_V4_7_WINDOWS.zip` depuis la [Release GitHub](https://github.com/shinobione/SHINO-CODEX-MONITOR/releases) correspondante.
2. Décompresser le ZIP, puis lancer `INSTALLER-V4.7.bat` dans le dossier extrait **sans droits administrateur**.
3. L'installateur contrôle l'intégrité SHA-256 de l'exécutable, lance les autotests embarqués (**76 pour la Release V4.7.1 ; 80 pour la candidate V4.7.2**), puis installe sous `%LOCALAPPDATA%\Programs\SHINO Codex Monitor`.
4. Codex CLI doit être installé et déjà authentifié sur le compte ChatGPT concerné ; pas de clé API ni de mot de passe à saisir dans le widget.

Le binaire **n'est pas signé**. Windows/Chrome peuvent afficher des avertissements ; ne désactivez pas Defender, SmartScreen ou Safe Browsing. En cas de blocage de téléchargement, ne forcez pas son exécution : privilégiez l'inspection et une compilation locale depuis les sources officielles. Un scan antivirus négatif ne constitue pas une garantie d'innocuité.

## Compiler / tester depuis les sources

- Compiler sans installer : `BUILD-RELEASE-V4.7.bat` (Windows, compilateur C# .NET Framework disponible).
- Compiler et demander l'installation : `PREPARER-ET-INSTALLER-V4.7.bat`.
- Autotests du binaire : `SHINO-CODEX-MONITOR.exe --self-test`.
- Audit statique : `python -m unittest discover -s tests -p "test_*.py"`.
- Build Windows officiel : `.github/workflows/build-windows.yml`. La publication doit porter sur un commit testé.

## Désinstaller

Menu Démarrer → « Désinstaller SHINO Codex Monitor », ou « Applications installées » Windows. La désinstallation demande confirmation et conserve par défaut les données locales.

## Limites

La disponibilité des fenêtres et resets dépend des données réellement renvoyées par Codex. Les relevés périmés doivent apparaître comme tels. Cette application est communautaire et **non officielle**, sans affiliation avec OpenAI. Les binaires ne sont pas signés ; vérifier la provenance des artefacts.

Voir [README-FR.txt](README-FR.txt) pour la procédure complète.

**Licence :** source visible publiquement pour l'instant ; aucune licence de réutilisation n'a encore été choisie par le propriétaire. Public ne signifie pas automatiquement « open source sous licence ».

## Vérifier les mises à jour (V4.5)

Clic droit sur l'icône `%` → **Vérifier les mises à jour**. L'application effectue
un appel HTTPS en lecture seule vers l'API publique GitHub Releases du dépôt.
Elle compare la version de l'exécutable à la dernière Release **stable**,
sans lire le compte Codex, sans jeton GitHub et sans téléchargement automatique.
Si une version plus récente existe, tu peux ouvrir sa page officielle.
Le programme actuel continue de fonctionner si GitHub est indisponible.

**Publication :** seules les Releases visibles dans la page officielle doivent être considérées comme publiées.

## Avertissement de sécurité au démarrage (V4.7.1)

Après vérification du SHA-256 et réussite des autotests, l’installateur retire, **sur le seul exécutable installé**, le marquage Internet éventuel (`Unblock-File`). Il ne désactive aucune protection Windows, ne modifie pas les clés de sécurité et n’accorde aucune confiance à l’éditeur : **le binaire reste non signé**. Si l’opération échoue, un avertissement explicite est affiché. L’intégrité SHA-256 contenue dans le même ZIP ne constitue pas une preuve d’authenticité de l’éditeur. Télécharger uniquement la Release du dépôt officiel.

## V4.7 : mise à jour assistée

Clic droit > **Vérifier les mises à jour** > confirmer **téléchargement** > confirmer **installation**.
L'archive provient de GitHub Releases et son SHA-256 doit correspondre à `assets[].digest` dans l'API GitHub.
Un deuxième contrôle vérifie les chemins du ZIP et le SHA-256 de l'exécutable.
En l'absence de digest ou en cas de fichier inattendu, réseau interrompu ou incohérence : l'installation est refusée.
L'installateur reprend la sauvegarde binaire et conserve les JSON locaux. Une console temporaire peut afficher
le résultat de l'installation. Ne jamais exécuter le téléchargement en mode silencieux.

**Limites :** cela vérifie l'intégrité de ce que GitHub a servi, pas une signature numérique de l'éditeur.
Le processus doit encore être éprouvé de bout en bout sur un PC Windows (veille, réseau, réinstallations et rollback).

## V4.7 Circular Dashboard

Dans l'icône système → **Disposition → Circulaire · 2 roues**, affiche les quotas 5 h et 7 jours comme deux anneaux.
Le pourcentage au centre signifie **quota restant**, jamais consommé. Les réinitialisations affichées
proviennent exclusivement de Codex CLI. L'état, l'origine et l'âge du relevé restent visibles.
Donnée absente : « — » ; donnée périmée : anneau gris et pourcentage marqué « * ».

**Couleurs selon le pourcentage entier affiché** : 76–100 violet (`#A477FF`),
51–75 turquoise (`#51D7AF`), 26–50 doré (`#E5B64D`),
11–25 orange (`#F28C38`), 0–10 rouge (`#EE4B55`).
La même échelle colore l'icône système et les anciennes barres.
Ces cinq nouvelles couleurs ont leurs propres clés dans le JSON, sans effacer les anciennes.
Une installation neuve propose le mode circulaire par défaut ; les dispositions déjà enregistrées sont respectées.

Les dispositions restent sélectionnables indépendamment ; aucune valeur simulée ne doit être présentée comme quota réel.

## V4.7.1 — Apparence & disposition

Clic droit sur le tray → **Apparence** : opacité 70/80/90/96/100 %, choix de quatre fonds sombres ou couleur personnalisée via `ColorDialog`. Les fonds trop clairs sont refusés afin de conserver le contraste des états et des resets. Les préférences sont persistées dans le `settings.json` local ; les anciennes dispositions et données ne sont pas modifiées.

Le mode **Micro** réserve une marge au bouton de fermeture ; le mode **Circulaire** réduit la fenêtre à 540 × 278 et agrandit les anneaux. Version affichée dans le diagnostic : version réelle du binaire. Les mises à jour visuelles ont été vérifiées sur Windows ; les variations de DPI et le dernier correctif de reset bloqué restent à confirmer.

### Réinitialisation lorsque Codex indique « usage bloqué »

Dans la V4.7.1, le menu **Réinitialisations** reste fonctionnel si `ordinaryUsageAllowed=false` et si les quotas sont lus directement et récemment : le message de blocage apparaît en pied de widget, mais la date et/ou le compte à rebours du reset restent affichés sous chaque fenêtre **uniquement si** `resetsAt` a réellement été communiqué par Codex. Sinon : « Reset non communiqué ». Un relevé ancien n'affiche pas son ancien reset comme une donnée fraîche. Les choix « Date/heure + décompte », « Date/heure uniquement », « Décompte uniquement » et « Masquées » restent indépendants du statut bloqué.

## V4.7.2 — Identification fiable des mises à jour (candidate)

La V4.7.1 avait reçu un correctif après qu'un premier exécutable 4.7.1 eut déjà été installé localement. Le vérificateur de mises à jour compare les numéros `Major.Minor.Build` et ne peut pas distinguer deux exécutables différents avec **le même numéro**. La V4.7.2 incrémente explicitement ce numéro dans l'assembly, les libellés Windows, le rapport de tests et l'installateur. Les noms ZIP et scripts restent `V4_7` pour assurer la compatibilité avec le téléchargeur vérifié.

Les User-Agent des requêtes GitHub utilisent désormais automatiquement la version de l'exécutable ; aucune collecte Codex, préférence ou historique local n'est modifié.

La candidate V4.7.2 a passé la CI Windows. Le test de détection de mise à jour sur le PC utilisateur reste à faire avant publication. Une nouvelle Release n'est **pas** créée automatiquement.
