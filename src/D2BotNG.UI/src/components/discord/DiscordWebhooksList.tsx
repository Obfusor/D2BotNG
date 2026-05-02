import { useCallback } from "react";
import { Button, Input, HelpTooltip } from "@/components/ui";

export interface DiscordWebhookInput {
  url: string;
  postItems: boolean;
  postConsole: boolean;
  postAnnounce: boolean;
}

export interface DiscordWebhooksListProps {
  webhooks: DiscordWebhookInput[];
  onChange: (next: DiscordWebhookInput[]) => void;
  /** Prefix used for input/checkbox ids — must be unique per page mount */
  idPrefix: string;
  /** Per-row URL error messages. Element undefined = no error for that row. */
  errors?: (string | undefined)[];
  onUrlBlur?: (index: number) => void;
}

const EMPTY_WEBHOOK: DiscordWebhookInput = {
  url: "",
  postItems: false,
  postConsole: false,
  postAnnounce: false,
};

export function DiscordWebhooksList({
  webhooks,
  onChange,
  idPrefix,
  errors,
  onUrlBlur,
}: DiscordWebhooksListProps) {
  const update = useCallback(
    (index: number, patch: Partial<DiscordWebhookInput>) => {
      onChange(webhooks.map((w, i) => (i === index ? { ...w, ...patch } : w)));
    },
    [webhooks, onChange],
  );

  const remove = useCallback(
    (index: number) => {
      onChange(webhooks.filter((_, i) => i !== index));
    },
    [webhooks, onChange],
  );

  const add = useCallback(() => {
    onChange([...webhooks, { ...EMPTY_WEBHOOK }]);
  }, [webhooks, onChange]);

  return (
    <div className="space-y-2">
      {webhooks.length === 0 && (
        <p className="text-sm text-zinc-500">No webhooks configured</p>
      )}
      {webhooks.map((webhook, index) => (
        <div
          key={index}
          className="flex flex-col gap-2 rounded-lg border border-zinc-800 bg-zinc-900/40 p-2 sm:flex-row sm:items-center"
        >
          <div className="flex-1">
            <Input
              id={`${idPrefix}-url-${index}`}
              aria-label="Webhook URL"
              value={webhook.url}
              onChange={(e) => update(index, { url: e.target.value })}
              onBlur={() => onUrlBlur?.(index)}
              placeholder="https://discord.com/api/webhooks/..."
              error={errors?.[index]}
            />
          </div>
          <div className="flex flex-wrap items-center gap-3">
            <CheckboxRow
              id={`${idPrefix}-items-${index}`}
              label="Items"
              tooltip="Post items the bot loots, with a rendered tooltip image."
              checked={webhook.postItems}
              onChange={(v) => update(index, { postItems: v })}
            />
            <CheckboxRow
              id={`${idPrefix}-console-${index}`}
              label="Console"
              tooltip="Post every console line from this profile. Can be very noisy."
              checked={webhook.postConsole}
              onChange={(v) => update(index, { postConsole: v })}
            />
            <CheckboxRow
              id={`${idPrefix}-announce-${index}`}
              label="Announce"
              tooltip="Post explicit script announcements (e.g. shrines, runes, key events). Lower volume than Console."
              checked={webhook.postAnnounce}
              onChange={(v) => update(index, { postAnnounce: v })}
            />
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => remove(index)}
            >
              Remove
            </Button>
          </div>
        </div>
      ))}
      <div>
        <Button type="button" variant="secondary" size="sm" onClick={add}>
          Add Webhook
        </Button>
      </div>
    </div>
  );
}

interface CheckboxRowProps {
  id: string;
  label: string;
  tooltip: string;
  checked: boolean;
  onChange: (next: boolean) => void;
}

function CheckboxRow({
  id,
  label,
  tooltip,
  checked,
  onChange,
}: CheckboxRowProps) {
  return (
    <div className="flex items-center gap-2">
      <input
        id={id}
        type="checkbox"
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
        className="h-4 w-4 rounded border-zinc-700 bg-zinc-800 text-d2-gold focus:ring-d2-gold"
      />
      <label htmlFor={id} className="text-sm text-zinc-400">
        {label}
      </label>
      <HelpTooltip text={tooltip} />
    </div>
  );
}
