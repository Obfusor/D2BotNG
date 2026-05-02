/**
 * DiscordSettings component
 *
 * Card for configuring Discord integration.
 */

import { useCallback, useMemo } from "react";
import { create } from "@bufbuild/protobuf";
import {
  Card,
  CardHeader,
  CardContent,
  Input,
  PasswordInput,
  Button,
} from "@/components/ui";
import {
  DiscordWebhooksList,
  type DiscordWebhookInput,
} from "@/components/discord/DiscordWebhooksList";
import { useTestDiscord } from "@/hooks";
import { ArrowPathIcon } from "@heroicons/react/24/outline";
import type { DiscordSettings as DiscordSettingsType } from "@/generated/settings_pb";
import { DiscordWebhookSchema } from "@/generated/profiles_pb";

export interface DiscordValidationErrors {
  token?: string;
  serverId?: string;
  webhookUrls?: (string | undefined)[];
}

interface DiscordSettingsProps {
  /** Current discord settings */
  discord?: Partial<DiscordSettingsType>;
  /** Callback when a field changes */
  onChange: (discord: Partial<DiscordSettingsType>) => void;
  /** Validation errors to display */
  errors?: DiscordValidationErrors;
}

export function DiscordSettings({
  discord,
  onChange,
  errors,
}: DiscordSettingsProps) {
  const testDiscord = useTestDiscord();
  const webhooks: DiscordWebhookInput[] = useMemo(
    () =>
      (discord?.webhooks ?? []).map((w) => ({
        url: w.url,
        postItems: w.postItems,
        postConsole: w.postConsole,
        postAnnounce: w.postAnnounce,
      })),
    [discord?.webhooks],
  );

  const canTest =
    discord?.enabled && discord?.token?.trim() && discord?.serverId?.trim();

  const handleTest = () => {
    if (canTest) {
      testDiscord.mutate({
        token: discord.token!,
        serverId: discord.serverId!,
      });
    }
  };

  const handleWebhooksChange = useCallback(
    (next: DiscordWebhookInput[]) => {
      onChange({
        webhooks: next.map((w) => create(DiscordWebhookSchema, w)),
      });
    },
    [onChange],
  );

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader
          title="Discord Bot"
          description="Slash commands (/list, /status, /start, ...) via a Discord bot."
        />
        <CardContent>
          <div className="space-y-3">
            <label className="flex cursor-pointer items-center gap-3">
              <input
                type="checkbox"
                checked={discord?.enabled ?? false}
                onChange={(e) => onChange({ enabled: e.target.checked })}
                className="h-4 w-4 rounded border-zinc-600 bg-zinc-800 text-d2-gold focus:ring-d2-gold focus:ring-offset-zinc-900"
              />
              <span className="text-sm text-zinc-300">Enable Discord Bot</span>
            </label>

            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <Input
                id="discord-token"
                label="Bot Token"
                tooltip="Bot token from the Discord Developer Portal."
                type="password"
                placeholder="Enter Discord bot token"
                value={discord?.token ?? ""}
                onChange={(e) => onChange({ token: e.target.value })}
                disabled={!discord?.enabled}
                error={errors?.token}
              />

              <Input
                id="discord-server"
                label="Server ID"
                tooltip="Right-click your server name in Discord and Copy Server ID (requires Developer Mode)."
                placeholder="Enter Discord server ID"
                value={discord?.serverId ?? ""}
                onChange={(e) => onChange({ serverId: e.target.value })}
                disabled={!discord?.enabled}
                error={errors?.serverId}
              />

              <PasswordInput
                id="discord-password"
                label="Password"
                tooltip="Optional password for Discord bot commands. Users must /identify with this password to use commands."
                placeholder="Optional"
                value={discord?.password ?? ""}
                onChange={(e) => onChange({ password: e.target.value })}
                disabled={!discord?.enabled}
              />

              <div className="flex items-end">
                <Button
                  variant="secondary"
                  onClick={handleTest}
                  disabled={!canTest || testDiscord.isPending}
                >
                  {testDiscord.isPending ? (
                    <ArrowPathIcon className="h-4 w-4 animate-spin" />
                  ) : null}
                  Test Connection
                </Button>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader
          title="Discord Webhooks"
          description="Fire for all profiles. For per-profile rules, edit a profile."
        />
        <CardContent>
          <DiscordWebhooksList
            webhooks={webhooks}
            onChange={handleWebhooksChange}
            idPrefix="settings-webhook"
            errors={errors?.webhookUrls}
          />
        </CardContent>
      </Card>
    </div>
  );
}
