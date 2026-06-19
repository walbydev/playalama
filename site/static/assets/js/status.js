(function () {
  const HEALTH_PATH = "/health";
  const POLL_INTERVAL_MS = 30000;
  const REQUEST_TIMEOUT_MS = 3500;

  function setStatus(state, text) {
    const statusRoots = document.querySelectorAll("[data-server-status]");
    statusRoots.forEach((root) => {
      root.setAttribute("data-state", state);
      const label = root.querySelector("[data-server-status-label]");
      if (label) {
        label.textContent = text;
      }
    });
  }

  async function checkServer() {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS);

    try {
      const response = await fetch(HEALTH_PATH, {
        method: "GET",
        cache: "no-store",
        signal: controller.signal
      });

      if (response.ok) {
        setStatus("online", "En ligne");
      } else {
        setStatus("offline", "Hors ligne");
      }
    } catch {
      setStatus("offline", "Hors ligne");
    } finally {
      clearTimeout(timeout);
    }
  }

  document.addEventListener("DOMContentLoaded", () => {
    setStatus("checking", "Verification...");
    checkServer();
    window.setInterval(checkServer, POLL_INTERVAL_MS);
  });
})();

