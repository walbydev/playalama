/* ═══════════════════════════════════════════════
   PLAYALAMA — app.js
   Thème, auth localStorage, animations légères
   ═══════════════════════════════════════════════ */

// ── Thèmes visuels ───────────────────────────────────────────────────────────
window.playalamaTheme = {
    availableThemes: ['dark', 'light', 'blue', 'green', 'vermillion'],
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

// ── Zoom du plateau ────────────────────────────────────────────────────────────
window.playalamaBoardZoom = {
    getZoom() {
        const raw = localStorage.getItem('playalama-board-zoom');
        if (!raw) return '100';
        if (raw === 'auto') return 'auto';

        const value = Number.parseInt(raw, 10);
        if (value === 100 || value === 125 || value === 150 || value === 200) return String(value);
        return '100';
    },
    setZoom(zoomMode) {
        const raw = String(zoomMode || '100');
        const value = Number.parseInt(raw, 10);
        const normalized = raw === 'auto'
            ? 'auto'
            : (value === 125 || value === 150 || value === 200 ? String(value) : '100');
        localStorage.setItem('playalama-board-zoom', normalized);
        return normalized;
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
