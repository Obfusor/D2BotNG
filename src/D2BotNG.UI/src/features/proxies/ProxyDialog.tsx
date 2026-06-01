/**
 * ProxyDialog component
 *
 * Dialog for adding or editing a single proxy address.
 */

import { useState, useEffect, useCallback } from "react";
import {
  Dialog,
  DialogHeader,
  DialogContent,
  DialogFooter,
  Button,
  Input,
} from "@/components/ui";
import { useCreateProxy, useUpdateProxy } from "@/hooks";

/** Accept socks5://[user:pass@]host:port or bare host:port[:user:pass]. */
function isValidProxy(value: string): boolean {
  const trimmed = value.trim();
  if (trimmed === "") return false;

  if (!trimmed.includes("://")) {
    const parts = trimmed.split(":");
    if (parts.length !== 2 && parts.length !== 4) return false;
    const port = Number(parts[1]);
    return (
      parts[0].length > 0 &&
      Number.isInteger(port) &&
      port >= 1 &&
      port <= 65535
    );
  }

  try {
    const url = new URL(trimmed);
    if (url.protocol !== "socks5:") return false;
    if (!url.hostname || url.port === "") return false;
    const port = Number(url.port);
    return port >= 1 && port <= 65535;
  } catch {
    return false;
  }
}

export interface ProxyDialogProps {
  open: boolean;
  onClose: () => void;
  /** Address being edited, or null/undefined to add a new proxy. */
  address?: string | null;
}

export function ProxyDialog({ open, onClose, address }: ProxyDialogProps) {
  const [value, setValue] = useState("");
  const [error, setError] = useState<string | undefined>();
  const createProxy = useCreateProxy();
  const updateProxy = useUpdateProxy();
  const isEditing = address != null;

  useEffect(() => {
    if (open) {
      setValue(address ?? "");
      setError(undefined);
    }
  }, [open, address]);

  const handleSubmit = useCallback(
    async (e: React.FormEvent) => {
      e.preventDefault();
      const trimmed = value.trim();
      if (!isValidProxy(trimmed)) {
        setError(
          "Enter socks5://[user:pass@]host:port (or host:port[:user:pass])",
        );
        return;
      }
      try {
        if (isEditing && address) {
          await updateProxy.mutateAsync({
            proxy: { address: trimmed },
            originalAddress: address,
          });
        } else {
          await createProxy.mutateAsync(trimmed);
        }
        onClose();
      } catch {
        // Error toast handled by the mutation hooks
      }
    },
    [value, isEditing, address, updateProxy, createProxy, onClose],
  );

  const isPending = createProxy.isPending || updateProxy.isPending;

  return (
    <Dialog open={open} onClose={onClose}>
      <form onSubmit={handleSubmit}>
        <DialogHeader
          title={isEditing ? "Edit Proxy" : "Add Proxy"}
          description="A SOCKS5 proxy address. Credentials are optional."
          onClose={onClose}
        />
        <DialogContent>
          <Input
            id="proxy-address"
            label="Address"
            value={value}
            onChange={(e) => setValue(e.target.value)}
            error={error}
            placeholder="socks5://user:pass@host:1080"
            autoFocus
          />
        </DialogContent>
        <DialogFooter>
          <Button
            type="button"
            variant="ghost"
            onClick={onClose}
            disabled={isPending}
          >
            Cancel
          </Button>
          <Button type="submit" disabled={isPending}>
            {isEditing ? "Save Changes" : "Add Proxy"}
          </Button>
        </DialogFooter>
      </form>
    </Dialog>
  );
}
