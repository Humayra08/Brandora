// Shared signature capture. The form lives inside the panel on both agreement pages.
(() => {
    document.querySelectorAll('[data-signature-panel]').forEach(panel => {
        const canvas = panel.querySelector('[data-signature-canvas]');
        const form = panel.querySelector('form') || panel.closest('form');
        const tabs = [...panel.querySelectorAll('[data-signature-tab]')];
        const file = panel.querySelector('[data-signature-file]');
        const data = panel.querySelector('[data-signature-dataurl]');
        const modeInput = panel.querySelector('[data-signature-mode]');
        const typed = panel.querySelector('[data-signature-typed]');
        const hint = panel.querySelector('[data-signature-hint]');
        const preview = panel.querySelector('[data-signature-upload-preview]');
        const error = panel.querySelector('[data-signature-error]');
        let mode = 'draw', drawing = false, hasDrawn = false, ctx, previewUrl;
        let canvasWidth = 0, canvasHeight = 0, snapshot = null;
        function showError(message) {
            if (error) { error.textContent = message; error.hidden = !message; }
            else if (message) window.alert(message);
        }
        function setupCanvas() {
            if (!canvas || canvas.closest('[hidden]')) return;
            const rect = canvas.getBoundingClientRect();
            if (!rect.width || !rect.height || (canvasWidth === rect.width && canvasHeight === rect.height)) return;
            const saved = hasDrawn ? canvas.toDataURL('image/png') : snapshot;
            canvasWidth = rect.width; canvasHeight = rect.height;
            const ratio = window.devicePixelRatio || 1;
            canvas.width = Math.round(rect.width * ratio);
            canvas.height = Math.round(rect.height * ratio);
            ctx = canvas.getContext('2d');
            ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
            ctx.lineWidth = 2.2; ctx.lineCap = 'round'; ctx.lineJoin = 'round'; ctx.strokeStyle = '#171d3d';
            if (saved) {
                const img = new Image();
                img.onload = () => ctx.drawImage(img, 0, 0, canvasWidth, canvasHeight);
                img.src = saved;
            }
        }
        if (canvas) {
            setupCanvas();
            new ResizeObserver(setupCanvas).observe(canvas);
            canvas.addEventListener('pointerdown', e => {
                if (!ctx) return;
                e.preventDefault(); canvas.setPointerCapture(e.pointerId);
                drawing = true; hasDrawn = true;
                if (hint) hint.hidden = true;
                showError('');
                const rect = canvas.getBoundingClientRect();
                ctx.beginPath(); ctx.moveTo(e.clientX - rect.left, e.clientY - rect.top);
                ctx.lineTo(e.clientX - rect.left + .1, e.clientY - rect.top + .1); ctx.stroke();
            });
            canvas.addEventListener('pointermove', e => {
                if (!drawing) return;
                const rect = canvas.getBoundingClientRect();
                ctx.lineTo(e.clientX - rect.left, e.clientY - rect.top); ctx.stroke();
            });
            const stop = () => { drawing = false; if (hasDrawn) snapshot = canvas.toDataURL('image/png'); };
            canvas.addEventListener('pointerup', stop);
            canvas.addEventListener('pointercancel', stop);
            canvas.addEventListener('lostpointercapture', stop);
        }
        panel.querySelector('[data-signature-clear]')?.addEventListener('click', () => {
            ctx?.clearRect(0, 0, canvasWidth, canvasHeight);
            hasDrawn = false; snapshot = null; data.value = '';
            if (hint) hint.hidden = false;
        });
        function selectMode(tab) {
            mode = tab.dataset.signatureTab;
            if (modeInput) modeInput.value = mode;
            tabs.forEach(t => {
                const selected = t === tab;
                t.classList.toggle('is-active', selected);
                if (t.getAttribute('role') === 'tab') {
                    t.setAttribute('aria-selected', String(selected)); t.tabIndex = selected ? 0 : -1;
                }
            });
            panel.querySelectorAll('[data-signature-pane]').forEach(p => p.hidden = p.dataset.signaturePane !== mode);
            if (file) file.disabled = mode !== 'upload';
            showError('');
            if (mode === 'draw') setupCanvas();
            if (mode === 'type') typed?.focus();
        }
        tabs.forEach((tab, index) => {
            tab.addEventListener('click', () => selectMode(tab));
            tab.addEventListener('keydown', e => {
                if (!['ArrowLeft','ArrowRight','Home','End'].includes(e.key)) return;
                e.preventDefault();
                const next = e.key === 'Home' ? 0 : e.key === 'End' ? tabs.length - 1 : (index + (e.key === 'ArrowRight' ? 1 : -1) + tabs.length) % tabs.length;
                selectMode(tabs[next]); tabs[next].focus();
            });
        });
        if (file) file.disabled = true;
        typed?.addEventListener('input', () => {
            panel.querySelector('[data-signature-preview]').textContent = typed.value;
            showError('');
        });
        file?.addEventListener('change', () => {
            showError('');
            if (previewUrl) URL.revokeObjectURL(previewUrl);
            const chosen = file.files[0];
            if (preview) preview.hidden = true;
            if (!chosen) return;
            if (!['image/png','image/jpeg','image/webp'].includes(chosen.type) || chosen.size > 5 * 1024 * 1024) {
                file.value = ''; showError('Choose a PNG, JPG or WebP image smaller than 5 MB.'); return;
            }
            if (preview) {
                previewUrl = URL.createObjectURL(chosen); preview.src = previewUrl; preview.hidden = false;
                preview.onerror = () => { file.value = ''; preview.hidden = true; showError('This image could not be opened. Please choose another signature image.'); };
            }
        });
        form?.addEventListener('submit', e => {
            data.value = '';
            if (mode === 'draw' && hasDrawn) data.value = canvas.toDataURL('image/png');
            else if (mode === 'type' && typed?.value.trim()) {
                const output = document.createElement('canvas');
                output.width = 1000; output.height = 220;
                const pen = output.getContext('2d');
                let size = 72;
                const text = typed.value.trim();
                pen.font = 'italic ' + size + 'px Georgia, serif';
                while (pen.measureText(text).width > 940 && size > 16) { size -= 2; pen.font = 'italic ' + size + 'px Georgia, serif'; }
                pen.fillStyle = '#171d3d'; pen.textBaseline = 'middle'; pen.fillText(text, 30, 110);
                data.value = output.toDataURL('image/png');
            } else if (mode !== 'upload' || !file?.files.length) {
                e.preventDefault(); showError('Please ' + (mode === 'draw' ? 'draw your signature' : mode === 'type' ? 'type your signature' : 'choose a signature image') + ' before signing.');
                return;
            }
            const submit = form.querySelector('[type="submit"]');
            if (submit) { submit.disabled = true; submit.textContent = 'Signing…'; }
        });
    });
})();
