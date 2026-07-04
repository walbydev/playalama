# Intégration HomeAssistant — Webhook sortant et métriques REST

**Date :** 2026-07-04  
**Statut :** ✅ Implémentée

---

## Vue d'ensemble

Lama.Server expose deux mécanismes complémentaires pour permettre à HomeAssistant (ou tout système externe) de suivre l'activité du serveur :

| Mécanisme | Sens | Usage typique |
|-----------|------|---------------|
| **REST `/stats`** | HA → Lama (polling) | Compteurs sur tableau de bord HA |
| **Webhook sortant** | Lama → HA (push) | Notification instantanée à chaque nouvelle inscription |

---

## 1. Endpoint REST `/stats`

### Réponse

```http
GET /stats
```

```json
{
  "activePlayers": 42,
  "gamesPlayed": 183,
  "activeGames": 3,
  "languages": 3
}
```

| Champ | Source | Description |
|-------|--------|-------------|
| `activePlayers` | PostgreSQL | Nombre total de joueurs inscrits |
| `gamesPlayed` | PostgreSQL | Parties terminées (persistées en DB) |
| `activeGames` | Mémoire (`GameHubState`) | Parties en cours **non clôturées** |
| `languages` | `LanguageProviderRegistry` | Nombre de langues disponibles |

> **Note :** Avant cette implémentation, `gamesPlayed` incluait les parties en cours. Désormais les deux compteurs sont indépendants.

### Configuration HomeAssistant (polling)

```yaml
# configuration.yaml
sensor:
  - platform: rest
    resource: http://lama-server:5201/stats
    name: "Lama — Joueurs inscrits"
    unique_id: lama_active_players
    value_template: "{{ value_json.activePlayers }}"
    unit_of_measurement: "joueurs"
    scan_interval: 60
    icon: mdi:account-group

  - platform: rest
    resource: http://lama-server:5201/stats
    name: "Lama — Parties en cours"
    unique_id: lama_active_games
    value_template: "{{ value_json.activeGames }}"
    unit_of_measurement: "parties"
    scan_interval: 60
    icon: mdi:cards-playing
```

---

## 2. Webhook sortant (push)

### Principe

À chaque inscription réussie d'un joueur, `Lama.Server` envoie un `POST` HTTP asynchrone (fire-and-forget, timeout 5 s) vers une URL configurable.

### Configuration serveur

Variable d'environnement ou clé de configuration :

```
LAMA_HA_WEBHOOK_URL=http://homeassistant.local:8123/api/webhook/lama-events
```

Exemples d'intégration dans les fichiers d'environnement :

```bash
# .env
LAMA_HA_WEBHOOK_URL=http://192.168.1.100:8123/api/webhook/lama-events
```

```yaml
# docker-compose.yml
environment:
  LAMA_HA_WEBHOOK_URL: "http://homeassistant.local:8123/api/webhook/lama-events"
```

Si la variable est absente, un `NullOutboundNotifier` no-op est utilisé — aucun appel HTTP n'est émis.

### Payload

```json
{
  "event": "player.registered",
  "playerName": "Alice",
  "totalPlayers": 42,
  "activeGames": 3,
  "timestamp": "2026-07-04T10:14:00Z"
}
```

### Configuration HomeAssistant (automation)

Dans **Settings → Automations → Create Automation → Trigger : Webhook** :

```yaml
# automations.yaml
automation:
  alias: "Lama — Nouveau joueur inscrit"
  trigger:
    platform: webhook
    webhook_id: "lama-events"        # doit correspondre à l'URL configurée
    allowed_methods:
      - POST
    local_only: false
  condition:
    condition: template
    value_template: "{{ trigger.json.event == 'player.registered' }}"
  action:
    - service: notify.mobile_app_votre_telephone
      data:
        title: "🦙 Nouveau joueur Lama !"
        message: >
          {{ trigger.json.playerName }} vient de s'inscrire !
          {{ trigger.json.totalPlayers }} joueurs inscrits,
          {{ trigger.json.activeGames }} partie(s) en cours.
```

> L'`webhook_id` dans HA doit correspondre à la dernière partie de l'URL configurée dans `LAMA_HA_WEBHOOK_URL`.

---

## 3. Architecture du code

```
src/apps/Lama.Server/
├── Services/
│   ├── IOutboundNotifier.cs          ← interface
│   └── HomeAssistantNotifier.cs      ← impl HTTP + NullOutboundNotifier
├── Endpoints/
│   ├── StatsEndpoints.cs             ← champ activeGames ajouté
│   └── Auth/AuthEndpoints.cs         ← appel notifier post-inscription
└── Program.cs                        ← enregistrement DI conditionnel
```

### Injection de dépendances (`Program.cs`)

```csharp
// Enregistré automatiquement selon LAMA_HA_WEBHOOK_URL
if (!string.IsNullOrWhiteSpace(haWebhookUrl))
    services.AddSingleton<IOutboundNotifier>(sp =>
        new HomeAssistantNotifier(factory, haWebhookUrl, logger));
else
    services.AddSingleton<IOutboundNotifier, NullOutboundNotifier>();
```

### Comportement en cas d'erreur

- Timeout HTTP : 5 secondes (ne bloque pas la réponse `/register`)
- Erreur réseau : loguée en `Warning`, inscription non affectée
- HA indisponible : transparent pour le joueur

---

## 4. Évolutions possibles

| Évolution | Effort | Description |
|-----------|--------|-------------|
| Événement `game.started` / `game.ended` | Faible | Appeler le notifier depuis `GamesCommandEndpoints` |
| SSE global `/api/v1/stream` | Moyen | Flux temps réel consommable par AppDaemon |
| Multi-destinations webhook | Faible | Passer `LAMA_HA_WEBHOOK_URL` en liste CSV |
| Sécurisation webhook HA | Faible | Ajouter un secret HMAC dans le header `X-Lama-Signature` |
