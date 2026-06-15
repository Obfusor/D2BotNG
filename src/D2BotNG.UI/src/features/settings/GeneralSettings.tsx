/**
 * GeneralSettings component
 *
 * Card for configuring server, paths, display, and application behavior.
 */

import { useState } from "react";
import {
  Card,
  CardHeader,
  CardContent,
  Input,
  PasswordInput,
  PathInput,
  Select,
  PathSelectorDialog,
} from "@/components/ui";
import { CloseAction, ItemFont } from "@/generated/settings_pb";
import type { ServerSettings as ServerSettingsType } from "@/generated/settings_pb";
import type { GameSettings as GameSettingsType } from "@/generated/settings_pb";
import type { DisplaySettings as DisplaySettingsType } from "@/generated/settings_pb";
import type { StartupSettings as StartupSettingsType } from "@/generated/settings_pb";
import type { EngineSettings as EngineSettingsType } from "@/generated/settings_pb";

interface GeneralSettingsProps {
  /** Current server settings */
  server?: Partial<ServerSettingsType>;
  /** Current game settings */
  game?: Partial<GameSettingsType>;
  /** Current display settings */
  display?: Partial<DisplaySettingsType>;
  /** Current startup pacing settings */
  startup?: Partial<StartupSettingsType>;
  /** Current engine health & crash-recovery thresholds */
  engine?: Partial<EngineSettingsType>;
  /** Whether to start minimized */
  startMinimized: boolean;
  /** Whether the minimize button hides to the system tray (vs taskbar) */
  minimizeToTray: boolean;
  /** What action to take on close */
  closeAction: CloseAction;
  /** Application base directory path */
  basePath: string;
  /** Callback when server settings change */
  onServerChange: (server: Partial<ServerSettingsType>) => void;
  /** Callback when game settings change */
  onGameChange: (game: Partial<GameSettingsType>) => void;
  /** Callback when display settings change */
  onDisplayChange: (display: Partial<DisplaySettingsType>) => void;
  /** Callback when startup pacing changes */
  onStartupChange: (startup: Partial<StartupSettingsType>) => void;
  /** Callback when engine health thresholds change */
  onEngineChange: (engine: Partial<EngineSettingsType>) => void;
  /** Callback when start minimized changes */
  onStartMinimizedChange: (value: boolean) => void;
  /** Callback when minimize-to-tray changes */
  onMinimizeToTrayChange: (value: boolean) => void;
  /** Callback when close action changes */
  onCloseActionChange: (value: CloseAction) => void;
  /** Callback when base path changes */
  onBasePathChange: (value: string) => void;
}

const closeActionOptions = [
  { value: CloseAction.ASK.toString(), label: "Ask" },
  { value: CloseAction.MINIMIZE_TO_TRAY.toString(), label: "Minimize to Tray" },
  { value: CloseAction.EXIT.toString(), label: "Exit" },
];

const fontOptions = [
  { value: ItemFont.EXOCET.toString(), label: "Exocet" },
  { value: ItemFont.CONSOLAS.toString(), label: "Consolas (monospace)" },
  { value: ItemFont.SYSTEM.toString(), label: "System Default" },
];

export function GeneralSettings({
  server,
  game,
  display,
  startup,
  engine,
  startMinimized,
  minimizeToTray,
  closeAction,
  basePath,
  onServerChange,
  onGameChange,
  onDisplayChange,
  onStartupChange,
  onEngineChange,
  onStartMinimizedChange,
  onMinimizeToTrayChange,
  onCloseActionChange,
  onBasePathChange,
}: GeneralSettingsProps) {
  const [showD2PathPicker, setShowD2PathPicker] = useState(false);
  const [showBasePathPicker, setShowBasePathPicker] = useState(false);

  return (
    <Card>
      <CardHeader
        title="General Configuration"
        description="Server connection, paths, and application settings."
      />
      <CardContent className="space-y-3">
        {/* Server settings & Game version */}
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <Input
            id="server-host"
            label="Host"
            tooltip="Address to listen on. Use 0.0.0.0 to allow remote connections."
            placeholder="localhost"
            autoComplete="off"
            value={server?.host || ""}
            onChange={(e) => onServerChange({ host: e.target.value })}
          />

          <Input
            id="server-port"
            label="Port"
            tooltip="Port for the web UI and gRPC connections."
            type="number"
            placeholder="50051"
            autoComplete="one-time-code"
            min={1}
            max={65535}
            value={server?.port?.toString() || ""}
            onChange={(e) => {
              const value = parseInt(e.target.value, 10);
              const port = isNaN(value)
                ? 0
                : Math.max(1, Math.min(65535, value));
              onServerChange({ port });
            }}
          />

          <PasswordInput
            id="server-password"
            label="Password"
            tooltip="Protects the web UI. Clients must authenticate to access controls."
            placeholder="Optional"
            value={server?.password || ""}
            onChange={(e) => onServerChange({ password: e.target.value })}
          />

          <Input
            id="game-version"
            label="Game Version"
            tooltip="Used only for selecting which memory patches to apply. Does not affect any other behavior."
            placeholder="1.14d"
            autoComplete="off"
            value={game?.gameVersion || ""}
            onChange={(e) => onGameChange({ gameVersion: e.target.value })}
          />
        </div>

        {/* App behavior & display */}
        <div className="grid grid-cols-1 items-end gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <div className="flex flex-col gap-2 pb-2">
            <label className="flex cursor-pointer items-center gap-3">
              <input
                type="checkbox"
                checked={startMinimized}
                onChange={(e) => onStartMinimizedChange(e.target.checked)}
                className="h-4 w-4 rounded border-zinc-600 bg-zinc-800 text-d2-gold focus:ring-d2-gold focus:ring-offset-zinc-900"
              />
              <span className="text-sm text-zinc-300">Start Minimized</span>
            </label>

            <label
              className="flex cursor-pointer items-center gap-3"
              title="When minimizing, hide to the system tray instead of the taskbar."
            >
              <input
                type="checkbox"
                checked={minimizeToTray}
                onChange={(e) => onMinimizeToTrayChange(e.target.checked)}
                className="h-4 w-4 rounded border-zinc-600 bg-zinc-800 text-d2-gold focus:ring-d2-gold focus:ring-offset-zinc-900"
              />
              <span className="text-sm text-zinc-300">Minimize to Tray</span>
            </label>
          </div>

          <Select
            id="close-action"
            label="On Close"
            tooltip="What happens when you click the close button on the desktop window."
            options={closeActionOptions}
            value={closeAction.toString()}
            onChange={(e) =>
              onCloseActionChange(parseInt(e.target.value, 10) as CloseAction)
            }
          />

          <label className="flex cursor-pointer items-center gap-3 pb-2">
            <input
              type="checkbox"
              checked={display?.showItemHeader ?? false}
              onChange={(e) =>
                onDisplayChange({ showItemHeader: e.target.checked })
              }
              className="h-4 w-4 rounded border-zinc-600 bg-zinc-800 text-d2-gold focus:ring-d2-gold focus:ring-offset-zinc-900"
            />
            <span className="text-sm text-zinc-300">Show Item Header</span>
          </label>

          <Select
            id="item-font"
            label="Item Font"
            tooltip="Font for rendering item name headers on the items page."
            options={fontOptions}
            value={(display?.itemFont ?? ItemFont.EXOCET).toString()}
            onChange={(e) =>
              onDisplayChange({
                itemFont: parseInt(e.target.value, 10) as ItemFont,
              })
            }
          />
        </div>

        {/* Paths */}
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <PathInput
            id="d2-install-path"
            label="Diablo II Install Path"
            tooltip="Default game directory for new profiles. Individual profiles can override this."
            placeholder="C:\Program Files\Diablo II"
            autoComplete="off"
            value={game?.d2InstallPath || ""}
            onChange={(e) => onGameChange({ d2InstallPath: e.target.value })}
            onBrowse={() => setShowD2PathPicker(true)}
          />

          <PathInput
            id="base-path"
            label="Base Path"
            tooltip="Root directory for bot data. The data/ folder (profiles, keys, schedules) and d2bs/ directory are read from here."
            placeholder="Directory for d2bs and application files"
            autoComplete="off"
            value={basePath}
            onChange={(e) => onBasePathChange(e.target.value)}
            onBrowse={() => setShowBasePathPicker(true)}
          />
        </div>

        {/* Game directory cleanup */}
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Input
            id="screenshot-retention-days"
            label="Screenshot Retention (days)"
            tooltip="Auto-delete Screenshot*.jpg files in the Diablo II install root older than this many days. 0 = disabled. Cleanup runs hourly."
            type="number"
            min={0}
            placeholder="0"
            autoComplete="off"
            value={game?.screenshotRetentionDays?.toString() ?? "0"}
            onChange={(e) => {
              const value = parseInt(e.target.value, 10);
              onGameChange({
                screenshotRetentionDays: isNaN(value) ? 0 : Math.max(0, value),
              });
            }}
          />

          <Input
            id="crash-log-retention-days"
            label="Crash Log Retention (days)"
            tooltip="Auto-delete BlizzardError subdirectories (game crash dumps) older than this many days. 0 = disabled. Cleanup runs hourly."
            type="number"
            min={0}
            placeholder="0"
            autoComplete="off"
            value={game?.crashLogRetentionDays?.toString() ?? "0"}
            onChange={(e) => {
              const value = parseInt(e.target.value, 10);
              onGameChange({
                crashLogRetentionDays: isNaN(value) ? 0 : Math.max(0, value),
              });
            }}
          />
        </div>

        {/* Startup pacing */}
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Input
            id="startup-concurrency"
            label="Max Profiles Starting At Once"
            tooltip="How many profiles can be starting at the same time. Extra profiles wait their turn. Combine with Startup Delay to space out logins. 0 = no limit."
            type="number"
            min={0}
            placeholder="0"
            autoComplete="off"
            value={startup?.concurrency?.toString() ?? "0"}
            onChange={(e) => {
              const value = parseInt(e.target.value, 10);
              onStartupChange({
                concurrency: isNaN(value) ? 0 : Math.max(0, value),
              });
            }}
          />

          <Input
            id="startup-delay"
            label="Startup Delay (milliseconds)"
            tooltip="How long each profile waits before launching, once it's its turn. Combine with Max Profiles Starting At Once to space out logins."
            type="number"
            min={0}
            placeholder="0"
            autoComplete="off"
            value={startup?.delayMs?.toString() ?? "0"}
            onChange={(e) => {
              const value = parseInt(e.target.value, 10);
              onStartupChange({
                delayMs: isNaN(value) ? 0 : Math.max(0, value),
              });
            }}
          />
        </div>

        {/* Crash recovery & heartbeat monitoring */}
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <Input
            id="heartbeat-timeout-seconds"
            label="Heartbeat Timeout (s)"
            tooltip="How long a bot can go without sending a heartbeat before it counts as a miss. Default 30."
            type="number"
            min={1}
            placeholder="30"
            autoComplete="off"
            value={(engine?.heartbeatTimeoutSeconds || 30).toString()}
            onChange={(e) => {
              const value = parseInt(e.target.value, 10);
              onEngineChange({
                heartbeatTimeoutSeconds: isNaN(value) ? 0 : Math.max(1, value),
              });
            }}
          />

          <Input
            id="max-missed-heartbeats"
            label="Missed Heartbeats"
            tooltip="Consecutive missed heartbeats before a bot is killed and restarted. Default 3."
            type="number"
            min={1}
            placeholder="3"
            autoComplete="off"
            value={(engine?.maxMissedHeartbeats || 3).toString()}
            onChange={(e) => {
              const value = parseInt(e.target.value, 10);
              onEngineChange({
                maxMissedHeartbeats: isNaN(value) ? 0 : Math.max(1, value),
              });
            }}
          />

          <Input
            id="max-crash-retries"
            label="Crash Retries"
            tooltip="How many times a bot is restarted after crashing before giving up and disabling its schedule. Default 5."
            type="number"
            min={1}
            placeholder="5"
            autoComplete="off"
            value={(engine?.maxCrashRetries || 5).toString()}
            onChange={(e) => {
              const value = parseInt(e.target.value, 10);
              onEngineChange({
                maxCrashRetries: isNaN(value) ? 0 : Math.max(1, value),
              });
            }}
          />

          <Input
            id="unresponsive-timeout-seconds"
            label="Unresponsive Timeout (s)"
            tooltip="Kill and restart a game whose window stops responding (frozen / hung) for this long, even if heartbeats still arrive. Default 30."
            type="number"
            min={1}
            placeholder="30"
            autoComplete="off"
            value={(engine?.unresponsiveTimeoutSeconds || 30).toString()}
            onChange={(e) => {
              const value = parseInt(e.target.value, 10);
              onEngineChange({
                unresponsiveTimeoutSeconds: isNaN(value)
                  ? 0
                  : Math.max(1, value),
              });
            }}
          />
        </div>
      </CardContent>

      <PathSelectorDialog
        open={showD2PathPicker}
        onClose={() => setShowD2PathPicker(false)}
        onSelect={(path) => {
          onGameChange({ d2InstallPath: path });
          setShowD2PathPicker(false);
        }}
        mode="directory"
        title="Select Diablo II Install Directory"
        initialPath={game?.d2InstallPath || ""}
      />

      <PathSelectorDialog
        open={showBasePathPicker}
        onClose={() => setShowBasePathPicker(false)}
        onSelect={(path) => {
          onBasePathChange(path);
          setShowBasePathPicker(false);
        }}
        mode="directory"
        title="Select Base Directory"
        initialPath={basePath}
      />
    </Card>
  );
}
