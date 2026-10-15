# Sécurité et confidentialité

SHINO // CODEX MONITOR est une application locale en lecture seule. Elle utilise `codex app-server --listen stdio://` et `account/rateLimits/read` avec la session **déjà ouverte** dans Codex CLI. Elle ne demande jamais le mot de passe ChatGPT et ne lit pas `.codex/auth.json`.

Le dépôt public doit contenir exclusivement le code source, les icônes, les scripts de build et les tests. Ne publiez **jamais** : `settings.json`, `history.jsonl`, fichiers de `.codex`, cookies, jetons, captures de comptes, diagnostics privés, logs ou fichiers `.env`.

Les fichiers de préférences et d'historique ne sont créés que sur le PC de l'utilisateur dans `%LOCALAPPDATA%\SHINO-CODEX-MONITOR`. La CI n'interroge pas de compte Codex réel ; les tests utilisent des réponses simulées. Aucun secret n'est nécessaire dans GitHub Actions.

Avant toute contribution, vérifier `git status`, `git diff --cached --name-only` et lancer `python -m unittest discover -s tests -p "test_*.py"`. Une règle `.gitignore` ne protège pas un fichier déjà suivi : l'outil de contrôle bloque ces chemins avant publication.

Pour une vulnérabilité ou une fuite potentielle, **ne publiez pas les secrets dans une issue publique**. Contactez le mainteneur en privé, ou utilisez les conseils privés GitHub si configurés. Révoquez immédiatement toute donnée sensible déjà exposée : la supprimer d'un commit ne suffit pas.

**Important :** un SHA-256 local vérifie l'intégrité du paquet mais ne prouve pas la signature de l'éditeur. Les binaires compilés ne sont pas signés.
