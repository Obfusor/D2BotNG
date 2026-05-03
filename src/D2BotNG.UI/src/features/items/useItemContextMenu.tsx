/**
 * Right-click context menu for items.
 *
 * Wraps the generic useContextMenu hook with item-specific actions:
 * Save Image, Copy Image, Copy Description, and (optionally) Remove.
 */

import { useMemo } from "react";
import {
  ArrowDownTrayIcon,
  PhotoIcon,
  DocumentTextIcon,
  TrashIcon,
} from "@heroicons/react/24/outline";
import type { Item } from "@/generated/items_pb";
import { useContextMenu } from "@/components/ui/ContextMenu";
import type { DropdownItem } from "@/components/ui/Dropdown";
import { toast } from "@/stores/toast-store";
import {
  copyItemDescription,
  copyItemImage,
  saveItemImage,
} from "./itemActions";

export interface UseItemContextMenuOptions {
  item: Item | null | undefined;
  /**
   * If provided, the Remove menu entry is added. The caller is responsible
   * for any confirmation dialog and the actual removal call.
   */
  onRemove?: () => void;
}

export function useItemContextMenu({
  item,
  onRemove,
}: UseItemContextMenuOptions) {
  const items: DropdownItem[] = useMemo(() => {
    if (!item) return [];

    const entries: DropdownItem[] = [
      {
        label: "Save Image",
        icon: ArrowDownTrayIcon,
        onClick: () => {
          void (async () => {
            try {
              await saveItemImage(item);
            } catch (e) {
              toast.error(
                "Failed to save image",
                e instanceof Error ? e.message : String(e),
              );
            }
          })();
        },
      },
      {
        label: "Copy Image",
        icon: PhotoIcon,
        onClick: () => {
          void (async () => {
            try {
              await copyItemImage(item);
              toast.success("Image copied to clipboard");
            } catch (e) {
              toast.error(
                "Failed to copy image",
                e instanceof Error ? e.message : String(e),
              );
            }
          })();
        },
      },
      {
        label: "Copy Description",
        icon: DocumentTextIcon,
        onClick: () => {
          void (async () => {
            try {
              await copyItemDescription(item);
              toast.success("Description copied to clipboard");
            } catch (e) {
              toast.error(
                "Failed to copy description",
                e instanceof Error ? e.message : String(e),
              );
            }
          })();
        },
      },
    ];

    if (onRemove) {
      entries.push({
        label: "Remove",
        icon: TrashIcon,
        danger: true,
        onClick: onRemove,
      });
    }

    return entries;
  }, [item, onRemove]);

  return useContextMenu(items);
}
