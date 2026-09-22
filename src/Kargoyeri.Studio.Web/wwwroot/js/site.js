/* ============================================================
   Kargoyeri Studio — site.js
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

})();
