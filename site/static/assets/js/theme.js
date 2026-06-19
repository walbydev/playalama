(function () {
  const STORAGE_KEY = "playalama-theme";

  function safeGetStoredTheme() {
    try {
      return localStorage.getItem(STORAGE_KEY);
    } catch {
      return null;
    }
  }

  function safeSetStoredTheme(theme) {
    try {
      localStorage.setItem(STORAGE_KEY, theme);
    } catch {
      // Ignore storage errors in private browsing contexts.
    }
  }

  function preferredTheme() {
    const stored = safeGetStoredTheme();
    if (stored === "light" || stored === "dark") {
      return stored;
    }

    return window.matchMedia("(prefers-color-scheme: light)").matches ? "light" : "dark";
  }

  function applyTheme(theme) {
    document.body.setAttribute("data-theme", theme);
    document.querySelectorAll("[data-theme-toggle]").forEach((button) => {
      const nextTheme = theme === "dark" ? "light" : "dark";
      button.setAttribute("aria-pressed", String(theme === "light"));
      button.setAttribute("aria-label", "Basculer le theme");
      button.textContent = nextTheme === "dark" ? "Mode sombre" : "Mode clair";
    });
  }

  function toggleTheme() {
    const current = document.body.getAttribute("data-theme") || "dark";
    const next = current === "dark" ? "light" : "dark";
    applyTheme(next);
    safeSetStoredTheme(next);
  }

  document.addEventListener("DOMContentLoaded", () => {
    applyTheme(preferredTheme());
    document.querySelectorAll("[data-theme-toggle]").forEach((button) => {
      button.addEventListener("click", toggleTheme);
    });
  });
})();

