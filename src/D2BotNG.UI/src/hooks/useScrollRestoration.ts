import { useEffect } from "react";
import { useLocation } from "react-router-dom";

const STORAGE_PREFIX = "d2bot-scroll:";
// Stop trying to restore after this long. A page that's permanently shorter
// than the saved scroll position (e.g. items deleted while away) would
// otherwise leave us stuck never persisting subsequent user scrolls.
const RESTORE_TIMEOUT_MS = 2000;

/**
 * Save and restore the scroll position of the layout's <main> element keyed
 * by current pathname. React Router's built-in <ScrollRestoration /> only
 * tracks window scroll, but the layout uses an internal scroll container —
 * so this hook fills the gap. Call once per page that should preserve scroll.
 */
export function useScrollRestoration() {
  const { pathname } = useLocation();

  useEffect(() => {
    const main = document.querySelector("main");
    if (!main) return;

    const key = STORAGE_PREFIX + pathname;
    const saved = sessionStorage.getItem(key);
    const parsed = saved !== null ? parseInt(saved, 10) : NaN;
    const target = Number.isFinite(parsed) ? parsed : null;

    // Don't persist new positions until restoration completes — the
    // browser auto-clamps scrollTop to 0 while content is short (e.g.
    // a loading spinner is showing), and that bogus 0 would otherwise
    // overwrite the saved position before the real content mounts.
    let restored = target === null;

    const finish = () => {
      if (restored) return;
      restored = true;
      observer.disconnect();
      clearTimeout(timer);
    };

    const tryRestore = () => {
      if (restored || target === null) return;
      main.scrollTop = target;
      // The browser clamps to (scrollHeight - clientHeight); when content
      // hasn't mounted yet that's a smaller value than target. Wait for
      // the next ResizeObserver fire and try again.
      if (Math.abs(main.scrollTop - target) <= 1) finish();
    };

    const observer = new ResizeObserver(tryRestore);
    const timer = setTimeout(finish, RESTORE_TIMEOUT_MS);
    observer.observe(main);
    tryRestore();

    const handleScroll = () => {
      if (restored) sessionStorage.setItem(key, String(main.scrollTop));
    };
    main.addEventListener("scroll", handleScroll, { passive: true });

    return () => {
      clearTimeout(timer);
      observer.disconnect();
      main.removeEventListener("scroll", handleScroll);
    };
  }, [pathname]);
}
