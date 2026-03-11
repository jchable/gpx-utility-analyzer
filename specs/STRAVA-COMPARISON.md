# Comparaison exhaustive : GPX Utility Analyzer vs Strava

> Document de spécification — Mars 2026
> Objectif : identifier les gaps fonctionnels et prioriser les évolutions

---

## 1. Vue d'ensemble

| Aspect | GPX Utility Analyzer | Strava |
|--------|---------------------|--------|
| **Type** | Self-hosted / Docker | SaaS cloud |
| **Modèle** | Open source, mono-utilisateur | Freemium (gratuit + abo ~12€/mois) |
| **Plateforme** | Web (PWA) | Web + iOS + Android |
| **Données** | Privées, stockées localement | Cloud Strava |
| **Sports** | 7 types (run, trail, hike, cycle, walk, swim, other) | 30+ types (dont sports d'hiver, nautiques, indoor) |

---

## 2. Comparatif détaillé par catégorie

### 2.1 Enregistrement & import d'activités

| Fonctionnalité | Notre app | Strava | Gap |
|---|---|---|---|
| Upload GPX manuel | **Oui** (multi-fichier, drag & drop) | Oui | = |
| Upload FIT/TCX | Non (GPX uniquement) | Oui (GPX, FIT, TCX) | **Gap** |
| Enregistrement GPS natif (mobile) | Non | **Oui** (app mobile) | **Gap majeur** |
| Import Strava (webhook) | **Oui** (OAuth + webhook auto) | N/A | = |
| Import Garmin Connect | **Oui** (OAuth + FIT→GPX) | Oui (sync auto) | = |
| Import COROS/Suunto/Polar/Komoot | UI uniquement (non implémenté) | Oui (sync auto pour la plupart) | **Gap** |
| Import depuis Decathlon | Non | Non | = |
| Multi-fichier upload simultané | **Oui** (queue avec statuts) | Oui | = |
| Détection type d'activité auto | Non (sélection manuelle) | **Oui** (auto-détection) | **Gap mineur** |
| Edition post-upload (nom, type) | **Oui** (+ re-analyse auto) | Oui | = |

### 2.2 Analyse d'activité — Statistiques de base

| Fonctionnalité | Notre app | Strava | Gap |
|---|---|---|---|
| Distance 2D / 3D | **Oui** (les deux) | Distance 2D uniquement | **Notre force** |
| Dénivelé positif / négatif | **Oui** (3 algorithmes : threshold, Douglas-Peucker, segments) | Oui (1 algorithme) | **Notre force** |
| Altitude max / min | **Oui** | Oui | = |
| Temps total / en mouvement / arrêté | **Oui** | Oui | = |
| Vitesse moyenne / en mouvement | **Oui** | Oui | = |
| Vitesse max | **Oui** | Oui | = |
| Allure moyenne / en mouvement | **Oui** | Oui | = |
| Nombre d'arrêts + détails | **Oui** (table avec coords, durée, fly-to carte) | Partiel (temps arrêté) | **Notre force** |
| Points GPS par km | **Oui** | Non | **Notre force** |
| Correction DEM (SRTM) | **Oui** (correction altitude par données satellite) | Non (correction basique) | **Notre force** |
| Lissage élévation configurable | **Oui** (none/light/medium/heavy) | Non configurable | **Notre force** |
| Lissage trace GPS configurable | **Oui** (none/light/medium/heavy) | Non | **Notre force** |
| Filtrage outliers GPS | **Oui** (seuils par preset) | Basique | **Notre force** |
| Détection anomalies GPS | **Oui** (score qualité 0-100, catégorisation) | Non | **Notre force** |
| Calories | Non | **Oui** (estimation) | **Gap** |

### 2.3 Analyse d'activité — Biométrie & zones

| Fonctionnalité | Notre app | Strava | Gap |
|---|---|---|---|
| FC moyenne / max / min | **Oui** | Oui | = |
| Zones FC (5 zones) | **Oui** (durée + %, TRIMP) | Oui (5 zones, Relative Effort) | ≈ |
| TRIMP | **Oui** | Non (Relative Effort à la place) | **Notre force** |
| Relative Effort | Non | **Oui** (métrique propriétaire Strava) | **Gap** |
| Puissance moyenne / max / NP | **Oui** | Oui | = |
| IF, TSS, VI | **Oui** | Partiel | **Notre force** |
| Zones de puissance (7 zones) | **Oui** | Oui | = |
| Courbe de puissance (Power Curve) | Non | **Oui** (best efforts historiques) | **Gap** |
| Cadence (pas/min, rpm) | **Oui** (adapté au type d'activité) | Oui | = |
| Température | **Oui** (avg/min/max) | Partiel | **Notre force** |
| GAP (Grade Adjusted Pace) | **Oui** (Minetti) | **Oui** | = |

### 2.4 Analyse d'activité — Effort & terrain

| Fonctionnalité | Notre app | Strava | Gap |
|---|---|---|---|
| KE (Kilomètre-Effort) | **Oui** | Non | **Notre force** |
| Points ITRA + catégorie | **Oui** | Non | **Notre force** |
| Distance équivalente plate (Minetti) | **Oui** | Non | **Notre force** |
| Difficulté terrain (score + grade) | **Oui** | Non | **Notre force** |
| Pente moyenne / max / variance | **Oui** | Partiel | **Notre force** |
| Ratio sections raides | **Oui** | Non | **Notre force** |
| Estimations temps (Naismith/Tobler/Munter) | **Oui** | Non | **Notre force** |
| Ratio performance vs modèles | **Oui** | Non | **Notre force** |
| Effort Comparison section | **Oui** (tableau multi-modèles) | Non | **Notre force** |

### 2.5 Profil d'élévation & graphiques

| Fonctionnalité | Notre app | Strava | Gap |
|---|---|---|---|
| Profil d'élévation | **Oui** (Recharts, 500 points) | Oui | = |
| Axe X : distance ou temps | **Oui** (toggle) | Distance uniquement | **Notre force** |
| Overlay vitesse | **Oui** (vitesse réelle + GAP + Tobler théorique) | Oui | **Notre force** |
| Overlay FC | **Oui** | Oui | = |
| Overlay puissance | **Oui** | Oui | = |
| Bandes d'arrêt sur graphique | **Oui** | Non | **Notre force** |
| Sélection interactive (zoom sur section) | Non | **Oui** (click & drag → stats mises à jour) | **Gap** |
| Graphique allure par zone | Non | **Oui** (Pace Zone Analysis) | **Gap** |

### 2.6 Splits & Best Efforts

| Fonctionnalité | Notre app | Strava | Gap |
|---|---|---|---|
| Splits kilométriques | **Oui** (allure, D+/D-, FC, puissance) | Oui | = |
| Visualisation bar chart splits | **Oui** (fastest/slowest colorés) | Oui | = |
| Best Efforts (distances fixes) | **Oui** (400m → marathon) | **Oui** (mêmes distances) | = |
| Best Efforts historiques (PR) | Non | **Oui** (records personnels sur toutes activités) | **Gap** |
| Laps (du device GPS) | Non | **Oui** (laps automatiques/manuels) | **Gap** |
| Workout Analysis (structuré) | Non | **Oui** (analyse des intervalles) | **Gap** |

### 2.7 Carte & visualisation

| Fonctionnalité | Notre app | Strava | Gap |
|---|---|---|---|
| Carte de la trace | **Oui** (MapLibre GL JS) | Oui (Mapbox) | = |
| Vue 3D terrain | **Oui** (MapTiler + terrain exaggeration) | Oui (3D Flyover pour abonnés) | = |
| Vue satellite | **Oui** (MapTiler Hybrid) | Oui | = |
| Vue OpenTopo | **Oui** | Non (style Strava uniquement) | **Notre force** |
| Marqueurs départ/arrivée | **Oui** (vert + drapeau damier) | Oui | = |
| Fly-to sur les arrêts | **Oui** (clic table → carte) | Non | **Notre force** |
| Segments sur la carte | Non | **Oui** (affichage segments traversés) | **Gap** |
| Photos géolocalisées sur carte | Non | **Oui** | **Gap** |
| Personal Heatmap | Non | **Oui** (carte de chaleur personnelle) | **Gap** |
| Global Heatmap | Non | **Oui** (carte communautaire) | **Gap** |
| Flyby (animation 3D) | Non | **Oui** (animation du parcours) | **Gap** |

### 2.8 Rapport IA

| Fonctionnalité | Notre app | Strava | Gap |
|---|---|---|---|
| Analyse IA structurée | **Oui** (difficulté, segments clés, recommandations, effort, résumé) | Non (Athlete Intelligence = résumé simple) | **Notre force** |
| Multi-provider IA | **Oui** (6 providers : OpenAI, Anthropic, Mistral, Ollama, Gemini, Azure) | Non (propriétaire) | **Notre force** |
| Score de difficulté | **Oui** (easy/moderate/hard/expert, score /10) | Non | **Notre force** |
| Segments clés identifiés | **Oui** (type + description + distance/dénivelé) | Non | **Notre force** |
| Recommandations | **Oui** (checklist) | Non | **Notre force** |
| Chat IA sur activité | Non (prévu dans TODO) | Non | = |
| Athlete Intelligence | Non | **Oui** (résumés IA personnalisés, tendances) | **Gap** |

### 2.9 Éditeur de routes

| Fonctionnalité | Notre app | Strava | Gap |
|---|---|---|---|
| Création de route sur carte | **Oui** (waypoints, freehand, routing) | Oui (Route Builder) | = |
| Import GPX comme route | **Oui** | Oui | = |
| Conversion activité → route | **Oui** ("Edit as Route") | Non | **Notre force** |
| Modes de dessin multiples | **Oui** (select, add, freehand, split, crop, POI) | Point par point uniquement | **Notre force** |
| Profils de routage | **Oui** (manual, hiking, trail, cycling, road) | Oui (run, ride) | **Notre force** |
| POIs (points d'intérêt) | **Oui** (9 types : eau, parking, refuge, sommet, etc.) | Non | **Notre force** |
| Undo/Redo | **Oui** (50 niveaux) | Partiel | **Notre force** |
| Split de route | **Oui** | Non | **Notre force** |
| Crop de route | **Oui** | Non | **Notre force** |
| Inversion de route | **Oui** | Oui | = |
| Export multi-format | **Oui** (GPX, GeoJSON, KML) | GPX uniquement | **Notre force** |
| Enrichissement DEM | **Oui** (SRTM on-demand) | Non | **Notre force** |
| Métadonnées riches | **Oui** (catégorie, tags, description) | Basique (nom) | **Notre force** |
| Auto-save | **Oui** | Non | **Notre force** |
| Route suggestions IA | Non | **Oui** (routes basées sur heatmap) | **Gap** |
| Recherche/filtrage routes | Basique (type, pagination) | **Oui** (keyword, distance, dénivelé, surface) | **Gap** |
| Génération route circulaire | Non | **Oui** (distance + difficulté) | **Gap** |

### 2.10 Route Predictor

| Fonctionnalité | Notre app | Strava | Gap |
|---|---|---|---|
| Analyse éphémère (sans stockage) | **Oui** | Non | **Notre force** |
| Estimation effort multi-modèles | **Oui** (Naismith/Tobler/Munter/KE/ITRA/EFD) | Non | **Notre force** |
| Détail terrain (pente, ratio raide) | **Oui** | Non | **Notre force** |

### 2.11 Dashboard

| Fonctionnalité | Notre app | Strava | Gap |
|---|---|---|---|
| Stats résumées (total, mois) | **Oui** (4 cartes) | Oui | = |
| Répartition par type d'activité | **Oui** (donut chart CSS) | Oui | = |
| Activités récentes | **Oui** | Oui (feed social) | ≈ |
| Feed social (activités followers) | Non | **Oui** | **Gap** |
| Weekly Summary | Non | **Oui** (résumé hebdomadaire) | **Gap** |
| Graphiques de progression | Non | **Oui** (distance/dénivelé/temps par semaine/mois) | **Gap** |
| Training Log (vue calendrier) | Non | **Oui** | **Gap** |

### 2.12 Suivi d'entraînement

| Fonctionnalité | Notre app | Strava | Gap |
|---|---|---|---|
| Fitness & Freshness | Non | **Oui** (courbe fitness/fatigue/forme) | **Gap majeur** |
| Relative Effort | Non | **Oui** (charge cardiovasculaire) | **Gap** |
| Training Load | Non | **Oui** | **Gap** |
| Goals (objectifs) | Non | **Oui** (hebdo/mensuel/annuel, par sport) | **Gap** |
| Race Predictions | Non | **Oui** (5K/10K/semi/marathon, ML) | **Gap** |
| Matched Activities | Non | **Oui** (détection routes récurrentes, tendance) | **Gap** |
| Power Curve historique | Non | **Oui** | **Gap** |
| Best Efforts historiques | Non | **Oui** (PRs cross-activités) | **Gap** |
| Training Plans | Non | **Oui** (plans structurés course/vélo) | **Gap** |

### 2.13 Social & communauté

| Fonctionnalité | Notre app | Strava | Gap |
|---|---|---|---|
| Profils utilisateurs | Non | **Oui** | **Gap** |
| Feed d'activités | Non | **Oui** | **Gap** |
| Kudos (likes) | Non | **Oui** | **Gap** |
| Commentaires | Non | **Oui** | **Gap** |
| Followers / following | Non | **Oui** | **Gap** |
| Partage d'activité (lien public) | Non | **Oui** | **Gap** |
| Segments communautaires | Non | **Oui** (création, leaderboards) | **Gap** |
| KOM/QOM | Non | **Oui** | **Gap** |
| Local Legend | Non | **Oui** | **Gap** |
| Clubs | Non | **Oui** | **Gap** |
| Challenges | Non | **Oui** | **Gap** |
| Group activities | Non | **Oui** (activités conjointes) | **Gap** |
| Flyby (replay animé) | Non | **Oui** | **Gap** |

### 2.14 Photos & médias

| Fonctionnalité | Notre app | Strava | Gap |
|---|---|---|---|
| Upload photos sur activité | Non | **Oui** (multi-photos) | **Gap** |
| Géolocalisation photos sur carte | Non | **Oui** | **Gap** |
| Photos dans le feed | Non | **Oui** | **Gap** |

### 2.15 Données enrichies

| Fonctionnalité | Notre app | Strava | Gap |
|---|---|---|---|
| Calories estimées | Non | **Oui** | **Gap** |
| Météo de l'activité | Non | **Oui** (température, vent, humidité) | **Gap** |
| Gestion équipement (chaussures, vélos) | Non | **Oui** (usure, distance cumulée) | **Gap** |
| Description d'activité | Non (nom uniquement) | **Oui** (texte libre) | **Gap** |
| Tags / labels | Non (routes uniquement) | Partiel (commute, indoor) | **Gap mineur** |
| Perceived Exertion (RPE) | Non | **Oui** (effort ressenti 1-10) | **Gap** |

### 2.16 Sécurité & privacy

| Fonctionnalité | Notre app | Strava | Gap |
|---|---|---|---|
| Données self-hosted | **Oui** (100% local) | Non (cloud) | **Notre force** |
| Beacon (live tracking) | Non | **Oui** (partage position temps réel) | **Gap** |
| Privacy zones | Non (pas nécessaire, self-hosted) | **Oui** | N/A |
| Contrôles de visibilité | Non (mono-user) | **Oui** (everyone/followers/only me) | Gap futur |

### 2.17 Multi-utilisateur & plateforme

| Fonctionnalité | Notre app | Strava | Gap |
|---|---|---|---|
| Multi-utilisateur | Non | **Oui** | **Gap majeur** |
| Authentification | Non | **Oui** (email, Google, Apple, Facebook) | **Gap** |
| RGPD / export données | Non | **Oui** | **Gap** |
| App mobile native | Non (PWA uniquement) | **Oui** (iOS + Android) | **Gap** |
| API publique | Non | **Oui** (API REST complète) | **Gap** |
| Webhooks pour tiers | Non | **Oui** | **Gap** |
| Administration | Non | **Oui** (back-office) | **Gap** |

### 2.18 Intégrations

| Fonctionnalité | Notre app | Strava | Gap |
|---|---|---|---|
| Strava (import) | **Oui** (OAuth + webhook) | N/A | = |
| Garmin Connect | **Oui** (OAuth + FIT→GPX) | Oui (sync auto) | = |
| COROS | UI seulement | Oui | **Gap** |
| Suunto | UI seulement | Oui | **Gap** |
| Polar | UI seulement | Oui | **Gap** |
| Komoot | UI seulement | Oui | **Gap** |
| Apple Health | Non | Oui | **Gap** |
| Google Fit | Non | Oui | **Gap** |
| Wahoo | Non | Oui | **Gap** |

### 2.19 PWA & offline

| Fonctionnalité | Notre app | Strava | Gap |
|---|---|---|---|
| PWA installable | **Oui** | Non (app native) | **Notre force** |
| Cache offline (activités) | **Oui** (Workbox, StaleWhileRevalidate/CacheFirst) | Via app native | = |
| Offline banner | **Oui** | N/A | = |
| Service Worker auto-update | **Oui** | N/A | = |

### 2.20 Internationalisation

| Fonctionnalité | Notre app | Strava | Gap |
|---|---|---|---|
| Multi-langue | **Oui** (EN/FR) | **Oui** (20+ langues) | Gap quantitatif |
| IA localisée | **Oui** (rapports dans la langue) | N/A | **Notre force** |
| Unités métriques/impériales | Non (métrique uniquement) | **Oui** (choix utilisateur) | **Gap mineur** |

---

## 3. Nos forces — Ce que Strava ne fait pas

### 3.1 Analyse de terrain & effort (exclusif)
- **KE (Kilomètre-Effort)** — métrique trail running
- **Points ITRA + catégorie** — classification officielle trail
- **Estimations multi-modèles** : Naismith, Tobler, Munter avec ratios de performance
- **Distance équivalente plate** (Minetti GAP)
- **Score de difficulté terrain** avec grade, pente max, ratio sections raides
- **Section Effort Comparison** — tableau comparatif complet

### 3.2 Qualité GPS (exclusif)
- **Score de qualité GPS** (0-100) avec catégorisation anomalies
- **Détection anomalies** par sévérité (critical/warning/info)
- **Correction DEM SRTM** des altitudes
- **Filtrage outliers** configurable par preset

### 3.3 Analyse configurable
- **3 algorithmes d'élévation** (threshold, Douglas-Peucker, segments)
- **Lissage configurable** (élévation + trace GPS, 4 niveaux chacun)
- **Presets par sport** (hiking, trail, cycling, running, walking, swimming)

### 3.4 Éditeur de routes avancé
- **6 modes de dessin** (select, add, freehand, split, crop, POI)
- **5 profils de routage** (manual, hiking, trail, cycling, road)
- **9 types de POI** (eau, parking, refuge, sommet, point de vue, danger, nourriture, camping, custom)
- **Split/Crop** de route
- **Export multi-format** (GPX, GeoJSON, KML)
- **Enrichissement DEM** à la demande
- **Conversion activité → route**

### 3.5 IA avancée
- **6 providers IA** au choix (OpenAI, Anthropic, Mistral, Ollama, Gemini, Azure)
- **Rapport structuré** : difficulté scorée, segments clés, recommandations, effort
- **Self-hosted** : données ne quittent pas votre infra (avec Ollama)

### 3.6 Route Predictor
- Analyse éphémère sans stockage (preview avant sortie)
- Estimation effort multi-modèles sur route planifiée

### 3.7 Données privées & self-hosted
- 100% contrôle des données
- Pas de dépendance cloud
- Docker Compose pour déploiement simple

---

## 4. Gaps identifiés — Priorisation

### Priorité 1 — Données enrichies ⭐⭐⭐

| Fonctionnalité | Complexité | Impact utilisateur | Dépendances |
|---|---|---|---|
| **Calories** | Moyenne | Fort | Formule basée sur FC/poids/dénivelé, nécessite profil athlète |
| **Météo** | Moyenne | Fort | API externe (OpenMeteo gratuit), lat/lon + date |
| **Photos** | Moyenne-Haute | Fort | Upload, stockage, géolocalisation, affichage carte |
| **Équipement** (chaussures/vélos) | Moyenne | Moyen | Entité Equipment, association activité, suivi usure |
| **Description d'activité** | Faible | Moyen | Champ texte sur Activity, édition inline |
| **RPE** (effort ressenti) | Faible | Moyen | Champ 1-10 sur Activity |
| **Types d'activité étendus** | Faible-Moyenne | Moyen | Ajout types : trail running variants, VTT, gravel, ski, etc. |
| **Support FIT/TCX** | Haute | Moyen | Parsing de formats supplémentaires |
| **Unités impériales** | Faible | Faible | Conversion côté client, préférence user |

### Priorité 2 — Multi-utilisateur & SaaS ⭐⭐⭐

| Fonctionnalité | Complexité | Impact utilisateur | Dépendances |
|---|---|---|---|
| **Authentification** | Haute | Critique | JWT/cookies, registration, login, password reset |
| **Multi-utilisateur** | Haute | Critique | UserId sur toutes entités, isolation données |
| **Profil utilisateur** | Moyenne | Fort | Entité User (poids, taille, âge, FC max, FTP, photo) |
| **RGPD** | Moyenne | Critique (légal) | Export données, suppression compte, consentement |
| **Administration** | Moyenne | Moyen | Back-office utilisateurs, stats, configuration |
| **Paiement** | Haute | Moyen | Stripe/autre, plans, gestion abonnements |

### Priorité 3 — Suivi d'entraînement ⭐⭐

| Fonctionnalité | Complexité | Impact utilisateur | Dépendances |
|---|---|---|---|
| **Training Log / Calendrier** | Moyenne | Fort | Vue calendrier, volume par jour/semaine |
| **Goals** (objectifs) | Moyenne | Fort | Hebdo/mensuel/annuel par sport, suivi progression |
| **Fitness & Freshness** | Haute | Fort | Modèle Banister (CTL/ATL/TSB), nécessite FC ou puissance |
| **Best Efforts historiques (PRs)** | Moyenne | Fort | Cross-activités, distances fixes, notifications PR |
| **Matched Activities** | Haute | Moyen | Détection routes similaires, comparaison tendance |
| **Power Curve historique** | Moyenne | Moyen | Best power par durée, cross-activités |
| **Race Predictions** | Haute | Moyen | ML ou formule (Riegel, Vickers), historique nécessaire |
| **Graphiques progression** | Moyenne | Fort | Distance/dénivelé/temps par semaine/mois, vue tendance |
| **Weekly Summary** | Faible | Moyen | Résumé auto hebdomadaire |

### Priorité 4 — Social & communauté (phase ultérieure) ⭐

| Fonctionnalité | Complexité | Impact utilisateur | Dépendances |
|---|---|---|---|
| **Feed d'activités** | Haute | Fort | Multi-user requis, followers |
| **Kudos / Commentaires** | Moyenne | Fort | Multi-user requis |
| **Segments** | Très haute | Fort | Création, matching, leaderboards |
| **Clubs** | Haute | Moyen | Multi-user requis, groupes |
| **Challenges** | Haute | Moyen | Multi-user requis, règles, classements |
| **Partage public** | Moyenne | Moyen | Liens publics, privacy controls |

### Priorité 5 — Améliorations carte & graphiques ⭐

| Fonctionnalité | Complexité | Impact utilisateur | Dépendances |
|---|---|---|---|
| **Sélection interactive graphique** | Moyenne | Fort | Click & drag → stats section |
| **Personal Heatmap** | Haute | Moyen | Agrégation toutes traces, rendu carte chaleur |
| **Photos géolocalisées sur carte** | Moyenne | Moyen | Dépend de Photos (P1) |
| **Flyby / animation 3D** | Haute | Faible | Animation parcours |

---

## 5. Récapitulatif chiffré

| Catégorie | Fonctionnalités communes | Nos forces exclusives | Gaps vs Strava |
|---|---|---|---|
| Analyse de base | 12 | 8 | 1 (calories) |
| Biométrie & zones | 6 | 4 | 2 |
| Effort & terrain | 0 | 10 | 0 |
| Graphiques | 5 | 3 | 2 |
| Splits & efforts | 3 | 0 | 3 |
| Carte | 5 | 2 | 5 |
| IA | 0 | 5 | 1 |
| Éditeur routes | 4 | 10 | 3 |
| Dashboard | 3 | 0 | 4 |
| Entraînement | 0 | 0 | 9 |
| Social | 0 | 0 | 13 |
| Données enrichies | 0 | 0 | 7 |
| Multi-user/plateforme | 0 | 0 | 7 |
| Intégrations | 2 | 0 | 7 |
| **TOTAL** | **40** | **42** | **64** |

**Conclusion** : Notre application excelle en analyse technique (42 fonctionnalités exclusives), surtout en terrain/effort, qualité GPS, éditeur de routes et IA. Les gaps sont concentrés sur le social (13), le suivi d'entraînement (9), les données enrichies (7), le multi-utilisateur (7) et les intégrations (7).

---

## 6. Proposition d'axes d'évolution

### Phase 1 — Fondations (Multi-utilisateur + données enrichies essentielles)
> Pré-requis pour toute évolution sociale et SaaS

1. Authentification + multi-utilisateur + profil athlète enrichi
2. Description d'activité + RPE (effort ressenti)
3. Calories (estimation basée FC/poids/dénivelé)
4. Météo (intégration API Open-Meteo)
5. Gestion équipement (chaussures, vélos, usure)
6. RGPD (export, suppression)

### Phase 2 — Suivi d'entraînement
> Différenciateur fort vs "simple tracker"

7. Training Log / vue calendrier
8. Graphiques de progression (volume hebdo/mensuel)
9. Goals / objectifs (hebdo, mensuel, annuel)
10. Best Efforts historiques & PRs
11. Fitness & Freshness (modèle Banister CTL/ATL/TSB)
12. Weekly Summary

### Phase 3 — Enrichissement de l'expérience
> Qualité de l'expérience utilisateur

13. Photos (upload, géolocalisation, affichage carte)
14. Sélection interactive graphiques (zoom section → stats)
15. Personal Heatmap
16. Power Curve historique
17. Chat IA sur activité
18. Race Predictions

### Phase 4 — Social & communauté
> Extension vers plateforme communautaire

19. Feed d'activités + kudos + commentaires
20. Partage public d'activités
21. Segments communautaires + leaderboards
22. Clubs + challenges
23. Matched Activities

### Phase 5 — SaaS & scale
> Commercialisation

24. Administration back-office
25. Plans de paiement (Stripe)
26. API publique
27. App mobile (React Native ou capacitor)
28. Intégrations supplémentaires (COROS, Suunto, Polar)
