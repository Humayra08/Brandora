// Paginate a presentation copy of the frozen agreement; never change its stored terms.
(async () => {
    const root = document.querySelector('[data-agreement-viewer]');
    if (!root) return;
    await document.fonts.ready;
    const source = root.querySelector('[data-document-source]');
    const sourceDoc = source.querySelector('.agreement-doc');
    if (!sourceDoc) return;
    const pagesHost = root.querySelector('[data-document-pages]');
    const thumbnails = root.querySelector('[data-page-thumbnails]');
    const viewport = root.querySelector('.agreement-document-viewport');
    const heading = root.querySelector('[data-paper-heading]');
    const sections = [];
    let section;
    [...sourceDoc.children].forEach(node => {
        if (node.matches('h1')) return;
        if (node.matches('h2') || !section) { section = []; sections.push(section); }
        section.push(node.cloneNode(true));
    });
    const pages = [];
    const maxHeight = 800;
    function createPage() {
        const frame = document.createElement('div'); frame.className = 'agreement-sheet-frame';
        const sheet = document.createElement('article'); sheet.className = 'agreement-sheet';
        sheet.append(heading.content.cloneNode(true));
        const title = document.createElement('h1');
        title.textContent = sourceDoc.querySelector('h1')?.textContent || 'Campaign Collaboration Agreement';
        sheet.insertBefore(title, sheet.querySelector('.agreement-document-meta'));
        const body = document.createElement('div'); body.className = 'agreement-doc'; sheet.append(body);
        const footer = document.createElement('footer'); footer.className = 'agreement-page-footer'; sheet.append(footer);
        frame.append(sheet); pagesHost.append(frame);
        const page = { frame, sheet, body, footer }; pages.push(page); return page;
    }
    let current = createPage();
    function appendNode(node) {
        current.body.append(node);
        if (current.sheet.scrollHeight > maxHeight && current.body.children.length > 1) {
            node.remove(); current = createPage(); current.body.append(node);
        }
    }
    sections.forEach((nodes, index) => {
        const group = document.createElement('section'); group.className = 'agreement-document-section';
        nodes.forEach(n => group.append(n));
        if (index === 0) {
            const paragraphs = [...group.querySelectorAll(':scope > p')];
            if (paragraphs.length) {
                const parties = document.createElement('div'); parties.className = 'agreement-parties';
                paragraphs[0].before(parties); paragraphs.forEach(p => parties.append(p));
            }
        }
        // Repeat the platform already recorded in the frozen campaign details.
        const deliverables = group.querySelector('.agreement-table');
        if (deliverables && deliverables.querySelectorAll('thead th').length === 3) {
            const platformRow = [...sourceDoc.querySelectorAll('.agreement-kv tr')].find(row => row.querySelector('th')?.textContent.trim() === 'Platform(s)');
            const platform = platformRow?.querySelector('td')?.textContent || 'Not specified';
            const header = deliverables.querySelector('thead tr');
            const numberHeading = document.createElement('th'); numberHeading.textContent = '#';
            const platformHeading = document.createElement('th'); platformHeading.textContent = 'Platform';
            header.prepend(numberHeading, platformHeading);
            let number = 0;
            deliverables.querySelectorAll('tbody tr').forEach(row => {
                if (row.classList.contains('agreement-total-row')) {
                    row.firstElementChild.colSpan += 2;
                } else {
                    const indexCell = document.createElement('td'); indexCell.textContent = ++number;
                    const platformCell = document.createElement('td'); platformCell.textContent = platform;
                    row.prepend(indexCell, platformCell);
                }
            });
        }
        current.body.append(group);
        if (current.sheet.scrollHeight > maxHeight) {
            group.remove();
            if (current.body.children.length) current = createPage();
            current.body.append(group);
            // Long campaign descriptions and deliverable lists must remain fully readable.
            // Oversized sections get a taller sheet and additional PDF pages, never clipping.
        }
    });
    const signatures = source.querySelector('.agreement-sign-grid');
    if (signatures) appendNode(signatures.cloneNode(true));
    source.hidden = true;
    let active = 0, zoom = 1;
    const count = root.querySelector('[data-page-count]');
    const previous = root.querySelector('[data-page-prev]');
    const next = root.querySelector('[data-page-next]');
    const zoomLabel = root.querySelector('[data-zoom-reset]');
    const thumbnailButtons = [];
    function layout() {
        const available = viewport.clientWidth - parseFloat(getComputedStyle(viewport).paddingLeft) - parseFloat(getComputedStyle(viewport).paddingRight);
        const scale = Math.min(1, available / 700) * zoom;
        pages.forEach(({ frame, sheet }) => {
            sheet.style.transform = 'scale(' + scale + ')';
            frame.style.width = (700 * scale) + 'px';
            frame.style.height = (sheet.offsetHeight * scale) + 'px';
        });
        thumbnailButtons.forEach((button, index) => {
            const preview = button.firstElementChild;
            const mini = preview.firstElementChild;
            mini.style.transform = 'scale(' + (preview.clientWidth / 700) + ')';
        });
    }
    function showPage(index) {
        active = Math.max(0, Math.min(index, pages.length - 1));
        pages.forEach((p, i) => p.frame.hidden = i !== active);
        thumbnailButtons.forEach((button, i) => {
            if (i === active) button.setAttribute('aria-current', 'page');
            else button.removeAttribute('aria-current');
        });
        count.textContent = (active + 1) + ' / ' + pages.length;
        previous.disabled = active === 0; next.disabled = active === pages.length - 1;
        viewport.scrollTop = 0; viewport.scrollLeft = 0;
        layout();
    }
    pages.forEach(({ sheet, footer }, i) => {
        footer.textContent = 'Page ' + (i + 1) + ' of ' + pages.length;
        sheet.setAttribute('aria-label', 'Agreement page ' + (i + 1));
        const button = document.createElement('button'); button.type = 'button'; button.className = 'agreement-thumbnail';
        button.setAttribute('aria-label', 'Go to page ' + (i + 1));
        const preview = document.createElement('span'); preview.className = 'agreement-thumbnail-preview'; preview.setAttribute('aria-hidden', 'true');
        const copy = sheet.cloneNode(true); copy.querySelectorAll('[id]').forEach(n => n.removeAttribute('id'));
        preview.append(copy);
        const number = document.createElement('span'); number.textContent = i + 1;
        button.append(preview, number); button.addEventListener('click', () => showPage(i));
        thumbnails.append(button); thumbnailButtons.push(button);
    });
    previous.addEventListener('click', () => showPage(active - 1));
    next.addEventListener('click', () => showPage(active + 1));
    function setZoom(value) { zoom = Math.min(1.75, Math.max(.5, value)); zoomLabel.textContent = Math.round(zoom * 100) + '%'; layout(); }
    root.querySelector('[data-zoom-in]').addEventListener('click', () => setZoom(zoom + .25));
    root.querySelector('[data-zoom-out]').addEventListener('click', () => setZoom(zoom - .25));
    zoomLabel.addEventListener('click', () => setZoom(1));
    root.querySelector('[data-print-agreement]').addEventListener('click', () => window.print());
    viewport.addEventListener('keydown', e => {
        if (e.key === 'ArrowRight' || e.key === 'ArrowLeft') { e.preventDefault(); showPage(active + (e.key === 'ArrowRight' ? 1 : -1)); }
    });
    new ResizeObserver(layout).observe(viewport);
    showPage(0);
    const download = root.querySelector('[data-download-pdf]');
    download.addEventListener('click', async () => {
        const message = root.querySelector('[data-export-message]');
        message.hidden = true; download.disabled = true;
        const exportRoot = document.createElement('div'); exportRoot.className = 'brand-agreement agreement-pdf-export';
        pages.forEach(({ sheet }) => {
            const page = sheet.cloneNode(true); page.style.transform = '';
            exportRoot.append(page);
        });
        try {
            if (!window.html2pdf) throw new Error('PDF library unavailable');
            // Image signatures must be included in the export, not silently omitted.
            await Promise.all([...exportRoot.querySelectorAll('img')].map(async img => {
                const response = await fetch(img.src);
                if (!response.ok) throw new Error('Signature image could not be loaded');
                const blob = await response.blob();
                img.src = await new Promise((resolve, reject) => {
                    const reader = new FileReader(); reader.onload = () => resolve(reader.result); reader.onerror = reject; reader.readAsDataURL(blob);
                });
            }));
            await html2pdf().set({
                margin: 10, filename: root.dataset.agreementCode + '.pdf',
                image: { type: 'jpeg', quality: .98 },
                html2canvas: { scale: 2, useCORS: true, backgroundColor: '#ffffff' },
                jsPDF: { unit: 'mm', format: 'a4', orientation: 'portrait' },
                pagebreak: { mode: ['css','legacy'], avoid: ['tr', '.agreement-sign-grid'] }
            }).from(exportRoot).save();
        } catch {
            message.textContent = 'The PDF could not be downloaded. Please try again, or use Print and choose Save as PDF.';
            message.hidden = false;
        } finally { download.disabled = false; }
    });
})();
