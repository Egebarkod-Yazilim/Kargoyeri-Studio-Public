/* ============================================================
   Kargoyeri — site.js
   Hafif, dependency'siz UI davranislar
   ============================================================ */

(function () {
    'use strict';

    /* ---- Cancel confirm -------------------------------------------------- */
    // <form data-confirm="Emin misiniz?"> ile calisir
    document.addEventListener('submit', function (e) {
        var form = e.target;
        var msg = form.getAttribute('data-confirm');
        if (msg && !window.confirm(msg)) {
            e.preventDefault();
        }
    });

    /* ---- Ping butonu — loading state ------------------------------------ */
    // Provider Configure sayfasindaki "Baglanti Test Et" butonuna spinner ekler
    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (!form.classList.contains('ping-form')) return;
        var btn = form.querySelector('button[type="submit"]');
        if (!btn) return;
        btn.disabled = true;
        btn.textContent = 'Test ediliyor…';
    });

    /* ---- Form submit — generic loading state ---------------------------- */
    // data-loading="..." attribute'u olan butonlar icin
    document.addEventListener('click', function (e) {
        var btn = e.target.closest('button[data-loading]');
        if (!btn) return;
        var form = btn.closest('form');
        if (!form) return;
        // Form zaten valid mi kontrol etmek icin kisa gecikme
        setTimeout(function () {
            if (form.checkValidity && !form.checkValidity()) return;
            btn.disabled = true;
            var label = btn.getAttribute('data-loading');
            if (label) btn.textContent = label;
        }, 50);
    });

    /* ---- Flash mesaji — otomatik kapat ---------------------------------- */
    // 6 saniye sonra flash kutusunu soluktur
    var flashes = document.querySelectorAll('.flash:not(.flash--warn)');
    flashes.forEach(function (el) {
        setTimeout(function () {
            el.style.transition = 'opacity 0.6s ease';
            el.style.opacity = '0';
            setTimeout(function () { el.style.display = 'none'; }, 700);
        }, 6000);
    });

    /* ---- Details acik kaldiysa kapat butonu ----------------------------- */
    // <button data-close-details="cancel-details"> ile calisir
    document.addEventListener('click', function (e) {
        var btn = e.target.closest('[data-close-details]');
        if (!btn) return;
        var id = btn.getAttribute('data-close-details');
        var details = document.getElementById(id);
        if (details) details.removeAttribute('open');
    });

    /* ---- Tablo satiri tiklama — detail sayfasina git ------------------- */
    // <tr data-href="/shipments/detail?..."> ile calisir
    document.addEventListener('click', function (e) {
        var row = e.target.closest('tr[data-href]');
        if (!row) return;
        // Linke ya da butona tiklama ise row davranisini atlat
        if (e.target.closest('a, button, form')) return;
        window.location.href = row.getAttribute('data-href');
    });

    /* ---- Sidebar aktif item scroll ------------------------------------ */
    var active = document.querySelector('.menu .menu-active');
    if (active) active.scrollIntoView({ block: 'nearest' });

    /* ============================================================
       P3-#3 — Toast notification
       ============================================================ */
    function dismissToast(t) {
        if (!t || t.classList.contains('is-closing')) return;
        t.classList.add('is-closing');
        setTimeout(function () { if (t.parentNode) t.parentNode.removeChild(t); }, 280);
    }
    document.querySelectorAll('.toast-container .toast').forEach(function (t) {
        var closeBtn = t.querySelector('.toast-close');
        if (closeBtn) closeBtn.addEventListener('click', function () { dismissToast(t); });
        if (!t.classList.contains('toast--error') && !t.classList.contains('toast--warning')) {
            setTimeout(function () { dismissToast(t); }, 4500);
        }
    });
    // Public API: window.studioToast(msg, type)
    window.studioToast = function (msg, type) {
        type = (type || 'info').toLowerCase();
        var mount = document.querySelector('[data-toast-mount]');
        if (!mount) return;
        var icons = { success: '\u2713', warning: '\u26A0', error: '\u2716', info: '\u2139' };
        var el = document.createElement('div');
        el.className = 'toast toast--' + type;
        el.setAttribute('role', 'status');
        el.innerHTML = '<span class="toast-icon">' + (icons[type] || icons.info) +
                       '</span><span class="toast-msg"></span>' +
                       '<button type="button" class="toast-close" aria-label="Kapat">&times;</button>';
        el.querySelector('.toast-msg').textContent = msg;
        el.querySelector('.toast-close').addEventListener('click', function () { dismissToast(el); });
        mount.appendChild(el);
        if (type !== 'error' && type !== 'warning') {
            setTimeout(function () { dismissToast(el); }, 4500);
        }
    };

    /* ============================================================
       P3-#7 — Dark mode toggle
       ============================================================ */
    function setCookie(name, value, days) {
        var d = new Date();
        d.setTime(d.getTime() + (days * 864e5));
        document.cookie = name + '=' + encodeURIComponent(value) + ';expires=' + d.toUTCString() + ';path=/;SameSite=Lax';
    }
    document.querySelectorAll('[data-theme-toggle]').forEach(function (btn) {
        btn.addEventListener('click', function () {
            var html = document.documentElement;
            var current = html.getAttribute('data-theme') || 'light';
            var next = current === 'dark' ? 'light' : 'dark';
            html.setAttribute('data-theme', next);
            setCookie('studio-theme', next, 365);
            var label = btn.querySelector('.theme-toggle-label');
            if (label) label.textContent = next === 'dark' ? 'Gündüz teması' : 'Gece teması';
        });
    });

    /* ---- Workspace tabs ------------------------------------------------- */
    (function () {
        var host = document.querySelector('[data-workspace-tabs]');
        if (!host) return;

        var scope = host.getAttribute('data-scope') || 'default';
        var storageKey = 'kargoyeri-open-tabs:' + scope;
        var currentHref = window.location.pathname + window.location.search;
        var currentTitle = (document.querySelector('.content .topbar h1, .emb-content h1') || {}).textContent;
        currentTitle = (currentTitle || document.title.replace(/\s*[—-]\s*Kargoyeri.*$/i, '')).trim() || 'Sayfa';
        var tabs = [];
        try {
            var stored = JSON.parse(window.localStorage.getItem(storageKey) || '[]');
            if (Array.isArray(stored)) tabs = stored.filter(function (tab) {
                return tab && typeof tab.href === 'string' && typeof tab.title === 'string' && tab.href.charAt(0) === '/';
            });
        } catch (_) { tabs = []; }

        var current = tabs.find(function (tab) { return tab.href === currentHref; });
        if (current) current.title = currentTitle;
        else tabs.push({ href: currentHref, title: currentTitle });
        if (tabs.length > 16) tabs = tabs.slice(-16);

        function save() {
            try { window.localStorage.setItem(storageKey, JSON.stringify(tabs)); } catch (_) { }
        }

        function navigateAfterClose(closedHref, oldIndex) {
            if (closedHref !== currentHref) return;
            var next = tabs[Math.min(oldIndex, tabs.length - 1)] || tabs[tabs.length - 1];
            window.location.assign(next ? next.href : '/');
        }

        function render() {
            host.innerHTML = '';
            var list = document.createElement('div');
            list.className = 'workspace-tabs-list';
            list.setAttribute('role', 'tablist');

            tabs.forEach(function (tab) {
                var item = document.createElement('div');
                item.className = 'workspace-tab-item' + (tab.href === currentHref ? ' is-active' : '');
                var link = document.createElement('a');
                link.className = 'workspace-tab';
                link.href = tab.href;
                link.textContent = tab.title;
                link.setAttribute('role', 'tab');
                if (tab.href === currentHref) link.setAttribute('aria-current', 'page');

                var close = document.createElement('button');
                close.type = 'button';
                close.className = 'workspace-tab-close';
                close.textContent = '×';
                close.setAttribute('aria-label', tab.title + ' sekmesini kapat');
                close.addEventListener('click', function () {
                    var index = tabs.indexOf(tab);
                    tabs.splice(index, 1);
                    save();
                    render();
                    navigateAfterClose(tab.href, index);
                });

                item.appendChild(link);
                item.appendChild(close);
                list.appendChild(item);
            });

            var actions = document.createElement('details');
            actions.className = 'workspace-tabs-actions';
            var summary = document.createElement('summary');
            summary.textContent = 'Kapat ▾';
            actions.appendChild(summary);
            var menu = document.createElement('div');
            menu.className = 'workspace-tabs-menu';
            var closeOthers = document.createElement('button');
            closeOthers.type = 'button';
            closeOthers.textContent = 'Diğer sekmeleri kapat';
            closeOthers.addEventListener('click', function () {
                tabs = tabs.filter(function (tab) { return tab.href === currentHref; });
                save(); render(); actions.open = false;
            });
            var closeAll = document.createElement('button');
            closeAll.type = 'button';
            closeAll.textContent = 'Tüm sekmeleri kapat';
            closeAll.addEventListener('click', function () {
                tabs = [];
                save();
                window.location.assign('/');
            });
            menu.appendChild(closeOthers);
            menu.appendChild(closeAll);
            actions.appendChild(menu);

            host.appendChild(list);
            host.appendChild(actions);
            save();
            var activeTab = host.querySelector('.workspace-tab-item.is-active');
            if (activeTab) activeTab.scrollIntoView({ block: 'nearest', inline: 'nearest' });
        }

        render();
    }());

    /* ============================================================
       P3-#8 — Mobile sidebar toggle
       ============================================================ */
    document.querySelectorAll('[data-toggle-sidebar]').forEach(function (el) {
        el.addEventListener('click', function () {
            document.body.classList.toggle('sidebar-open');
        });
    });
    // Auto-close when clicking a menu link
    document.querySelectorAll('.sidebar .menu a').forEach(function (a) {
        a.addEventListener('click', function () {
            document.body.classList.remove('sidebar-open');
        });
    });

    /* ============================================================
       P3-#6 — Command palette (Ctrl+K)
       ============================================================ */
    var cmdModal = document.querySelector('[data-cmd-modal]');
    var cmdInput = document.querySelector('[data-cmd-input]');
    var cmdList  = document.querySelector('[data-cmd-list]');

    // Index built from sidebar nav items
    var cmdIndex = [];
    document.querySelectorAll('.sidebar .menu a').forEach(function (a) {
        var label = a.textContent.trim();
        var href = a.getAttribute('href');
        if (label && href) cmdIndex.push({ title: label, href: href, icon: '\u27A4', sub: 'Sayfa' });
    });

    function openCmd() {
        if (!cmdModal) return;
        cmdModal.hidden = false;
        if (cmdInput) {
            cmdInput.value = '';
            renderCmd('');
            setTimeout(function () { cmdInput.focus(); }, 30);
        }
    }
    function closeCmd() { if (cmdModal) cmdModal.hidden = true; }

    function fuzzyMatch(query, text) {
        query = query.toLowerCase();
        text = text.toLowerCase();
        if (!query) return true;
        var qi = 0;
        for (var i = 0; i < text.length && qi < query.length; i++) {
            if (text[i] === query[qi]) qi++;
        }
        return qi === query.length;
    }

    function renderCmd(query) {
        if (!cmdList) return;
        cmdList.innerHTML = '';
        var items = cmdIndex.filter(function (it) { return fuzzyMatch(query, it.title); });

        // Tracking number shortcut: digits/letters >= 6 chars
        var qTrim = (query || '').trim();
        if (qTrim.length >= 6 && /^[A-Za-z0-9\-]+$/.test(qTrim)) {
            items.unshift({
                title: 'Takip No ile ac: ' + qTrim,
                href: '/track/' + encodeURIComponent(qTrim),
                icon: '\u{1F50D}',
                sub: 'Public takip sayfasi'
            });
        }

        if (items.length === 0) {
            var empty = document.createElement('div');
            empty.className = 'cmd-empty';
            empty.textContent = 'Sonuc yok';
            cmdList.appendChild(empty);
            return;
        }
        items.slice(0, 12).forEach(function (it, idx) {
            var row = document.createElement('div');
            row.className = 'cmd-result' + (idx === 0 ? ' is-active' : '');
            row.setAttribute('data-href', it.href);
            row.innerHTML =
                '<span class="cmd-result-icon"></span>' +
                '<div class="cmd-result-meta">' +
                    '<div class="cmd-result-title"></div>' +
                    '<div class="cmd-result-sub"></div>' +
                '</div>';
            row.querySelector('.cmd-result-icon').textContent = it.icon || '\u27A4';
            row.querySelector('.cmd-result-title').textContent = it.title;
            row.querySelector('.cmd-result-sub').textContent = it.sub || '';
            row.addEventListener('click', function () { window.location.href = it.href; });
            cmdList.appendChild(row);
        });
    }

    function moveActive(delta) {
        if (!cmdList) return;
        var rows = Array.prototype.slice.call(cmdList.querySelectorAll('.cmd-result'));
        if (rows.length === 0) return;
        var idx = rows.findIndex(function (r) { return r.classList.contains('is-active'); });
        if (idx < 0) idx = 0;
        rows[idx].classList.remove('is-active');
        idx = (idx + delta + rows.length) % rows.length;
        rows[idx].classList.add('is-active');
        rows[idx].scrollIntoView({ block: 'nearest' });
    }

    document.querySelectorAll('[data-open-cmd]').forEach(function (b) {
        b.addEventListener('click', openCmd);
    });
    document.querySelectorAll('[data-close-cmd]').forEach(function (b) {
        b.addEventListener('click', closeCmd);
    });

    document.addEventListener('keydown', function (e) {
        if ((e.ctrlKey || e.metaKey) && (e.key === 'k' || e.key === 'K')) {
            e.preventDefault();
            openCmd();
            return;
        }
        if (!cmdModal || cmdModal.hidden) return;
        if (e.key === 'Escape') { e.preventDefault(); closeCmd(); }
        else if (e.key === 'ArrowDown') { e.preventDefault(); moveActive(1); }
        else if (e.key === 'ArrowUp')   { e.preventDefault(); moveActive(-1); }
        else if (e.key === 'Enter') {
            e.preventDefault();
            var act = cmdList && cmdList.querySelector('.cmd-result.is-active');
            if (act) window.location.href = act.getAttribute('data-href');
        }
    });
    if (cmdInput) {
        cmdInput.addEventListener('input', function () { renderCmd(cmdInput.value); });
    }

})();
