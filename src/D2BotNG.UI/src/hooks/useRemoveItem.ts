/**
 * useRemoveItem hook
 *
 * Mutation to remove an item from its source mule .txt file. The backend
 * rewrites the file atomically and the FileSystemWatcher broadcasts an
 * EntitiesChanged event, which refreshes the UI automatically — no manual
 * invalidation needed here.
 */

import { useMutation } from "@tanstack/react-query";
import { create } from "@bufbuild/protobuf";
import { itemClient } from "@/lib/grpc-client";
import { toast } from "@/stores/toast-store";
import { RemoveItemRequestSchema } from "@/generated/items_pb";

export interface RemoveItemInput {
  entityPath: string;
  descriptionId: string;
}

export function useRemoveItem() {
  return useMutation({
    mutationFn: async (input: RemoveItemInput) => {
      const request = create(RemoveItemRequestSchema, {
        entityPath: input.entityPath,
        descriptionId: input.descriptionId,
      });
      await itemClient.removeItem(request);
    },
    onError: (error) => {
      toast.error("Failed to remove item", error.message);
    },
  });
}
