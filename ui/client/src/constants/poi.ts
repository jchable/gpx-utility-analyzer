import {
  Droplets,
  ParkingCircle,
  Home,
  Mountain,
  Eye,
  AlertTriangle,
  UtensilsCrossed,
  Tent,
  MapPin,
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import type { PoiType } from '../types/route';
import type maplibregl from 'maplibre-gl';

export const POI_TYPE_CONFIG: { id: PoiType; icon: LucideIcon; color: string }[] = [
  { id: 'water', icon: Droplets, color: '#3b82f6' },
  { id: 'parking', icon: ParkingCircle, color: '#8b5cf6' },
  { id: 'refuge', icon: Home, color: '#f59e0b' },
  { id: 'summit', icon: Mountain, color: '#ef4444' },
  { id: 'viewpoint', icon: Eye, color: '#10b981' },
  { id: 'danger', icon: AlertTriangle, color: '#ef4444' },
  { id: 'food', icon: UtensilsCrossed, color: '#f97316' },
  { id: 'camping', icon: Tent, color: '#22c55e' },
  { id: 'custom', icon: MapPin, color: '#6b7280' },
];

/** Build a MapLibre match expression for POI circle colors based on type. */
export function poiColorMatchExpression(): maplibregl.ExpressionSpecification {
  const entries = POI_TYPE_CONFIG.flatMap(({ id, color }) => [id, color]);
  return ['match', ['get', 'type'], ...entries, '#6b7280'] as unknown as maplibregl.ExpressionSpecification;
}
