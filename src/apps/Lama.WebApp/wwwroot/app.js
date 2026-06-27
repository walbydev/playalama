/* ═══════════════════════════════════════════════
   PLAYALAMA — app.js
   Thème, auth localStorage, animations légères
   ═══════════════════════════════════════════════ */

// ── Thème dark/light ─────────────────────────────────────────────────────────
window.playalamaTheme = {
    getTheme() {
        return localStorage.getItem('playalama-theme') || 'dark';
    },
    setTheme(theme) {
        localStorage.setItem('playalama-theme', theme);
        document.documentElement.classList.remove('dark', 'light');
        document.documentElement.classList.add(theme);
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
        const value = raw ? Number.parseInt(raw, 10) : 100;
        return value === 125 || value === 150 ? value : 100;
    },
    setZoom(zoomPercent) {
        const value = Number.parseInt(zoomPercent, 10);
        const normalized = value === 125 || value === 150 ? value : 100;
        localStorage.setItem('playalama-board-zoom', String(normalized));
        return normalized;
    }
};

// Appliquer le thème immédiatement (évite le flash)
(function () {
    const theme = localStorage.getItem('playalama-theme') || 'dark';
    document.documentElement.classList.remove('dark', 'light');
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
(function initFloatingLetters() {
    const container = document.getElementById('floating-letters');
    if (!container) return;

    // Respecter prefers-reduced-motion
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;

    const letters = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';
    const count = 18;
    for (let i = 0; i < count; i++) {
        const el = document.createElement('span');
        el.className = 'fl';
        el.textContent = letters[Math.floor(Math.random() * letters.length)];
        el.style.left = Math.random() * 100 + '%';
        el.style.animationDelay = -(Math.random() * 18) + 's';
        el.style.animationDuration = (14 + Math.random() * 10) + 's';
        el.style.fontSize = (3 + Math.random() * 5) + 'rem';
        container.appendChild(el);
    }

    // Re-init si navigation Blazor (SPA)
    document.addEventListener('blazor:navigated', () => {
        const c = document.getElementById('floating-letters');
        if (c && c.children.length === 0) initFloatingLetters();
    });
})();
