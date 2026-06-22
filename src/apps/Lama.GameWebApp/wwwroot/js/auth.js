window.playalamaAuth = (function () {
  const SESSION_KEY = "playalama-session";

  function saveSession(session) {
    try {
      localStorage.setItem(SESSION_KEY, JSON.stringify(session || {}));
    } catch (e) {
      console.warn("playalamaAuth: impossible de sauvegarder la session", e);
    }
  }

  function loadSession() {
    try {
      const raw = localStorage.getItem(SESSION_KEY);
      if (!raw) return null;
      return JSON.parse(raw);
    } catch (e) {
      return null;
    }
  }

  function clearSession() {
    try {
      localStorage.removeItem(SESSION_KEY);
    } catch (e) {}
  }

  return { saveSession, loadSession, clearSession };
})();

