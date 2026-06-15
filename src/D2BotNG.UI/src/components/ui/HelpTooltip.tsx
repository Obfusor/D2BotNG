import { QuestionMarkCircleIcon } from "@heroicons/react/24/outline";
import { Tooltip } from "./Tooltip";

export interface HelpTooltipProps {
  /** The help text to display on hover */
  text: string;
  /** Additional CSS classes */
  className?: string;
}

/** Question-mark icon that shows `text` on hover/tap via the shared, viewport-aware Tooltip. */
export function HelpTooltip({ text, className }: HelpTooltipProps) {
  return (
    <Tooltip content={text} className={className}>
      <QuestionMarkCircleIcon
        className="h-4 w-4 cursor-help text-zinc-500 hover:text-zinc-400 transition-colors"
        aria-hidden="true"
      />
    </Tooltip>
  );
}
