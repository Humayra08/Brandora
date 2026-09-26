/* Brandora public pages: directory filters and the public brand / creator profiles.
   Everything here is progressive enhancement — the pages work without it. */
(function () {
    'use strict';

    var root = document.documentElement;
    var reduceMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    root.classList.add('js-reveal');
    // Tells the inline safety net in each page's <head> that reveals are handled here
    // (if this file ever fails to load, the page un-hides its content by itself).
    window.brandoraRevealReady = true;

    // ---- Directory: filters and sort apply as soon as they change ----
    document.querySelectorAll('form[data-auto-submit]').forEach(function (form) {
        var controls = Array.prototype.slice.call(form.querySelectorAll('select'));
        if (form.id) {
            controls = controls.concat(Array.prototype.slice.call(document.querySelectorAll('select[form="' + form.id + '"]')));
        }
        controls.forEach(function (select) {
            select.addEventListener('change', function () {
                if (typeof form.requestSubmit === 'function') { form.requestSubmit(); } else { form.submit(); }
            });
        });
    });

    // ---- Number counters ----
    function formatCount(value, format, decimals) {
        if (format === 'compact') {
            if (value >= 1e9) return (value / 1e9).toFixed(1).replace(/\.0$/, '') + 'B';
            if (value >= 1e6) return (value / 1e6).toFixed(1).replace(/\.0$/, '') + 'M';
            if (value >= 1e3) return (value / 1e3).toFixed(1).replace(/\.0$/, '') + 'K';
            return Math.round(value).toString();
        }
        if (format === 'percent') return value.toFixed(decimals) + '%';
        return Math.round(value).toLocaleString('en-US');
    }

    function runCounter(el) {
        var target = parseFloat(el.getAttribute('data-count-to'));
        if (isNaN(target)) return;
        var format = el.getAttribute('data-count-format') || 'plain';
        var decimals = parseInt(el.getAttribute('data-count-decimals') || '1', 10);
        if (reduceMotion) { el.textContent = formatCount(target, format, decimals); return; }
        var start = null;
        var duration = 1400;
        function step(ts) {
            if (start === null) start = ts;
            var t = Math.min(1, (ts - start) / duration);
            var eased = 1 - Math.pow(1 - t, 3);
            el.textContent = formatCount(target * eased, format, decimals);
            if (t < 1) requestAnimationFrame(step);
        }
        requestAnimationFrame(step);
    }

    // ---- Scroll reveal (also fills progress bars and starts counters) ----
    var revealTargets = document.querySelectorAll('[data-reveal], .brand-card, .influencer-card');
    var counters = document.querySelectorAll('[data-count-to]');
    var bars = document.querySelectorAll('[data-progress]');

    function reveal(el) {
        el.classList.add('is-in');
        // Once it has arrived, drop the stagger so hover effects respond instantly.
        var delay = parseInt(el.style.getPropertyValue('--reveal-delay'), 10) || 0;
        setTimeout(function () { el.style.setProperty('--reveal-delay', '0ms'); }, delay + 850);
        el.querySelectorAll('[data-progress]').forEach(fillBar);
        if (el.hasAttribute('data-progress')) fillBar(el);
    }

    function fillBar(bar) {
        bar.style.width = Math.max(0, Math.min(100, parseFloat(bar.getAttribute('data-progress')) || 0)) + '%';
    }

    if ('IntersectionObserver' in window && !reduceMotion) {
        // Stagger siblings that enter together.
        var groups = new Map();
        revealTargets.forEach(function (el) {
            var parent = el.parentElement;
            var index = groups.get(parent) || 0;
            if (!el.style.getPropertyValue('--reveal-delay')) {
                el.style.setProperty('--reveal-delay', Math.min(index, 6) * 80 + 'ms');
            }
            groups.set(parent, index + 1);
        });

        var io = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (!entry.isIntersecting) return;
                reveal(entry.target);
                io.unobserve(entry.target);
            });
        }, { rootMargin: '0px 0px -8% 0px', threshold: 0.08 });
        revealTargets.forEach(function (el) { io.observe(el); });

        var counterIo = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (!entry.isIntersecting) return;
                runCounter(entry.target);
                counterIo.unobserve(entry.target);
            });
        }, { threshold: 0.4 });
        counters.forEach(function (el) { counterIo.observe(el); });

        bars.forEach(function (bar) {
            if (!bar.closest('[data-reveal]')) io.observe(bar);
        });
    } else {
        revealTargets.forEach(reveal);
        counters.forEach(runCounter);
        bars.forEach(fillBar);
    }

    // ---- Gentle 3D tilt on campaign cards (mouse only) ----
    if (!reduceMotion && window.matchMedia && window.matchMedia('(hover: hover) and (pointer: fine)').matches) {
        document.querySelectorAll('[data-tilt]').forEach(function (card) {
            card.addEventListener('mousemove', function (e) {
                var r = card.getBoundingClientRect();
                var x = (e.clientX - r.left) / r.width - 0.5;
                var y = (e.clientY - r.top) / r.height - 0.5;
                card.style.transform = 'perspective(900px) rotateX(' + (-y * 5).toFixed(2) + 'deg) rotateY(' + (x * 6).toFixed(2) + 'deg) translateY(-4px)';
            });
            card.addEventListener('mouseleave', function () { card.style.transform = ''; });
        });
    }

    // ---- Section nav: highlight the section in view ----
    var navLinks = Array.prototype.slice.call(document.querySelectorAll('.pp-nav a[href^="#"]'));
    if (navLinks.length && 'IntersectionObserver' in window) {
        var sections = navLinks
            .map(function (a) { return document.querySelector(a.getAttribute('href')); })
            .filter(Boolean);
        var setActive = function (id) {
            navLinks.forEach(function (a) {
                var on = a.getAttribute('href') === '#' + id;
                a.classList.toggle('is-active', on);
                if (on && a.scrollIntoView && a.parentElement.scrollWidth > a.parentElement.clientWidth) {
                    a.parentElement.scrollTo({ left: a.offsetLeft - 24, behavior: reduceMotion ? 'auto' : 'smooth' });
                }
            });
        };
        var spy = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (entry.isIntersecting) setActive(entry.target.id);
            });
        }, { rootMargin: '-45% 0px -50% 0px' });
        sections.forEach(function (s) { spy.observe(s); });
        navLinks.forEach(function (a) {
            a.addEventListener('click', function (e) {
                var target = document.querySelector(a.getAttribute('href'));
                if (!target) return;
                e.preventDefault();
                target.scrollIntoView({ behavior: reduceMotion ? 'auto' : 'smooth', block: 'start' });
                setActive(target.id);
                if (history.replaceState) history.replaceState(null, '', a.getAttribute('href'));
            });
        });
    }

    // ---- Campaign filter chips ----
    document.querySelectorAll('[data-filter-group]').forEach(function (group) {
        var list = document.querySelector(group.getAttribute('data-filter-group'));
        if (!list) return;
        var empty = document.querySelector(group.getAttribute('data-filter-empty'));
        group.addEventListener('click', function (e) {
            var chip = e.target.closest('[data-filter]');
            if (!chip) return;
            var value = chip.getAttribute('data-filter');
            group.querySelectorAll('[data-filter]').forEach(function (c) {
                var on = c === chip;
                c.classList.toggle('is-active', on);
                c.setAttribute('aria-pressed', on ? 'true' : 'false');
            });
            var shown = 0;
            list.querySelectorAll('[data-state]').forEach(function (card) {
                var match = value === 'all' || card.getAttribute('data-state') === value;
                card.hidden = !match;
                if (match) {
                    shown++;
                    card.classList.add('is-in');
                }
            });
            if (empty) empty.hidden = shown > 0;
        });
    });

    // ---- Dialogs (campaign details) ----
    function openDialog(id, trigger) {
        var dialog = document.getElementById(id);
        if (!dialog || typeof dialog.showModal !== 'function') return false;
        dialog.showModal();
        dialog._trigger = trigger;
        return true;
    }

    document.addEventListener('click', function (e) {
        var opener = e.target.closest('[data-dialog-open]');
        if (opener && !e.target.closest('a[href]:not([data-dialog-open])')) {
            if (openDialog(opener.getAttribute('data-dialog-open'), opener)) e.preventDefault();
            return;
        }
        if (e.target.closest('[data-dialog-close]')) {
            var d = e.target.closest('dialog');
            if (d) d.close();
            return;
        }
        if (e.target.tagName === 'DIALOG') {
            var r = e.target.getBoundingClientRect();
            if (e.clientX < r.left || e.clientX > r.right || e.clientY < r.top || e.clientY > r.bottom) e.target.close();
        }
    });

    document.addEventListener('keydown', function (e) {
        if ((e.key === 'Enter' || e.key === ' ') && e.target.matches && e.target.matches('[data-dialog-open]:not(a):not(button)')) {
            e.preventDefault();
            openDialog(e.target.getAttribute('data-dialog-open'), e.target);
        }
    });

    document.querySelectorAll('dialog').forEach(function (d) {
        d.addEventListener('close', function () {
            d.querySelectorAll('video').forEach(function (v) { v.pause(); });
            if (d._trigger && typeof d._trigger.focus === 'function') d._trigger.focus();
        });
    });
})();
