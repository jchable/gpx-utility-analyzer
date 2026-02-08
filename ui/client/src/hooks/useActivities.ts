import { useQuery } from '@tanstack/react-query';
import { api } from '../api/client';

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

export function useIntegrations() {
  return useQuery({
    queryKey: ['integrations'],
    queryFn: api.getIntegrations,
  });
}
