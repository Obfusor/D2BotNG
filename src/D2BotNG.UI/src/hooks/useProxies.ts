/**
 * Proxy service hooks using TanStack Query
 *
 * Mutations for the Proxies tab. State comes from the event store; mutations
 * (except Import/Test) return Empty, with updates arriving via the event stream.
 */

import { useState, useCallback, useEffect } from "react";
import { useMutation } from "@tanstack/react-query";
import { create, type MessageInitShape } from "@bufbuild/protobuf";
import { proxyClient } from "@/lib/grpc-client";
import { toast } from "@/stores/toast-store";
import {
  ProxySchema,
  UpdateProxyRequestSchema,
  DeleteProxyRequestSchema,
  ImportProxiesRequestSchema,
  TestProxyRequestSchema,
} from "@/generated/proxies_pb";

export type UpdateProxyInput = MessageInitShape<
  typeof UpdateProxyRequestSchema
>;

/** Create a proxy from a raw address (normalized server-side). */
export function useCreateProxy() {
  return useMutation({
    mutationFn: async (address: string) => {
      await proxyClient.createProxy(create(ProxySchema, { address }));
    },
    onSuccess: () => toast.success("Proxy added"),
    onError: (error) => toast.error("Failed to add proxy", error.message),
  });
}

/** Update a proxy's address (pass originalAddress to change an existing one). */
export function useUpdateProxy() {
  return useMutation({
    mutationFn: async (input: UpdateProxyInput) => {
      await proxyClient.updateProxy(create(UpdateProxyRequestSchema, input));
    },
    onSuccess: () => toast.success("Proxy updated"),
    onError: (error) => toast.error("Failed to update proxy", error.message),
  });
}

export function useDeleteProxy() {
  return useMutation({
    mutationFn: async (address: string) => {
      await proxyClient.deleteProxy(
        create(DeleteProxyRequestSchema, { address }),
      );
    },
    onSuccess: () => toast.success("Proxy deleted"),
    onError: (error) => toast.error("Failed to delete proxy", error.message),
  });
}

/** Bulk add from a pasted list. Returns counts of added/skipped lines. */
export function useImportProxies() {
  return useMutation({
    mutationFn: async (text: string) => {
      return await proxyClient.importProxies(
        create(ImportProxiesRequestSchema, { text }),
      );
    },
    onSuccess: (response) => {
      const detail =
        response.skipped > 0
          ? `${response.added} added, ${response.skipped} skipped`
          : `${response.added} added`;
      toast.success("Proxies imported", detail);
    },
    onError: (error) => toast.error("Failed to import proxies", error.message),
  });
}

export interface ProxyTestResult {
  success: boolean;
  message: string;
  latencyMs: number;
}

/**
 * Tracks ad-hoc proxy test results for the Proxies tab. Results drive a per-row
 * status bubble (no toasts). `test` runs one proxy; `testAll` runs many concurrently.
 */
export function useProxyTester(knownAddresses: string[]) {
  const [results, setResults] = useState<Map<string, ProxyTestResult>>(
    () => new Map(),
  );
  const [testing, setTesting] = useState<Set<string>>(() => new Set());

  // Drop results / in-flight markers for proxies that no longer exist, so
  // re-adding the same address later doesn't surface a stale bubble.
  useEffect(() => {
    const known = new Set(knownAddresses);
    setResults((prev) => {
      let changed = false;
      const next = new Map(prev);
      for (const address of next.keys()) {
        if (!known.has(address)) {
          next.delete(address);
          changed = true;
        }
      }
      return changed ? next : prev;
    });
    setTesting((prev) => {
      let changed = false;
      const next = new Set(prev);
      for (const address of next) {
        if (!known.has(address)) {
          next.delete(address);
          changed = true;
        }
      }
      return changed ? next : prev;
    });
  }, [knownAddresses]);

  const test = useCallback(async (address: string) => {
    setTesting((prev) => new Set(prev).add(address));
    let result: ProxyTestResult;
    try {
      const response = await proxyClient.testProxy(
        create(TestProxyRequestSchema, { address }),
      );
      result = {
        success: response.success,
        message: response.message,
        latencyMs: response.latencyMs,
      };
    } catch (error) {
      result = {
        success: false,
        message: error instanceof Error ? error.message : "Test failed",
        latencyMs: 0,
      };
    }
    setResults((prev) => new Map(prev).set(address, result));
    setTesting((prev) => {
      const next = new Set(prev);
      next.delete(address);
      return next;
    });
  }, []);

  const testAll = useCallback(
    async (addresses: string[]) => {
      await Promise.allSettled(addresses.map((address) => test(address)));
    },
    [test],
  );

  return { results, testing, test, testAll };
}
