import { useEffect, useState } from "react";
import { fileClient } from "@/lib/grpc-client";

export interface EntryScriptOption {
  value: string;
  label: string;
}

export function useEntryScripts(
  basePath: string | undefined,
): EntryScriptOption[] {
  const [options, setOptions] = useState<EntryScriptOption[]>([]);

  useEffect(() => {
    async function load() {
      if (!basePath) return;
      try {
        const d2bsPath = `${basePath}/d2bs`;
        const d2bsListing = await fileClient.listDirectory({ path: d2bsPath });

        const botDirs = d2bsListing.entries
          .filter((e) => e.isDirectory && e.name.toLowerCase().endsWith("bot"))
          .map((e) => e.name)
          .sort((a, b) => a.localeCompare(b));

        if (botDirs.length === 0) {
          setOptions([]);
          return;
        }

        const botPath = `${d2bsPath}/${botDirs[0]}`;
        const botListing = await fileClient.listDirectory({ path: botPath });
        const dbjFiles = botListing.entries
          .filter(
            (e) => !e.isDirectory && e.name.toLowerCase().endsWith(".dbj"),
          )
          .map((e) => e.name)
          .sort((a, b) => a.localeCompare(b));

        setOptions(dbjFiles.map((name) => ({ value: name, label: name })));
      } catch (err) {
        console.error("Failed to load entry scripts:", err);
        setOptions([]);
      }
    }
    load();
  }, [basePath]);

  return options;
}
