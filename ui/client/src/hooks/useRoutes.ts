import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { routesApi } from '../api/routes-client';
import type { RouteCreateRequest, RouteUpdateRequest } from '../types/route';

export function useRoutes(page = 1, type?: string, status?: string) {
  return useQuery({
    queryKey: ['routes', page, type, status],
    queryFn: () => routesApi.getRoutes(page, 20, type, status),
  });
}

export function useRoute(id: string, options?: { enabled?: boolean }) {
  return useQuery({
    queryKey: ['route', id],
    queryFn: () => routesApi.getRoute(id),
    enabled: options?.enabled ?? !!id,
  });
}

export function useRouteTags() {
  return useQuery({
    queryKey: ['route-tags'],
    queryFn: routesApi.getTags,
    staleTime: 300_000,
  });
}

export function useCreateRoute() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: RouteCreateRequest) => routesApi.createRoute(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['routes'] });
    },
  });
}

export function useUpdateRoute() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: RouteUpdateRequest }) =>
      routesApi.updateRoute(id, data),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: ['routes'] });
      queryClient.invalidateQueries({ queryKey: ['route', variables.id] });
    },
  });
}

export function useDeleteRoute() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => routesApi.deleteRoute(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['routes'] });
    },
  });
}

export function useCreateRouteFromActivity() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (activityId: string) => routesApi.createFromActivity(activityId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['routes'] });
    },
  });
}

export function useImportRouteGpx() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (file: File) => routesApi.importGpx(file),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['routes'] });
    },
  });
}
