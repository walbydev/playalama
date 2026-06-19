(function () {
  const fallbackReleases = {
    "1.0.0": {
      label: "1.0.0",
      platforms: {
        "linux-x64": { file: "lama-1.0.0-linux-x64.zip", size: "33 Mo", name: "Linux x64" },
        "linux-arm64": { file: "lama-1.0.0-linux-arm64.zip", size: "31 Mo", name: "Linux arm64" },
        "win-x64": { file: "lama-1.0.0-win-x64.zip", size: "33 Mo", name: "Windows x64" },
        "win-arm64": { file: "lama-1.0.0-win-arm64.zip", size: "32 Mo", name: "Windows arm64" },
        "osx-x64": { file: "lama-1.0.0-osx-x64.zip", size: "33 Mo", name: "macOS x64" },
        "osx-arm64": { file: "lama-1.0.0-osx-arm64.zip", size: "31 Mo", name: "macOS arm64" }
      }
    }
  };

  function isAndroid() {
    return /android/.test((navigator.userAgent || "").toLowerCase());
  }

  function detectRecommendedRid() {
    const ua = navigator.userAgent || "";
    const uaDataPlatform = navigator.userAgentData?.platform || "";
    const uaDataArch = navigator.userAgentData?.architecture || "";
    const platformText = `${ua} ${uaDataPlatform}`.toLowerCase();
    const archText = `${ua} ${uaDataArch}`.toLowerCase();

    const isArm = /arm|aarch64|apple/.test(archText) || /iphone|ipad/.test(platformText);
    const isMac = /mac os x|macintosh|macos/.test(platformText);
    const isWindows = /win/.test(platformText);
    const isLinux = /linux|x11/.test(platformText) && !isAndroid();

    if (isMac) return isArm ? "osx-arm64" : "osx-x64";
    if (isWindows) return isArm ? "win-arm64" : "win-x64";
    if (isLinux) return isArm ? "linux-arm64" : "linux-x64";
    return "linux-x64";
  }

  async function loadReleases() {
    try {
      const response = await fetch("/assets/data/releases.json", { cache: "no-cache" });
      if (!response.ok) {
        throw new Error(`Failed to load releases JSON: ${response.status}`);
      }

      const payload = await response.json();
      if (!payload || typeof payload !== "object" || !payload.releases || typeof payload.releases !== "object") {
        throw new Error("Invalid releases JSON format");
      }

      return payload.releases;
    } catch {
      return fallbackReleases;
    }
  }

  document.addEventListener("DOMContentLoaded", async () => {
    const versionSelect = document.getElementById("version-select");
    const channelSelect = document.getElementById("channel-select");
    const cards = Array.from(document.querySelectorAll(".download-card"));
    const primaryDownload = document.getElementById("primary-download");
    const recommendation = document.getElementById("recommendation");

    if (!versionSelect || !channelSelect || cards.length === 0) {
      return;
    }

    const releaseVersions = Object.keys(releases);
    if (releaseVersions.length > 0) {
      versionSelect.innerHTML = "";
      releaseVersions.sort((a, b) => b.localeCompare(a, undefined, { numeric: true })).forEach((version) => {
        const option = document.createElement("option");
        option.value = version;
        option.textContent = version;
        versionSelect.appendChild(option);
      });
    }

    function baseUrl() {
      return channelSelect.value === "subdomain"
        ? "https://downloads.playalama.online"
        : "https://playalama.online/downloads";
    }

    const releases = await loadReleases();

    function updateCards() {
      const version = versionSelect.value;
      const release = releases[version] || fallbackReleases[version] || fallbackReleases["1.0.0"];
      const recommendedRid = detectRecommendedRid();

      cards.forEach((card) => {
        const rid = card.dataset.rid;
        const platform = release.platforms[rid];
        const href = `${baseUrl()}/${platform.file}`;

        card.querySelector(".version-label").textContent = release.label;
        card.querySelector(".size-label").textContent = platform.size;
        card.querySelector(".filename-label").textContent = platform.file;

        const link = card.querySelector("[data-link]");
        link.href = href;
        link.setAttribute("download", platform.file);

        card.classList.toggle("recommended", rid === recommendedRid);
      });

      const selected = release.platforms[recommendedRid] || release.platforms["linux-x64"];
      const href = `${baseUrl()}/${selected.file}`;
      primaryDownload.href = href;
      primaryDownload.setAttribute("download", selected.file);
      primaryDownload.textContent = `Telecharger ${selected.name}`;
      recommendation.textContent = `Version recommandee: ${selected.name} (${release.label})`;
    }

    versionSelect.addEventListener("change", updateCards);
    channelSelect.addEventListener("change", updateCards);
    updateCards();
  });
})();

