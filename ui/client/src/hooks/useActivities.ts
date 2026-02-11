import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../api/client';
import type { AppSettings } from '../types/activity';

export function useDashboard() {
  return useQuery({
    queryKey: ['dashboard'],
    queryFn: api.getDashboardSummary,
    refetchInterval: 10000,
  });
}

export function useActivities(page = 1, type?: string) {
  return useQuery({
    queryKey: ['activities', page, type],
    queryFn: () => api.getActivities(page, 20, type),
  });
}

export function useActivity(id: string) {
  return useQuery({
    queryKey: ['activity', id],
    queryFn: () => api.getActivity(id),
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      return status === 'Completed' || status === 'Failed' ? false : 3000;
    },
  });
}

export function useProfile(id: string) {
  return useQuery({
    queryKey: ['activity', id, 'profile'],
    queryFn: () => api.getProfile(id),
    enabled: !!id,
    staleTime: 3600_000,
  });
}

export function useTrack(id: string) {
  return useQuery({
    queryKey: ['activity', id, 'track'],
    queryFn: () => api.getTrack(id),
    enabled: !!id,
    staleTime: 3600_000,
  });
}

export function useSplits(id: string) {
  return useQuery({
    queryKey: ['activity', id, 'splits'],
    queryFn: () => api.getSplits(id),
    enabled: !!id,
    staleTime: 3600_000,
  });
}

export function useIntegrations() {
  return useQuery({
    queryKey: ['integrations'],
    queryFn: api.getIntegrations,
  });
}

export function useSettings() {
  return useQuery({
    queryKey: ['settings'],
    queryFn: api.getSettings,
  });
}

export function useUpdateSettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (settings: AppSettings) => api.updateSettings(settings),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['settings'] });
    },
  });
}
