window.playalamaProfile = (function () {
  const key = "playalama-web-profile";

  function save(profile) {
    localStorage.setItem(key, JSON.stringify(profile || {}));
  }

  function load() {
    const raw = localStorage.getItem(key);
    if (!raw) {
      return null;
    }

    try {
      return JSON.parse(raw);
    } catch {
      return null;
    }
  }

  return { save, load };
})();

