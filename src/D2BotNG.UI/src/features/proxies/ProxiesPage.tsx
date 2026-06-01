/**
 * ProxiesPage component
 *
 * Manage the flat list of SOCKS5 proxies: add/remove, paste a list, test each
 * (or all), and see how many profiles have each proxy configured vs running.
 */

import { useState, useMemo } from "react";
import {
  Button,
  Card,
  EmptyState,
  LoadingSpinner,
  DeleteConfirmationDialog,
  Table,
  TableHead,
  TableBody,
  TableRow,
  TableHeader,
  TableCell,
  Tooltip,
} from "@/components/ui";
import { useDeleteProxy, useProxyTester, type ProxyTestResult } from "@/hooks";
import {
  useProxies,
  useIsLoading,
  type ProxyWithUsageData,
} from "@/stores/event-store";
import {
  GlobeAltIcon,
  PlusIcon,
  ArrowUpTrayIcon,
  ArrowPathIcon,
  PencilIcon,
  TrashIcon,
} from "@heroicons/react/24/outline";
import { ProxyDialog } from "./ProxyDialog";
import { ImportProxiesDialog } from "./ImportProxiesDialog";

export function ProxiesPage() {
  const isLoading = useIsLoading();
  const proxies = useProxies();
  const deleteProxy = useDeleteProxy();
  const addresses = useMemo(
    () => proxies.map((p) => p.proxy.address),
    [proxies],
  );
  const { results, testing, test, testAll } = useProxyTester(addresses);

  const [isDialogOpen, setIsDialogOpen] = useState(false);
  const [editing, setEditing] = useState<string | null>(null);
  const [importOpen, setImportOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<string | null>(null);

  const handleAdd = () => {
    setEditing(null);
    setIsDialogOpen(true);
  };

  if (isLoading) {
    return <LoadingSpinner fullPage />;
  }

  const hasProxies = proxies.length > 0;
  const isTestingAll = testing.size > 0;

  return (
    <div className="space-y-4">
      {/* Sticky header */}
      <div className="sticky top-0 z-20 bg-zinc-950 -mx-4 px-4 sm:-mx-6 sm:px-6 lg:-mx-8 lg:px-8 pt-4 pb-3 border-b border-zinc-800/50">
        <div className="flex items-center justify-between gap-3">
          <h1 className="text-lg font-bold text-zinc-100">Proxies</h1>
          <div className="flex items-center gap-2">
            <Button
              variant="secondary"
              size="sm"
              onClick={() => testAll(addresses)}
              disabled={!hasProxies || isTestingAll}
            >
              {isTestingAll ? (
                <ArrowPathIcon className="h-4 w-4 animate-spin" />
              ) : null}
              Test All
            </Button>
            <Button
              variant="secondary"
              size="sm"
              onClick={() => setImportOpen(true)}
            >
              <ArrowUpTrayIcon className="h-4 w-4" />
              Import
            </Button>
            <Button size="sm" onClick={handleAdd}>
              <PlusIcon className="h-4 w-4" />
              Add Proxy
            </Button>
          </div>
        </div>
      </div>

      {/* Content */}
      {hasProxies ? (
        <Card>
          <Table>
            <TableHead>
              <TableRow>
                <TableHeader>Proxy</TableHeader>
                <TableHeader>Profiles</TableHeader>
                <TableHeader className="text-right">Actions</TableHeader>
              </TableRow>
            </TableHead>
            <TableBody>
              {proxies.map((data) => (
                <ProxyRow
                  key={data.proxy.address}
                  data={data}
                  result={results.get(data.proxy.address)}
                  isTesting={testing.has(data.proxy.address)}
                  onTest={() => test(data.proxy.address)}
                  onEdit={() => {
                    setEditing(data.proxy.address);
                    setIsDialogOpen(true);
                  }}
                  onDelete={() => setDeleteTarget(data.proxy.address)}
                />
              ))}
            </TableBody>
          </Table>
        </Card>
      ) : (
        <EmptyState
          icon={GlobeAltIcon}
          title="No proxies yet"
          description="Add a proxy or paste a list, then select one per profile."
          action={
            <Button onClick={handleAdd}>
              <PlusIcon className="h-4 w-4" />
              Add Proxy
            </Button>
          }
        />
      )}

      {/* Add / Edit dialog */}
      <ProxyDialog
        open={isDialogOpen}
        onClose={() => {
          setIsDialogOpen(false);
          setEditing(null);
        }}
        address={editing}
      />

      {/* Import dialog */}
      <ImportProxiesDialog
        open={importOpen}
        onClose={() => setImportOpen(false)}
      />

      {/* Delete confirmation */}
      <DeleteConfirmationDialog
        open={deleteTarget !== null}
        entityType="Proxy"
        entityName={deleteTarget ?? ""}
        warningMessage="Profiles using this proxy will fall back to a direct connection."
        isPending={deleteProxy.isPending}
        onConfirm={async () => {
          if (deleteTarget) {
            await deleteProxy.mutateAsync(deleteTarget);
            setDeleteTarget(null);
          }
        }}
        onCancel={() => setDeleteTarget(null)}
      />
    </div>
  );
}

interface ProxyRowProps {
  data: ProxyWithUsageData;
  result: ProxyTestResult | undefined;
  isTesting: boolean;
  onTest: () => void;
  onEdit: () => void;
  onDelete: () => void;
}

function ProxyRow({
  data,
  result,
  isTesting,
  onTest,
  onEdit,
  onDelete,
}: ProxyRowProps) {
  const { proxy, configuredProfiles, activeProfiles } = data;
  const activeSet = new Set(activeProfiles);

  const usageTooltip =
    configuredProfiles.length > 0 ? (
      <ul className="space-y-0.5">
        {configuredProfiles.map((name) => (
          <li key={name}>
            {name}
            {activeSet.has(name) ? " (active)" : ""}
          </li>
        ))}
      </ul>
    ) : (
      "No profiles use this proxy"
    );

  return (
    <TableRow>
      <TableCell className="font-mono text-zinc-200">{proxy.address}</TableCell>
      <TableCell>
        <Tooltip content={usageTooltip}>
          <span className="inline-flex cursor-default items-center gap-2">
            <span className="text-zinc-300">
              {configuredProfiles.length} configured
            </span>
            {activeProfiles.length > 0 && (
              <span className="text-green-400">
                {activeProfiles.length} active
              </span>
            )}
          </span>
        </Tooltip>
      </TableCell>
      <TableCell className="text-right">
        <div className="inline-flex items-center gap-2">
          <TestStatusBubble result={result} />
          <Button
            variant="ghost"
            size="sm"
            onClick={onTest}
            disabled={isTesting}
            className="w-16"
          >
            {isTesting ? (
              <ArrowPathIcon className="h-4 w-4 animate-spin" />
            ) : (
              "Test"
            )}
          </Button>
          <Button
            variant="ghost"
            size="sm"
            onClick={onEdit}
            aria-label="Edit proxy"
          >
            <PencilIcon className="h-4 w-4" />
          </Button>
          <Button
            variant="ghost"
            size="sm"
            onClick={onDelete}
            aria-label="Delete proxy"
          >
            <TrashIcon className="h-4 w-4" />
          </Button>
        </div>
      </TableCell>
    </TableRow>
  );
}

/** Small colored dot showing the last test outcome; latency/message on hover. */
function TestStatusBubble({ result }: { result: ProxyTestResult | undefined }) {
  const color = !result
    ? "bg-zinc-600"
    : result.success
      ? "bg-green-500"
      : "bg-red-500";

  const tip = !result
    ? "Not tested"
    : result.success
      ? result.latencyMs > 0
        ? `${result.message} · ${result.latencyMs} ms`
        : result.message
      : result.message;

  return (
    <Tooltip content={tip}>
      <span
        className={`inline-block h-2.5 w-2.5 rounded-full ${color}`}
        aria-label="Last test result"
      />
    </Tooltip>
  );
}
