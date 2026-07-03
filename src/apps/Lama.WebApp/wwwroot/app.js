/* ═══════════════════════════════════════════════
   PLAYALAMA — app.js
   Thème, auth localStorage, animations légères
   ═══════════════════════════════════════════════ */

// ── Thèmes visuels ───────────────────────────────────────────────────────────
window.playalamaTheme = {
    availableThemes: ['dark', 'light', 'blue', 'green', 'vermillion', 'highcontrast', 'deuteranopia', 'protanopia', 'tritanopia'],
    getTheme() {
        const stored = localStorage.getItem('playalama-theme') || 'dark';
        return this.availableThemes.includes(stored) ? stored : 'dark';
    },
    setTheme(theme) {
        const normalized = this.availableThemes.includes(theme) ? theme : 'dark';
        localStorage.setItem('playalama-theme', normalized);
        document.documentElement.classList.remove(...this.availableThemes);
        document.documentElement.classList.add(normalized);
    },
    toggleTheme() {
        const current = this.getTheme();
        this.setTheme(current === 'dark' ? 'light' : 'dark');
        return current === 'dark' ? 'light' : 'dark';
    }
};

// ── Accessibilité : Taille de police globale ─────────────────────────────────
window.playalamaAccessibility = {
    fontSizes: ['100', '125', '150', '200'],
    getFontSize() {
        const stored = localStorage.getItem('playalama-font-size') || '100';
        return this.fontSizes.includes(stored) ? stored : '100';
    },
    setFontSize(size) {
        const normalized = this.fontSizes.includes(size) ? size : '100';
        localStorage.setItem('playalama-font-size', normalized);
        document.documentElement.setAttribute('data-font-size', normalized);
    },
    increaseFontSize() {
        const current = this.getFontSize();
        const idx = this.fontSizes.indexOf(current);
        const next = idx < this.fontSizes.length - 1 ? this.fontSizes[idx + 1] : current;
        this.setFontSize(next);
        return next;
    },
    decreaseFontSize() {
        const current = this.getFontSize();
        const idx = this.fontSizes.indexOf(current);
        const prev = idx > 0 ? this.fontSizes[idx - 1] : current;
        this.setFontSize(prev);
        return prev;
    }
};

// Appliquer la taille de police au chargement
(function () {
    const fontSize = window.playalamaAccessibility.getFontSize();
    document.documentElement.setAttribute('data-font-size', fontSize);
})();

// ── Disposition du plateau (densité S/M/L, plein écran, panneaux) ───────────────
window.playalamaGameLayout = {
    get() {
        try {
            const raw = localStorage.getItem('playalama-game-layout');
            if (!raw) return { density: 'm', fullscreen: false, collapsed: [], activeTab: 'play', variant: 'a' };
            const parsed = JSON.parse(raw);
            return {
                density: ['s', 'm', 'l'].includes(parsed.density) ? parsed.density : 'm',
                fullscreen: !!parsed.fullscreen,
                collapsed: Array.isArray(parsed.collapsed) ? parsed.collapsed : [],
                activeTab: ['scores', 'play', 'messages'].includes(parsed.activeTab) ? parsed.activeTab : 'play',
                variant: ['a', 'b', 'c', 'd'].includes(parsed.variant) ? parsed.variant : 'a'
            };
        } catch { return { density: 'm', fullscreen: false, collapsed: [], activeTab: 'play', variant: 'a' }; }
    },
    set(state) {
        try { localStorage.setItem('playalama-game-layout', JSON.stringify(state)); } catch { }
    },
    setFullscreen(on) {
        try {
            if (on && document.documentElement.requestFullscreen) {
                document.documentElement.requestFullscreen().catch(() => { });
            } else if (!on && document.fullscreenElement && document.exitFullscreen) {
                document.exitFullscreen().catch(() => { });
            }
        } catch { }
    }
};

// Appliquer le thème immédiatement (évite le flash)
(function () {
    const theme = window.playalamaTheme.getTheme();
    document.documentElement.classList.remove(...window.playalamaTheme.availableThemes);
    document.documentElement.classList.add(theme);
})();

// ── Auth localStorage ─────────────────────────────────────────────────────────
window.playalamaAuth = {
    saveSession(session) {
        localStorage.setItem('playalama-session', JSON.stringify(session));
    },
    loadSession() {
        try {
            const raw = localStorage.getItem('playalama-session');
            return raw ? JSON.parse(raw) : null;
        } catch { return null; }
    },
    clearSession() {
        localStorage.removeItem('playalama-session');
    }
};

// ── Lettres flottantes (Hero) ────────────────────────────────────────────────
window.playalamaFloatingLetters = (function () {
    const LETTERS = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';
    const COUNT = 18;

    function populate() {
        const container = document.getElementById('floating-letters');
        if (!container || container.children.length > 0) return;

        // Respecter prefers-reduced-motion
        if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;

        for (let i = 0; i < COUNT; i++) {
            const el = document.createElement('span');
            el.className = 'fl';
            el.textContent = LETTERS[Math.floor(Math.random() * LETTERS.length)];
            el.style.left = Math.random() * 100 + '%';
            el.style.animationDelay = -(Math.random() * 18) + 's';
            el.style.animationDuration = (14 + Math.random() * 10) + 's';
            el.style.fontSize = (3 + Math.random() * 5) + 'rem';
            container.appendChild(el);
        }
    }

    // Premier rendu (DOM peut ne pas être prêt quand le script se charge)
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', populate);
    } else {
        populate();
    }

    // Re-init après navigation Blazor (SPA) — toujours enregistré
    document.addEventListener('blazor:navigated', populate);

    // Blazor (InteractiveServer) ré-hydrate et remplace le DOM prérendu,
    // ce qui efface les lettres injectées. On repeuple tant que le hero
    // est présent mais vide, pendant quelques secondes après chargement.
    let ticks = 0;
    const guard = setInterval(() => {
        populate();
        if (++ticks >= 20) clearInterval(guard);
    }, 250);

    return { populate };
})();

// Localisation : persiste la culture via cookie .AspNetCore.Culture + recharge
window.playalamaLang = {
  get: function () {
    const m = document.cookie.match(/(?:^|; )\.AspNetCore\.Culture=([^;]+)/);
    if (!m) return null;
    try {
      const v = decodeURIComponent(m[1]);
      const p = v.match(/c=([^|]+)/);
      return p ? p[1] : null;
    } catch { return null; }
  },
  set: function (culture) {
    const val = 'c=' + culture + '|uic=' + culture;
    document.cookie = '.AspNetCore.Culture=' + encodeURIComponent(val) + ';path=/;max-age=31536000';
    location.reload();
  }
};

// ── Debug utilities (development only) ───────────────────────────────────────
window.playalamaDebug = {
  downloadJson: function (filename, content) {
    const blob = new Blob([content], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  },
  copyToClipboard: async function (text) {
    await navigator.clipboard.writeText(text);
  }
};
