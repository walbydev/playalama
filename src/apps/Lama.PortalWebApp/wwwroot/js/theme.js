window.playalamaTheme = (function () {
  const themeKey = "playalama-web-theme";
  const densityKey = "playalama-web-density";

  function applyTheme(theme) {
    document.body.setAttribute("data-theme", theme);
  }

  function applyDensity(density) {
    document.body.setAttribute("data-density", density);
  }

  function currentTheme() {
    return document.body.getAttribute("data-theme") || "dark";
  }

  function currentDensity() {
    return document.body.getAttribute("data-density") || "comfortable";
  }

  function toggleTheme() {
    const next = currentTheme() === "dark" ? "light" : "dark";
    applyTheme(next);
    localStorage.setItem(themeKey, next);
  }

  function toggleDensity() {
    const next = currentDensity() === "compact" ? "comfortable" : "compact";
    applyDensity(next);
    localStorage.setItem(densityKey, next);
  }

  function boot() {
    const theme = localStorage.getItem(themeKey) || "dark";
    const density = localStorage.getItem(densityKey) || "comfortable";
    applyTheme(theme);
    applyDensity(density);
  }

  document.addEventListener("DOMContentLoaded", boot);

  return {
    toggleTheme,
    toggleDensity
  };
})();

