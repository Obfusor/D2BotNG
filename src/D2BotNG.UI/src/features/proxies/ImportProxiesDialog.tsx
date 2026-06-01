/**
 * ImportProxiesDialog component
 *
 * Paste a list of proxies (one per line). The server normalizes each line and
 * skips invalid or duplicate entries.
 */

import { useState, useEffect, useCallback } from "react";
import {
  Dialog,
  DialogHeader,
  DialogContent,
  DialogFooter,
  Button,
} from "@/components/ui";
import { useImportProxies } from "@/hooks";

export interface ImportProxiesDialogProps {
  open: boolean;
  onClose: () => void;
}

export function ImportProxiesDialog({
  open,
  onClose,
}: ImportProxiesDialogProps) {
  const [text, setText] = useState("");
  const importProxies = useImportProxies();

  useEffect(() => {
    if (open) setText("");
  }, [open]);

  const lineCount = text
    .split("\n")
    .map((line) => line.trim())
    .filter((line) => line.length > 0).length;

  const handleSubmit = useCallback(
    async (e: React.FormEvent) => {
      e.preventDefault();
      if (lineCount === 0) return;
      try {
        await importProxies.mutateAsync(text);
        onClose();
      } catch {
        // Error toast handled by the mutation hook
      }
    },
    [text, lineCount, importProxies, onClose],
  );

  return (
    <Dialog open={open} onClose={onClose}>
      <form onSubmit={handleSubmit}>
        <DialogHeader
          title="Import Proxies"
          description="Paste one proxy per line. Invalid lines and duplicates are skipped."
          onClose={onClose}
        />
        <DialogContent>
          <textarea
            value={text}
            onChange={(e) => setText(e.target.value)}
            className="block w-full rounded-lg border-0 bg-zinc-800 px-3 py-2 text-zinc-100 ring-1 ring-inset ring-zinc-700 placeholder:text-zinc-500 focus:ring-2 focus:ring-inset focus:ring-d2-gold sm:text-sm sm:leading-6 transition-colors font-mono"
            rows={10}
            placeholder={
              "socks5://user:pass@host:1080\nhost:1080\nhost:1080:user:pass"
            }
            autoFocus
          />
          <p className="mt-1.5 text-sm text-zinc-500">
            {lineCount} {lineCount === 1 ? "line" : "lines"}
          </p>
        </DialogContent>
        <DialogFooter>
          <Button
            type="button"
            variant="ghost"
            onClick={onClose}
            disabled={importProxies.isPending}
          >
            Cancel
          </Button>
          <Button
            type="submit"
            disabled={importProxies.isPending || lineCount === 0}
          >
            Import
          </Button>
        </DialogFooter>
      </form>
    </Dialog>
  );
}
