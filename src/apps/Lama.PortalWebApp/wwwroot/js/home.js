window.homeAnimations = (function () {

    /* ── PLATEAU ── */
    const BOARD_SIZE = 15;
    const SPECIALS = {
        '7,7': 'star',
        '0,0': 'tw', '0,7': 'tw', '0,14': 'tw', '7,0': 'tw', '7,14': 'tw',
        '14,0': 'tw', '14,7': 'tw', '14,14': 'tw',
        '1,1': 'dw', '2,2': 'dw', '3,3': 'dw', '4,4': 'dw',
        '1,13': 'dw', '2,12': 'dw', '3,11': 'dw', '4,10': 'dw',
        '13,1': 'dw', '12,2': 'dw', '11,3': 'dw', '10,4': 'dw',
        '13,13': 'dw', '12,12': 'dw', '11,11': 'dw', '10,10': 'dw',
        '5,1': 'tl', '5,5': 'tl', '5,9': 'tl', '5,13': 'tl',
        '9,1': 'tl', '9,5': 'tl', '9,9': 'tl', '9,13': 'tl',
        '1,5': 'tl', '3,7': 'dl', '7,3': 'dl', '7,11': 'dl', '11,7': 'dl',
    };

    const DEMO_WORDS = [
        { word: 'QUARTZ', row: 7, col: 4, dir: 'h' },
        { word: 'VEXE',   row: 4, col: 8, dir: 'v' },
        { word: 'JIFFY',  row: 3, col: 10, dir: 'v' },
        { word: 'ZEPHYR', row: 9, col: 5, dir: 'h' },
        { word: 'BLOX',   row: 5, col: 11, dir: 'v' },
    ];

    const LETTER_PTS = {
        A:1,B:3,C:3,D:2,E:1,F:4,G:2,H:4,I:1,J:8,
        K:10,L:1,M:2,N:1,O:1,P:3,Q:8,R:1,S:1,T:1,
        U:1,V:4,W:10,X:10,Y:10,Z:10
    };

    let _boardTimer = null;

    function buildBoard() {
        const grid = document.getElementById('board-grid');
        if (!grid) return false;
        grid.innerHTML = '';
        for (let r = 0; r < BOARD_SIZE; r++) {
            for (let c = 0; c < BOARD_SIZE; c++) {
                const cell = document.createElement('div');
                cell.className = 'cell';
                cell.id = `c-${r}-${c}`;
                const sp = SPECIALS[`${r},${c}`];
                if (sp) {
                    cell.classList.add(sp);
                    cell.textContent = { tw: '3W', dw: '2W', tl: '3L', dl: '2L', star: '★' }[sp] || '';
                }
                grid.appendChild(cell);
            }
        }
        return true;
    }

    function placeTiles(wordObj) {
        const { word, row, col, dir } = wordObj;
        word.split('').forEach((letter, i) => {
            const r = dir === 'h' ? row : row + i;
            const c = dir === 'h' ? col + i : col;
            const cell = document.getElementById(`c-${r}-${c}`);
            if (!cell) return;
            setTimeout(() => {
                cell.className = 'cell tile placed';
                cell.innerHTML = `${letter}<span class="pts">${LETTER_PTS[letter] || 1}</span>`;
            }, i * 120);
        });
    }

    function animateBoard() {
        if (!buildBoard()) return;
        let delay = 0;
        DEMO_WORDS.forEach(w => {
            setTimeout(() => placeTiles(w), delay);
            delay += w.word.length * 120 + 800;
        });
        _boardTimer = setTimeout(animateBoard, delay + 2000);
    }

    /* ── TERMINAL ── */
    const TERM_LINES = [
        { text: '$ playalama --version',              color: '#22c55e', delay: 400  },
        { text: 'Playalama CLI v2.4.1',               color: '#94a3b8', delay: 900  },
        { text: '$ playalama join --quick',            color: '#22c55e', delay: 1600 },
        { text: "\u{1F50D} Recherche d'une partie...", color: '#64748b', delay: 2200 },
        { text: '\u2713 Partie trouv\u00e9e\u00a0: Room #8821', color: '#c084fc', delay: 3000 },
        { text: '\u{1F464} Adversaire\u00a0: MotZard (ELO 1842)', color: '#94a3b8', delay: 3600 },
        { text: '', color: '', delay: 4000 },
        { text: '  Plateau 15\u00d715 \u00b7 Mode Classique', color: '#64748b', delay: 4200 },
        { text: '  Langue\u00a0: Fran\u00e7ais (ODS8)',         color: '#64748b', delay: 4500 },
        { text: '', color: '', delay: 4700 },
        { text: '  Tes tuiles\u00a0: Q U A R T Z X',            color: '#f59e0b', delay: 5000 },
        { text: '', color: '', delay: 5300 },
        { text: '$ pos H8 horizontal QUARTZ',          color: '#22c55e', delay: 5800 },
        { text: '\u2713 QUARTZ pos\u00e9\u00a0! +84 pts',       color: '#c084fc', delay: 6500 },
        { text: '  Score\u00a0: Toi 84 \u00b7 MotZard 0',       color: '#94a3b8', delay: 7000 },
        { text: '', color: '', delay: 7300 },
        { text: '\u23f3 Tour de MotZard...',            color: '#64748b', delay: 7600 },
        { text: '  MotZard joue VEXE +45 pts',          color: '#f472b6', delay: 8400 },
        { text: '  Score\u00a0: Toi 84 \u00b7 MotZard 45',      color: '#94a3b8', delay: 8900 },
    ];

    let _termTimer = null;

    function runTerminal() {
        const term = document.getElementById('hp-terminal');
        if (!term) return;
        term.innerHTML = '';
        TERM_LINES.forEach(line => {
            setTimeout(() => {
                if (!document.getElementById('hp-terminal')) return;
                const el = document.createElement('div');
                el.style.cssText = `color:${line.color || '#94a3b8'};font-size:.8rem;line-height:1.7;font-family:monospace;padding:0 1rem;min-height:1.2rem;`;
                el.textContent = line.text;
                term.appendChild(el);
                term.scrollTop = term.scrollHeight;
            }, line.delay);
        });
        _termTimer = setTimeout(runTerminal, 10500);
    }

    /* ── COMPTEURS ── */
    function animateCounter(el, target, suffix) {
        const step = Math.ceil(target / 60);
        let current = 0;
        const timer = setInterval(() => {
            current = Math.min(current + step, target);
            el.textContent = current.toLocaleString('fr-FR') + suffix;
            if (current >= target) clearInterval(timer);
        }, 25);
    }

    function initCounters() {
        const stats = document.querySelector('.hp-hero-stats');
        if (!stats) return;
        const observer = new IntersectionObserver(entries => {
            entries.forEach(entry => {
                if (!entry.isIntersecting) return;
                entry.target.querySelectorAll('[data-count]').forEach(el => {
                    animateCounter(el, parseInt(el.dataset.count), el.dataset.suffix || '');
                });
                observer.unobserve(entry.target);
            });
        }, { threshold: 0.3 });
        observer.observe(stats);
    }

    /* ── NAVBAR SCROLL ── */
    function initNavScroll() {
        const nav = document.getElementById('navbar');
        if (!nav) return;
        window.addEventListener('scroll', () => {
            nav.classList.toggle('scrolled', window.scrollY > 40);
        }, { passive: true });
    }

    /* ── LETTRES FLOTTANTES ── */
    function initFloatingLetters() {
        const container = document.querySelector('.hp-floating-letters');
        if (!container || container.childElementCount > 0) return;
        const letters = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';
        for (let i = 0; i < 18; i++) {
            const el = document.createElement('span');
            el.className = 'hp-fl';
            el.textContent = letters[Math.floor(Math.random() * letters.length)];
            el.style.left = (Math.random() * 100) + '%';
            el.style.animationDuration = (8 + Math.random() * 14) + 's';
            el.style.animationDelay = (-Math.random() * 20) + 's';
            container.appendChild(el);
        }
    }

    /* ── COPY ── */
    function copyText(text) {
        navigator.clipboard.writeText(text).catch(err => console.warn('Copy failed:', err));
    }

    /* ── INIT ── */
    function init() {
        if (_boardTimer) { clearTimeout(_boardTimer); _boardTimer = null; }
        if (_termTimer)  { clearTimeout(_termTimer);  _termTimer  = null; }
        animateBoard();
        runTerminal();
        initCounters();
        initNavScroll();
        initFloatingLetters();
    }

    return { init, copyText };

})();
