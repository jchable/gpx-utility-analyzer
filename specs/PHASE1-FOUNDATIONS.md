# Phase 1 — Fondations

> Spécification détaillée — Mars 2026
> Pré-requis pour toutes les phases suivantes (entraînement, social, SaaS)

---

## Table des matières

1. [Vue d'ensemble](#1-vue-densemble)
2. [Structure de la solution](#2-structure-de-la-solution)
3. [Authentification & autorisation](#3-authentification--autorisation)
4. [Modèle de données utilisateur](#4-modèle-de-données-utilisateur)
5. [Migration multi-utilisateur](#5-migration-multi-utilisateur)
6. [Profil athlète enrichi](#6-profil-athlète-enrichi)
7. [Enrichissement activité](#7-enrichissement-activité)
8. [Calcul des calories](#8-calcul-des-calories)
9. [Intégration météo](#9-intégration-météo)
10. [Gestion d'équipement](#10-gestion-déquipement)
11. [Stockage objet (S3/MinIO)](#11-stockage-objet-s3minio)
12. [Envoi d'emails](#12-envoi-demails)
13. [RGPD & conformité](#13-rgpd--conformité)
14. [Backend d'administration](#14-backend-dadministration)
15. [Impact frontend](#15-impact-frontend)
16. [Migrations EF Core](#16-migrations-ef-core)
17. [Configuration Docker](#17-configuration-docker)
18. [Plan de tests](#18-plan-de-tests)
19. [Ordre d'implémentation](#19-ordre-dimplémentation)

---

## 1. Vue d'ensemble

### Objectif

Transformer l'application mono-utilisateur en plateforme multi-utilisateur avec :
- Authentification complète (email/password, OAuth Google/Strava, magic link)
- Isolation des données par utilisateur
- Profil athlète enrichi
- Données d'activité enrichies (description, RPE, tags, type session, calories, météo)
- Gestion d'équipement
- Stockage objet (MinIO/S3)
- Conformité RGPD complète
- Backend d'administration séparé

### Décisions architecturales clés

| Décision | Choix |
|----------|-------|
| Auth backend | ASP.NET Identity + JWT Bearer |
| Auth méthodes | Email/password + OAuth (Google, Strava) + Passwordless (magic link) |
| Rôles | `Admin`, `Premium`, `User` |
| Multi-user | Instance partagée, isolation par `UserId` |
| Migration données | Reset propre (pas de migration des données existantes) |
| Profil | Complet (poids, taille, sexe, date naissance, photo, nom, bio, ville, unités) |
| Calories | MET + fallback FC (toujours un résultat) |
| Météo | Open-Meteo API (gratuite, historique) |
| Équipement | Générique + défaut par type d'activité |
| Stockage | Object storage (MinIO dev / S3 prod) |
| Email | SMTP par défaut + SendGrid optionnel |
| RGPD | Complet (export, suppression, registre, consentements, portabilité, DPO, journal) |

---

## 2. Structure de la solution

### Nouvelle organisation

```
ui/
  api/                        → Backend produit (API utilisateur)
    Controllers/
    Services/
    Entities/
    Data/
    ...
  admin-api/                  → Backend administration (API admin uniquement)
    Controllers/
    Services/
    Program.cs
  client/                     → Frontend produit (React, existant)
  admin-client/               → Frontend administration (React, nouveau)
```

### Backend produit (`ui/api/`)

L'API existante, enrichie avec :
- ASP.NET Identity
- JWT Bearer authentication
- Rôles `User` / `Premium` / `Admin`
- Tous les endpoints protégés par `[Authorize]`
- Isolation des données par `UserId`

### Backend administration (`ui/admin-api/`)

Projet ASP.NET Core séparé, accessible uniquement aux `Admin` :
- Gestion des utilisateurs (CRUD, rôles, suspension)
- Statistiques plateforme (nombre users, activités, stockage)
- Configuration globale (providers IA, limites)
- Journal d'accès RGPD
- Partage du même `DbContext` / entités (référence au projet `ui/api/` ou bibliothèque partagée)

### Frontend administration (`ui/admin-client/`)

Application React séparée :
- Dashboard admin (stats plateforme)
- Gestion utilisateurs (liste, détail, rôles, suspension)
- Configuration système
- Logs RGPD

---

## 3. Authentification & autorisation

### 3.1 Stack technique

- **ASP.NET Identity** — gestion users, passwords, rôles, claims, tokens
- **JWT Bearer** — authentification API stateless
- **Refresh tokens** — rotation automatique, stockés en base
- **Google OAuth 2.0** — via `Microsoft.AspNetCore.Authentication.Google`
- **Strava OAuth 2.0** — réutilisation du flow existant (`StravaService`), liaison au compte utilisateur
- **Passwordless** — magic link par email avec token à durée limitée

### 3.2 Entité User (ASP.NET Identity)

Extension de `IdentityUser<Guid>` :

```csharp
public class ApplicationUser : IdentityUser<Guid>
{
    // IdentityUser fournit : Id, UserName, Email, PasswordHash, EmailConfirmed,
    //                        PhoneNumber, TwoFactorEnabled, LockoutEnd, etc.

    // Extensions
    public string DisplayName { get; set; } = "";
    public string? Bio { get; set; }
    public string? City { get; set; }
    public string? ProfilePhotoPath { get; set; }
    public string PreferredUnits { get; set; } = "metric";  // "metric" | "imperial"
    public string Language { get; set; } = "en";             // "en" | "fr"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;               // suspension par admin

    // Navigation
    public AthleteProfile? AthleteProfile { get; set; }
    public ICollection<Activity> Activities { get; set; } = [];
    public ICollection<Route> Routes { get; set; } = [];
    public ICollection<Integration> Integrations { get; set; } = [];
    public ICollection<Equipment> Equipment { get; set; } = [];
    public ICollection<UserConsent> Consents { get; set; } = [];
}
```

### 3.3 Rôles

| Rôle | Accès |
|------|-------|
| `User` | Fonctionnalités de base (upload, analyse, routes, profil) |
| `Premium` | Toutes fonctionnalités User + IA, exports avancés, météo, équipement illimité |
| `Admin` | Tout + backend d'administration |

> Note : la distinction `User` / `Premium` est préparée mais non appliquée en Phase 1. Tous les utilisateurs ont accès à tout. Les restrictions viendront en Phase 5 (SaaS).

### 3.4 Endpoints d'authentification

```
POST   /api/auth/register          → Inscription email/password
POST   /api/auth/login             → Connexion email/password → JWT + refresh token
POST   /api/auth/refresh           → Renouvellement JWT via refresh token
POST   /api/auth/logout            → Révocation refresh token
POST   /api/auth/forgot-password   → Envoi email reset password
POST   /api/auth/reset-password    → Reset password avec token
POST   /api/auth/magic-link        → Envoi magic link par email
GET    /api/auth/magic-link/verify → Vérification magic link → JWT
GET    /api/auth/google            → Redirection OAuth Google
GET    /api/auth/google/callback   → Callback OAuth Google → JWT
GET    /api/auth/strava            → Redirection OAuth Strava (login)
GET    /api/auth/strava/callback   → Callback OAuth Strava → JWT
GET    /api/auth/me                → Profil utilisateur connecté
POST   /api/auth/confirm-email     → Confirmation email (token)
```

### 3.5 JWT Configuration

```json
{
  "Jwt": {
    "Secret": "...",              // min 256 bits, env var en prod
    "Issuer": "gpx-analyzer",
    "Audience": "gpx-analyzer-client",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 30
  }
}
```

### 3.6 Refresh Token Entity

```csharp
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Token { get; set; } = "";        // SHA256 hash
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedByIp { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedByIp { get; set; }
    public string? ReplacedByToken { get; set; }    // rotation chain

    public ApplicationUser User { get; set; } = null!;
}
```

### 3.7 Flow OAuth Google

1. Frontend → `GET /api/auth/google` → redirect vers Google consent screen
2. Google callback → `GET /api/auth/google/callback?code=...`
3. Backend échange le code → récupère profil Google (email, nom, photo)
4. Si email existe en base → login, sinon → création automatique du compte
5. Retour JWT + refresh token au frontend (via redirect avec token dans URL fragment)

### 3.8 Flow OAuth Strava (login)

Distinct du flow d'intégration Strava existant (import d'activités) :

1. Frontend → `GET /api/auth/strava` → redirect vers Strava authorize
2. Strava callback → `GET /api/auth/strava/callback?code=...`
3. Backend échange le code → récupère profil Strava (athlete ID, nom, photo)
4. Si Strava ID déjà lié → login. Sinon → création compte ou liaison au compte existant
5. Retour JWT + refresh token

> Note : Le flow d'import d'activités Strava (`/api/integrations/strava/connect`) reste séparé. Un utilisateur peut se connecter avec Google ET avoir une intégration Strava pour l'import.

### 3.9 Flow Passwordless (magic link)

1. Frontend → `POST /api/auth/magic-link` avec `{ email }``
2. Backend génère un token signé (HMAC, expiration 15 min)
3. Envoi email avec lien : `{frontendUrl}/auth/verify?token=...`
4. Clic → Frontend → `GET /api/auth/magic-link/verify?token=...`
5. Backend vérifie le token → login ou création du compte
6. Retour JWT + refresh token

### 3.10 Sécurité

- Passwords : ASP.NET Identity utilise PBKDF2 par défaut (configurable)
- JWT secret : minimum 256 bits, stocké en variable d'environnement
- Refresh tokens : hashés en base (SHA256), rotation à chaque utilisation
- Rate limiting : sur `/api/auth/*` (ex: 5 tentatives login / minute / IP)
- CORS : configurable par environnement
- HTTPS obligatoire en production

---

## 4. Modèle de données utilisateur

### 4.1 Ajout de `UserId` sur les entités existantes

Chaque entité reçoit une FK vers `ApplicationUser` :

```csharp
// Activity.cs — ajouts
public Guid UserId { get; set; }
public ApplicationUser User { get; set; } = null!;

// Route.cs — ajouts
public Guid UserId { get; set; }
public ApplicationUser User { get; set; } = null!;

// Integration.cs — ajouts
public Guid UserId { get; set; }
public ApplicationUser User { get; set; } = null!;
```

### 4.2 Setting → Scoped par utilisateur

La table `Setting` (key/value global) est remplacée par deux mécanismes :

1. **`AthleteProfile`** — entité typée pour le profil athlète (voir §6)
2. **`UserSetting`** — key/value par utilisateur pour les préférences

```csharp
public class UserSetting
{
    public Guid UserId { get; set; }
    public string Key { get; set; } = "";       // max 200, PK composite (UserId, Key)
    public string Value { get; set; } = "";     // max 4000
    public DateTime UpdatedAt { get; set; }

    public ApplicationUser User { get; set; } = null!;
}
```

**Configuration globale** (AI provider, limites) → `appsettings.json` + variables d'environnement (plus en base). Seul l'admin peut modifier via l'admin-api.

**Settings migrées vers UserSetting :**

| Ancien Key (Setting) | Nouveau Key (UserSetting) | Scope |
|---|---|---|
| `Athlete:MaxHR` | → `AthleteProfile.MaxHeartRate` | Entité typée |
| `Athlete:Age` | → `AthleteProfile.DateOfBirth` | Entité typée |
| `Athlete:FTP` | → `AthleteProfile.Ftp` | Entité typée |
| `GpxCli:DefaultPreset` | `analysis.defaultPreset` | Par user |
| `GpxCli:DefaultSmoothing` | `analysis.defaultSmoothing` | Par user |
| `GpxCli:DefaultTrackSmoothing` | `analysis.defaultTrackSmoothing` | Par user |
| `GpxCli:ElevationAlgorithm` | `analysis.elevationAlgorithm` | Par user |
| `GpxCli:FixAnomalies` | `analysis.fixAnomalies` | Par user |
| `AiProvider:*` | → `appsettings.json` | Global (admin) |
| `Integrations:Strava:*` | → `appsettings.json` | Global (admin) |
| `Integrations:Garmin:*` | → `appsettings.json` | Global (admin) |

### 4.3 Index et contraintes

```
Activity: Index (UserId, StartTime DESC) — requête principale
Route: Index (UserId, UpdatedAt DESC)
Integration: Unique (UserId, Provider) — remplace Unique (Provider)
UserSetting: PK composite (UserId, Key)
RefreshToken: Index (UserId), Index (Token)
```

### 4.4 Filtrage automatique par utilisateur

Chaque requête de données est filtrée par `UserId` extrait du JWT :

```csharp
// Extension pour extraire le UserId du ClaimsPrincipal
public static Guid GetUserId(this ClaimsPrincipal user)
    => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

// Exemple dans un contrôleur
[Authorize]
public async Task<IActionResult> GetActivities()
{
    var userId = User.GetUserId();
    var activities = await _context.Activities
        .Where(a => a.UserId == userId)
        .OrderByDescending(a => a.StartTime)
        .ToListAsync();
    // ...
}
```

---

## 5. Migration multi-utilisateur

### Stratégie : Reset propre

Lors de l'application de la migration EF Core :

1. Les tables existantes (`Activities`, `Routes`, `Integrations`, `Settings`) sont **vidées**
2. Les nouvelles tables Identity sont créées (`AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, etc.)
3. Les colonnes `UserId` (NOT NULL, FK) sont ajoutées aux tables existantes
4. Les nouvelles tables sont créées (`AthleteProfiles`, `Equipment`, `UserSettings`, `RefreshTokens`, `UserConsents`, `AuditLogs`)
5. Les fichiers GPX existants sur disque peuvent être supprimés manuellement

### Migration EF Core

```csharp
// Dans la migration : vider les tables avant d'ajouter la FK NOT NULL
migrationBuilder.Sql("DELETE FROM Activities;");
migrationBuilder.Sql("DELETE FROM Routes;");
migrationBuilder.Sql("DELETE FROM Integrations;");
migrationBuilder.Sql("DELETE FROM Settings;");

// Puis ajouter les colonnes UserId avec FK
migrationBuilder.AddColumn<Guid>("UserId", "Activities", nullable: false);
// ... etc.
```

### Seeding initial

Au premier lancement, un compte admin est créé si aucun utilisateur n'existe :

```json
{
  "Admin": {
    "Email": "admin@example.com",      // configurable via env var
    "Password": "ChangeMe123!"         // configurable via env var
  }
}
```

---

## 6. Profil athlète enrichi

### 6.1 Entité AthleteProfile

Remplace les settings `Athlete:*` par une entité typée 1-to-1 avec User :

```csharp
public class AthleteProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    // Biométrie
    public double? WeightKg { get; set; }           // Poids en kg
    public double? HeightCm { get; set; }           // Taille en cm
    public string? Sex { get; set; }                // "male" | "female" | "other"
    public DateTime? DateOfBirth { get; set; }      // Remplace "Age"

    // Performance
    public int? MaxHeartRate { get; set; }           // bpm (ex-Setting Athlete:MaxHR)
    public int? RestingHeartRate { get; set; }       // bpm (nouveau)
    public int? Ftp { get; set; }                    // watts (ex-Setting Athlete:FTP)
    public double? Vo2Max { get; set; }              // mL/kg/min (nouveau, optionnel)

    // Timestamps
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ApplicationUser User { get; set; } = null!;
}
```

### 6.2 Propriétés calculées

```csharp
// Âge calculé à partir de DateOfBirth
public int? Age => DateOfBirth.HasValue
    ? (int)((DateTime.UtcNow - DateOfBirth.Value).TotalDays / 365.25)
    : null;

// FC max estimée (si non renseignée)
public int EstimatedMaxHR => MaxHeartRate
    ?? (Age.HasValue ? 220 - Age.Value : 185);

// BMI (si poids et taille renseignés)
public double? Bmi => (WeightKg.HasValue && HeightCm.HasValue && HeightCm > 0)
    ? WeightKg.Value / Math.Pow(HeightCm.Value / 100.0, 2)
    : null;
```

### 6.3 Endpoint

```
GET    /api/profile              → Profil athlète complet
PUT    /api/profile              → Mise à jour profil athlète
POST   /api/profile/photo        → Upload photo de profil
DELETE /api/profile/photo        → Suppression photo de profil
```

### 6.4 Impact sur les calculs existants

- **Zones FC** : utilisent `AthleteProfile.MaxHeartRate` (ou estimé via âge)
- **Zones puissance** : utilisent `AthleteProfile.Ftp`
- **Calories** : utilisent poids, âge, sexe, FC (voir §8)
- **IA Report** : le prompt peut inclure le profil athlète pour personnaliser l'analyse

---

## 7. Enrichissement activité

### 7.1 Nouveaux champs sur Activity

```csharp
// Activity.cs — ajouts
public string? Description { get; set; }               // Texte libre (markdown)
public int? PerceivedExertion { get; set; }             // RPE 1-10
public string? Tags { get; set; }                       // JSON array ["sortie longue", "montagne"]
public string? SessionType { get; set; }                // Voir enum ci-dessous
public double? EstimatedCalories { get; set; }          // kcal (calculé, voir §8)
public string? CalorieMethod { get; set; }              // "hr" | "met" (méthode utilisée)
public string? WeatherJson { get; set; }                // Données météo (voir §9)
public Guid? EquipmentId { get; set; }                  // FK équipement principal
public Equipment? Equipment { get; set; }
```

### 7.2 Type de session

Valeurs possibles (enum string) :

| Valeur | Label FR | Label EN |
|--------|----------|----------|
| `long_run` | Sortie longue | Long run |
| `race` | Course / Compétition | Race |
| `training` | Entraînement | Training |
| `recovery` | Récupération | Recovery |
| `intervals` | Fractionné | Intervals |
| `tempo` | Tempo | Tempo |
| `easy` | Sortie facile | Easy run |

### 7.3 Tags

Stockés en JSON array dans un champ string :

```json
["montagne", "neige", "nocturne", "altitude"]
```

Tags libres saisis par l'utilisateur. Pas de liste prédéfinie (contrairement au type de session).

### 7.4 Endpoints modifiés

```
PATCH  /api/activities/{id}    → Ajout : description, perceivedExertion, tags, sessionType, equipmentId
GET    /api/activities/tags    → Liste de tous les tags utilisés (autocomplétion)
```

### 7.5 i18n

Nouvelles clés à ajouter :

```
common.json:
  sessionType.long_run, sessionType.race, sessionType.training,
  sessionType.recovery, sessionType.intervals, sessionType.tempo,
  sessionType.easy

activities.json:
  label.description, label.perceivedExertion, label.tags,
  label.sessionType, label.calories, label.weather, label.equipment
  placeholder.description, placeholder.tags
  rpe.1 à rpe.10 (labels descriptifs)
```

---

## 8. Calcul des calories

### 8.1 Approche : MET + fallback FC

Deux méthodes, la plus précise est utilisée en priorité :

**Méthode 1 — FC (prioritaire, si FC disponible + profil complet)**

Formule Keytel et al. (2005) :

```
Homme : kcal/min = (-55.0969 + 0.6309 × FC + 0.1988 × poids + 0.2017 × âge) / 4.184
Femme : kcal/min = (-20.4022 + 0.4472 × FC + 0.1263 × poids + 0.0740 × âge) / 4.184
```

Prérequis : FC moyenne, poids (kg), âge, sexe. Si un champ manque → fallback MET.

**Méthode 2 — MET (fallback, toujours disponible)**

```
kcal = MET × poids (kg) × durée (h)
```

Si poids non renseigné : poids par défaut 70 kg (homme) / 60 kg (femme) / 65 kg (non précisé).

### 8.2 Valeurs MET par type d'activité et intensité

| Type | Allure lente | Allure modérée | Allure rapide |
|------|-------------|----------------|---------------|
| `run` | 8.0 | 10.0 | 12.5 |
| `trail` | 9.0 | 11.0 | 14.0 |
| `hike` | 5.5 | 7.0 | 8.5 |
| `cycle` | 6.0 | 8.0 | 12.0 |
| `walk` | 3.0 | 4.0 | 5.0 |
| `swim` | 6.0 | 8.0 | 10.0 |
| `other` | 5.0 | 7.0 | 9.0 |

L'intensité est déterminée par la vitesse moyenne en mouvement rapportée au type d'activité :
- Lente : < 60% de la vitesse typique
- Modérée : 60-120%
- Rapide : > 120%

Ajustement dénivelé (trail/hike) : `MET_ajusté = MET × (1 + D+ par km / 100 × 0.1)`

### 8.3 Implémentation

- **Fichier** : `cli/src/GpxAnalyzer.Cli.Core/Stats/CalorieCalculator.cs` (nouveau)
- **Appelé dans** : `ComputePipeline.Compute()` après le calcul biométrique
- **Résultat** : ajouté à `Summary` (nouveau champ `EstimatedCalories` + `CalorieMethod`)
- **Propagé** : `SummaryMapper.ToGpxStats()` → `GpxStats` → `Activity.EstimatedCalories`

### 8.4 Affichage

- Carte stat sur la page activité détail : "Calories estimées" avec icône flamme
- Indication de la méthode utilisée (FC / MET) en tooltip
- Dashboard : total calories du mois (agrégé)

---

## 9. Intégration météo

### 9.1 API Open-Meteo

API gratuite, sans clé, données historiques.

**Endpoint** : `https://archive-api.open-meteo.com/v1/archive`

**Paramètres** :
```
latitude, longitude          → point central de l'activité
start_date, end_date         → date de l'activité
hourly=temperature_2m,relative_humidity_2m,wind_speed_10m,precipitation,weather_code
timezone=auto
```

### 9.2 Modèle WeatherData

```csharp
public class WeatherData
{
    public double? TemperatureCelsius { get; set; }        // °C
    public double? FeelsLikeCelsius { get; set; }          // °C (wind chill / heat index)
    public double? Humidity { get; set; }                   // %
    public double? WindSpeedKmh { get; set; }               // km/h
    public double? PrecipitationMm { get; set; }            // mm
    public int? WeatherCode { get; set; }                   // WMO code
    public string? WeatherDescription { get; set; }         // "Sunny", "Light rain", etc.
    public string? WeatherIcon { get; set; }                // Icône (pour affichage)
}
```

Stocké en JSON dans `Activity.WeatherJson`.

### 9.3 Implémentation

- **Service** : `ui/api/Services/WeatherService.cs` (nouveau)
- **Appelé par** : `ActivityProcessingService` après l'analyse GPX (coordonnées et date disponibles)
- **Point central** : moyenne des lat/lon du premier et dernier point de la trace
- **Heure** : heure de début de l'activité → sélection de la tranche horaire la plus proche
- **Cache** : pas nécessaire (données historiques stables, appelé une seule fois par activité)

### 9.4 WMO Weather Codes

Mapping des codes WMO vers descriptions et icônes :

| Code | Description | Icône |
|------|-------------|-------|
| 0 | Ciel dégagé | ☀️ |
| 1-3 | Partiellement nuageux | ⛅ |
| 45, 48 | Brouillard | 🌫️ |
| 51-55 | Bruine | 🌧️ |
| 61-65 | Pluie | 🌧️ |
| 71-75 | Neige | 🌨️ |
| 80-82 | Averses | 🌦️ |
| 95 | Orage | ⛈️ |

### 9.5 Affichage

- Bandeau météo sur la page activité détail (température, vent, conditions)
- Icône météo dans la carte d'activité (liste)
- Pas d'emojis dans le code — utiliser des icônes SVG ou classes CSS

---

## 10. Gestion d'équipement

### 10.1 Entité Equipment

```csharp
public class Equipment
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public string Name { get; set; } = "";                    // "Salomon Speedcross 6"
    public string Type { get; set; } = "";                    // "shoes" | "bike" | "watch" | "poles" | "other"
    public string? Brand { get; set; }                        // "Salomon"
    public string? Model { get; set; }                        // "Speedcross 6"
    public DateTime? PurchaseDate { get; set; }
    public bool IsRetired { get; set; } = false;              // Retiré de l'usage

    // Cumuls (calculés à partir des activités liées)
    public double TotalDistanceKm { get; set; }
    public double TotalElevationGainM { get; set; }
    public int TotalActivities { get; set; }
    public double TotalDurationSeconds { get; set; }

    // Alerte usure
    public double? WearAlertDistanceKm { get; set; }         // Seuil distance (ex: 800 km)
    public double? WearAlertElevationM { get; set; }         // Seuil dénivelé (ex: 50000 m)

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ApplicationUser User { get; set; } = null!;
    public ICollection<Activity> Activities { get; set; } = [];
}
```

### 10.2 Équipement par défaut par type d'activité

```csharp
public class DefaultEquipment
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string ActivityType { get; set; } = "";     // "trail", "cycle", etc.
    public Guid EquipmentId { get; set; }

    public ApplicationUser User { get; set; } = null!;
    public Equipment Equipment { get; set; } = null!;
}
```

Index unique : `(UserId, ActivityType)` — un seul équipement par défaut par type.

Lors de l'upload d'une activité, si aucun équipement n'est spécifié, l'équipement par défaut du type d'activité est automatiquement assigné.

### 10.3 Mise à jour des cumuls

Lors de chaque association/dissociation activité-équipement :
- Recalcul des cumuls (`TotalDistanceKm`, `TotalElevationGainM`, `TotalActivities`, `TotalDurationSeconds`)
- Requête agrégée sur les activités liées à cet équipement

### 10.4 Endpoints

```
GET    /api/equipment                    → Liste équipements de l'utilisateur
GET    /api/equipment/{id}               → Détail équipement (+ activités récentes)
POST   /api/equipment                    → Créer un équipement
PUT    /api/equipment/{id}               → Modifier un équipement
DELETE /api/equipment/{id}               → Supprimer (dissocier les activités)
POST   /api/equipment/{id}/retire        → Retirer de l'usage (soft)
GET    /api/equipment/defaults           → Liste des équipements par défaut
PUT    /api/equipment/defaults           → Définir équipement par défaut par type d'activité
```

### 10.5 Alertes usure

Vérification côté frontend :
- Si `TotalDistanceKm >= WearAlertDistanceKm` → badge "Usure" orange
- Si `TotalDistanceKm >= WearAlertDistanceKm * 1.2` → badge "Remplacer" rouge
- Même logique pour le dénivelé si le seuil est défini

### 10.6 i18n

```
common.json:
  equipmentType.shoes, equipmentType.bike, equipmentType.watch,
  equipmentType.poles, equipmentType.other

equipment.json (nouveau namespace):
  title, addEquipment, editEquipment, retire, wearAlert,
  totalDistance, totalElevation, totalActivities, totalDuration,
  defaultFor, noEquipment, wearWarning, wearReplace
```

---

## 11. Stockage objet (S3/MinIO)

### 11.1 Architecture

Remplacement de `GpxStorageService` (filesystem) par un service abstrait supportant :
- **MinIO** en développement (conteneur Docker)
- **S3** en production (ou tout service compatible S3 : Scaleway, OVH, etc.)

### 11.2 Interface

```csharp
public interface IObjectStorageService
{
    Task<string> UploadAsync(string bucket, string key, Stream content, string contentType);
    Task<Stream> DownloadAsync(string bucket, string key);
    Task DeleteAsync(string bucket, string key);
    Task<bool> ExistsAsync(string bucket, string key);
    Task<string> GetPresignedUrlAsync(string bucket, string key, TimeSpan expiration);
}
```

### 11.3 Organisation des buckets/clés

```
Bucket : gpx-analyzer

Clés :
  gpx/{userId}/{activityId}.gpx              → GPX original
  gpx/{userId}/{activityId}.original.zip     → Archive original
  photos/{userId}/{activityId}/{photoId}.jpg  → Photos d'activité (futur)
  avatars/{userId}/profile.jpg                → Photo de profil
  exports/{userId}/{exportId}.zip             → Exports RGPD (temporaire)
```

### 11.4 Configuration

```json
{
  "Storage": {
    "Provider": "minio",           // "minio" | "s3"
    "Endpoint": "minio:9000",      // MinIO endpoint
    "AccessKey": "minioadmin",
    "SecretKey": "minioadmin",
    "Bucket": "gpx-analyzer",
    "UseSSL": false,               // true pour S3
    "Region": "eu-west-1"          // S3 uniquement
  }
}
```

### 11.5 Package NuGet

- `AWSSDK.S3` — client S3 compatible avec MinIO et AWS S3

### 11.6 Docker Compose (dev)

```yaml
services:
  minio:
    image: minio/minio:latest
    command: server /data --console-address ":9001"
    ports:
      - "9000:9000"    # API S3
      - "9001:9001"    # Console web MinIO
    environment:
      MINIO_ROOT_USER: minioadmin
      MINIO_ROOT_PASSWORD: minioadmin
    volumes:
      - minio-data:/data

volumes:
  minio-data:
```

---

## 12. Envoi d'emails

### 12.1 Interface

```csharp
public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody);
    Task SendTemplateAsync(string to, string templateName, Dictionary<string, string> variables);
}
```

### 12.2 Implémentations

**SmtpEmailService** (par défaut) :
- Utilise `System.Net.Mail.SmtpClient` (ou `MailKit` pour plus de fiabilité)
- Configuration via `appsettings.json`

**SendGridEmailService** (optionnel) :
- Package `SendGrid`
- Activé si `Email:Provider` = `"sendgrid"` et `Email:SendGridApiKey` configuré

### 12.3 Templates email

| Template | Usage | Variables |
|----------|-------|-----------|
| `confirm-email` | Confirmation inscription | `{name}`, `{link}` |
| `magic-link` | Connexion passwordless | `{name}`, `{link}`, `{expiration}` |
| `reset-password` | Réinitialisation mot de passe | `{name}`, `{link}` |
| `welcome` | Bienvenue (post-confirmation) | `{name}` |
| `wear-alert` | Alerte usure équipement | `{name}`, `{equipment}`, `{distance}`, `{threshold}` |

Templates HTML simples, inline CSS, responsive. Stockés comme ressources embarquées ou fichiers dans le projet.

### 12.4 Configuration

```json
{
  "Email": {
    "Provider": "smtp",                 // "smtp" | "sendgrid"
    "From": "noreply@gpx-analyzer.com",
    "FromName": "GPX Analyzer",
    "Smtp": {
      "Host": "smtp.example.com",
      "Port": 587,
      "Username": "...",
      "Password": "...",
      "EnableSsl": true
    },
    "SendGrid": {
      "ApiKey": "..."
    }
  }
}
```

---

## 13. RGPD & conformité

### 13.1 Registre des traitements

Document interne décrivant chaque traitement de données :

| Traitement | Finalité | Base légale | Données | Durée conservation |
|------------|----------|-------------|---------|-------------------|
| Compte utilisateur | Fourniture du service | Contrat | Email, nom, mot de passe hashé | Durée du compte |
| Profil athlète | Personnalisation analyse | Consentement | Poids, taille, sexe, date naissance | Durée du compte |
| Activités GPS | Analyse sportive | Contrat | Traces GPS, stats, biométrie | Durée du compte |
| Analyse IA | Rapport d'analyse | Consentement | Stats agrégées envoyées au provider | Pas de stockage externe |
| Météo | Enrichissement activité | Intérêt légitime | Coordonnées + date → API Open-Meteo | Durée de l'activité |
| Intégrations | Import données | Consentement | Tokens OAuth (chiffrés) | Jusqu'à déconnexion |
| Analytics | Amélioration service | Consentement | Données d'usage anonymisées | 13 mois |

### 13.2 Consentement granulaire

```csharp
public class UserConsent
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string ConsentType { get; set; } = "";   // Voir types ci-dessous
    public bool IsGranted { get; set; }
    public DateTime GrantedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? IpAddress { get; set; }

    public ApplicationUser User { get; set; } = null!;
}
```

**Types de consentement** :

| Type | Description | Requis ? |
|------|-------------|----------|
| `terms` | CGU / Conditions d'utilisation | Oui (contrat) |
| `privacy` | Politique de confidentialité | Oui (contrat) |
| `ai_analysis` | Envoi de données à un provider IA externe | Non |
| `weather` | Envoi de coordonnées à Open-Meteo | Non |
| `analytics` | Collecte de données d'usage anonymisées | Non |
| `marketing` | Communications marketing | Non |

### 13.3 Export des données (portabilité)

**Endpoint** : `POST /api/profile/export`

Génère un fichier ZIP contenant :
```
export/
  profile.json          → Données utilisateur + profil athlète
  activities/
    {id}.json           → Métadonnées de chaque activité
    {id}.gpx            → Fichier GPX original
  routes/
    {id}.json           → Métadonnées de chaque route
    {id}.gpx            → Export GPX de la route
  equipment.json        → Liste des équipements
  settings.json         → Préférences utilisateur
  consents.json         → Historique des consentements
```

Format standard, réimportable.

Processus asynchrone (background job) car potentiellement volumineux. Notification par email quand prêt, lien de téléchargement temporaire (URL pré-signée, 24h).

### 13.4 Suppression de compte

**Endpoint** : `DELETE /api/profile`

1. Demande de confirmation (re-saisie mot de passe ou code email)
2. Suppression en cascade :
   - Toutes les activités + fichiers GPX (object storage)
   - Toutes les routes
   - Tous les équipements
   - Tous les settings
   - Toutes les intégrations (+ révocation tokens OAuth si possible)
   - Tous les consentements
   - Tous les refresh tokens
   - Le profil athlète
   - La photo de profil (object storage)
   - Le compte utilisateur
3. Journal d'audit : entrée "account_deleted" (anonymisée, conserve uniquement l'horodatage)

### 13.5 Journal d'audit (RGPD)

```csharp
public class AuditLog
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }               // null si anonymisé (post-suppression)
    public string Action { get; set; } = "";         // "login", "data_export", "account_deleted", etc.
    public string? Details { get; set; }              // JSON libre
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

Actions auditées :
- `login`, `logout`, `failed_login`
- `password_changed`, `password_reset`
- `profile_updated`, `profile_photo_updated`
- `data_export_requested`, `data_export_downloaded`
- `account_deleted`
- `consent_granted`, `consent_revoked`
- `integration_connected`, `integration_disconnected`

### 13.6 DPO Contact

Page `/privacy` dans le frontend avec :
- Politique de confidentialité
- Contact DPO (email configurable via `appsettings.json`)
- Lien vers l'export de données et la suppression de compte (dans les settings)

### 13.7 Endpoints RGPD

```
POST   /api/profile/export           → Demander un export de données
GET    /api/profile/export/{id}      → Télécharger l'export (URL pré-signée)
DELETE /api/profile                   → Supprimer le compte
GET    /api/profile/consents         → Liste des consentements
PUT    /api/profile/consents         → Mettre à jour les consentements
GET    /api/profile/audit-log        → Journal d'accès (propres données)
```

---

## 14. Backend d'administration

### 14.1 Projet `ui/admin-api/`

ASP.NET Core minimal, même DbContext que `ui/api/`, accès restreint au rôle `Admin`.

### 14.2 Endpoints admin

```
# Utilisateurs
GET    /api/admin/users                    → Liste paginée (recherche, filtre rôle/statut)
GET    /api/admin/users/{id}               → Détail utilisateur
PATCH  /api/admin/users/{id}               → Modifier (rôle, suspension)
DELETE /api/admin/users/{id}               → Supprimer un compte (cascade RGPD)

# Statistiques plateforme
GET    /api/admin/stats                    → Dashboard admin (nb users, activités, stockage, etc.)

# Configuration
GET    /api/admin/config                   → Configuration globale (providers IA, limites)
PUT    /api/admin/config                   → Modifier la configuration

# Audit
GET    /api/admin/audit-logs               → Journal d'audit global (paginé, filtrable)

# Stockage
GET    /api/admin/storage/stats            → Utilisation stockage par user
```

### 14.3 Frontend admin (`ui/admin-client/`)

Application React séparée (même stack : Vite + TailwindCSS) :

**Pages** :
- `/admin` — Dashboard (stats plateforme)
- `/admin/users` — Liste utilisateurs
- `/admin/users/:id` — Détail utilisateur
- `/admin/config` — Configuration système
- `/admin/audit` — Journal d'audit

---

## 15. Impact frontend

### 15.1 Nouvelles pages

| Route | Page | Description |
|-------|------|-------------|
| `/auth/login` | LoginPage | Email/password + boutons OAuth + magic link |
| `/auth/register` | RegisterPage | Inscription email/password |
| `/auth/forgot-password` | ForgotPasswordPage | Formulaire email reset |
| `/auth/reset-password` | ResetPasswordPage | Formulaire nouveau mot de passe |
| `/auth/verify` | VerifyPage | Vérification magic link / email |
| `/privacy` | PrivacyPage | Politique de confidentialité |
| `/equipment` | EquipmentPage | Liste + gestion équipements |
| `/equipment/:id` | EquipmentDetailPage | Détail équipement + activités |

### 15.2 Pages modifiées

| Page | Modifications |
|------|--------------|
| **Settings** | Profil athlète enrichi (poids, taille, sexe, date naissance, photo). Préférences analyse → UserSettings. Consentements RGPD. Bouton export données. Bouton suppression compte. Section équipement par défaut par type. |
| **Activity Detail** | Description éditable. RPE (sélecteur 1-10). Tags (input chips). Type de session (dropdown). Bandeau météo. Calories. Équipement associé. |
| **Activity List** | Badge météo (icône). Badge session type. Filtre par tag. |
| **Upload** | Association équipement (auto via défaut ou sélection manuelle). |
| **Dashboard** | Calories du mois. |
| **Sidebar** | Nom utilisateur + avatar. Lien équipement. Bouton déconnexion. |

### 15.3 Auth context (frontend)

```typescript
interface AuthContext {
  user: User | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (email: string, password: string) => Promise<void>;
  loginWithGoogle: () => void;
  loginWithStrava: () => void;
  requestMagicLink: (email: string) => Promise<void>;
  register: (email: string, password: string, displayName: string) => Promise<void>;
  logout: () => Promise<void>;
  refreshToken: () => Promise<void>;
}
```

JWT stocké en mémoire (pas localStorage pour la sécurité). Refresh token en cookie HttpOnly.

### 15.4 Route protection

```tsx
<Route element={<ProtectedRoute />}>
  <Route path="/" element={<Dashboard />} />
  <Route path="/activities" element={<Activities />} />
  {/* ... toutes les routes protégées */}
</Route>
<Route path="/auth/*" element={<AuthLayout />}>
  <Route path="login" element={<LoginPage />} />
  <Route path="register" element={<RegisterPage />} />
  {/* ... routes publiques */}
</Route>
```

### 15.5 i18n — Nouveaux namespaces

| Namespace | Contenu |
|-----------|---------|
| `auth` | Login, register, forgot-password, magic-link, verify, errors |
| `equipment` | Equipment management, types, wear alerts |
| `privacy` | Privacy policy, RGPD, consents, data export |
| `profile` | Athlete profile, photo, bio, preferences |

---

## 16. Migrations EF Core

### Migration unique pour la Phase 1

Nom suggéré : `AddMultiUserAndFoundations`

**Tables créées** :
- `AspNetUsers` (ASP.NET Identity)
- `AspNetRoles` (ASP.NET Identity)
- `AspNetUserRoles` (ASP.NET Identity)
- `AspNetUserClaims` (ASP.NET Identity)
- `AspNetUserLogins` (ASP.NET Identity — OAuth)
- `AspNetUserTokens` (ASP.NET Identity)
- `AspNetRoleClaims` (ASP.NET Identity)
- `AthleteProfiles`
- `Equipment`
- `DefaultEquipment`
- `UserSettings`
- `RefreshTokens`
- `UserConsents`
- `AuditLogs`

**Tables modifiées** :
- `Activities` : +`UserId` FK, +`Description`, +`PerceivedExertion`, +`Tags`, +`SessionType`, +`EstimatedCalories`, +`CalorieMethod`, +`WeatherJson`, +`EquipmentId` FK
- `Routes` : +`UserId` FK
- `Integrations` : +`UserId` FK, index unique `(UserId, Provider)` remplace `(Provider)`

**Tables supprimées** :
- `Settings` (remplacée par `UserSettings` + `AthleteProfile` + `appsettings.json`)

**Données purgées** :
- Toutes les lignes des tables `Activities`, `Routes`, `Integrations`, `Settings`

### Seeding des rôles

```csharp
// Dans la migration ou dans Program.cs au démarrage
roleManager.CreateAsync(new IdentityRole<Guid> { Name = "Admin" });
roleManager.CreateAsync(new IdentityRole<Guid> { Name = "Premium" });
roleManager.CreateAsync(new IdentityRole<Guid> { Name = "User" });
```

---

## 17. Configuration Docker

### 17.1 docker-compose.yml (dev)

Ajouts :
- Service `minio` (voir §11.6)
- Variables d'environnement `Jwt:Secret`, `Email:*`, `Storage:*` sur le service `api`
- Service `admin-api` (nouveau)
- Service `admin-client` (nouveau)

### 17.2 docker-compose.prod.yml

Ajouts :
- Configuration S3 au lieu de MinIO (ou MinIO persistant)
- Variables d'environnement pour SMTP/SendGrid
- JWT secret via Docker secrets ou env var
- Service `admin-api` et `admin-client`

### 17.3 Ports

| Service | Port dev | Port prod |
|---------|----------|-----------|
| api | 5000 | 5000 |
| admin-api | 5001 | 5001 |
| client | 5173 (vite) / 8080 (nginx) | 80 |
| admin-client | 5174 (vite) / 8081 (nginx) | 8081 |
| minio API | 9000 | 9000 |
| minio console | 9001 | — |
| PostgreSQL | — | 5432 |

---

## 18. Plan de tests

### 18.1 Tests unitaires (.NET)

| Composant | Tests |
|-----------|-------|
| `CalorieCalculator` | Formule FC (homme/femme), MET par type, ajustement dénivelé, fallback sans FC, fallback sans poids |
| `WeatherService` | Parsing réponse Open-Meteo, mapping codes WMO, gestion erreur API |
| Auth services | Génération JWT, validation refresh token, rotation, expiration |
| Equipment cumuls | Calcul distance/dénivelé cumulés, alertes usure |

### 18.2 Tests d'intégration (.NET)

| Scénario | Vérifie |
|----------|---------|
| Register + login + access protected route | Flow auth complet |
| OAuth callback + auto-create user | Création de compte via Google/Strava |
| Upload activity + check UserId isolation | Isolation multi-user |
| RGPD export | Contenu du ZIP |
| RGPD delete | Suppression cascade complète |

### 18.3 Tests E2E (Playwright)

| Scénario | Vérifie |
|----------|---------|
| Register flow | Inscription, confirmation, redirection dashboard |
| Login/Logout | Connexion, sidebar user, déconnexion |
| Activity detail enrichi | Description, RPE, tags, météo, calories, équipement |
| Equipment CRUD | Création, modification, association activité, alerte usure |
| Settings profil | Profil athlète complet, photo, unités |
| Privacy / RGPD | Consentements, export, suppression |

### 18.4 Mock API (E2E)

Mise à jour de `e2e/helpers/mock-api.ts` et `e2e/fixtures/` pour inclure :
- Endpoints auth (register, login, me)
- Données activité enrichies (météo, calories, description, tags)
- Endpoints équipement
- Endpoints profil

---

## 19. Ordre d'implémentation

Découpage en sous-phases ordonnées par dépendances :

### Étape 1 — Socle auth + multi-user
> Tout le reste en dépend

1. Entités : `ApplicationUser`, `RefreshToken`, `AuditLog`
2. ASP.NET Identity setup dans `Program.cs`
3. JWT configuration + middleware
4. `AuthController` : register, login, refresh, logout, me
5. Migration EF Core (reset données + ajout UserId + tables Identity)
6. Filtrage par `UserId` dans tous les contrôleurs existants
7. Frontend : AuthContext, LoginPage, RegisterPage, ProtectedRoute, Sidebar user
8. Seeding admin + rôles

### Étape 2 — Profil athlète + settings migrées
> Dépend de l'étape 1 (UserId)

9. Entité `AthleteProfile` (1-to-1 User)
10. Entité `UserSetting` (remplace `Setting`)
11. Migration settings → UserSettings + AthleteProfile
12. Nouveaux endpoints `/api/profile`
13. Frontend : Settings profil enrichi (poids, taille, sexe, date naissance)
14. Suppression de l'ancienne table `Settings` et du `SettingsService` global

### Étape 3 — Stockage objet
> Dépend de l'étape 1 (UserId pour les chemins)

15. `IObjectStorageService` + implémentation MinIO/S3
16. Migration `GpxStorageService` → object storage
17. Docker Compose : ajout service MinIO
18. Upload photo de profil

### Étape 4 — Email
> Dépend de l'étape 1 (nécessaire pour confirm email, magic link)

19. `IEmailService` + implémentations SMTP / SendGrid
20. Templates email (confirm, magic link, reset password, welcome)
21. Intégration dans auth flows (confirm email, magic link, reset password)

### Étape 5 — OAuth social
> Dépend des étapes 1 + 4

22. Google OAuth setup + callback
23. Strava OAuth login (distinct de l'intégration import)
24. Frontend : boutons OAuth sur LoginPage

### Étape 6 — Enrichissement activité
> Dépend de l'étape 2 (profil pour calories)

25. Nouveaux champs Activity (description, RPE, tags, sessionType)
26. `CalorieCalculator` dans Core + intégration pipeline
27. Frontend : édition description/RPE/tags/session sur activity detail
28. Migration EF Core pour les nouveaux champs

### Étape 7 — Météo
> Indépendant (juste besoin des coordonnées GPS)

29. `WeatherService` + appel Open-Meteo
30. Intégration dans `ActivityProcessingService`
31. Frontend : bandeau météo sur activity detail

### Étape 8 — Équipement
> Dépend de l'étape 1 (UserId)

32. Entités `Equipment` + `DefaultEquipment`
33. Endpoints CRUD équipement
34. Association activité-équipement + auto-assign
35. Calcul cumuls + alertes usure
36. Frontend : page équipement, sélecteur sur upload/activity detail

### Étape 9 — RGPD
> Dépend des étapes 1-8 (doit tout couvrir)

37. Entités `UserConsent`
38. Export données (ZIP asynchrone)
39. Suppression de compte (cascade)
40. Frontend : consentements, export, suppression dans Settings
41. Page privacy policy

### Étape 10 — Backend admin
> Dépend des étapes 1-9

42. Projet `ui/admin-api/` (endpoints admin)
43. Projet `ui/admin-client/` (frontend admin)
44. Dashboard admin, gestion users, audit logs, config système
