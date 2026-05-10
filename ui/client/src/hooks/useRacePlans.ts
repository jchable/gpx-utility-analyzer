import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { racePlansApi, nutritionProductsApi } from '../api/race-plans-client';
import type {
  RacePlanUpdateRequest,
  RacePlanCheckpointCreateRequest,
  RacePlanCheckpointUpdateRequest,
  RacePlanNutritionItemCreateRequest,
  NutritionProductCreateRequest,
  NutritionProductUpdateRequest,
} from '../types/race-plan';

// ─────────────────────────────────────────────
// Race Plans
// ─────────────────────────────────────────────

export function useRacePlans(page = 1, type?: string, status?: string) {
  return useQuery({
    queryKey: ['race-plans', page, type, status],
    queryFn: () => racePlansApi.getPlans(page, 20, type, status),
  });
}

export function useRacePlan(id: string, options?: { enabled?: boolean }) {
  return useQuery({
    queryKey: ['race-plan', id],
    queryFn: () => racePlansApi.getPlan(id),
    enabled: options?.enabled ?? !!id,
  });
}

export function useRacePlanShared(token: string, options?: { enabled?: boolean }) {
  return useQuery({
    queryKey: ['race-plan-shared', token],
    queryFn: () => racePlansApi.getShared(token),
    enabled: options?.enabled ?? !!token,
    staleTime: 5 * 60_000, // 5 min cache
  });
}

export function useCreateRacePlanFromRoute() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (routeId: string) => racePlansApi.createFromRoute(routeId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['race-plans'] });
    },
  });
}

export function useImportRacePlanGpx() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (file: File) => racePlansApi.importGpx(file),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['race-plans'] });
    },
  });
}

export function useUpdateRacePlan() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: RacePlanUpdateRequest }) =>
      racePlansApi.updatePlan(id, data),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: ['race-plans'] });
      queryClient.setQueryData(['race-plan', variables.id], _data);
    },
  });
}

export function useDeleteRacePlan() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => racePlansApi.deletePlan(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['race-plans'] });
    },
  });
}

// ─────────────────────────────────────────────
// Checkpoints
// ─────────────────────────────────────────────

export function useAddCheckpoint() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ planId, data }: { planId: string; data: RacePlanCheckpointCreateRequest }) =>
      racePlansApi.addCheckpoint(planId, data),
    onSuccess: (updatedPlan, variables) => {
      queryClient.setQueryData(['race-plan', variables.planId], updatedPlan);
    },
  });
}

export function useUpdateCheckpoint() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      planId,
      checkpointId,
      data,
    }: {
      planId: string;
      checkpointId: string;
      data: RacePlanCheckpointUpdateRequest;
    }) => racePlansApi.updateCheckpoint(planId, checkpointId, data),
    onSuccess: (updatedPlan, variables) => {
      queryClient.setQueryData(['race-plan', variables.planId], updatedPlan);
    },
  });
}

export function useDeleteCheckpoint() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ planId, checkpointId }: { planId: string; checkpointId: string }) =>
      racePlansApi.deleteCheckpoint(planId, checkpointId),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: ['race-plan', variables.planId] });
    },
  });
}

export function useComputeTimes() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (planId: string) => racePlansApi.computeTimes(planId),
    onSuccess: (updatedPlan, planId) => {
      queryClient.setQueryData(['race-plan', planId], updatedPlan);
    },
  });
}

// ─────────────────────────────────────────────
// Nutrition
// ─────────────────────────────────────────────

export function useAddNutritionItem() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ planId, data }: { planId: string; data: RacePlanNutritionItemCreateRequest }) =>
      racePlansApi.addNutritionItem(planId, data),
    onSuccess: (updatedPlan, variables) => {
      queryClient.setQueryData(['race-plan', variables.planId], updatedPlan);
    },
  });
}

export function useDeleteNutritionItem() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ planId, itemId }: { planId: string; itemId: string }) =>
      racePlansApi.deleteNutritionItem(planId, itemId),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: ['race-plan', variables.planId] });
    },
  });
}

// ─────────────────────────────────────────────
// Partage
// ─────────────────────────────────────────────

export function useEnableShare() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (planId: string) => racePlansApi.enableShare(planId),
    onSuccess: (_data, planId) => {
      queryClient.invalidateQueries({ queryKey: ['race-plan', planId] });
    },
  });
}

export function useDisableShare() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (planId: string) => racePlansApi.disableShare(planId),
    onSuccess: (_data, planId) => {
      queryClient.invalidateQueries({ queryKey: ['race-plan', planId] });
    },
  });
}

// ─────────────────────────────────────────────
// Post-course
// ─────────────────────────────────────────────

export function useLinkActivity() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ planId, activityId }: { planId: string; activityId: string }) =>
      racePlansApi.linkActivity(planId, activityId),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: ['race-plan', variables.planId] });
    },
  });
}

export function useRacePlanComparison(planId: string, options?: { enabled?: boolean }) {
  return useQuery({
    queryKey: ['race-plan-comparison', planId],
    queryFn: () => racePlansApi.getComparison(planId),
    enabled: options?.enabled ?? !!planId,
  });
}

// ─────────────────────────────────────────────
// Nutrition Products
// ─────────────────────────────────────────────

export function useNutritionProducts(type?: string) {
  return useQuery({
    queryKey: ['nutrition-products', type],
    queryFn: () => nutritionProductsApi.getProducts(type),
    staleTime: 5 * 60_000,
  });
}

export function useCreateNutritionProduct() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: NutritionProductCreateRequest) => nutritionProductsApi.createProduct(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['nutrition-products'] });
    },
  });
}

export function useUpdateNutritionProduct() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: NutritionProductUpdateRequest }) =>
      nutritionProductsApi.updateProduct(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['nutrition-products'] });
    },
  });
}

export function useDeleteNutritionProduct() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => nutritionProductsApi.deleteProduct(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['nutrition-products'] });
    },
  });
}

export function useImportDefaultProducts() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => nutritionProductsApi.importDefaults(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['nutrition-products'] });
    },
  });
}
