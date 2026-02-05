# gpx-analyzer

Outil en ligne de commande en Go pour analyser des fichiers GPX : distance, dénivelé, vitesse, détection d'arrêts, découpage temporel et fusion de fichiers. Inclut un lissage d'élévation et une correction automatique par modèle numérique de terrain (SRTM avec téléchargement auto des tuiles).

## Installation

```bash
go install github.com/jchable/gpx-utility-analyzer@latest
```

Ou depuis les sources :

```bash
git clone https://github.com/jchable/gpx-utility-analyzer.git
cd gpx-utility-analyzer/cli
go build -o gpx-analyzer .
```

## Utilisation rapide

**Analyser un fichier GPX :**

```bash
gpx-analyzer analyze ma-rando.gpx
```

**Découper une trace multi-jours en segments de 24h :**

```bash
gpx-analyzer split traversee-alpes.gpx
```

**Fusionner plusieurs fichiers :**

```bash
gpx-analyzer merge jour1.gpx jour2.gpx jour3.gpx -o randonnee-complete.gpx
```

**Sortie JSON :**

```bash
gpx-analyzer analyze ma-rando.gpx --format json
```

**Lissage fort pour GPS bruité :**

```bash
gpx-analyzer analyze trace.gpx --smoothing heavy
```

**Algorithme par segments de pente constante (meilleur D+ avec DEM) :**

```bash
gpx-analyzer analyze pct.gpx --elevation-algo segments
```

**Lissage de la trace GPS + Douglas-Peucker :**

```bash
gpx-analyzer analyze pct.gpx --track-smoothing medium --elevation-algo douglas-peucker
```

**Exporter le GPX avec altitudes corrigées :**

```bash
gpx-analyzer analyze ma-rando.gpx --export ./processed/
```

Pour la documentation complète des commandes, flags et exemples avancés, voir [docs/CLI_USAGE.md](docs/CLI_USAGE.md).

## Développement

### Prérequis

- Go 1.22+

### Build

```bash
go build -o gpx-analyzer .
```

### Tests

```bash
go test ./...
```
